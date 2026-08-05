using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Focused read-only Settings child for canonical capability disclosure.</summary>
public sealed class SupportMatrixPresentationViewModel(
    ICanonicalSupportMatrixQuery query) : ObservableObject
{
    private static readonly string[] s_preferredWorkflowOrder =
    [
        WorkbenchWorkflowIds.StandardMerge,
        WorkbenchWorkflowIds.AbMerge,
        WorkbenchWorkflowIds.DpReplace,
        WorkbenchWorkflowIds.CtrlRamReplace,
        WorkbenchWorkflowIds.GeneralMerge,
        WorkbenchWorkflowIds.GeneralReplace,
    ];

    private readonly ICanonicalSupportMatrixQuery _query =
        query ?? throw new ArgumentNullException(nameof(query));

    /// <summary>Localized exact-route rows in canonical stable order.</summary>
    public ObservableCollection<SupportMatrixRowViewModel> Rows { get; } = [];

    /// <summary>Stable workflow headers for the pivoted matrix.</summary>
    public ObservableCollection<SupportMatrixWorkflowColumnViewModel> WorkflowColumns { get; } = [];

    /// <summary>IC rows aligned to <see cref="WorkflowColumns"/>.</summary>
    public ObservableCollection<SupportMatrixIcRowViewModel> IcRows { get; } = [];

    /// <summary>Current loading, empty, retained, or blocking disclosure.</summary>
    public DisclosureStatusViewModel? StatusNotice { get; private set; }

    /// <summary>Localized route count.</summary>
    public string RouteCountLabel { get; private set; } = string.Empty;

    /// <summary>Localized catalog lifecycle.</summary>
    public string CatalogStateLabel { get; private set; } = string.Empty;

    /// <summary>Typed lifecycle of the publication represented by the current rows.</summary>
    public CanonicalSupportMatrixCatalogState CatalogState { get; private set; } =
        CanonicalSupportMatrixCatalogState.Loading;

    /// <summary>Published catalog version.</summary>
    public string CatalogVersion { get; private set; } = string.Empty;

    /// <summary>Compact exact-source digest.</summary>
    public string SourceHash { get; private set; } = string.Empty;

    /// <summary>Opaque current publication token.</summary>
    public string ResolutionToken { get; private set; } = string.Empty;

    /// <summary>True when exact routes are available for disclosure.</summary>
    public bool HasRows => Rows.Count != 0;

    /// <summary>True when an accessible state notice is visible.</summary>
    public bool HasStatusNotice => StatusNotice is not null;

    /// <summary>True when a failed reload retained an older coherent publication.</summary>
    public bool IsStale { get; private set; }

    internal void Refresh(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);
        CanonicalSupportMatrixQueryResult result = _query.Query();
        ReplaceItems(
            Rows,
            result.Matrix?.Rows.Select(row =>
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
