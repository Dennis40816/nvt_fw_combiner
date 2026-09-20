using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Loads declared read-only views from exact owning providers during one catalog candidate load.</summary>
internal static class CanonicalFullImageMetadataInventory
{
    internal static IReadOnlyList<MetadataPlanDefinition> Create()
    {
        return Create(BuiltInV2BundleRegistry.TrustIndex.Bundles);
    }

    internal static IReadOnlyList<MetadataPlanDefinition> Create(IEnumerable<ProfileBundlePackageTrustEntry> bundles)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        return [.. bundles.SelectMany(CreatePlans)
            .OrderBy(static plan => plan.FullImageContext!.Family.FamilyId, StringComparer.Ordinal)
            .ThenBy(static plan => plan.FullImageContext!.View.ViewId, StringComparer.Ordinal)
            .ThenBy(static plan => plan.FullImageContext!.MemberId, StringComparer.Ordinal)];
    }

    private static IEnumerable<MetadataPlanDefinition> CreatePlans(ProfileBundlePackageTrustEntry bundle)
    {
        return !BuiltInV2BundleRegistry.All.TryGetValue(bundle.BundleDirectory, out BuiltInV2Bundle? owner) ||
            !StringComparer.Ordinal.Equals(owner.ContentHash, bundle.ContentHash)
            ? throw new InvalidDataException("Full-image provider must match its exact trusted owning bundle.")
            : bundle.MetadataProviderFamilies.SelectMany(owner.CreateFullImageMetadataPlans);
    }
}
