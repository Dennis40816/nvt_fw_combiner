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

        ArgumentNullException.ThrowIfNull(structures);
        _structures = [.. structures];
        if (_structures.Length == 0)
        {
            throw new ArgumentException("Firmware metadata sets cannot be empty.", nameof(structures));
        }

        if (_structures.Any(static structure => structure is null))
        {
            throw new ArgumentException("Firmware metadata sets cannot contain null.", nameof(structures));
        }

        if (_structures.Select(static structure => structure.StructureId).Distinct(StringComparer.Ordinal).Count() !=
            _structures.Length)
        {
            throw new ArgumentException("Metadata structure ids must be ordinally unique within a set.", nameof(structures));
        }

        Array.Sort(_structures, static (left, right) =>
            StringComparer.Ordinal.Compare(left.StructureId, right.StructureId));
        _evidenceRefs = SnapshotEvidenceRefs(evidenceRefs);

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

    private static string[] SnapshotEvidenceRefs(IEnumerable<string> evidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        string[] snapshot = [.. evidenceRefs];
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("Firmware metadata sets require evidence.", nameof(evidenceRefs));
        }

        if (snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Evidence references cannot contain null or whitespace.", nameof(evidenceRefs));
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Evidence references must be ordinally unique.", nameof(evidenceRefs));
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }
}
