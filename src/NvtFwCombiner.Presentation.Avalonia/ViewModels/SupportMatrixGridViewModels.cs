using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;

// Bindable matrix projection members are intentionally concise; per-member XML adds noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Conservative visual summary of all exact routes in one IC/workflow cell.</summary>
internal enum SupportMatrixCellStatus
{
    ReviewedEvidence,
    ContractOnly,
    ReviewRequired,
    Blocked,
    NotDeclared,
}

internal sealed record SupportMatrixWorkflowColumnViewModel(
    string WorkflowId,
    string Label);

internal sealed class SupportMatrixIcRowViewModel(
    string icId,
    IEnumerable<SupportMatrixCellViewModel> cells)
{
    public string IcId { get; } = icId;

    public IReadOnlyList<SupportMatrixCellViewModel> Cells { get; } =
        new ReadOnlyCollection<SupportMatrixCellViewModel>([.. cells]);
}

/// <summary>Hover/focus disclosure for all exact routes at one matrix intersection.</summary>
internal sealed class SupportMatrixCellViewModel
{
    internal SupportMatrixCellViewModel(
        string icId,
        SupportMatrixWorkflowColumnViewModel workflow,
        IEnumerable<SupportMatrixRowViewModel> routes,
        ShellTextResources text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(text);

        IcId = icId;
        WorkflowLabel = workflow.Label;
        Routes = new ReadOnlyCollection<SupportMatrixRowViewModel>([.. routes]);
        Status = Classify(Routes);
        StatusLabel = text.SupportMatrixCellStatusValue(Status);
        VariantCountDisplay =
            $"×{Routes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        RouteCountLabel = text.FormatSupportMatrixRouteCount(Routes.Count);
        DecisionHeading =
            $"{text.SupportMatrixAuthoringLabel} · {text.SupportMatrixExecutionLabel} · " +
            $"{text.SupportMatrixPublicationLabel} · " +
            $"{text.SupportMatrixEvidenceLabel} · {text.SupportMatrixBlockerLabel}";
        AccessibleLabel = Routes.Count == 0
            ? $"{IcId}, {WorkflowLabel}: {StatusLabel}."
            : $"{IcId}, {WorkflowLabel}: {StatusLabel}; " +
              RouteCountLabel + ".";
        AccessibleDetail = Routes.Count == 0
            ? AccessibleLabel
            : AccessibleLabel + " " + string.Join(
                " ",
                Routes.Select(static route =>
                    route.AccessibleLabel + " " + route.ProvenanceDetail));
    }

    public string IcId { get; }

    public string WorkflowLabel { get; }

    public IReadOnlyList<SupportMatrixRowViewModel> Routes { get; }

    public SupportMatrixCellStatus Status { get; }

    public string StatusLabel { get; }

    public string VariantCountDisplay { get; }

    public string RouteCountLabel { get; }

    public string DecisionHeading { get; }

    public string AccessibleLabel { get; }

    public string AccessibleDetail { get; }

    public bool HasMultipleRoutes => Routes.Count > 1;

    public bool IsReviewedEvidence => Status == SupportMatrixCellStatus.ReviewedEvidence;

    public bool IsContractOnly => Status == SupportMatrixCellStatus.ContractOnly;

    public bool IsReviewRequired => Status == SupportMatrixCellStatus.ReviewRequired;

    public bool IsBlocked => Status == SupportMatrixCellStatus.Blocked;

    public bool IsNotDeclared => Status == SupportMatrixCellStatus.NotDeclared;

    private static SupportMatrixCellStatus Classify(
        IReadOnlyList<SupportMatrixRowViewModel> routes)
    {
        return routes.Count == 0
            ? SupportMatrixCellStatus.NotDeclared
            : routes.Any(static route => route.HasBlocker)
            ? SupportMatrixCellStatus.Blocked
            : routes.All(static route => route.EvidenceStatus is
                CapabilityEvidenceStatus.DirectGolden or
                CapabilityEvidenceStatus.ApprovedAlias or
                CapabilityEvidenceStatus.SyntheticOracle)
            ? SupportMatrixCellStatus.ReviewedEvidence
            : routes.All(static route =>
                route.EvidenceStatus == CapabilityEvidenceStatus.ContractOnly)
            ? SupportMatrixCellStatus.ContractOnly
            : SupportMatrixCellStatus.ReviewRequired;
    }
}

#pragma warning restore CS1591
