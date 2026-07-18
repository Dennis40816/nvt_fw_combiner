using System.Collections.ObjectModel;
using System.Globalization;
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
    public long ReportHistoryTotalBytes => ReportHistoryEntries.Sum(static entry => entry.StoredByteCount);

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

    /// <summary>Cancellable command used by the UI to reopen a report without blocking the dispatcher.</summary>
    public IAsyncRelayCommand<ReportHistoryEntryViewModel> OpenReportHistoryEntryAsyncCommand { get; }

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
        _ = BeginReportProjection();

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
            LoadReportHistoryEntry(latest);
        }

        NotifyReportHistoryChanged();
        NotifyReportChanged();
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

        CancelReportHistoryReopen();
        IsReportHistoryViewOpen = false;
        NotifyReportViewModeChanged();
    }

    private async Task OpenReportHistoryEntryAsync(
        ReportHistoryEntryViewModel? entry,
        CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return;
        }

        long generation = BeginReportProjection(preserveHistoryReopen: true);
        string sourceName = string.IsNullOrWhiteSpace(entry.SourceName)
            ? "persisted report"
            : entry.SourceName;
        ReportReviewViewModel report;
        try
        {
            report = await ProjectReportAsync(
                entry.ReportJson,
                sourceName,
                entry.ArtifactPath,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!IsCurrentReportProjection(generation))
        {
            return;
        }

        LoadedReport = report;
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
    }

    private void ClearReportHistory()
    {
        if (!HasReportHistory)
        {
            return;
        }

        CancelReportHistoryReopen();
        ReportHistoryEntries.Clear();
        NotifyReportHistoryChanged();
    }

    private void RemoveReportHistoryEntry(ReportHistoryEntryViewModel? entry)
    {
        if (entry is null || !ReportHistoryEntries.Remove(entry))
        {
            return;
        }

        CancelReportHistoryReopen();
        NotifyReportHistoryChanged();
        if (!HasReportHistory)
        {
            IsReportHistoryViewOpen = false;
            NotifyReportViewModeChanged();
        }

    }

    private void CaptureLoadedReportInHistory()
    {
        if (!HasLoadedReport)
        {
            return;
        }

        ReportHistoryEntries.Insert(
            0,
            new ReportHistoryEntryViewModel(
                ++_reportHistorySequence,
                CreateReportHistorySnapshot(LoadedReport, LoadedReportJson)));
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

        ReportHistorySnapshot normalizedSnapshot = snapshot;
        try
        {
            if (snapshot.Metadata == ReportHistoryMetadataSnapshot.Empty)
            {
                var report = ReportReviewViewModel.FromJson(
                    snapshot.ReportJson,
                    string.IsNullOrWhiteSpace(snapshot.SourceName) ? "persisted report" : snapshot.SourceName,
                    snapshot.OutputArtifactPath,
                    Text.Language);
                normalizedSnapshot = CreateReportHistorySnapshot(report, snapshot.ReportJson);
            }
            else
            {
                using JsonDocument _ = JsonDocument.Parse(snapshot.ReportJson);
            }
        }
        catch (Exception exception) when (IsReportMaterializationException(exception))
        {
            return false;
        }

        entry = new ReportHistoryEntryViewModel(++_reportHistorySequence, normalizedSnapshot);
        return true;
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
        OpenReportHistoryEntryAsyncCommand.NotifyCanExecuteChanged();
        RemoveReportHistoryEntryCommand.NotifyCanExecuteChanged();
    }

    private void OpenReportHistoryEntryAsyncCommand_CanExecuteChanged(object? sender, EventArgs e)
    {
        OpenReportHistoryEntryCommand.NotifyCanExecuteChanged();
    }

    private void CancelReportHistoryReopen()
    {
        if (OpenReportHistoryEntryAsyncCommand.IsRunning)
        {
            _ = BeginReportProjection();
        }
    }

    private void RelocalizeLoadedReport()
    {
        if (string.IsNullOrWhiteSpace(LoadedReportJson))
        {
            return;
        }

        ReportReviewViewModel localizedReport;
        try
        {
            localizedReport = ReportReviewViewModel.FromJson(
                LoadedReportJson,
                LoadedReport.SourceName,
                LoadedReport.OutputArtifactPath,
                Text.Language);
        }
        catch (Exception exception) when (IsReportMaterializationException(exception))
        {
            return;
        }

        LoadedReport = localizedReport;
        for (int index = 0; index < ReportHistoryEntries.Count; index++)
        {
            ReportHistoryEntryViewModel entry = ReportHistoryEntries[index];
            if (!string.Equals(entry.ReportJson, LoadedReportJson, StringComparison.Ordinal))
            {
                continue;
            }

            ReportHistoryEntries[index] = new ReportHistoryEntryViewModel(
                entry.Sequence,
                CreateReportHistorySnapshot(LoadedReport, entry.ReportJson));
            break;
        }

        NotifyReportChanged();
        NotifyReportHistoryChanged();
    }

    private void LoadReportHistoryEntry(ReportHistoryEntryViewModel entry)
    {
        _ = BeginReportProjection();
        try
        {
            LoadedReport = ReportReviewViewModel.FromJson(
                entry.ReportJson,
                string.IsNullOrWhiteSpace(entry.SourceName) ? "persisted report" : entry.SourceName,
                entry.ArtifactPath,
                Text.Language);
        }
        catch (Exception exception) when (IsReportMaterializationException(exception))
        {
            LoadedReport = ReportReviewViewModel.Error(entry.SourceName, exception.Message, language: Text.Language);
        }

        LoadedReportJson = entry.ReportJson;
    }

    private static ReportHistorySnapshot CreateReportHistorySnapshot(
        ReportReviewViewModel report,
        string reportJson)
    {
        return new ReportHistorySnapshot(
            report.SourceName,
            reportJson,
            report.OutputArtifactPath,
            new ReportHistoryMetadataSnapshot(
                report.Title,
                report.Status,
                CreateReportHistoryContext(report),
                string.IsNullOrWhiteSpace(report.OutputFileName)
                    ? "No output"
                    : $"{report.OutputFileName} / {report.OutputSize} bytes",
                report.OutputHashLabel,
                report.HasPostbuildInvocations
                    ? FormatReportHistoryCount(report.PostbuildInvocationCount, "command")
                    : "No external command",
                report.HasPrimaryIssue
                    ? FormatReportHistoryCount(report.BlockingIssueCount, "issue")
                    : report.HasWarnings
                        ? FormatReportHistoryCount(report.WarningCount, "warning")
                        : "No issue",
                $"{FormatReportHistoryCount(report.InputCount, "input")} / {FormatReportHistoryCount(report.OperationCount, "step")} / {FormatReportHistoryCount(report.MutationCount, "mutation")}",
                report.RunId,
                report.StartedAtUtc,
                report.IcId,
                report.ModeId,
                report.ExperienceId,
                report.CompositionKind));
    }

    private static string CreateReportHistoryContext(ReportReviewViewModel report)
    {
        string workflow = string.IsNullOrWhiteSpace(report.CompositionKind)
            ? "Report"
            : report.CompositionKind;
        string experience = string.IsNullOrWhiteSpace(report.ExperienceId)
            ? report.SourceName
            : report.ExperienceId;
        string ic = string.IsNullOrWhiteSpace(report.IcId) ? "unknown IC" : report.IcId;
        return $"{workflow} / {experience} / {ic}";
    }

    private static string FormatReportHistoryCount(int count, string noun)
    {
        return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
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
