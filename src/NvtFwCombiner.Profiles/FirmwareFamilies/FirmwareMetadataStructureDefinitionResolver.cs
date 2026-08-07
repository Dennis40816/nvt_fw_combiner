using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

/// <summary>Exact trusted identity of one canonical metadata structure definition.</summary>
internal sealed record FirmwareMetadataStructureDefinitionReference
{
    public FirmwareMetadataStructureDefinitionReference(
        string familyId,
        string familyVersion,
        string familyContentHash,
        string structureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyContentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(structureId);
        FamilyId = familyId;
        FamilyVersion = familyVersion;
        FamilyContentHash = familyContentHash;
        StructureId = structureId;
    }

    public string FamilyId { get; }

    public string FamilyVersion { get; }

    public string FamilyContentHash { get; }

    public string StructureId { get; }
}

/// <summary>
/// Resolves only exact, trusted canonical metadata definitions. Implementations
/// must not infer aliases or copy field declarations.
/// </summary>
internal interface IFirmwareMetadataStructureDefinitionResolver
{
    bool TryResolve(
        FirmwareMetadataStructureDefinitionReference reference,
        out FirmwareMetadataStructureDefinition? definition);
}
