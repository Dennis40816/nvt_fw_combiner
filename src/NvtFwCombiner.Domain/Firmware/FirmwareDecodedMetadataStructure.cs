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
        ArtifactBindingId = RequiredValue.NotBlank(artifactBindingId);
        MetadataStructureId = RequiredValue.NotBlank(metadataStructureId);
        FieldId = RequiredValue.NotBlank(fieldId);
        Value = RequiredValue.NotNull(value);
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

/// <summary>Evaluated result of one canonical metadata field relation.</summary>
public sealed record FirmwareDecodedMetadataRelation
{
    internal FirmwareDecodedMetadataRelation(
        string relationId,
        FirmwareMetadataFieldRelationKind kind,
        string sourceFieldId,
        string relatedFieldId,
        bool isSatisfied)
    {
        RelationId = RequiredValue.NotBlank(relationId);
        ClosedEnum.ThrowIfUndefined(kind, "Unknown metadata relation kind.");

        SourceFieldId = RequiredValue.NotBlank(sourceFieldId);
        RelatedFieldId = RequiredValue.NotBlank(relatedFieldId);
        Kind = kind;
        IsSatisfied = isSatisfied;
    }

    /// <summary>Canonical relation identifier.</summary>
    public string RelationId { get; }

    /// <summary>Closed relation kind.</summary>
    public FirmwareMetadataFieldRelationKind Kind { get; }

    /// <summary>Canonical source-field identifier.</summary>
    public string SourceFieldId { get; }

    /// <summary>Canonical related-field identifier.</summary>
    public string RelatedFieldId { get; }

    /// <summary>Whether the decoded values satisfy the declared relation.</summary>
    public bool IsSatisfied { get; }
}

/// <summary>Atomic successful decode of one exact firmware metadata structure slice.</summary>
public sealed class FirmwareDecodedMetadataStructure
{
    private readonly FirmwareDecodedMetadataFact[] _facts;
    private readonly FirmwareDecodedMetadataRelation[] _relations;

    internal FirmwareDecodedMetadataStructure(
        string artifactBindingId,
        string metadataStructureId,
        IEnumerable<FirmwareDecodedMetadataFact> facts,
        IEnumerable<FirmwareDecodedMetadataRelation>? relations = null)
    {
        ArtifactBindingId = RequiredValue.NotBlank(artifactBindingId);
        MetadataStructureId = RequiredValue.NotBlank(metadataStructureId);
        _facts = Composition.ImmutableReferenceSnapshot.Create(
            facts,
            "Decoded metadata structures cannot contain null facts.");

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

        _relations = Composition.ImmutableReferenceSnapshot.Create(
            relations ?? [],
            "Decoded metadata structures cannot contain null relations.");
        if (_relations.Select(static relation => relation.RelationId)
            .Distinct(StringComparer.Ordinal).Count() != _relations.Length)
        {
            throw new ArgumentException("Decoded metadata relation ids must be ordinally unique.", nameof(relations));
        }

        Facts = Array.AsReadOnly(_facts);
        Relations = Array.AsReadOnly(_relations);
    }

    /// <summary>Exact runtime artifact binding used by the structure.</summary>
    public string ArtifactBindingId { get; }

    /// <summary>Family-global metadata structure identifier.</summary>
    public string MetadataStructureId { get; }

    /// <summary>Decoded facts in the declaration's canonical field order.</summary>
    public IReadOnlyList<FirmwareDecodedMetadataFact> Facts { get; }

    /// <summary>Evaluated relation results in canonical relation-id order.</summary>
    public IReadOnlyList<FirmwareDecodedMetadataRelation> Relations { get; }
}
