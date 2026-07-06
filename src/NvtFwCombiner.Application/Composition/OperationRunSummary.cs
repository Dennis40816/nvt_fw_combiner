using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report status for one planned operation.</summary>
public sealed class OperationRunSummary
{
    private readonly ByteRange[] _processorAllowedReadRanges;
    private readonly ByteRange[] _processorAllowedWriteRanges;

    /// <summary>Creates an operation run summary.</summary>
    public OperationRunSummary(
        string operationId,
        int sequence,
        CompositionOperationKind kind,
        OperationRunStatus status,
        string? sourceSpaceId,
        ByteRange? sourceRange,
        string targetSpaceId,
        ByteRange targetRange,
        OverlapPolicy overlapPolicy,
        string? processorId,
        string? toolBindingId,
        IReadOnlyList<ByteRange> processorAllowedReadRanges,
        IReadOnlyList<ByteRange> processorAllowedWriteRanges,
        string reason,
        OperationProvenance? provenance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSpaceId);
        ArgumentNullException.ThrowIfNull(processorAllowedReadRanges);
        ArgumentNullException.ThrowIfNull(processorAllowedWriteRanges);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        if ((processorId is null) != (toolBindingId is null))
        {
            throw new ArgumentException("Processor id and tool binding id must both be supplied or both be null.");
        }

        OperationId = operationId;
        Sequence = sequence;
        Kind = kind;
        Status = status;
        SourceSpaceId = sourceSpaceId;
        SourceRange = sourceRange;
        TargetSpaceId = targetSpaceId;
        TargetRange = targetRange;
        OverlapPolicy = overlapPolicy;
        ProcessorId = string.IsNullOrWhiteSpace(processorId) ? null : processorId;
        ToolBindingId = string.IsNullOrWhiteSpace(toolBindingId) ? null : toolBindingId;
        _processorAllowedReadRanges = [.. processorAllowedReadRanges];
        _processorAllowedWriteRanges = [.. processorAllowedWriteRanges];
        Reason = reason;
        Provenance = provenance ?? OperationProvenance.BuiltInProfile;
    }

    /// <summary>Stable operation id.</summary>
    public string OperationId { get; }

    /// <summary>Plan sequence.</summary>
    public int Sequence { get; }

    /// <summary>Operation primitive kind.</summary>
    public CompositionOperationKind Kind { get; }

    /// <summary>Run status.</summary>
    public OperationRunStatus Status { get; }

    /// <summary>Source address space for copy-like operations.</summary>
    public string? SourceSpaceId { get; }

    /// <summary>Source range for copy-like operations.</summary>
    public ByteRange? SourceRange { get; }

    /// <summary>Mutable target address space.</summary>
    public string TargetSpaceId { get; }

    /// <summary>Target range written by this operation.</summary>
    public ByteRange TargetRange { get; }

    /// <summary>Declared overlap policy.</summary>
    public OverlapPolicy OverlapPolicy { get; }

    /// <summary>External processor id when this operation invokes a processor.</summary>
    public string? ProcessorId { get; }

    /// <summary>External tool binding id when this operation invokes a processor.</summary>
    public string? ToolBindingId { get; }

    /// <summary>Profile-declared processor read ranges.</summary>
    public IReadOnlyList<ByteRange> ProcessorAllowedReadRanges => _processorAllowedReadRanges;

    /// <summary>Profile-declared processor write ranges.</summary>
    public IReadOnlyList<ByteRange> ProcessorAllowedWriteRanges => _processorAllowedWriteRanges;

    /// <summary>Profile-declared reason shown in reports.</summary>
    public string Reason { get; }

    /// <summary>Traceable origin for this operation.</summary>
    public OperationProvenance Provenance { get; }
}
