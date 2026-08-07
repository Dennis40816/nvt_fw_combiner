namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed validation relationship between canonical metadata fields.</summary>
public enum FirmwareMetadataFieldRelationKind
{
    /// <summary>The related field is the fixed-width bitwise complement of the source field.</summary>
    BitwiseComplement,
}

/// <summary>Immutable typed relation between two fields in one metadata structure.</summary>
public sealed class FirmwareMetadataFieldRelation
{
    /// <summary>Creates one checked relation declaration.</summary>
    public FirmwareMetadataFieldRelation(
        string relationId,
        FirmwareMetadataFieldRelationKind kind,
        string sourceFieldId,
        string relatedFieldId)
    {
        RelationId = RequiredValue.NotBlank(relationId);
        ClosedEnum.ThrowIfUndefined(kind, "Unknown metadata field relation kind.");

        SourceFieldId = RequiredValue.NotBlank(sourceFieldId);
        RelatedFieldId = RequiredValue.NotBlank(relatedFieldId);
        if (StringComparer.Ordinal.Equals(sourceFieldId, relatedFieldId))
        {
            throw new ArgumentException("Metadata relations require two distinct fields.");
        }

        Kind = kind;
    }

    /// <summary>Stable relation identifier unique inside one metadata structure.</summary>
    public string RelationId { get; }

    /// <summary>Closed relation kind.</summary>
    public FirmwareMetadataFieldRelationKind Kind { get; }

    /// <summary>Canonical source-field identifier.</summary>
    public string SourceFieldId { get; }

    /// <summary>Canonical related-field identifier.</summary>
    public string RelatedFieldId { get; }

    internal bool Evaluate(
        IReadOnlyDictionary<string, FirmwareMetadataValue> values,
        int widthBytes)
    {
        return !values.TryGetValue(SourceFieldId, out FirmwareMetadataValue? source) ||
            !values.TryGetValue(RelatedFieldId, out FirmwareMetadataValue? related) ||
            source.UnsignedIntegerValue is not { } sourceValue ||
            related.UnsignedIntegerValue is not { } relatedValue
            ? throw new InvalidOperationException(
                "Validated metadata relation fields must decode as unsigned integers.")
            : Kind switch
            {
                FirmwareMetadataFieldRelationKind.BitwiseComplement =>
                    (sourceValue ^ relatedValue) == ((1UL << checked(widthBytes * 8)) - 1UL),
                _ => throw new InvalidOperationException("Unknown metadata field relation kind."),
            };
    }
}
