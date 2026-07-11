using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const int MaxReportHistoryEntries = 12;
    private const int ReportHistoryStorageWarningBytes = 1024 * 1024;
    private int _reportHistorySequence;

    /// <summary>Gets session-local reports that can be reopened without re-running firmware workflows.</summary>
    public ObservableCollection<ReportHistoryEntryViewModel> ReportHistoryEntries { get; } = [];

    /// <summary>True when the session has at least one report.</summary>
    public bool HasReportHistory => ReportHistoryEntries.Count > 0;

    /// <summary>True when the report history list is empty.</summary>
    public bool IsReportHistoryEmpty => !HasReportHistory;

    /// <summary>Number of reports captured in this UI session.</summary>
    public int ReportHistoryCount => ReportHistoryEntries.Count;

    /// <summary>Compact report history summary.</summary>
    public string ReportHistorySummary => Text.GetReportHistorySummary(ReportHistoryCount);

    /// <summary>Total in-memory persisted history payload size.</summary>
    public long ReportHistoryTotalBytes => ReportHistoryEntries.Sum(entry =>
        Encoding.UTF8.GetByteCount(entry.ReportJson) +
        Encoding.UTF8.GetByteCount(entry.ArtifactPath));

    /// <summary>Human-readable local report history storage summary.</summary>
    public string ReportHistoryStorageSummary => Text.GetReportHistoryStorageSummary(FormatByteCount(ReportHistoryTotalBytes));

    /// <summary>True when local report history has crossed the cleanup warning size.</summary>
    public bool HasReportHistoryStorageWarning => ReportHistoryTotalBytes >= ReportHistoryStorageWarningBytes;

    /// <summary>Cleanup warning for oversized local report history.</summary>
    public string ReportHistoryStorageWarning => Text.GetReportHistoryStorageWarning(
        FormatByteCount(ReportHistoryTotalBytes),
        FormatByteCount(ReportHistoryStorageWarningBytes));

    /// <summary>True when the report modal shows the dedicated history view.</summary>
    public bool IsReportHistoryViewOpen { get; private set; }

    /// <summary>True when the report modal shows the current report review.</summary>
    public bool IsReportReviewViewOpen => !IsReportHistoryViewOpen;

    /// <summary>True when a report history view can be opened.</summary>
    public bool CanOpenReportHistory => HasReportHistory;

    /// <summary>True when local report history can be cleared.</summary>
    public bool CanClearReportHistory => HasReportHistory;

    /// <summary>Command that opens the dedicated report history view.</summary>
    public IRelayCommand ShowReportHistoryCommand { get; }

    /// <summary>Command that returns from report history to the current report review.</summary>
    public IRelayCommand CloseReportHistoryCommand { get; }

    /// <summary>Command that clears local report history entries.</summary>
    public IRelayCommand ClearReportHistoryCommand { get; }

    /// <summary>Command that reopens a report history entry.</summary>
    public IRelayCommand<ReportHistoryEntryViewModel> OpenReportHistoryEntryCommand { get; }

    /// <summary>Command that removes one local report history entry.</summary>
    public IRelayCommand<ReportHistoryEntryViewModel> RemoveReportHistoryEntryCommand { get; }

    /// <summary>Exports persistable report history snapshots, newest first.</summary>
    public IReadOnlyList<ReportHistorySnapshot> ExportReportHistory()
    {
        return
        [
            .. ReportHistoryEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.ReportJson))
                .Select(entry => entry.ToSnapshot()),
        ];
    }

    /// <summary>Restores report history snapshots without showing a toast or re-running firmware workflows.</summary>
    public void LoadReportHistory(IEnumerable<ReportHistorySnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        ReportHistoryEntries.Clear();
        _reportHistorySequence = 0;
        foreach (ReportHistorySnapshot snapshot in snapshots.Take(MaxReportHistoryEntries))
        {
            if (TryCreateReportHistoryEntry(snapshot, out ReportHistoryEntryViewModel? entry) &&
                entry is not null)
            {
                ReportHistoryEntries.Add(entry);
            }
        }

        if (ReportHistoryEntries.Count == 0)
        {
            LoadedReport = ReportReviewViewModel.Empty;
            LoadedReportJson = string.Empty;
        }
        else
        {
            ReportHistoryEntryViewModel latest = ReportHistoryEntries[0];
            LoadedReport = latest.Report;
            LoadedReportJson = latest.ReportJson;
        }

        NotifyReportHistoryChanged();
        NotifyReportChanged();
        RefreshSettingsState();
    }

    private void ShowReportHistory()
    {
        if (!CanOpenReportHistory)
        {
            return;
        }

        CloseReplaceSelectionForRun();
        IsReportModalOpen = true;
        IsReportHistoryViewOpen = true;
        HasReportToast = false;
        ReportToastOpacity = 0;
        OnPropertyChanged(nameof(IsReportModalOpen));
        NotifyReportViewModeChanged();
        OnPropertyChanged(nameof(HasReportToast));
        OnPropertyChanged(nameof(ReportToastOpacity));
    }

    private void CloseReportHistory()
    {
        if (!IsReportHistoryViewOpen)
        {
            return;
        }

        IsReportHistoryViewOpen = false;
        NotifyReportViewModeChanged();
    }

    private void OpenReportHistoryEntry(ReportHistoryEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        LoadedReport = entry.Report;
        LoadedReportJson = entry.ReportJson;
        CloseReplaceSelectionForRun();
        IsReportModalOpen = true;
        IsReportHistoryViewOpen = false;
        HasReportToast = false;
        ReportToastOpacity = 0;
        NotifyReportChanged();
        OnPropertyChanged(nameof(IsReportModalOpen));
        NotifyReportViewModeChanged();
        OnPropertyChanged(nameof(HasReportToast));
        OnPropertyChanged(nameof(ReportToastOpacity));
        RefreshSettingsState();
    }

    private void ClearReportHistory()
    {
        if (!HasReportHistory)
        {
            return;
        }

        ReportHistoryEntries.Clear();
        NotifyReportHistoryChanged();
        RefreshSettingsState();
    }

    private void RemoveReportHistoryEntry(ReportHistoryEntryViewModel? entry)
    {
        if (entry is null || !ReportHistoryEntries.Remove(entry))
        {
            return;
        }

        NotifyReportHistoryChanged();
        if (!HasReportHistory)
        {
            IsReportHistoryViewOpen = false;
            NotifyReportViewModeChanged();
        }

        RefreshSettingsState();
    }

    private void CaptureLoadedReportInHistory()
    {
        if (!HasLoadedReport)
        {
            return;
        }

        ReportHistoryEntries.Insert(
            0,
            new ReportHistoryEntryViewModel(++_reportHistorySequence, LoadedReport, LoadedReportJson));
        while (ReportHistoryEntries.Count > MaxReportHistoryEntries)
        {
            ReportHistoryEntries.RemoveAt(ReportHistoryEntries.Count - 1);
        }

        NotifyReportHistoryChanged();
    }

    private bool TryCreateReportHistoryEntry(
        ReportHistorySnapshot snapshot,
        out ReportHistoryEntryViewModel? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(snapshot.ReportJson))
        {
            return false;
        }

        try
        {
            var report = ReportReviewViewModel.FromJson(
                snapshot.ReportJson,
                string.IsNullOrWhiteSpace(snapshot.SourceName) ? "persisted report" : snapshot.SourceName,
                snapshot.OutputArtifactPath,
                Text.Language);
            entry = new ReportHistoryEntryViewModel(++_reportHistorySequence, report, snapshot.ReportJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void NotifyReportHistoryChanged()
    {
        OnPropertyChanged(nameof(ReportHistoryEntries));
        OnPropertyChanged(nameof(HasReportHistory));
        OnPropertyChanged(nameof(IsReportHistoryEmpty));
        OnPropertyChanged(nameof(ReportHistoryCount));
        OnPropertyChanged(nameof(ReportHistorySummary));
        OnPropertyChanged(nameof(ReportHistoryTotalBytes));
        OnPropertyChanged(nameof(ReportHistoryStorageSummary));
        OnPropertyChanged(nameof(HasReportHistoryStorageWarning));
        OnPropertyChanged(nameof(ReportHistoryStorageWarning));
        OnPropertyChanged(nameof(CanOpenReportHistory));
        OnPropertyChanged(nameof(CanClearReportHistory));
        ShowReportHistoryCommand.NotifyCanExecuteChanged();
        ClearReportHistoryCommand.NotifyCanExecuteChanged();
        OpenReportHistoryEntryCommand.NotifyCanExecuteChanged();
        RemoveReportHistoryEntryCommand.NotifyCanExecuteChanged();
    }

    private void RelocalizeLoadedReport()
    {
        if (string.IsNullOrWhiteSpace(LoadedReportJson))
        {
            return;
        }

        try
        {
            LoadedReport = ReportReviewViewModel.FromJson(
                LoadedReportJson,
                LoadedReport.SourceName,
                LoadedReport.OutputArtifactPath,
                Text.Language);
            for (int index = 0; index < ReportHistoryEntries.Count; index++)
            {
                ReportHistoryEntryViewModel entry = ReportHistoryEntries[index];
                if (!string.Equals(entry.ReportJson, LoadedReportJson, StringComparison.Ordinal))
                {
                    continue;
                }

                ReportHistoryEntries[index] = new ReportHistoryEntryViewModel(
                    entry.Sequence,
                    LoadedReport,
                    entry.ReportJson);
                break;
            }

            NotifyReportChanged();
            NotifyReportHistoryChanged();
        }
        catch (JsonException)
        {
        }
    }

    private static string FormatByteCount(long byteCount)
    {
        const double kib = 1024;
        const double mib = 1024 * kib;
        return byteCount >= mib
            ? $"{(byteCount / mib).ToString("0.0", CultureInfo.InvariantCulture)} MB"
            : byteCount >= kib
                ? $"{(byteCount / kib).ToString("0.0", CultureInfo.InvariantCulture)} KB"
                : $"{byteCount.ToString(CultureInfo.InvariantCulture)} B";
    }
}
