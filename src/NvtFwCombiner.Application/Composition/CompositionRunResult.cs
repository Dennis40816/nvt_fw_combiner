using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application-level preview or build result with report summary.</summary>
public sealed class CompositionRunResult
{
    internal CompositionRunResult(
        CompositionExecutionStatus status,
        ReadOnlyMemory<byte> immutableOutputBytes,
        CompositionRunReport report,
        string? committedOutputId,
        string? previewToken,
        string? inspectionOutputSpaceId,
        string? inspectionReferenceSpaceId,
        byte[]? inspectionReferenceBytes,
        ReadOnlyMemory<byte>? inspectionOutputBytes,
        string? outcomeStatus = null,
        bool suppressOutputInExternalReport = false,
        bool hasRunReport = true)
    {
        ArgumentNullException.ThrowIfNull(report);
        bool hasInspection = inspectionReferenceBytes is not null;
        if ((inspectionOutputSpaceId is not null) != hasInspection ||
            (inspectionReferenceSpaceId is not null) != hasInspection ||
            inspectionOutputBytes.HasValue != hasInspection)
        {
            throw new ArgumentException(
                "Replace inspection requires output/reference space ids and before/after bytes together.");
        }

        Status = status;
        OutputBytes = immutableOutputBytes;
        Report = report;
        CommittedOutputId = string.IsNullOrWhiteSpace(committedOutputId) ? null : committedOutputId;
        PreviewToken = string.IsNullOrWhiteSpace(previewToken) ? null : previewToken;
        OutcomeStatus = string.IsNullOrWhiteSpace(outcomeStatus)
            ? status.ToString()
            : outcomeStatus;
        SuppressOutputInExternalReport = suppressOutputInExternalReport;
        HasRunReport = hasRunReport;
        InspectionSnapshot = inspectionReferenceBytes is { } referenceBytes
            ? new CompositionRunInspectionSnapshot(
                report.RunId,
                inspectionOutputSpaceId!,
                inspectionReferenceSpaceId!,
                report.Output.Sha256,
                referenceBytes,
                inspectionOutputBytes!.Value)
            : null;
    }

    /// <summary>Execution status returned by the shared domain engine.</summary>
    public CompositionExecutionStatus Status { get; }

    /// <summary>True only when the shared composition engine completed successfully.</summary>
    public bool Succeeded => Status == CompositionExecutionStatus.Succeeded;

    /// <summary>Stable user-facing outcome category, including non-executed blocked/diagnostic outcomes.</summary>
    public string OutcomeStatus { get; }

    /// <summary>Output bytes for preview/build when execution succeeded.</summary>
    public ReadOnlyMemory<byte> OutputBytes { get; }

    /// <summary>
    /// In-memory reference/output bytes when Replace execution produced a complete image, including
    /// an image later rejected by publication gates, or <see langword="null"/> when unavailable.
    /// </summary>
    public CompositionRunInspectionSnapshot? InspectionSnapshot { get; }

    /// <summary>Application summary for UI, CLI, and regression tests; not the canonical v1 report artifact.</summary>
    public CompositionRunReport Report { get; internal set; }

    /// <summary>Adapter-owned destination id when build committed output.</summary>
    public string? CommittedOutputId { get; }

    /// <summary>Deterministic token returned by preview and required before build commit.</summary>
    public string? PreviewToken { get; }

    /// <summary>Profile that produced, or was selected for, this typed result.</summary>
    public string ProfileId => Report.ProfileId;

    /// <summary>Output size declared by the typed report.</summary>
    public long OutputSize => Report.Output.Size;

    /// <summary>Output SHA-256 declared by the typed report.</summary>
    public string OutputSha256 => Report.Output.Sha256;

    /// <summary>Output file name declared by the typed report.</summary>
    public string OutputFileName => Report.Output.FileName;

    /// <summary>Exact output-naming provenance accepted by the Application run.</summary>
    internal OutputNamingSummary? OutputNaming => Report.OutputNaming;

    /// <summary>Exact accepted General draft retained for Preview-to-Build reuse.</summary>
    public GeneralMappingDraftState? AcceptedGeneralMappingDraft { get; internal set; }

    /// <summary>Exact immutable capability consumed by this run.</summary>
    public ResolvedCapability? ResolvedCapability { get; internal set; }

    /// <summary>True when this outcome owns a typed run report rather than readiness only.</summary>
    public bool HasRunReport { get; }

    /// <summary>Shared action readiness retained for a non-executed typed result.</summary>
    public CapabilityActionReadinessSnapshot? ActionReadiness { get; internal set; }

    /// <summary>Additional artifacts committed from the completed primary output.</summary>
    public IReadOnlyList<CompositionDeliveryArtifact> DeliveryArtifacts { get; internal set; } = [];

    /// <summary>True when every selected additional delivery completed.</summary>
    public bool IsDeliveryComplete { get; internal set; } = true;

    /// <summary>Operator-safe detail when the primary output committed but an additional delivery failed.</summary>
    public string? DeliveryFailureMessage { get; internal set; }

    internal bool SuppressOutputInExternalReport { get; }

}
