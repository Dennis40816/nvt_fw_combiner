using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report status for one planned operation.</summary>
public sealed class OperationRunSummary
{
    /// <summary>Creates an operation run summary.</summary>
    public OperationRunSummary(string operationId, int sequence, CompositionOperationKind kind, OperationRunStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        OperationId = operationId;
        Sequence = sequence;
        Kind = kind;
        Status = status;
    }

    /// <summary>Stable operation id.</summary>
    public string OperationId { get; }

    /// <summary>Plan sequence.</summary>
    public int Sequence { get; }

    /// <summary>Operation primitive kind.</summary>
    public CompositionOperationKind Kind { get; }

    /// <summary>Run status.</summary>
    public OperationRunStatus Status { get; }
}
