namespace NvtFwCombiner.Domain.Composition;

/// <summary>Byte-level mutation trace for one executed operation.</summary>
public sealed class MutationRecord
{
    /// <summary>Creates a mutation record from the observed before and after bytes.</summary>
    public MutationRecord(
        string operationId,
        CompositionOperationKind operationKind,
        string targetSpaceId,
        ByteRange targetRange,
        IReadOnlyList<ByteRange> changedRanges,
        string beforeSha256,
        string afterSha256,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSpaceId);
        ArgumentNullException.ThrowIfNull(changedRanges);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(afterSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        OperationId = operationId;
        OperationKind = operationKind;
        TargetSpaceId = targetSpaceId;
        TargetRange = targetRange;
        ChangedRanges = changedRanges;
        BeforeSha256 = beforeSha256;
        AfterSha256 = afterSha256;
        Reason = reason;
    }

    /// <summary>Operation id that produced this trace.</summary>
    public string OperationId { get; }

    /// <summary>Operation primitive kind.</summary>
    public CompositionOperationKind OperationKind { get; }

    /// <summary>Target address space mutated by the operation.</summary>
    public string TargetSpaceId { get; }

    /// <summary>Declared target range for the operation.</summary>
    public ByteRange TargetRange { get; }

    /// <summary>Observed changed ranges in target address-space coordinates.</summary>
    public IReadOnlyList<ByteRange> ChangedRanges { get; }

    /// <summary>SHA-256 hash of the target range before the operation.</summary>
    public string BeforeSha256 { get; }

    /// <summary>SHA-256 hash of the target range after the operation.</summary>
    public string AfterSha256 { get; }

    /// <summary>Profile-declared reason for the mutation.</summary>
    public string Reason { get; }
}
