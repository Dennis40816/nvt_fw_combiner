using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

/// <summary>
/// Resolves only exact, trusted canonical metadata definitions. Implementations
/// must not infer aliases or copy field declarations.
/// </summary>
internal interface IFirmwareMetadataStructureDefinitionResolver
{
    bool TryResolve(
        FirmwareMetadataStructureDefinitionReferenceDocument reference,
        out FirmwareMetadataStructureDefinition? definition);
}
