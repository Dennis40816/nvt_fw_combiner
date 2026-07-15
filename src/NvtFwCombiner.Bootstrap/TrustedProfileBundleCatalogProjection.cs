using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Structural Bootstrap-only bridge from Infrastructure's trusted JSON projection to Profiles normalization.</summary>
internal static class TrustedProfileBundleCatalogProjection
{
    internal static TrustedProfileBundleCatalog Create(TrustedProfileBundleDocumentProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return TrustedProfileBundleCatalogFactory.Create(new TrustedProfileBundleCatalogSource(
            projection.ManifestSha256,
            projection.BundleId,
            projection.BundleVersion,
            projection.BundleContentHash,
            projection.TrustAnchorBindingId,
            projection.Families.Select(static family => new TrustedFirmwareFamilyJsonSource(
                CopyIdentity(family.Identity),
                family.Document)),
            projection.Profiles.Select(static profile => new TrustedCompositionProfileJsonSource(
                CopyIdentity(profile.Identity),
                profile.Document))));
    }

    private static TrustedProfileBundleCatalogEntryIdentity CopyIdentity(
        TrustedProfileBundleDocumentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return new TrustedProfileBundleCatalogEntryIdentity(
            identity.EntryId,
            identity.Path,
            identity.SchemaId,
            identity.ContentHash);
    }
}
