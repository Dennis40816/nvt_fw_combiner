using System.Text.Json;
using NvtFwCombiner.Domain;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Exact immutable identity of a canonical JSON document supplied by the trusted Bootstrap seam.</summary>
internal sealed record TrustedProfileBundleCatalogEntryIdentity(
    string EntryId,
    string Path,
    string SchemaId,
    string ContentHash);

/// <summary>One immutable firmware-family JSON tree and its trusted entry identity.</summary>
internal sealed class TrustedFirmwareFamilyJsonSource(
    TrustedProfileBundleCatalogEntryIdentity identity,
    JsonElement document)
{
    internal TrustedProfileBundleCatalogEntryIdentity Identity { get; } = RequiredValue.NotNull(identity);
    internal JsonElement Document { get; } = document;
}

/// <summary>One immutable composition-profile JSON tree and its trusted entry identity.</summary>
internal sealed class TrustedCompositionProfileJsonSource(
    TrustedProfileBundleCatalogEntryIdentity identity,
    JsonElement document)
{
    internal TrustedProfileBundleCatalogEntryIdentity Identity { get; } = RequiredValue.NotNull(identity);
    internal JsonElement Document { get; } = document;
}

/// <summary>All trusted bundle identities and immutable canonical JSON trees consumed atomically by Profiles.</summary>
internal sealed class TrustedProfileBundleCatalogSource
{
    private readonly TrustedFirmwareFamilyJsonSource[] _families;
    private readonly TrustedCompositionProfileJsonSource[] _profiles;

    internal TrustedProfileBundleCatalogSource(
        string manifestSha256,
        ProfileBundleIdentity bundleIdentity,
        IEnumerable<TrustedFirmwareFamilyJsonSource> families,
        IEnumerable<TrustedCompositionProfileJsonSource> profiles)
    {
        _ = CanonicalSha256.Require(manifestSha256, nameof(manifestSha256));
        ArgumentNullException.ThrowIfNull(bundleIdentity);
        _families = ImmutableReferenceSnapshot.Create(
            families,
            "Trusted bundle sources cannot contain null values.");
        _profiles = ImmutableReferenceSnapshot.Create(
            profiles,
            "Trusted bundle sources cannot contain null values.");

        ManifestSha256 = manifestSha256;
        BundleIdentity = bundleIdentity;
        Families = Array.AsReadOnly(_families);
        Profiles = Array.AsReadOnly(_profiles);
    }

    internal string ManifestSha256 { get; }

    internal ProfileBundleIdentity BundleIdentity { get; }

    internal IReadOnlyList<TrustedFirmwareFamilyJsonSource> Families { get; }

    internal IReadOnlyList<TrustedCompositionProfileJsonSource> Profiles { get; }

}
