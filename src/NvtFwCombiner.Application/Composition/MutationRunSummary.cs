using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application report-safe mutation summary derived from domain mutation records.</summary>
public sealed class MutationRunSummary
{
    /// <summary>Creates a mutation run summary.</summary>
    public MutationRunSummary(
        string operationId,
        CompositionOperationKind kind,
        string targetSpaceId,
        ByteRange targetRange,
        long changedByteCount,
        string beforeSha256,
        string afterSha256,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSpaceId);
        ArgumentOutOfRangeException.ThrowIfNegative(changedByteCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(afterSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        OperationId = operationId;
        Kind = kind;
        TargetSpaceId = targetSpaceId;
        TargetRange = targetRange;
        ChangedByteCount = changedByteCount;
        BeforeSha256 = beforeSha256;
        AfterSha256 = afterSha256;
        Reason = reason;
    }

    /// <summary>Operation that produced the mutation.</summary>
    public string OperationId { get; }

    /// <summary>Operation primitive kind.</summary>
    public CompositionOperationKind Kind { get; }

    /// <summary>Target address space.</summary>
    public string TargetSpaceId { get; }

    /// <summary>Target byte range.</summary>
    public ByteRange TargetRange { get; }

    /// <summary>Total changed bytes across changed ranges.</summary>
    public long ChangedByteCount { get; }

    /// <summary>Lowercase SHA-256 before mutation.</summary>
    public string BeforeSha256 { get; }

    /// <summary>Lowercase SHA-256 after mutation.</summary>
    public string AfterSha256 { get; }

    /// <summary>Human-readable mutation reason.</summary>
    public string Reason { get; }
}
