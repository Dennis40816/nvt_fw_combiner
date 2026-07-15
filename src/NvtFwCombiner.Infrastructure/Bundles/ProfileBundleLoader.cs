using System.Text.Json;
using NvtFwCombiner.Contracts.Bundles;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Externally supplied binding that identifies the only bundle content hash this load may trust.</summary>
internal sealed class ProfileBundleTrustAnchor
{
    internal ProfileBundleTrustAnchor(string expectedContentHash, string trustAnchorBindingId)
    {
        ValidateCanonicalSha256(expectedContentHash, nameof(expectedContentHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(trustAnchorBindingId);
        ExpectedContentHash = expectedContentHash;
        TrustAnchorBindingId = trustAnchorBindingId;
    }

    internal string ExpectedContentHash { get; }

    internal string TrustAnchorBindingId { get; }

    internal void Verify(ProfileBundleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!StringComparer.Ordinal.Equals(TrustAnchorBindingId, manifest.TrustAnchorBindingId))
        {
            throw new InvalidDataException(
                $"Bundle trust-anchor binding '{manifest.TrustAnchorBindingId}' does not match the expected binding.");
        }

        if (!StringComparer.Ordinal.Equals(ExpectedContentHash, manifest.ContentHash))
        {
            throw new InvalidDataException("Bundle content hash does not match the external trust anchor.");
        }
    }

    private static void ValidateCanonicalSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(static character =>
            character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Expected a lowercase 64-character SHA-256 hash.", parameterName);
        }
    }
}

/// <summary>Bounds manifest parsing and all entry snapshots for one trusted bundle load.</summary>
internal sealed class ProfileBundleLoadLimits
{
    internal ProfileBundleLoadLimits(
        int maximumManifestBytes,
        int maximumJsonDepth,
        ProfileBundleEntrySnapshotLimits entrySnapshotLimits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumManifestBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumJsonDepth);
        ArgumentNullException.ThrowIfNull(entrySnapshotLimits);
        MaximumManifestBytes = maximumManifestBytes;
        MaximumJsonDepth = maximumJsonDepth;
        EntrySnapshotLimits = entrySnapshotLimits;
    }

    internal int MaximumManifestBytes { get; }

    internal int MaximumJsonDepth { get; }

    internal ProfileBundleEntrySnapshotLimits EntrySnapshotLimits { get; }
}

/// <summary>One external-anchor-verified manifest and immutable validated entry snapshots.</summary>
internal sealed class TrustedProfileBundle
{
    private readonly ProfileBundleEntrySnapshotCollection _entrySnapshots;
    private readonly int _maximumJsonDepth;

    internal TrustedProfileBundle(
        ProfileBundleFileSnapshot manifestSnapshot,
        ProfileBundleEntrySnapshotCollection entrySnapshots,
        int maximumJsonDepth)
    {
        ArgumentNullException.ThrowIfNull(manifestSnapshot);
        ArgumentNullException.ThrowIfNull(entrySnapshots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumJsonDepth);
        ManifestSha256 = manifestSnapshot.ActualSha256;
        Manifest = entrySnapshots.Manifest;
        Entries = entrySnapshots.Entries;
        _entrySnapshots = entrySnapshots;
        _maximumJsonDepth = maximumJsonDepth;
    }

    internal string ManifestSha256 { get; }

    internal ProfileBundleManifest Manifest { get; }

    internal IReadOnlyList<ProfileBundleEntrySnapshot> Entries { get; }

    /// <summary>Projects immutable canonical JSON trees after source-generated DTO compatibility validation.</summary>
    internal TrustedProfileBundleDocumentProjection CreateDocumentProjection()
    {
        return new TrustedProfileBundleDocumentProjection(
            ManifestSha256,
            Manifest,
            _entrySnapshots.Entries,
            _maximumJsonDepth);
    }
}

/// <summary>Loads one closed profile bundle into immutable, schema-validated snapshots.</summary>
internal static class ProfileBundleLoader
{
    internal static TrustedProfileBundle Load(
        string bundleRoot,
        string manifestPath,
        ProfileBundleTrustAnchor trustAnchor,
        ProfileBundleLoadLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        return Load(
            new DirectoryProfileBundleSnapshotSource(bundleRoot, manifestPath),
            trustAnchor,
            limits);
    }

    internal static TrustedProfileBundle Load(
        IProfileBundleSnapshotSource source,
        ProfileBundleTrustAnchor trustAnchor,
        ProfileBundleLoadLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(trustAnchor);
        ArgumentNullException.ThrowIfNull(limits);

        ProfileBundleFileSnapshot manifestSnapshot = source.ReadManifest(limits.MaximumManifestBytes);
        ProfileBundleSchemaValidator.ValidateManifest(manifestSnapshot, limits.MaximumJsonDepth);
        ProfileBundleManifest manifest = ProfileBundleManifestNormalizer.Normalize(
            DeserializeManifest(manifestSnapshot, limits.MaximumJsonDepth));
        trustAnchor.Verify(manifest);

        ProfileBundleEntrySnapshotCollection entrySnapshots = source.CaptureEntries(
            manifest,
            limits.EntrySnapshotLimits);
        ProfileBundleSchemaValidator.ValidateEntries(entrySnapshots, limits.MaximumJsonDepth);

        ProfileBundleFileSnapshot finalManifestSnapshot = source.ReadManifest(limits.MaximumManifestBytes);
        _ = StringComparer.Ordinal.Equals(manifestSnapshot.ActualSha256, finalManifestSnapshot.ActualSha256)
            ? true
            : throw new IOException("Bundle manifest changed during trusted bundle capture.");

        return new TrustedProfileBundle(manifestSnapshot, entrySnapshots, limits.MaximumJsonDepth);
    }

    private static ProfileBundleDocument DeserializeManifest(
        ProfileBundleFileSnapshot manifestSnapshot,
        int maximumJsonDepth)
    {
        using JsonDocument document = manifestSnapshot.ParseStrictJson(maximumJsonDepth);
        try
        {
            return JsonSerializer.Deserialize(
                document.RootElement,
                ProfileBundleJsonContext.Default.ProfileBundleDocument) ?? throw new InvalidDataException(
                "Bundle manifest cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Bundle manifest could not be deserialized.", exception);
        }
    }
}
