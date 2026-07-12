using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Exact immutable identity of a canonical JSON document supplied by the trusted Bootstrap seam.</summary>
internal sealed class TrustedProfileBundleCatalogEntryIdentity
{
    internal TrustedProfileBundleCatalogEntryIdentity(
        string entryId,
        string path,
        string schemaId,
        string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ProfileBundleIdentity.ValidateSha256(contentHash, nameof(contentHash));
        EntryId = entryId;
        Path = path;
        SchemaId = schemaId;
        ContentHash = contentHash;
    }

    internal string EntryId { get; }

    internal string Path { get; }

    internal string SchemaId { get; }

    internal string ContentHash { get; }
}

/// <summary>One immutable firmware-family JSON tree and its trusted entry identity.</summary>
internal sealed class TrustedFirmwareFamilyJsonSource
{
    internal TrustedFirmwareFamilyJsonSource(
        TrustedProfileBundleCatalogEntryIdentity identity,
        JsonElement document)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
        Document = document;
    }

    internal TrustedProfileBundleCatalogEntryIdentity Identity { get; }

    internal JsonElement Document { get; }
}

/// <summary>One immutable composition-profile JSON tree and its trusted entry identity.</summary>
internal sealed class TrustedCompositionProfileJsonSource
{
    internal TrustedCompositionProfileJsonSource(
        TrustedProfileBundleCatalogEntryIdentity identity,
        JsonElement document)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
        Document = document;
    }

    internal TrustedProfileBundleCatalogEntryIdentity Identity { get; }

    internal JsonElement Document { get; }
}

/// <summary>All trusted bundle identities and immutable canonical JSON trees consumed atomically by Profiles.</summary>
internal sealed class TrustedProfileBundleCatalogSource
{
    private readonly TrustedFirmwareFamilyJsonSource[] _families;
    private readonly TrustedCompositionProfileJsonSource[] _profiles;

    internal TrustedProfileBundleCatalogSource(
        string manifestSha256,
        string bundleId,
        string bundleVersion,
        string bundleContentHash,
        string trustAnchorBindingId,
        IEnumerable<TrustedFirmwareFamilyJsonSource> families,
        IEnumerable<TrustedCompositionProfileJsonSource> profiles)
    {
        ProfileBundleIdentity.ValidateSha256(manifestSha256, nameof(manifestSha256));
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleVersion);
        ProfileBundleIdentity.ValidateSha256(bundleContentHash, nameof(bundleContentHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(trustAnchorBindingId);
        _families = Snapshot(families, nameof(families));
        _profiles = Snapshot(profiles, nameof(profiles));

        ManifestSha256 = manifestSha256;
        BundleId = bundleId;
        BundleVersion = bundleVersion;
        BundleContentHash = bundleContentHash;
        TrustAnchorBindingId = trustAnchorBindingId;
        Families = Array.AsReadOnly(_families);
        Profiles = Array.AsReadOnly(_profiles);
    }

    internal string ManifestSha256 { get; }

    internal string BundleId { get; }

    internal string BundleVersion { get; }

    internal string BundleContentHash { get; }

    internal string TrustAnchorBindingId { get; }

    internal IReadOnlyList<TrustedFirmwareFamilyJsonSource> Families { get; }

    internal IReadOnlyList<TrustedCompositionProfileJsonSource> Profiles { get; }

    private static T[] Snapshot<T>(IEnumerable<T> values, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values);
        T[] snapshot = [.. values];
        return snapshot.Any(static value => value is null)
            ? throw new ArgumentException("Trusted bundle sources cannot contain null values.", parameterName)
            : snapshot;
    }
}
