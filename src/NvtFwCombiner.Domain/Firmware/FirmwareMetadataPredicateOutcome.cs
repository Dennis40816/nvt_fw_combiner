namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable result of evaluating one metadata predicate against candidate-scoped facts.</summary>
public sealed class FirmwareMetadataPredicateOutcome
{
    internal FirmwareMetadataPredicateOutcome(
        FirmwareMetadataPredicate predicate,
        FirmwarePredicateResult result,
        FirmwareMetadataValue? actualValue)
    {
        Predicate = RequiredValue.NotNull(predicate);
        ClosedEnum.ThrowIfUndefined(result, "Unknown predicate result.");

        bool isMissing = result == FirmwarePredicateResult.Missing;
        if (isMissing != (actualValue is null))
        {
            throw new ArgumentException(
                "Only a missing predicate outcome may omit the actual value.",
                nameof(actualValue));
        }

        Result = result;
        ActualValue = actualValue;
    }

    /// <summary>Exact immutable predicate that was evaluated.</summary>
    public FirmwareMetadataPredicate Predicate { get; }

    /// <summary>Three-state typed comparison result.</summary>
    public FirmwarePredicateResult Result { get; }

    /// <summary>Exact decoded value for match/no-match; null only when the fact is missing.</summary>
    public FirmwareMetadataValue? ActualValue { get; }
}
