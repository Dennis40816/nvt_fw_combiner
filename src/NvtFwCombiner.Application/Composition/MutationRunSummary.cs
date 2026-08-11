using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application report-safe mutation summary derived from domain mutation records.</summary>
public sealed class MutationRunSummary(
    string operationId,
    CompositionOperationKind kind,
    string targetSpaceId,
    ByteRange targetRange,
    long changedByteCount,
    string beforeSha256,
    string afterSha256,
    string reason)
{
    /// <summary>Operation that produced the mutation.</summary>
    public string OperationId { get; } = CompositionSummaryValue.NotBlank(operationId, nameof(operationId));

    /// <summary>Operation primitive kind.</summary>
    public CompositionOperationKind Kind { get; } = kind;

    /// <summary>Target address space.</summary>
    public string TargetSpaceId { get; } = CompositionSummaryValue.NotBlank(targetSpaceId, nameof(targetSpaceId));

    /// <summary>Target byte range.</summary>
    public ByteRange TargetRange { get; } = targetRange;

    /// <summary>Total changed bytes across changed ranges.</summary>
    public long ChangedByteCount { get; } = CompositionSummaryValue.NonNegative(
        changedByteCount,
        nameof(changedByteCount));

    /// <summary>Lowercase SHA-256 before mutation.</summary>
    public string BeforeSha256 { get; } = CompositionSummaryValue.NotBlank(
        beforeSha256,
        nameof(beforeSha256));

    /// <summary>Lowercase SHA-256 after mutation.</summary>
    public string AfterSha256 { get; } = CompositionSummaryValue.NotBlank(
        afterSha256,
        nameof(afterSha256));

    /// <summary>Human-readable mutation reason.</summary>
    public string Reason { get; } = CompositionSummaryValue.NotBlank(reason, nameof(reason));
}
