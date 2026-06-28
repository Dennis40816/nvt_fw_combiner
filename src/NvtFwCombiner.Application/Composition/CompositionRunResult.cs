using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application-level preview or build result with report summary.</summary>
public sealed class CompositionRunResult
{
    private readonly byte[] _outputBytes;

    /// <summary>Creates a run result.</summary>
    public CompositionRunResult(
        CompositionExecutionStatus status,
        byte[] outputBytes,
        CompositionRunReport report,
        string? committedOutputId)
    {
        ArgumentNullException.ThrowIfNull(outputBytes);
        ArgumentNullException.ThrowIfNull(report);

        Status = status;
        _outputBytes = [.. outputBytes];
        Report = report;
        CommittedOutputId = string.IsNullOrWhiteSpace(committedOutputId) ? null : committedOutputId;
    }

    /// <summary>Execution status returned by the shared domain engine.</summary>
    public CompositionExecutionStatus Status { get; }

    /// <summary>Output bytes for preview/build when execution succeeded.</summary>
    public ReadOnlyMemory<byte> OutputBytes => _outputBytes;

    /// <summary>Deterministic report summary for UI, CLI, and regression tests.</summary>
    public CompositionRunReport Report { get; }

    /// <summary>Adapter-owned destination id when build committed output.</summary>
    public string? CommittedOutputId { get; }
}
