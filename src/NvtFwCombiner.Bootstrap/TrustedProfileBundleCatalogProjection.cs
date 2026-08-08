using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.FirmwareFamilies;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Structural Bootstrap-only bridge from Infrastructure's trusted JSON projection to Profiles normalization.</summary>
internal static class TrustedProfileBundleCatalogProjection
{
    internal static TrustedProfileBundleCatalog Create(
        TrustedProfileBundleDocumentProjection projection,
        IFirmwareMetadataStructureDefinitionResolver? metadataDefinitionResolver = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return TrustedProfileBundleCatalogFactory.Create(
            projection.ManifestSha256,
            new ProfileBundleIdentity(
                projection.BundleId,
                projection.BundleVersion,
                projection.BundleContentHash,
                projection.TrustAnchorBindingId),
            projection.Families.Select(static family =>
                (CopyIdentity(family.Identity), family.Document)),
            projection.Profiles.Select(static profile =>
                (CopyIdentity(profile.Identity), profile.Document)),
            metadataDefinitionResolver);
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
