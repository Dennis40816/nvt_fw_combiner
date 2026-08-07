namespace NvtFwCombiner.Domain.Firmware;

internal sealed record FirmwareMetadataPredicateOutcome(
    FirmwareMetadataPredicate Predicate,
    FirmwarePredicateResult Result,
    FirmwareMetadataValue? ActualValue);
