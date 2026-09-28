using System.Collections.Frozen;
using System.Text.Json;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Immutable build-bound admission token. Only the checked factory can mint one.</summary>
internal sealed class AcceptedPrebuiltProfileCatalog
{
    private readonly FrozenDictionary<string, AcceptedBundle> _bundles;

    private AcceptedPrebuiltProfileCatalog(Dictionary<string, AcceptedBundle> bundles)
    {
        _bundles = bundles.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal ProfileBundleFileSnapshot ManifestSnapshot(string directory)
    {
        return _bundles[directory].Snapshot;
    }

    internal ProfileBundleManifest Manifest(string directory)
    {
        return _bundles[directory].Manifest;
    }

    internal IReadOnlyList<ProfileBundleEntrySnapshot> Documents(string directory)
    {
        return _bundles[directory].Documents;
    }

    internal static AcceptedPrebuiltProfileCatalog? TryAccept(string applicationRoot,
        ProfileBundlePackageTrustIndex index, out BuiltInProfileAdmissionRejectionReason? reason)
    {
        ArgumentNullException.ThrowIfNull(index);
        ProfileBundleFileSnapshot? file = ProfileBundleFileSnapshot.TryReadPrebuilt(applicationRoot, out ProfileBundleCaptureFailure capture);
        reason = capture switch
        {
            ProfileBundleCaptureFailure.None => null,
            ProfileBundleCaptureFailure.Missing => BuiltInProfileAdmissionRejectionReason.Missing,
            ProfileBundleCaptureFailure.FileBound => BuiltInProfileAdmissionRejectionReason.FileBound,
            ProfileBundleCaptureFailure.FileAccess => BuiltInProfileAdmissionRejectionReason.FileAccess,
            _ => throw new InvalidOperationException("Unknown capture failure."),
        };
        if (file is null) { return null; }
        PrebuiltProfileCatalogSnapshot? pack = PrebuiltProfileCatalogCodec.TryDecode(file.Content.ToArray(), out PrebuiltProfileCatalogDecodeFailure decode);
        if (pack is null)
        {
            reason = decode switch
            {
                PrebuiltProfileCatalogDecodeFailure.BodyIntegrity => BuiltInProfileAdmissionRejectionReason.BodyIntegrity,
                PrebuiltProfileCatalogDecodeFailure.ManifestIntegrity => BuiltInProfileAdmissionRejectionReason.ManifestSetMismatch,
                PrebuiltProfileCatalogDecodeFailure.DocumentIntegrity => BuiltInProfileAdmissionRejectionReason.DocumentIntegrity,
                PrebuiltProfileCatalogDecodeFailure.Format => BuiltInProfileAdmissionRejectionReason.Format,
                _ => throw new InvalidOperationException("Unknown transport failure."),
            };
            return null;
        }
        BuiltInProfileBuildAdmissionIdentity? identity = BuiltInProfileBuildAdmissionIdentity.TryRead(out BuildAdmissionIdentityFailure? failure);
        if (identity is null) { reason = MapIdentityFailure(failure!); return null; }
        JsonElement trust = pack.Header.GetProperty("trustIndex");
        if (index.ActualSha256 != identity.TrustIndexSha256 || Text(trust, "sha256") != index.ActualSha256 ||
            Text(trust, "trustIndexId") != index.TrustIndexId || Text(trust, "trustIndexVersion") != index.TrustIndexVersion ||
            Text(trust, "trustAnchorBindingId") != index.TrustAnchorBindingId)
        { reason = BuiltInProfileAdmissionRejectionReason.TrustIndexMismatch; return null; }

        JsonElement[] descriptors = [.. pack.Header.GetProperty("bundles").EnumerateArray()];
        if (!descriptors.Select(b => (Text(b, "bundleDirectory"), Text(b, "bundleVersion"), Text(b, "contentHash")))
            .SequenceEqual(index.Bundles.Select(b => (b.BundleDirectory, b.BundleVersion, b.ContentHash))))
        { reason = BuiltInProfileAdmissionRejectionReason.BundleSetMismatch; return null; }
        if (BuiltInProfileBuildAdmissionIdentity.CalculateManifestSet(descriptors.Select(b =>
                (Text(b, "bundleDirectory"), Text(b.GetProperty("manifest"), "sha256")))) != identity.ManifestSetSha256)
        { reason = BuiltInProfileAdmissionRejectionReason.ManifestSetMismatch; return null; }

        var bundles = new Dictionary<string, AcceptedBundle>(StringComparer.Ordinal);
        ProfileBundleLoadLimits limits = BuiltInProfileBundleAdmissionSettings.Limits;
        for (int i = 0; i < descriptors.Length; i++)
        {
            JsonElement descriptor = descriptors[i];
            ProfileBundlePackageTrustEntry indexed = index.Bundles[i];
            JsonElement range = descriptor.GetProperty("manifest");
            if (range.GetProperty("length").GetInt32() > limits.MaximumManifestBytes)
            { reason = BuiltInProfileAdmissionRejectionReason.EntryLimits; return null; }
            ProfileBundleFileSnapshot snapshot = CopyRange(pack, range, "profile-bundle.json", limits.MaximumManifestBytes);
            ProfileBundleManifest manifest;
            try
            {
                manifest = ProfileBundleLoader.AdmitManifest(snapshot,
                    new(indexed.ContentHash, index.TrustAnchorBindingId), limits);
                if (manifest.BundleVersion != indexed.BundleVersion)
                { reason = BuiltInProfileAdmissionRejectionReason.ManifestAdmission; return null; }
            }
            catch (Exception error) when (error is InvalidDataException or JsonException or ArgumentException)
            { reason = BuiltInProfileAdmissionRejectionReason.ManifestAdmission; return null; }
            reason = ValidateDocuments(manifest, descriptor.GetProperty("documents"), limits);
            if (reason is not null) { return null; }
            ProfileBundleEntry[] entries = [.. manifest.Entries.Where(IsDocument)];
            var documents = new ProfileBundleEntrySnapshot[entries.Length];
            for (int j = 0; j < entries.Length; j++)
            {
                ProfileBundleEntry entry = entries[j];
                documents[j] = new(entry, CopyRange(pack, descriptor.GetProperty("documents")[j], entry.Path,
                    limits.EntrySnapshotLimits.MaximumEntryBytes));
            }
            bundles.Add(indexed.BundleDirectory, new(snapshot, manifest, Array.AsReadOnly(documents)));
        }
        reason = null;
        return new AcceptedPrebuiltProfileCatalog(bundles);
    }

    // Value checks cannot mint a token; synthetic tests can reach closure/limit failures behind the build binding.
    internal static BuiltInProfileAdmissionRejectionReason? ValidateDocuments(ProfileBundleManifest manifest,
        JsonElement documents, ProfileBundleLoadLimits limits)
    {
        if (manifest.Entries.Count > limits.EntrySnapshotLimits.MaximumEntryCount)
        { return BuiltInProfileAdmissionRejectionReason.EntryLimits; }
        ProfileBundleEntry[] entries = [.. manifest.Entries.Where(IsDocument)];
        if (documents.GetArrayLength() != entries.Length) { return BuiltInProfileAdmissionRejectionReason.DocumentSetMismatch; }
        var schemas = manifest.Entries.Where(e => e.Kind == ProfileBundleEntryKind.Schema).Select(e => e.SchemaId).ToHashSet(StringComparer.Ordinal);
        for (int i = 0; i < entries.Length; i++)
        {
            JsonElement document = documents[i];
            ProfileBundleEntry entry = entries[i];
            string kind = entry.Kind == ProfileBundleEntryKind.FirmwareFamily ? "firmware-family" : "composition-profile";
            if (Text(document, "entryId") != entry.EntryId || Text(document, "kind") != kind ||
                Text(document, "path") != entry.Path || Text(document, "schemaId") != entry.SchemaId || !schemas.Contains(entry.SchemaId))
            { return BuiltInProfileAdmissionRejectionReason.DocumentSetMismatch; }
            if (Text(document, "contentHash") != entry.ContentHash) { return BuiltInProfileAdmissionRejectionReason.DocumentIntegrity; }
            if (document.GetProperty("length").GetInt64() > limits.EntrySnapshotLimits.MaximumEntryBytes)
            { return BuiltInProfileAdmissionRejectionReason.EntryLimits; }
        }
        return null;
    }

    internal static BuiltInProfileAdmissionRejectionReason MapIdentityFailure(BuildAdmissionIdentityFailure failure)
    {
        return failure.Field == BuildAdmissionIdentityField.TrustIndex
            ? BuiltInProfileAdmissionRejectionReason.TrustIndexMismatch : BuiltInProfileAdmissionRejectionReason.ManifestSetMismatch;
    }

    private static bool IsDocument(ProfileBundleEntry entry)
    {
        return entry.Kind is ProfileBundleEntryKind.FirmwareFamily or ProfileBundleEntryKind.CompositionProfile;
    }

    private static string Text(JsonElement value, string property)
    {
        return value.GetProperty(property).GetString()!;
    }

    private static ProfileBundleFileSnapshot CopyRange(PrebuiltProfileCatalogSnapshot pack, JsonElement range, string path, int bound)
    {
        return ProfileBundleFileSnapshot.Copy(path, pack.Body.Slice(range.GetProperty("offset").GetInt32(), range.GetProperty("length").GetInt32()), bound);
    }

    private sealed record AcceptedBundle(ProfileBundleFileSnapshot Snapshot, ProfileBundleManifest Manifest,
        IReadOnlyList<ProfileBundleEntrySnapshot> Documents);
}
