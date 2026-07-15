namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed comparison used by firmware-map metadata applicability.</summary>
public enum FirmwareMetadataPredicateOperator
{
    /// <summary>The decoded value must equal one expected value.</summary>
    Equal,

    /// <summary>The decoded value must not equal one expected value.</summary>
    NotEqual,

    /// <summary>The decoded value must equal one of multiple expected values.</summary>
    OneOf,
}

/// <summary>Three-state predicate result used by fail-closed map resolution.</summary>
public enum FirmwarePredicateResult
{
    /// <summary>The required metadata fact has not been decoded.</summary>
    Missing,

    /// <summary>The decoded fact satisfies the predicate.</summary>
    Match,

    /// <summary>The decoded fact contradicts the predicate.</summary>
    NoMatch,
}

/// <summary>One immutable metadata predicate in firmware-map applicability.</summary>
public sealed class FirmwareMetadataPredicate
{
    private readonly FirmwareMetadataValue[] _expectedValues;

    /// <summary>Creates a validated predicate.</summary>
    public FirmwareMetadataPredicate(
        string metadataStructureId,
        string fieldId,
        FirmwareMetadataPredicateOperator comparison,
        IEnumerable<FirmwareMetadataValue> expectedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataStructureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        if (!Enum.IsDefined(comparison))
        {
            throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown metadata comparison.");
        }

        ArgumentNullException.ThrowIfNull(expectedValues);
        _expectedValues = [.. expectedValues];
        if (_expectedValues.Length == 0)
        {
            throw new ArgumentException("Metadata predicates require an expected value.", nameof(expectedValues));
        }

        if (_expectedValues.Any(static value => value is null))
        {
            throw new ArgumentException("Metadata predicate expected values cannot contain null.", nameof(expectedValues));
        }

        if (_expectedValues.Distinct().Count() != _expectedValues.Length)
        {
            throw new ArgumentException("Metadata predicate expected values must be unique.", nameof(expectedValues));
        }

        if (comparison is FirmwareMetadataPredicateOperator.Equal or FirmwareMetadataPredicateOperator.NotEqual &&
            _expectedValues.Length != 1)
        {
            throw new ArgumentException("Equal and not-equal predicates require exactly one value.", nameof(expectedValues));
        }

        MetadataStructureId = metadataStructureId;
        FieldId = fieldId;
        Comparison = comparison;
        ExpectedValues = Array.AsReadOnly(_expectedValues);
    }

    /// <summary>Canonical metadata structure that scopes this field predicate.</summary>
    public string MetadataStructureId { get; }

    /// <summary>Canonical metadata field identifier.</summary>
    public string FieldId { get; }

    /// <summary>Closed comparison operator.</summary>
    public FirmwareMetadataPredicateOperator Comparison { get; }

    /// <summary>Immutable expected values.</summary>
    public IReadOnlyList<FirmwareMetadataValue> ExpectedValues { get; }

    /// <summary>Evaluates fields already scoped to <see cref="MetadataStructureId"/>.</summary>
    public FirmwareMetadataPredicateOutcome Evaluate(
        IReadOnlyDictionary<string, FirmwareMetadataValue> scopedFields)
    {
        ArgumentNullException.ThrowIfNull(scopedFields);
        FirmwareMetadataValue? actual = FindExactField(scopedFields);
        FirmwarePredicateResult result = actual is not null
            ? Compare(actual)
            : FirmwarePredicateResult.Missing;
        return new FirmwareMetadataPredicateOutcome(this, result, actual);
    }

    private FirmwareMetadataValue? FindExactField(
        IReadOnlyDictionary<string, FirmwareMetadataValue> scopedFields)
    {
        foreach (KeyValuePair<string, FirmwareMetadataValue> field in scopedFields)
        {
            if (StringComparer.Ordinal.Equals(field.Key, FieldId))
            {
                return field.Value;
            }
        }

        return null;
    }

    private FirmwarePredicateResult Compare(FirmwareMetadataValue actual)
    {
        return Comparison switch
        {
            FirmwareMetadataPredicateOperator.Equal =>
                actual == _expectedValues[0]
                    ? FirmwarePredicateResult.Match
                    : FirmwarePredicateResult.NoMatch,
            FirmwareMetadataPredicateOperator.NotEqual =>
                actual != _expectedValues[0]
                    ? FirmwarePredicateResult.Match
                    : FirmwarePredicateResult.NoMatch,
            FirmwareMetadataPredicateOperator.OneOf =>
                _expectedValues.Contains(actual)
                    ? FirmwarePredicateResult.Match
                    : FirmwarePredicateResult.NoMatch,
            _ => throw new InvalidOperationException("Unknown metadata comparison."),
        };
    }
}
