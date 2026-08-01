using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

/// <summary>Exact trusted identity of one canonical metadata structure definition.</summary>
public sealed record FirmwareMetadataStructureDefinitionReference
{
    /// <summary>Creates one checked exact definition identity.</summary>
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

    /// <summary>Exact provider family identifier.</summary>
    public string FamilyId { get; }

    /// <summary>Exact provider family semantic version.</summary>
    public string FamilyVersion { get; }

    /// <summary>Exact provider family content hash.</summary>
    public string FamilyContentHash { get; }

    /// <summary>Canonical logical structure identifier in the provider.</summary>
    public string StructureId { get; }
}

/// <summary>
/// Resolves only exact, trusted canonical metadata definitions. Implementations
/// must not infer aliases or copy field declarations.
/// </summary>
public interface IFirmwareMetadataStructureDefinitionResolver
{
    /// <summary>Resolves one exact trusted definition identity.</summary>
    bool TryResolve(
        FirmwareMetadataStructureDefinitionReference reference,
        out FirmwareMetadataStructureDefinition? definition);
}
