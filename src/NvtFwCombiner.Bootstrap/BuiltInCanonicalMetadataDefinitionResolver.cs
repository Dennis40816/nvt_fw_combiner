using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Resolves exact metadata identities from the hash-closed built-in bundle set.
/// </summary>
internal sealed class BuiltInCanonicalMetadataDefinitionResolver() :
    IFirmwareMetadataStructureDefinitionResolver
{
    internal static BuiltInCanonicalMetadataDefinitionResolver Instance { get; } = new();

    public bool TryResolve(
        FirmwareMetadataStructureDefinitionReferenceDocument reference,
        out FirmwareMetadataStructureDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ProfileBundlePackageTrustEntry? providerBundle =
            BuiltInV2BundleRegistry.TrustIndex.Bundles.SingleOrDefault(entry =>
                entry.MetadataProviderFamilies.Any(provider =>
                    StringComparer.Ordinal.Equals(provider.FamilyId, reference.FamilyId) &&
                    StringComparer.Ordinal.Equals(provider.FamilyVersion, reference.FamilyVersion)));
        definition = null;
        return providerBundle is not null &&
            BuiltInV2BundleRegistry.All[providerBundle.BundleDirectory]
                .TryResolveMetadataDefinition(
                    reference,
                    out definition);
    }
}
