namespace NvtFwCombiner.Domain.Firmware;

/// <summary>One immutable typed fact decoded from an exact metadata field source.</summary>
public sealed record FirmwareDecodedMetadataFact
{
    internal FirmwareDecodedMetadataFact(
        string artifactBindingId,
        string metadataStructureId,
        string fieldId,
        FirmwareMetadataValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataStructureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        ArgumentNullException.ThrowIfNull(value);

        ArtifactBindingId = artifactBindingId;
        MetadataStructureId = metadataStructureId;
        FieldId = fieldId;
        Value = value;
    }

    /// <summary>Exact runtime artifact binding used to decode this fact.</summary>
    public string ArtifactBindingId { get; }

    /// <summary>Family-global metadata structure identifier.</summary>
    public string MetadataStructureId { get; }

    /// <summary>Field identifier unique inside the metadata structure.</summary>
    public string FieldId { get; }

    /// <summary>Closed typed value decoded from the declared field carrier.</summary>
    public FirmwareMetadataValue Value { get; }
}

/// <summary>Atomic successful decode of one exact firmware metadata structure slice.</summary>
public sealed class FirmwareDecodedMetadataStructure
{
    private readonly FirmwareDecodedMetadataFact[] _facts;

    internal FirmwareDecodedMetadataStructure(
        string artifactBindingId,
        string metadataStructureId,
        IEnumerable<FirmwareDecodedMetadataFact> facts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataStructureId);
        ArgumentNullException.ThrowIfNull(facts);

        _facts = [.. facts];
        if (_facts.Any(static fact => fact is null))
        {
            throw new ArgumentException("Decoded metadata structures cannot contain null facts.", nameof(facts));
        }

        if (_facts.Any(fact =>
            !StringComparer.Ordinal.Equals(fact.ArtifactBindingId, artifactBindingId) ||
            !StringComparer.Ordinal.Equals(fact.MetadataStructureId, metadataStructureId)))
        {
            throw new ArgumentException("Decoded metadata fact identity must match its structure.", nameof(facts));
        }

        if (_facts.Select(static fact => fact.FieldId).Distinct(StringComparer.Ordinal).Count() != _facts.Length)
        {
            throw new ArgumentException("Decoded metadata field ids must be ordinally unique.", nameof(facts));
        }

        ArtifactBindingId = artifactBindingId;
        MetadataStructureId = metadataStructureId;
        Facts = Array.AsReadOnly(_facts);
    }

    /// <summary>Exact runtime artifact binding used by the structure.</summary>
    public string ArtifactBindingId { get; }

    /// <summary>Family-global metadata structure identifier.</summary>
    public string MetadataStructureId { get; }

    /// <summary>Decoded facts in the declaration's canonical field order.</summary>
    public IReadOnlyList<FirmwareDecodedMetadataFact> Facts { get; }
}
