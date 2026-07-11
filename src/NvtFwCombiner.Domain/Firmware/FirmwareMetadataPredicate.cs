namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed scalar kind decoded from a canonical firmware metadata field.</summary>
public enum FirmwareMetadataValueKind
{
    /// <summary>Boolean flag.</summary>
    Flag,

    /// <summary>Signed integer value.</summary>
    SignedInteger,

    /// <summary>Non-empty text value.</summary>
    Text,
}

/// <summary>One immutable typed firmware metadata scalar.</summary>
public sealed record FirmwareMetadataValue
{
    private FirmwareMetadataValue(
        FirmwareMetadataValueKind kind,
        bool? flagValue,
        long? integerValue,
        string? textValue)
    {
        Kind = kind;
        FlagValue = flagValue;
        IntegerValue = integerValue;
        TextValue = textValue;
    }

    /// <summary>Scalar kind.</summary>
    public FirmwareMetadataValueKind Kind { get; }

    /// <summary>Boolean value when <see cref="Kind"/> is <see cref="FirmwareMetadataValueKind.Flag"/>.</summary>
    public bool? FlagValue { get; }

    /// <summary>Integer value when <see cref="Kind"/> is <see cref="FirmwareMetadataValueKind.SignedInteger"/>.</summary>
    public long? IntegerValue { get; }

    /// <summary>Text value when <see cref="Kind"/> is <see cref="FirmwareMetadataValueKind.Text"/>.</summary>
    public string? TextValue { get; }

    /// <summary>Creates a boolean metadata value.</summary>
    public static FirmwareMetadataValue FromFlag(bool value)
    {
        return new FirmwareMetadataValue(FirmwareMetadataValueKind.Flag, value, null, null);
    }

    /// <summary>Creates a signed integer metadata value.</summary>
    public static FirmwareMetadataValue FromInteger(long value)
    {
        return new FirmwareMetadataValue(FirmwareMetadataValueKind.SignedInteger, null, value, null);
    }

    /// <summary>Creates a non-empty text metadata value.</summary>
    public static FirmwareMetadataValue FromText(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return new FirmwareMetadataValue(FirmwareMetadataValueKind.Text, null, null, value);
    }
}

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
        string fieldId,
        FirmwareMetadataPredicateOperator comparison,
        IEnumerable<FirmwareMetadataValue> expectedValues)
    {
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

        FieldId = fieldId;
        Comparison = comparison;
        ExpectedValues = Array.AsReadOnly(_expectedValues);
    }

    /// <summary>Canonical metadata field identifier.</summary>
    public string FieldId { get; }

    /// <summary>Closed comparison operator.</summary>
    public FirmwareMetadataPredicateOperator Comparison { get; }

    /// <summary>Immutable expected values.</summary>
    public IReadOnlyList<FirmwareMetadataValue> ExpectedValues { get; }

    /// <summary>Evaluates this predicate without treating a missing fact as a mismatch.</summary>
    public FirmwarePredicateResult Evaluate(
        IReadOnlyDictionary<string, FirmwareMetadataValue> decodedFacts)
    {
        ArgumentNullException.ThrowIfNull(decodedFacts);
        FirmwareMetadataValue? actual = FindExactFact(decodedFacts);
        return actual is not null
            ? Compare(actual)
            : FirmwarePredicateResult.Missing;
    }

    private FirmwareMetadataValue? FindExactFact(
        IReadOnlyDictionary<string, FirmwareMetadataValue> decodedFacts)
    {
        foreach (KeyValuePair<string, FirmwareMetadataValue> fact in decodedFacts)
        {
            if (StringComparer.Ordinal.Equals(fact.Key, FieldId))
            {
                return fact.Value;
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
