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
    private static readonly JsonSerializerOptions RunErrorReportJsonOptions = new() { WriteIndented = true };
    private int _reportHistorySequence;

    /// <summary>Gets the loaded run report summary.</summary>
    public ReportReviewViewModel LoadedReport { get; private set; } = ReportReviewViewModel.Empty;

    /// <summary>Gets the original report JSON used by Save report.</summary>
    public string LoadedReportJson { get; private set; } = string.Empty;

    /// <summary>Gets session-local reports that can be reopened without re-running firmware workflows.</summary>
    public ObservableCollection<ReportHistoryEntryViewModel> ReportHistoryEntries { get; } = [];

    /// <summary>True when the session has at least one report.</summary>
    public bool HasReportHistory => ReportHistoryEntries.Count > 0;

    /// <summary>True when the report history list is empty.</summary>
    public bool IsReportHistoryEmpty => !HasReportHistory;

    /// <summary>Number of reports captured in this UI session.</summary>
    public int ReportHistoryCount => ReportHistoryEntries.Count;

    /// <summary>Compact report history summary.</summary>
    public string ReportHistorySummary => HasReportHistory
        ? ReportHistoryCount == 1
            ? "1 report in history"
            : $"{ReportHistoryCount} reports in history"
        : "No reports in history";

    /// <summary>Total in-memory persisted history payload size.</summary>
    public long ReportHistoryTotalBytes => ReportHistoryEntries.Sum(entry =>
        Encoding.UTF8.GetByteCount(entry.ReportJson) +
        Encoding.UTF8.GetByteCount(entry.ArtifactPath));

    /// <summary>Human-readable local report history storage summary.</summary>
    public string ReportHistoryStorageSummary => $"{FormatByteCount(ReportHistoryTotalBytes)} stored locally";

    /// <summary>True when local report history has crossed the cleanup warning size.</summary>
    public bool HasReportHistoryStorageWarning => ReportHistoryTotalBytes >= ReportHistoryStorageWarningBytes;

    /// <summary>Cleanup warning for oversized local report history.</summary>
    public string ReportHistoryStorageWarning => $"History uses {FormatByteCount(ReportHistoryTotalBytes)}, above the {FormatByteCount(ReportHistoryStorageWarningBytes)} limit. Clear history to keep local UI state small.";

    /// <summary>True when a run report is loaded into the shell.</summary>
    public bool HasLoadedReport => !LoadedReport.IsEmpty;

    /// <summary>True when the report icon can open the report modal.</summary>
    public bool CanOpenReport => HasLoadedReport;

    /// <summary>Gets the shell report action label.</summary>
    public string ReportActionLabel => HasLoadedReport ? "Open report" : "No report";

    /// <summary>Gets the shell report action status.</summary>
    public string ReportActionStatus => HasLoadedReport
        ? LoadedReport.Status
        : "Preview or Build creates one";

    /// <summary>True when the latest report modal is open.</summary>
    public bool IsReportModalOpen { get; private set; }

    /// <summary>True when the report modal shows the dedicated history view.</summary>
    public bool IsReportHistoryViewOpen { get; private set; }

    /// <summary>True when the report modal shows the current report review.</summary>
    public bool IsReportReviewViewOpen => !IsReportHistoryViewOpen;

    /// <summary>True when a report history view can be opened.</summary>
    public bool CanOpenReportHistory => HasReportHistory;

    /// <summary>True when local report history can be cleared.</summary>
    public bool CanClearReportHistory => HasReportHistory;

    /// <summary>True when a compact report notification should be shown.</summary>
    public bool HasReportToast { get; private set; }

    /// <summary>Gets the compact report notification opacity.</summary>
    public double ReportToastOpacity { get; private set; }

    /// <summary>Gets the compact report notification text.</summary>
    public string ReportToastText { get; private set; } = string.Empty;

    /// <summary>Gets a suggested report JSON file name.</summary>
    public string ReportSaveFileName => HasLoadedReport
        ? $"{SanitizeFileName(LoadedReport.Title)}.json"
        : "nvt-fw-combiner-report.json";

    /// <summary>Command that opens the latest report modal.</summary>
    public IRelayCommand ShowReportCommand { get; }

    /// <summary>Command that closes the report modal.</summary>
    public IRelayCommand CloseReportCommand { get; }

    /// <summary>Command that dismisses the compact report notification.</summary>
    public IRelayCommand DismissReportToastCommand { get; }

    /// <summary>Command that opens the dedicated report history view.</summary>
    public IRelayCommand ShowReportHistoryCommand { get; }

    /// <summary>Command that returns from report history to the current report review.</summary>
    public IRelayCommand CloseReportHistoryCommand { get; }

    /// <summary>Command that clears local report history entries.</summary>
    public IRelayCommand ClearReportHistoryCommand { get; }

    /// <summary>Command that reopens a report history entry.</summary>
    public IRelayCommand<ReportHistoryEntryViewModel> OpenReportHistoryEntryCommand { get; }

    /// <summary>Loads a CLI/application run report JSON into the readable report modal.</summary>
    public void LoadReportJson(string json, string sourceName)
    {
        try
        {
            LoadedReport = ReportReviewViewModel.FromJson(json, sourceName);
        }
        catch (JsonException exception)
        {
            LoadedReport = ReportReviewViewModel.Error(sourceName, exception.Message);
        }

        LoadedReportJson = json;
        CaptureLoadedReportInHistory();
        SetReportToast($"Report loaded: {sourceName}");
        NotifyReportChanged();
        RefreshSettingsState();
    }

    /// <summary>Loads a report review error as the latest reopenable report.</summary>
    public void LoadReportError(string sourceName, string message)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(message);

        LoadedReport = ReportReviewViewModel.Error(sourceName, message, "Load error", "Load failed");
        LoadedReportJson = string.Empty;
        CaptureLoadedReportInHistory();
        SetReportToast($"Report issue: {sourceName}");
        NotifyReportChanged();
        RefreshSettingsState();
    }

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

    /// <summary>Shows a compact notification after the report is written to disk.</summary>
    public void NotifyReportSaved(string destinationName)
    {
        SetReportToast($"Report saved: {destinationName}");
    }

    /// <summary>Loads a UI-triggered run failure as the latest reopenable report.</summary>
    public void LoadRunErrorReport(
        string action,
        string profileId,
        string icId,
        string number,
        string message,
        IReadOnlyDictionary<string, string> slotPaths,
        string compositionKind = "Merge",
        string modeId = "standard-merge",
        string experienceId = "standard-merge",
        string issueCode = "ui.run.failed")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var report = new
        {
            RunId = $"ui-{action.ToLowerInvariant()}-error-{timestamp.ToUnixTimeMilliseconds()}",
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? "standard-merge" : profileId,
            ProfileVersion = string.Empty,
            IcId = icId,
            ModeId = modeId,
            ExperienceId = experienceId,
            CompositionKind = compositionKind,
            StartedAtUtc = timestamp,
            CompletedAtUtc = timestamp,
            Inputs = slotPaths
                .OrderBy(path => path.Key, StringComparer.Ordinal)
                .Select(path => new
                {
                    AddressSpaceId = path.Key,
                    BindingId = path.Key,
                    Size = 0,
                    Sha256 = string.Empty,
                    ArtifactId = Path.GetFileName(path.Value),
                }),
            Operations = Array.Empty<object>(),
            Mutations = Array.Empty<object>(),
            Issues = new[]
            {
                new
                {
                    Code = issueCode,
                    Severity = "error",
                    Message = message,
                    OperationId = $"{icId} / {number}",
                },
            },
            Output = new
            {
                FileName = "No output",
                Size = 0,
                Committed = false,
                Sha256 = string.Empty,
            },
        };

        string json = JsonSerializer.Serialize(report, RunErrorReportJsonOptions);
        LoadedReport = ReportReviewViewModel.FromJson(json, $"{action.ToLowerInvariant()} error report");
        LoadedReportJson = json;
        CaptureLoadedReportInHistory();
        SetReportToast($"{action} report generated");
        NotifyReportChanged();
        RefreshSettingsState();
    }

    /// <summary>Updates toast opacity during the view-owned fade-out animation.</summary>
    public void SetReportToastOpacity(double opacity)
    {
        double next = Math.Clamp(opacity, 0, 1);
        if (Math.Abs(ReportToastOpacity - next) < 0.001)
        {
            return;
        }

        ReportToastOpacity = next;
        OnPropertyChanged(nameof(ReportToastOpacity));
    }

    private void ShowReport()
    {
        if (!HasLoadedReport)
        {
            return;
        }

        CloseReplaceSelectionForRun();
        IsReportModalOpen = true;
        IsReportHistoryViewOpen = false;
        HasReportToast = false;
        ReportToastOpacity = 0;
        OnPropertyChanged(nameof(IsReportModalOpen));
        NotifyReportViewModeChanged();
        OnPropertyChanged(nameof(HasReportToast));
        OnPropertyChanged(nameof(ReportToastOpacity));
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

    private void CloseReport()
    {
        if (!IsReportModalOpen)
        {
            return;
        }

        IsReportModalOpen = false;
        IsReportHistoryViewOpen = false;
        OnPropertyChanged(nameof(IsReportModalOpen));
        NotifyReportViewModeChanged();
    }

    private void DismissReportToast()
    {
        if (!HasReportToast)
        {
            return;
        }

        HasReportToast = false;
        ReportToastOpacity = 0;
        OnPropertyChanged(nameof(HasReportToast));
        OnPropertyChanged(nameof(ReportToastOpacity));
    }

    private void SetReportToast(string text)
    {
        ReportToastText = text;
        HasReportToast = true;
        ReportToastOpacity = 1;
        OnPropertyChanged(nameof(ReportToastText));
        OnPropertyChanged(nameof(HasReportToast));
        OnPropertyChanged(nameof(ReportToastOpacity));
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
                snapshot.OutputArtifactPath);
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
    }

    private void NotifyReportViewModeChanged()
    {
        OnPropertyChanged(nameof(IsReportHistoryViewOpen));
        OnPropertyChanged(nameof(IsReportReviewViewOpen));
    }

    private void NotifyReportChanged()
    {
        OnPropertyChanged(nameof(LoadedReport));
        OnPropertyChanged(nameof(LoadedReportJson));
        OnPropertyChanged(nameof(HasLoadedReport));
        OnPropertyChanged(nameof(CanOpenReport));
        OnPropertyChanged(nameof(ReportActionLabel));
        OnPropertyChanged(nameof(ReportActionStatus));
        OnPropertyChanged(nameof(ReportSaveFileName));
        ShowReportCommand.NotifyCanExecuteChanged();
    }

    private static string SanitizeFileName(string title)
    {
        string candidate = string.IsNullOrWhiteSpace(title)
            ? "nvt-fw-combiner-report"
            : title.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalid, '-');
        }

        return candidate.Length == 0 ? "nvt-fw-combiner-report" : candidate;
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
