using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReportPresentationViewModel
{
    internal const int MaxReportHistoryEntries = 12;
    private const int ReportHistoryStorageWarningBytes = 1024 * 1024;
    internal const long MaximumReportHistoryStorageBytes = 16L * 1024 * 1024;
    private int _reportHistorySequence;

    /// <summary>Gets session-local reports that can be reopened without re-running firmware workflows.</summary>
    public ObservableCollection<ReportHistoryEntryViewModel> ReportHistoryEntries { get; } = [];

    public bool HasReportHistory => ReportHistoryEntries.Count > 0;

    public bool IsReportHistoryEmpty => !HasReportHistory;

    public int ReportHistoryCount => ReportHistoryEntries.Count;

    public string ReportHistorySummary => Text.GetReportHistorySummary(ReportHistoryCount);

    /// <summary>Total in-memory persisted history payload size.</summary>
    public long ReportHistoryTotalBytes => ReportHistoryEntries.Sum(static entry => entry.StoredByteCount);

    public string ReportHistoryStorageSummary => Text.GetReportHistoryStorageSummary(FormatByteCount(ReportHistoryTotalBytes));

    public bool HasReportHistoryStorageWarning => ReportHistoryTotalBytes >= ReportHistoryStorageWarningBytes;

    public string ReportHistoryStorageWarning => Text.GetReportHistoryStorageWarning(
        FormatByteCount(ReportHistoryTotalBytes),
        FormatByteCount(ReportHistoryStorageWarningBytes));

    public bool IsReportHistoryViewOpen { get; private set; }

    public bool IsReportReviewViewOpen => !IsReportHistoryViewOpen;

    public bool CanOpenReportHistory => HasReportHistory;

    public bool CanClearReportHistory => HasReportHistory;

    public IRelayCommand ShowReportHistoryCommand { get; }

    public IRelayCommand CloseReportHistoryCommand { get; }

    public IRelayCommand ClearReportHistoryCommand { get; }

    public IRelayCommand<ReportHistoryEntryViewModel> OpenReportHistoryEntryCommand { get; }

    /// <summary>Cancellable command used by the UI to reopen a report without blocking the dispatcher.</summary>
    public IAsyncRelayCommand<ReportHistoryEntryViewModel> OpenReportHistoryEntryAsyncCommand { get; }

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

    /// <summary>Loads and prepares persisted history away from the dispatcher, unless a newer report wins.</summary>
    internal async Task<ReportPublicationResult> LoadReportHistoryAsync(
        Func<CancellationToken, Task<IReadOnlyList<ReportHistorySnapshot>>> loadSnapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loadSnapshots);
        long generation = BeginReportProjection();
        IReadOnlyList<ReportHistorySnapshot> snapshots = await loadSnapshots(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentReportProjection(generation))
        {
            return new(ReportPublicationOutcome.Superseded);
        }

        while (true)
        {
            ShellLanguage language = Text.Language;
            PreparedReportHistory prepared = await Task.Run(
                () => PrepareReportHistory(snapshots, language, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentReportProjection(generation))
            {
                return new(ReportPublicationOutcome.Superseded);
            }

            if (language != Text.Language)
            {
                continue;
            }

            ApplyPreparedReportHistory(prepared);
            return new(ReportPublicationOutcome.Published);
        }
    }

    private static PreparedReportHistory PrepareReportHistory(
        IEnumerable<ReportHistorySnapshot> snapshots,
        ShellLanguage language,
        CancellationToken cancellationToken)
    {
        var entries = new List<ReportHistoryEntryViewModel>(MaxReportHistoryEntries);
        ReportReviewViewModel loadedReport = ReportReviewViewModel.Empty;
        string loadedReportJson = string.Empty;
        long retainedBytes = 0;
        int sequence = 0;
        foreach (ReportHistorySnapshot snapshot in snapshots.Take(MaxReportHistoryEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool materializeAsCurrent = entries.Count == 0;
            if (!TryPrepareReportHistoryEntry(
                    snapshot,
                    language,
                    materializeAsCurrent,
                    cancellationToken,
                    out ReportHistorySnapshot? normalizedSnapshot,
                    out ReportReviewViewModel? materializedReport) ||
                normalizedSnapshot is null)
            {
                continue;
            }

            var entry = new ReportHistoryEntryViewModel(
                sequence + 1,
                normalizedSnapshot,
                materializedReport?.ReportJsonUtf8ByteCount);
            if (entries.Count > 0 && ExceedsReportHistoryStorageBudget(retainedBytes, entry.StoredByteCount))
            {
                continue;
            }

            entries.Add(entry);
            sequence++;
            retainedBytes += entry.StoredByteCount;
            if (materializeAsCurrent && materializedReport is not null)
            {
                loadedReport = materializedReport;
                loadedReportJson = normalizedSnapshot.ReportJson;
            }
        }

        return new PreparedReportHistory(entries, sequence, loadedReport, loadedReportJson);
    }

    private static bool TryPrepareReportHistoryEntry(
        ReportHistorySnapshot snapshot,
        ShellLanguage language,
        bool materializeAsCurrent,
        CancellationToken cancellationToken,
        out ReportHistorySnapshot? normalizedSnapshot,
        out ReportReviewViewModel? materializedReport)
    {
        normalizedSnapshot = null;
        materializedReport = null;
        if (string.IsNullOrWhiteSpace(snapshot.ReportJson))
        {
            return false;
        }

        try
        {
            if (snapshot.Metadata == ReportHistoryMetadataSnapshot.Empty)
            {
                materializedReport = ReportReviewViewModel.FromJsonCancellable(
                    snapshot.ReportJson,
                    string.IsNullOrWhiteSpace(snapshot.SourceName) ? "persisted report" : snapshot.SourceName,
                    snapshot.OutputArtifactPath,
                    inspectionSnapshot: null,
                    language,
                    cancellationToken);
            }
            else if (!materializeAsCurrent)
            {
                using var _ = JsonDocument.Parse(snapshot.ReportJson);
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                try
                {
                    materializedReport = ReportReviewViewModel.FromJsonCancellable(
                        snapshot.ReportJson,
                        string.IsNullOrWhiteSpace(snapshot.SourceName) ? "persisted report" : snapshot.SourceName,
                        snapshot.OutputArtifactPath,
                        inspectionSnapshot: null,
                        language,
                        cancellationToken);
                }
                catch (Exception exception) when (IsReportMaterializationException(exception))
                {
                    using var _ = JsonDocument.Parse(snapshot.ReportJson);
                    cancellationToken.ThrowIfCancellationRequested();
                    materializedReport = ReportReviewViewModel.Error(
                        snapshot.SourceName,
                        exception.Message,
                        language: language);
                }
            }

            normalizedSnapshot = snapshot.Metadata == ReportHistoryMetadataSnapshot.Empty
                ? CreateReportHistorySnapshot(materializedReport!, snapshot.ReportJson)
                : snapshot;
            return true;
        }
        catch (Exception exception) when (IsReportMaterializationException(exception))
        {
            return false;
        }
    }

    private void ApplyPreparedReportHistory(PreparedReportHistory prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);

        PresentationObserver.Invoke(ReportHistoryEntries.Clear);
        _reportHistorySequence = prepared.Sequence;
        foreach (ReportHistoryEntryViewModel entry in prepared.Entries)
        {
            PresentationObserver.Invoke(() => ReportHistoryEntries.Add(entry));
        }

        if (ReportHistoryEntries.Count == 0)
        {
            LoadedReport = ReportReviewViewModel.Empty;
            LoadedReportJson = string.Empty;
        }
        else
        {
            LoadedReport = prepared.LoadedReport;
            LoadedReportJson = prepared.LoadedReportJson;
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

        _beforeOpen();
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
        _beforeOpen();
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

        var entry = new ReportHistoryEntryViewModel(
            ++_reportHistorySequence,
            CreateReportHistorySnapshot(LoadedReport, LoadedReportJson),
            LoadedReport.ReportJsonUtf8ByteCount);
        PresentationObserver.Invoke(() => ReportHistoryEntries.Insert(0, entry));
        while (ReportHistoryEntries.Count > MaxReportHistoryEntries ||
               (ReportHistoryEntries.Count > 1 && ReportHistoryTotalBytes > MaximumReportHistoryStorageBytes))
        {
            PresentationObserver.Invoke(() => ReportHistoryEntries.RemoveAt(ReportHistoryEntries.Count - 1));
        }

        NotifyReportHistoryChanged();
    }

    private void NotifyReportHistoryChanged()
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportHistoryEntries)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasReportHistory)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IsReportHistoryEmpty)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportHistoryCount)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportHistorySummary)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportHistoryTotalBytes)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportHistoryStorageSummary)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasReportHistoryStorageWarning)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReportHistoryStorageWarning)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(CanOpenReportHistory)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(CanClearReportHistory)));
        PresentationObserver.Invoke(ShowReportHistoryCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(ClearReportHistoryCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(OpenReportHistoryEntryAsyncCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(RemoveReportHistoryEntryCommand.NotifyCanExecuteChanged);
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

    private async Task RelocalizeLoadedReportAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            long requestVersion = Volatile.Read(ref _reportRelocalizationRequestVersion);
            string reportJson = LoadedReportJson;
            ReportReviewViewModel currentReport = LoadedReport;
            long generation = Volatile.Read(ref _reportProjectionGeneration);
            if (string.IsNullOrWhiteSpace(reportJson))
            {
                return;
            }

            using var iterationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Interlocked.Exchange(
                ref _reportRelocalizationIterationCancellation,
                iterationCancellation);
            ReportReviewViewModel localizedReport;
            try
            {
                localizedReport = await ProjectReportAsync(
                    reportJson,
                    currentReport.SourceName,
                    currentReport.OutputArtifactPath,
                    iterationCancellation.Token,
                    materializationErrorsAsReport: false,
                    inspectionSnapshot: currentReport.InspectionSnapshot);
            }
            catch (OperationCanceledException) when (iterationCancellation.IsCancellationRequested)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (requestVersion == Volatile.Read(ref _reportRelocalizationRequestVersion))
                {
                    return;
                }

                continue;
            }
            catch (Exception exception) when (IsReportMaterializationException(exception))
            {
                return;
            }
            finally
            {
                _ = Interlocked.CompareExchange(
                    ref _reportRelocalizationIterationCancellation,
                    null,
                    iterationCancellation);
            }

            if (IsCurrentReportProjection(generation) &&
                string.Equals(LoadedReportJson, reportJson, StringComparison.Ordinal))
            {
                ApplyRelocalizedReport(localizedReport, reportJson);
            }

            if (requestVersion == Volatile.Read(ref _reportRelocalizationRequestVersion))
            {
                return;
            }
        }
    }

    private void ApplyRelocalizedReport(ReportReviewViewModel localizedReport, string reportJson)
    {
        LoadedReport = localizedReport;
        for (int index = 0; index < ReportHistoryEntries.Count; index++)
        {
            ReportHistoryEntryViewModel entry = ReportHistoryEntries[index];
            if (!string.Equals(entry.ReportJson, reportJson, StringComparison.Ordinal))
            {
                continue;
            }

            ReportHistoryEntries[index] = new ReportHistoryEntryViewModel(
                entry.Sequence,
                CreateReportHistorySnapshot(LoadedReport, entry.ReportJson),
                LoadedReport.ReportJsonUtf8ByteCount);
            break;
        }

        NotifyReportChanged();
        NotifyReportHistoryChanged();
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
                report.IsOutputNotGenerated
                    ? "No output generated"
                    : string.IsNullOrWhiteSpace(report.OutputFileName)
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

    internal static ReportHistorySnapshot OmitDerivableReportHistoryMetadata(
        ReportHistorySnapshot snapshot)
    {
        var withoutMetadata = new ReportHistorySnapshot(
            snapshot.SourceName, snapshot.ReportJson, snapshot.OutputArtifactPath);
        return Enum.GetValues<ShellLanguage>().Any(language =>
                TryPrepareReportHistoryEntry(
                    withoutMetadata,
                    language,
                    materializeAsCurrent: true,
                    CancellationToken.None,
                    out ReportHistorySnapshot? normalized,
                    out _) &&
                normalized!.Metadata == snapshot.Metadata)
            ? withoutMetadata
            : snapshot;
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

    private static bool ExceedsReportHistoryStorageBudget(long retainedBytes, long candidateBytes)
    {
        return candidateBytes > MaximumReportHistoryStorageBytes ||
               retainedBytes > MaximumReportHistoryStorageBytes - candidateBytes;
    }

    private sealed record PreparedReportHistory(
        IReadOnlyList<ReportHistoryEntryViewModel> Entries,
        int Sequence,
        ReportReviewViewModel LoadedReport,
        string LoadedReportJson);
}
