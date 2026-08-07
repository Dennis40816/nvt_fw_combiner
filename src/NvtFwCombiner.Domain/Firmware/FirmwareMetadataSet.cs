using System.Collections.ObjectModel;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable evidence-backed collection of canonical metadata structures.</summary>
public sealed class FirmwareMetadataSet(
    string metadataSetId,
    IEnumerable<FirmwareMetadataStructure> structures,
    IEnumerable<string> evidenceRefs) : IFirmwareMapFact
{
    /// <summary>Stable metadata fact-set identifier.</summary>
    public string MetadataSetId { get; } = RequiredValue.NotBlank(metadataSetId);

    /// <inheritdoc />
    public FirmwareFactKind FactKind => FirmwareFactKind.MetadataSet;

    /// <inheritdoc />
    public string CanonicalFactId => MetadataSetId;

    /// <summary>Metadata structures in ordinal id order.</summary>
    public IReadOnlyList<FirmwareMetadataStructure> Structures { get; } = SnapshotStructures(structures);

    /// <summary>Evidence manifest ids in ordinal order.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; } = Array.AsReadOnly(
        ImmutableStringSnapshot.Create(
            evidenceRefs,
            nameof(evidenceRefs),
            "Firmware metadata sets require evidence.",
            "Evidence references cannot contain null or whitespace.",
            "Evidence references must be ordinally unique."));

    private static ReadOnlyCollection<FirmwareMetadataStructure> SnapshotStructures(
        IEnumerable<FirmwareMetadataStructure> structures)
    {
        FirmwareMetadataStructure[] snapshot = Composition.ImmutableReferenceSnapshot.CreateUnique(
            structures,
            static structure => structure.StructureId,
            "Firmware metadata sets require non-null structures.",
            "Metadata structure ids must be ordinally unique within a set.",
            StringComparer.Ordinal,
            requireValue: true);
        Array.Sort(snapshot, static (left, right) =>
            StringComparer.Ordinal.Compare(left.StructureId, right.StructureId));
        return Array.AsReadOnly(snapshot);
    }
}
