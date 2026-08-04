using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Display-only localization of one canonical Support Matrix row.</summary>
public sealed class SupportMatrixRowViewModel
{
    private SupportMatrixRowViewModel(
        CanonicalSupportMatrixRow row,
        ShellTextResources text)
    {
        IcId = row.Identity.IcId;
        WorkflowId = row.Identity.WorkflowId;
        WorkflowLabel = ShellTextResources.SupportMatrixWorkflowValue(
            row.Identity.WorkflowId);
        IcCountLabel = ShellTextResources.SupportMatrixIcCountValue(
            row.Identity.IcCountVariant);
        MapVariant = row.Identity.MapVariant;
        AuthoringLabel = text.SupportMatrixAuthoringValue(row.Authoring.Value);
        ExecutionLabel = text.SupportMatrixExecutionValue(row.ExecutionState);
        PublicationLabel = text.SupportMatrixPublicationValue(row.Publication.Value);
        EvidenceLabel = text.SupportMatrixEvidenceValue(row.Evidence.Value);
        EvidenceStatus = row.Evidence.Value;
        DecisionSummary =
            $"{AuthoringLabel} · {ExecutionLabel} · {PublicationLabel} · " +
            $"{EvidenceLabel} · {BlockerSummary(row.Blockers, text)}";
        IsAuthoringAvailable =
            row.Authoring.Value == CapabilityAuthoringAvailability.Available;
        Fingerprint = row.CapabilityFingerprint;
        FingerprintDisplay = $"{row.CapabilityFingerprint[..12]}…";
        HasBlocker = row.Blockers.Count != 0;
        BlockerLabel = BlockerSummary(row.Blockers, text);
        string blockerEvidence = row.Blockers.Count == 0
            ? text.SupportMatrixNoBlockerLabel
            : string.Join(
                "; ",
                row.Blockers.Select(static blocker =>
                    $"{blocker.Code} ({blocker.SourceReference})"));
        ProvenanceDetail =
            $"{text.SupportMatrixFingerprintLabel}: {row.CapabilityFingerprint} · " +
            $"{row.Authoring.DecisionId} ({row.Authoring.SourceReference}) · " +
            $"{row.Publication.DecisionId} ({row.Publication.SourceReference}) · " +
            $"{row.Evidence.DecisionId} ({row.Evidence.SourceReference})" +
            (row.Blockers.Count == 0
                ? string.Empty
                : " · " + string.Join(
                    " · ",
                    row.Blockers.Select(static blocker =>
                        $"{blocker.Kind}: {blocker.Code} ({blocker.SourceReference})")));
        AccessibleLabel =
            $"{IcId}, {WorkflowLabel}, {text.SupportMatrixIcCountLabel} {IcCountLabel}, " +
            $"{text.SupportMatrixMapVariantLabel} {MapVariant}, " +
            $"{text.SupportMatrixAuthoringLabel} {AuthoringLabel}, " +
            $"{text.SupportMatrixExecutionLabel} {ExecutionLabel}, " +
            $"{text.SupportMatrixPublicationLabel} {PublicationLabel}, " +
            $"{text.SupportMatrixEvidenceLabel} {EvidenceLabel}, " +
            $"{text.SupportMatrixBlockerLabel} {blockerEvidence}.";
    }

    /// <summary>Canonical IC id.</summary>
    public string IcId { get; }

    /// <summary>Stable workflow token used only for display grouping.</summary>
    public string WorkflowId { get; }

    /// <summary>Localized workflow label.</summary>
    public string WorkflowLabel { get; }

    /// <summary>Localized IC Count value.</summary>
    public string IcCountLabel { get; }

    /// <summary>Canonical map-variant token.</summary>
    public string MapVariant { get; }

    /// <summary>Localized authoring availability.</summary>
    public string AuthoringLabel { get; }

    /// <summary>Localized compiler-admission state.</summary>
    public string ExecutionLabel { get; }

    /// <summary>Localized publication classification.</summary>
    public string PublicationLabel { get; }

    /// <summary>Localized evidence classification.</summary>
    public string EvidenceLabel { get; }

    /// <summary>Typed evidence retained for lossless matrix-cell aggregation.</summary>
    public CapabilityEvidenceStatus EvidenceStatus { get; }

    /// <summary>Compact localized authoring, publication, evidence, and blocker values.</summary>
    public string DecisionSummary { get; }

    /// <summary>Full reviewed capability fingerprint.</summary>
    public string Fingerprint { get; }

    /// <summary>Compact fingerprint prefix.</summary>
    public string FingerprintDisplay { get; }

    /// <summary>Compact localized typed blocker summary.</summary>
    public string BlockerLabel { get; }

    /// <summary>Full decision and fingerprint provenance.</summary>
    public string ProvenanceDetail { get; }

    /// <summary>Full row description for assistive technology.</summary>
    public string AccessibleLabel { get; }

    /// <summary>Whether the exact route may appear in ordinary authoring selectors.</summary>
    public bool IsAuthoringAvailable { get; }

    /// <summary>Whether the row carries any typed blocker.</summary>
    public bool HasBlocker { get; }

    internal static SupportMatrixRowViewModel Create(
        CanonicalSupportMatrixRow row,
        ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(text);
        return new SupportMatrixRowViewModel(row, text);
    }

    private static string BlockerSummary(
        IReadOnlyList<CanonicalSupportMatrixBlocker> blockers,
        ShellTextResources text)
    {
        return blockers.Count == 0
            ? text.SupportMatrixNoBlockerLabel
            : string.Join(
                " + ",
                blockers.Select(blocker => text.SupportMatrixBlockerValue(blocker.Kind)));
    }
}
