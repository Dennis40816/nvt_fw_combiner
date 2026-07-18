namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable evidence-backed collection of canonical metadata structures.</summary>
public sealed class FirmwareMetadataSet : IFirmwareMapFact
{
    private readonly FirmwareMetadataStructure[] _structures;
    private readonly string[] _evidenceRefs;

    /// <summary>Creates a non-empty metadata fact set.</summary>
    public FirmwareMetadataSet(
        string metadataSetId,
        IEnumerable<FirmwareMetadataStructure> structures,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataSetId);

        _structures = Composition.ImmutableReferenceSnapshot.CreateUnique(
            structures,
            static structure => structure.StructureId,
            "Firmware metadata sets require non-null structures.",
            "Metadata structure ids must be ordinally unique within a set.",
            StringComparer.Ordinal,
            requireValue: true);

        Array.Sort(_structures, static (left, right) =>
            StringComparer.Ordinal.Compare(left.StructureId, right.StructureId));
        _evidenceRefs = ImmutableStringSnapshot.Create(
            evidenceRefs,
            nameof(evidenceRefs),
            "Firmware metadata sets require evidence.",
            "Evidence references cannot contain null or whitespace.",
            "Evidence references must be ordinally unique.");

        MetadataSetId = metadataSetId;
        Structures = Array.AsReadOnly(_structures);
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Stable metadata fact-set identifier.</summary>
    public string MetadataSetId { get; }

    /// <inheritdoc />
    public FirmwareFactKind FactKind => FirmwareFactKind.MetadataSet;

    /// <inheritdoc />
    public string CanonicalFactId => MetadataSetId;

    /// <summary>Metadata structures in ordinal id order.</summary>
    public IReadOnlyList<FirmwareMetadataStructure> Structures { get; }

    /// <summary>Evidence manifest ids in ordinal order.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

}
