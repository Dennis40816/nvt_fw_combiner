using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Focused read-only Settings child for canonical capability disclosure.</summary>
internal sealed class SupportMatrixPresentationViewModel(
    ICanonicalSupportMatrixQuery query) : ObservableObject
{
    private static readonly string[] s_preferredWorkflowOrder =
    [
        ExperienceIds.StandardMerge,
        ExperienceIds.AbMerge,
        ExperienceIds.CtrlRamReplace,
        ExperienceIds.GeneralMerge,
        ExperienceIds.GeneralReplace,
    ];

    private readonly ICanonicalSupportMatrixQuery _query =
        query ?? throw new ArgumentNullException(nameof(query));

    public ObservableCollection<SupportMatrixRowViewModel> Rows { get; } = [];

    /// <summary>Stable workflow headers for the pivoted matrix.</summary>
    public ObservableCollection<SupportMatrixWorkflowColumnViewModel> WorkflowColumns { get; } = [];

    public ObservableCollection<SupportMatrixIcRowViewModel> IcRows { get; } = [];

    public DisclosureStatusViewModel? StatusNotice { get; private set; }

    public string RouteCountLabel { get; private set; } = string.Empty;

    public string CatalogStateLabel { get; private set; } = string.Empty;

    public CanonicalSupportMatrixCatalogState CatalogState { get; private set; } =
        CanonicalSupportMatrixCatalogState.Loading;

    public string CatalogVersion { get; private set; } = string.Empty;

    public string SourceHash { get; private set; } = string.Empty;

    /// <summary>Opaque current publication token.</summary>
    public string ResolutionToken { get; private set; } = string.Empty;

    public bool HasRows => Rows.Count != 0;

    public bool HasStatusNotice => StatusNotice is not null;

    /// <summary>True when a failed reload retained an older coherent publication.</summary>
    public bool IsStale { get; private set; }

    internal void Refresh(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);
        CanonicalSupportMatrixQueryResult result = _query.Query();
        ReplaceItems(
            Rows,
            result.Matrix?.Rows.Where(static row => row.Identity.WorkflowId != ExperienceIds.DpReplace).Select(row =>
                SupportMatrixRowViewModel.Create(row, text)) ?? []);
        RebuildGrid(text);
        IsStale = result.IsStale;
        CatalogState = result.State;
        RouteCountLabel = text.FormatSupportMatrixRouteCount(Rows.Count);
        CatalogStateLabel = text.SupportMatrixCatalogStateValue(result.State);
        CatalogVersion = result.Matrix?.CatalogVersion ?? text.NotAvailableLabel;
        SourceHash = result.Matrix is null
            ? text.NotAvailableLabel
            : $"{result.Matrix.SourceSha256[..12]}…";
        ResolutionToken = result.Matrix?.ResolutionToken.ToString() ??
            text.NotAvailableLabel;
        StatusNotice = CreateStatusNotice(result, text);
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasStatusNotice));
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(CatalogState));
        OnPropertyChanged(nameof(RouteCountLabel));
        OnPropertyChanged(nameof(CatalogStateLabel));
        OnPropertyChanged(nameof(CatalogVersion));
        OnPropertyChanged(nameof(SourceHash));
        OnPropertyChanged(nameof(ResolutionToken));
        OnPropertyChanged(nameof(StatusNotice));
    }

    private static DisclosureStatusViewModel? CreateStatusNotice(
        CanonicalSupportMatrixQueryResult result,
        ShellTextResources text)
    {
        string technicalDetail = string.Join(
            "; ",
            result.ReloadIssues.Select(static issue => issue.Code));
        return result.State switch
        {
            CanonicalSupportMatrixCatalogState.Loading => new DisclosureStatusViewModel(
                DisclosureStatusTone.Info,
                text.SupportMatrixLoadingTitle,
                text.SupportMatrixLoadingDetail),
            CanonicalSupportMatrixCatalogState.Current when result.IsEmpty =>
                new DisclosureStatusViewModel(
                    DisclosureStatusTone.Neutral,
                    text.SupportMatrixEmptyTitle,
                    text.SupportMatrixEmptyDetail),
            CanonicalSupportMatrixCatalogState.ColdStartBlocked =>
                new DisclosureStatusViewModel(
                    DisclosureStatusTone.Error,
                    text.SupportMatrixColdStartTitle,
                    text.SupportMatrixColdStartDetail,
                    technicalDetail),
            CanonicalSupportMatrixCatalogState.LastKnownGood =>
                new DisclosureStatusViewModel(
                    DisclosureStatusTone.Warning,
                    text.SupportMatrixLastKnownGoodTitle,
                    text.SupportMatrixLastKnownGoodDetail,
                    technicalDetail),
            _ => null,
        };
    }

    private void RebuildGrid(ShellTextResources text)
    {
        string[] workflowIds =
        [
            .. Rows.Select(static row => row.WorkflowId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(WorkflowOrder)
                .ThenBy(static workflowId => workflowId, StringComparer.Ordinal),
        ];
        SupportMatrixWorkflowColumnViewModel[] workflows =
        [
            .. workflowIds.Select(static workflowId =>
                new SupportMatrixWorkflowColumnViewModel(
                    workflowId,
                    ShellTextResources.SupportMatrixWorkflowValue(workflowId))),
        ];
        ReplaceItems(WorkflowColumns, workflows);

        SupportMatrixIcRowViewModel[] icRows =
        [
            .. Rows.Select(static row => row.IcId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static icId => icId, StringComparer.Ordinal)
                .Select(icId => new SupportMatrixIcRowViewModel(
                    icId,
                    workflows.Select(workflow => new SupportMatrixCellViewModel(
                        icId,
                        workflow,
                        Rows.Where(row =>
                            StringComparer.Ordinal.Equals(row.IcId, icId) &&
                            StringComparer.Ordinal.Equals(
                                row.WorkflowId,
                                workflow.WorkflowId)),
                        text)))),
        ];
        ReplaceItems(IcRows, icRows);
    }

    private static int WorkflowOrder(string workflowId)
    {
        int index = Array.IndexOf(s_preferredWorkflowOrder, workflowId);
        return index < 0 ? int.MaxValue : index;
    }

    private static void ReplaceItems<T>(
        ObservableCollection<T> target,
        IEnumerable<T> items)
    {
        target.Clear();
        foreach (T item in items)
        {
            target.Add(item);
        }
    }
}
