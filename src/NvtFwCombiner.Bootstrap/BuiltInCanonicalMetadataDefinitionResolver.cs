using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Bootstrap-owned allow-list that links consumer bindings to exact trusted
/// canonical metadata providers.
/// </summary>
internal sealed class BuiltInCanonicalMetadataDefinitionResolver :
    IFirmwareMetadataStructureDefinitionResolver
{
    internal static BuiltInCanonicalMetadataDefinitionResolver Instance { get; } = new();

    private BuiltInCanonicalMetadataDefinitionResolver()
    {
    }

    public bool TryResolve(
        FirmwareMetadataStructureDefinitionReference reference,
        out FirmwareMetadataStructureDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(reference);
        string? providerBundle = (reference.FamilyId, reference.FamilyVersion) switch
        {
            ("nt51917-nt51927-nt51928-canonical-container", "1.4.0") =>
                "nt51927-standard-merge",
            ("nt51923-nt51926", "1.1.0") =>
                "nt51923-standard-merge",
            ("nt51929-nt51932", "1.3.0") =>
                "nt51929-dp-replace",
            ("nt51929-nt51932", "1.1.0") =>
                "nt51929-standard-merge",
            _ => null,
        };
        if (providerBundle is null)
        {
            definition = null;
            return false;
        }

        return BuiltInV2BundleRegistry.All[providerBundle]
            .TryResolveMetadataDefinition(reference, out definition);
    }
}
