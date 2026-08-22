using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum ReportPublicationOutcome { Unknown, Published, Superseded, Failed }

internal readonly record struct ReportPublicationResult(
    ReportPublicationOutcome Outcome,
    string Diagnostic = "");

internal sealed partial class ReportPresentationViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions RunErrorReportJsonOptions = new() { WriteIndented = true };
    private readonly Action _beforeOpen;
    private readonly Func<ShellTextResources> _textProvider;
    private readonly AsyncRelayCommand _relocalizeLoadedReportCommand;
    private CancellationTokenSource? _reportRelocalizationIterationCancellation;
    private long _reportRelocalizationRequestVersion;
    private long _reportProjectionGeneration;

    internal ReportPresentationViewModel(
        Func<ShellTextResources> textProvider,
        Action beforeOpen)
    {
        ArgumentNullException.ThrowIfNull(textProvider);
        ArgumentNullException.ThrowIfNull(beforeOpen);
        _textProvider = textProvider;
        _beforeOpen = beforeOpen;
        _relocalizeLoadedReportCommand = new AsyncRelayCommand(RelocalizeLoadedReportAsync);
        ShowReportCommand = new RelayCommand(ShowReport, () => CanOpenReport);
        CloseReportCommand = new RelayCommand(CloseReport);
        DismissReportToastCommand = new RelayCommand(DismissReportToast);
        ShowReportHistoryCommand = new RelayCommand(ShowReportHistory, () => CanOpenReportHistory);
        CloseReportHistoryCommand = new RelayCommand(CloseReportHistory);
        ClearReportHistoryCommand = new RelayCommand(ClearReportHistory, () => CanClearReportHistory);
        OpenReportHistoryEntryAsyncCommand = new AsyncRelayCommand<ReportHistoryEntryViewModel>(OpenReportHistoryEntryAsync);
        OpenReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(
            entry => OpenReportHistoryEntryAsyncCommand.Execute(entry),
            entry => OpenReportHistoryEntryAsyncCommand.CanExecute(entry));
        OpenReportHistoryEntryAsyncCommand.CanExecuteChanged += OpenReportHistoryEntryAsyncCommand_CanExecuteChanged;
        RemoveReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(RemoveReportHistoryEntry);
    }

    public ShellTextResources Text => _textProvider();

    internal bool IsRelocalizationRunning => _relocalizeLoadedReportCommand.IsRunning;

    internal Task? RelocalizationTask => _relocalizeLoadedReportCommand.ExecutionTask;

    public ReportReviewViewModel LoadedReport { get; private set; } = ReportReviewViewModel.Empty;

    public string LoadedReportJson { get; private set; } = string.Empty;

    public bool HasLoadedReport => !LoadedReport.IsEmpty;

    public bool CanOpenReport => HasLoadedReport;

    public string ReportActionLabel => Text.GetReportActionLabel(HasLoadedReport);

    public string ReportActionStatus => HasLoadedReport
        ? LoadedReport.Status
        : Text.GetReportActionStatus(hasLoadedReport: false, LoadedReport.Status);

    public bool IsReportModalOpen { get; private set; }

    public bool HasReportToast { get; private set; }

    public double ReportToastOpacity { get; private set; }

    public string ReportToastText { get; private set; } = string.Empty;

    public string ShellToastTitle { get; private set; } = string.Empty;

    public string ShellToastAccessibleLabel => $"{ShellToastTitle}. {ReportToastText}";

    public string ReportSaveFileName => HasLoadedReport
        ? $"{SanitizeFileName(LoadedReport.Title)}.json"
        : "nvt-fw-combiner-report.json";

    public IRelayCommand ShowReportCommand { get; }

    public IRelayCommand CloseReportCommand { get; }

    public IRelayCommand DismissReportToastCommand { get; }

    /// <summary>Loads a CLI/application run report JSON into the readable report modal.</summary>
    public void LoadReportJson(string json, string sourceName)
    {
        long generation = BeginReportProjection();
        ReportReviewViewModel report;
        try
        {
            report = ReportReviewViewModel.FromJson(json, sourceName, language: Text.Language);
        }
        catch (Exception exception) when (IsReportMaterializationException(exception))
        {
            report = ReportReviewViewModel.Error(sourceName, exception.Message, language: Text.Language);
        }

        if (IsCurrentReportProjection(generation))
        {
            ApplyLoadedReport(report, json, sourceName);
        }
    }

    /// <summary>Loads one admitted report-file snapshot unless a newer report wins.</summary>
    internal async Task<ReportPublicationResult> LoadReportFileAsync(
        Func<CancellationToken, ValueTask<string>> readFile,
        string sourceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(sourceName);
        long generation = BeginReportProjection();
        string json;
        ReportReviewViewModel report;
        try
        {
            json = await readFile(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentReportProjection(generation))
            {
                return new(ReportPublicationOutcome.Superseded);
            }

            report = await ProjectReportAsync(
                json,
                sourceName,
                outputArtifactPath: null,
                cancellationToken,
                materializationErrorsAsReport: false);
        }
        catch (Exception exception) when (
            exception is LocalFileReadException || IsReportMaterializationException(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentReportProjection(generation))
            {
                return new(ReportPublicationOutcome.Superseded);
            }

            SetShellToast(Text.ReportToastTitle, exception.Message);
            return new(ReportPublicationOutcome.Failed, exception.Message);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentReportProjection(generation))
        {
            return new(ReportPublicationOutcome.Superseded);
        }

        ApplyLoadedReport(report, json, sourceName);

        return new(ReportPublicationOutcome.Published);
    }

    internal async Task<ReportReviewViewModel> ProjectReportAsync(
        string json,
        string sourceName,
        string? outputArtifactPath,
        CancellationToken cancellationToken,
        bool materializationErrorsAsReport = true,
        CompositionRunInspectionSnapshot? inspectionSnapshot = null)
    {
        ShellLanguage language;
        ReportReviewViewModel report;
        do
        {
            language = Text.Language;
            try
            {
                report = await Task.Run(
                    () => ReportReviewViewModel.FromJsonCancellable(
                        json,
                        sourceName,
                        outputArtifactPath,
                        inspectionSnapshot,
                        language,
                        cancellationToken),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception exception) when (
                materializationErrorsAsReport && IsReportMaterializationException(exception))
            {
                report = ReportReviewViewModel.Error(sourceName, exception.Message, language: language);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (language != Text.Language);

        return report;
    }

    internal async Task<ReportReviewViewModel> ProjectReportAsync(
        CompositionRunReport report,
        bool suppressOutput,
        string sourceName,
        string? outputArtifactPath,
        CancellationToken cancellationToken,
        bool materializationErrorsAsReport = true,
        CompositionRunInspectionSnapshot? inspectionSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ShellLanguage language;
        ReportReviewViewModel projected;
        do
        {
            language = Text.Language;
            try
            {
                projected = await Task.Run(
                    () => ReportReviewViewModel.FromReportCancellable(
                        report,
                        suppressOutput,
                        sourceName,
                        outputArtifactPath,
                        inspectionSnapshot,
                        language,
                        cancellationToken),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception exception) when (
                materializationErrorsAsReport && IsReportMaterializationException(exception))
            {
                projected = ReportReviewViewModel.Error(sourceName, exception.Message, language: language);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (language != Text.Language);

        return projected;
    }

    internal long BeginReportProjection(bool preserveHistoryReopen = false)
    {
        if (!preserveHistoryReopen && OpenReportHistoryEntryAsyncCommand is { IsRunning: true } historyReopen)
        {
            historyReopen.Cancel();
        }

        CancelReportRelocalization();

        return Interlocked.Increment(ref _reportProjectionGeneration);
    }

    internal bool IsCurrentReportProjection(long generation)
    {
        return Volatile.Read(ref _reportProjectionGeneration) == generation;
    }

    private void ApplyLoadedReport(
        ReportReviewViewModel report,
        string json,
        string sourceName)
    {
        CancelReportRelocalization();
        LoadedReport = report;
        LoadedReportJson = json;
        CaptureLoadedReportInHistory();
        SetReportToast(Text.FormatReportLoadedToast(sourceName));
        NotifyReportChanged();
    }

    public void LoadReportError(string sourceName, string message)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(message);

        _ = BeginReportProjection();
        LoadedReport = ReportReviewViewModel.Error(
            sourceName,
            message,
            "Load error",
            "Load failed",
            Text.Language);
        LoadedReportJson = string.Empty;
        CaptureLoadedReportInHistory();
        SetReportToast(Text.FormatReportIssueToast(sourceName));
        NotifyReportChanged();
    }

    /// <summary>Shows a compact notification after the report is written to disk.</summary>
    public void NotifyReportSaved(string destinationName)
    {
        SetReportToast(Text.FormatReportSavedToast(destinationName));
    }

    public void LoadRunErrorReport(
        string action,
        string profileId,
        string icId,
        string number,
        string message,
        IReadOnlyDictionary<string, string> slotPaths,
        string compositionKind = "Merge",
        string modeId = ExperienceIds.StandardMerge,
        string experienceId = ExperienceIds.StandardMerge,
        string issueCode = CompositionPlanningIssueCodes.UiRunFailed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);

        _ = BeginReportProjection();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var report = new
        {
            RunId = $"ui-{action.ToLowerInvariant()}-error-{timestamp.ToUnixTimeMilliseconds()}",
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? ExperienceIds.StandardMerge : profileId,
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
        LoadedReport = ReportReviewViewModel.FromJson(json, $"{action.ToLowerInvariant()} error report", language: Text.Language);
        LoadedReportJson = json;
        CaptureLoadedReportInHistory();
        SetReportToast(Text.FormatReportGeneratedToast(action));
        NotifyReportChanged();
    }

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

    internal void ShowReport()
    {
        if (!HasLoadedReport)
        {
            return;
        }

        CancelReportHistoryReopen();
        _beforeOpen();
        IsReportModalOpen = true;
        IsReportHistoryViewOpen = false;
        HasReportToast = false;
        ReportToastOpacity = 0;
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IsReportModalOpen)));
        NotifyReportViewModeChanged();
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasReportToast)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportToastOpacity)));
    }

    private void CloseReport()
    {
        CancelReportHistoryReopen();
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
        SetShellToast(Text.ReportToastTitle, text);
    }

    internal void SetShellToast(string title, string text)
    {
        ShellToastTitle = title;
        ReportToastText = text;
        HasReportToast = true;
        ReportToastOpacity = 1;
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ShellToastTitle)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportToastText)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ShellToastAccessibleLabel)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasReportToast)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportToastOpacity)));
    }

    private void NotifyReportViewModeChanged()
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IsReportHistoryViewOpen)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IsReportReviewViewOpen)));
    }

    private void NotifyReportChanged()
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(LoadedReport)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(LoadedReportJson)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasLoadedReport)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(CanOpenReport)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportActionLabel)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportActionStatus)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportSaveFileName)));
        PresentationObserver.Invoke(ShowReportCommand.NotifyCanExecuteChanged);
    }

    internal void ApplyLanguageChanged()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(ReportActionLabel));
        OnPropertyChanged(nameof(ReportActionStatus));
        OnPropertyChanged(nameof(ReportHistorySummary));
        OnPropertyChanged(nameof(ReportHistoryStorageSummary));
        OnPropertyChanged(nameof(ReportHistoryStorageWarning));
        RequestReportRelocalization();
    }

    private void RequestReportRelocalization()
    {
        _ = Interlocked.Increment(ref _reportRelocalizationRequestVersion);
        Volatile.Read(ref _reportRelocalizationIterationCancellation)?.Cancel();
        if (!_relocalizeLoadedReportCommand.IsRunning)
        {
            _relocalizeLoadedReportCommand.Execute(null);
        }
    }

    private void CancelReportRelocalization()
    {
        Volatile.Read(ref _reportRelocalizationIterationCancellation)?.Cancel();
    }

    internal void PublishGeneratedReport(
        ReportReviewViewModel report,
        string reportJson,
        string action,
        bool show)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(reportJson);
        ArgumentNullException.ThrowIfNull(action);
        LoadedReport = report;
        LoadedReportJson = reportJson;
        CaptureLoadedReportInHistory();
        SetReportToast(Text.FormatReportGeneratedToast(action));
        NotifyReportChanged();
        if (show)
        {
            ShowReport();
        }
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

    private static bool IsReportMaterializationException(Exception exception)
    {
        return exception is JsonException or
            InvalidOperationException or
            ArgumentException or
            FormatException or
            OverflowException;
    }

}
