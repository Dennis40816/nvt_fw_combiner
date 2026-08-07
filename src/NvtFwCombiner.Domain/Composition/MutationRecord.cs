namespace NvtFwCombiner.Domain.Composition;

/// <summary>Byte-level mutation trace for one executed operation.</summary>
public sealed class MutationRecord(
    string operationId,
    CompositionOperationKind operationKind,
    string targetSpaceId,
    ByteRange targetRange,
    IReadOnlyList<ByteRange> changedRanges,
    string beforeSha256,
    string afterSha256,
    string reason)
{
    /// <summary>Operation id that produced this trace.</summary>
    public string OperationId { get; } = RequiredValue.NotBlank(operationId);

    /// <summary>Operation primitive kind.</summary>
    public CompositionOperationKind OperationKind { get; } = operationKind;

    /// <summary>Target address space mutated by the operation.</summary>
    public string TargetSpaceId { get; } = RequiredValue.NotBlank(targetSpaceId);

    /// <summary>Declared target range for the operation.</summary>
    public ByteRange TargetRange { get; } = targetRange;

    /// <summary>Observed changed ranges in target address-space coordinates.</summary>
    public IReadOnlyList<ByteRange> ChangedRanges { get; } = RequiredValue.NotNull(changedRanges);

    /// <summary>SHA-256 hash of the target range before the operation.</summary>
    public string BeforeSha256 { get; } = RequiredValue.NotBlank(beforeSha256);

    /// <summary>SHA-256 hash of the target range after the operation.</summary>
    public string AfterSha256 { get; } = RequiredValue.NotBlank(afterSha256);

    /// <summary>Profile-declared reason for the mutation.</summary>
    public string Reason { get; } = RequiredValue.NotBlank(reason);
}
