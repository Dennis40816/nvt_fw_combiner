using System.Text.Json;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Exact immutable identity of one schema-validated canonical document entry.</summary>
internal sealed class TrustedProfileBundleDocumentIdentity
{
    internal TrustedProfileBundleDocumentIdentity(ProfileBundleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EntryId = entry.EntryId;
        Path = entry.Path;
        SchemaId = entry.SchemaId;
        ContentHash = entry.ContentHash;
    }

    internal string EntryId { get; }

    internal string Path { get; }

    internal string SchemaId { get; }

    internal string ContentHash { get; }
}

/// <summary>One hash-verified firmware-family document from a trusted bundle snapshot.</summary>
internal sealed class TrustedFirmwareFamilyDocumentEntry
{
    internal TrustedFirmwareFamilyDocumentEntry(
        TrustedProfileBundleDocumentIdentity identity,
        JsonElement document)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
        Document = document;
    }

    internal TrustedProfileBundleDocumentIdentity Identity { get; }

    /// <summary>Immutable canonical JSON tree whose DTO compatibility was verified from the captured snapshot.</summary>
    internal JsonElement Document { get; }
}

/// <summary>One hash-verified composition-profile document from a trusted bundle snapshot.</summary>
internal sealed class TrustedCompositionProfileDocumentEntry
{
    internal TrustedCompositionProfileDocumentEntry(
        TrustedProfileBundleDocumentIdentity identity,
        JsonElement document)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
        Document = document;
    }

    internal TrustedProfileBundleDocumentIdentity Identity { get; }

    /// <summary>Immutable canonical JSON tree whose DTO compatibility was verified from the captured snapshot.</summary>
    internal JsonElement Document { get; }
}

/// <summary>
/// Immutable canonical JSON trees projected only from one already trusted immutable bundle capture.
/// This projection does not normalize family/profile semantics or authorize map resolution, compilation, or execution.
/// </summary>
internal sealed class TrustedProfileBundleDocumentProjection
{
    internal const string FirmwareFamilySchemaId =
        "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";

    internal const string CompositionProfileSchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    private readonly TrustedFirmwareFamilyDocumentEntry[] _families;
    private readonly TrustedCompositionProfileDocumentEntry[] _profiles;

    internal TrustedProfileBundleDocumentProjection(
        string manifestSha256,
        ProfileBundleManifest manifest,
        IReadOnlyList<ProfileBundleEntrySnapshot> entries,
        int maximumJsonDepth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestSha256);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumJsonDepth);

        var families = new List<TrustedFirmwareFamilyDocumentEntry>();
        var profiles = new List<TrustedCompositionProfileDocumentEntry>();
        foreach (ProfileBundleEntrySnapshot entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            switch (entry.Entry.Kind)
            {
                case ProfileBundleEntryKind.FirmwareFamily:
                    RequireCanonicalSchema(entry.Entry, FirmwareFamilySchemaId);
                    families.Add(new TrustedFirmwareFamilyDocumentEntry(
                        new TrustedProfileBundleDocumentIdentity(entry.Entry),
                        ValidateAndClone(
                            entry,
                            maximumJsonDepth,
                            ProfileBundleJsonContext.Default.FirmwareFamilyDocument)));
                    break;
                case ProfileBundleEntryKind.CompositionProfile:
                    RequireCanonicalSchema(entry.Entry, CompositionProfileSchemaId);
                    profiles.Add(new TrustedCompositionProfileDocumentEntry(
                        new TrustedProfileBundleDocumentIdentity(entry.Entry),
                        ValidateAndClone(
                            entry,
                            maximumJsonDepth,
                            ProfileBundleJsonContext.Default.CompositionProfileDocument)));
                    break;
                case ProfileBundleEntryKind.Schema:
                case ProfileBundleEntryKind.EvidenceManifest:
                case ProfileBundleEntryKind.SavedCompositionRule:
                    break;
                default:
                    throw new InvalidDataException("Trusted bundle contains an unknown entry kind.");
            }
        }

        _families = [.. families];
        _profiles = [.. profiles];
        Array.Sort(_families, static (left, right) =>
            StringComparer.Ordinal.Compare(left.Identity.EntryId, right.Identity.EntryId));
        Array.Sort(_profiles, static (left, right) =>
            StringComparer.Ordinal.Compare(left.Identity.EntryId, right.Identity.EntryId));

        ManifestSha256 = manifestSha256;
        BundleId = manifest.BundleId;
        BundleVersion = manifest.BundleVersion;
        BundleContentHash = manifest.ContentHash;
        TrustAnchorBindingId = manifest.TrustAnchorBindingId;
        Families = Array.AsReadOnly(_families);
        Profiles = Array.AsReadOnly(_profiles);
    }

    internal string ManifestSha256 { get; }

    internal string BundleId { get; }

    internal string BundleVersion { get; }

    internal string BundleContentHash { get; }

    internal string TrustAnchorBindingId { get; }

    internal IReadOnlyList<TrustedFirmwareFamilyDocumentEntry> Families { get; }

    internal IReadOnlyList<TrustedCompositionProfileDocumentEntry> Profiles { get; }

    private static void RequireCanonicalSchema(ProfileBundleEntry entry, string expectedSchemaId)
    {
        if (!StringComparer.Ordinal.Equals(entry.SchemaId, expectedSchemaId))
        {
            throw new InvalidDataException(
                $"Bundle entry '{entry.Path}' has schema '{entry.SchemaId}', but kind '{entry.Kind}' requires " +
                $"canonical schema '{expectedSchemaId}'.");
        }
    }

    private static JsonElement ValidateAndClone<TDocument>(
        ProfileBundleEntrySnapshot entry,
        int maximumJsonDepth,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TDocument> typeInfo)
        where TDocument : class
    {
        using JsonDocument document = entry.FileSnapshot.ParseStrictJson(maximumJsonDepth);
        try
        {
            _ = JsonSerializer.Deserialize(document.RootElement, typeInfo) ?? throw new InvalidDataException(
                $"Bundle entry '{entry.Entry.Path}' cannot deserialize to its canonical document type.");
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Bundle entry '{entry.Entry.Path}' cannot deserialize to its canonical document type.",
                exception);
        }
    }
}
