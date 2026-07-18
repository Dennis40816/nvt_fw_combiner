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
        string? committedOutputId,
        string? previewToken = null)
        : this(
            status,
            outputBytes ?? throw new ArgumentNullException(nameof(outputBytes)),
            report,
            committedOutputId,
            previewToken,
            inspectionReferenceSpaceId: null,
            inspectionReferenceBytes: null)
    {
    }

    internal CompositionRunResult(
        CompositionExecutionStatus status,
        ReadOnlyMemory<byte> outputBytes,
        CompositionRunReport report,
        string? committedOutputId,
        string? previewToken,
        string? inspectionReferenceSpaceId,
        byte[]? inspectionReferenceBytes)
    {
        ArgumentNullException.ThrowIfNull(report);
        if ((inspectionReferenceSpaceId is null) != (inspectionReferenceBytes is null))
        {
            throw new ArgumentException("Replace inspection requires both a reference space id and reference bytes.");
        }

        if (status != CompositionExecutionStatus.Succeeded && inspectionReferenceBytes is not null)
        {
            throw new ArgumentException("Only a successful Replace result may carry inspection bytes.");
        }

        Status = status;
        _outputBytes = outputBytes.ToArray();
        Report = report;
        CommittedOutputId = string.IsNullOrWhiteSpace(committedOutputId) ? null : committedOutputId;
        PreviewToken = string.IsNullOrWhiteSpace(previewToken) ? null : previewToken;
        InspectionSnapshot = inspectionReferenceBytes is { } referenceBytes
            ? new CompositionRunInspectionSnapshot(inspectionReferenceSpaceId!, referenceBytes, _outputBytes)
            : null;
    }

    /// <summary>Execution status returned by the shared domain engine.</summary>
    public CompositionExecutionStatus Status { get; }

    /// <summary>Output bytes for preview/build when execution succeeded.</summary>
    public ReadOnlyMemory<byte> OutputBytes => _outputBytes;

    /// <summary>
    /// In-memory reference/output bytes for successful Replace inspection, or <see langword="null"/> when unavailable.
    /// </summary>
    public CompositionRunInspectionSnapshot? InspectionSnapshot { get; }

    /// <summary>Application summary for UI, CLI, and regression tests; not the canonical v1 report artifact.</summary>
    public CompositionRunReport Report { get; }

    /// <summary>Adapter-owned destination id when build committed output.</summary>
    public string? CommittedOutputId { get; }

    /// <summary>Deterministic token returned by preview and required before build commit.</summary>
    public string? PreviewToken { get; }
}
