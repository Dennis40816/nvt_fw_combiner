using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static readonly JsonSerializerOptions RunErrorReportJsonOptions = new() { WriteIndented = true };

    /// <summary>Gets the loaded run report summary.</summary>
    public ReportReviewViewModel LoadedReport { get; private set; } = ReportReviewViewModel.Empty;

    /// <summary>Gets the original report JSON used by Save report.</summary>
    public string LoadedReportJson { get; private set; } = string.Empty;

    /// <summary>True when a run report is loaded into the shell.</summary>
    public bool HasLoadedReport => !LoadedReport.IsEmpty;

    /// <summary>True when the report icon can open the report modal.</summary>
    public bool CanOpenReport => HasLoadedReport;

    /// <summary>Gets the shell report action label.</summary>
    public string ReportActionLabel => Text.GetReportActionLabel(HasLoadedReport);

    /// <summary>Gets the shell report action status.</summary>
    public string ReportActionStatus => HasLoadedReport
        ? LoadedReport.Status
        : Text.GetReportActionStatus(hasLoadedReport: false, LoadedReport.Status);

    /// <summary>True when the latest report modal is open.</summary>
    public bool IsReportModalOpen { get; private set; }

    /// <summary>True when a compact report notification should be shown.</summary>
    public bool HasReportToast { get; private set; }

    /// <summary>Gets the compact report notification opacity.</summary>
    public double ReportToastOpacity { get; private set; }

    /// <summary>Gets the compact report notification text.</summary>
    public string ReportToastText { get; private set; } = string.Empty;

    /// <summary>Gets the title for the shell notification, including non-report context updates.</summary>
    public string ShellToastTitle { get; private set; } = string.Empty;

    /// <summary>Gets a suggested report JSON file name.</summary>
    public string ReportSaveFileName => HasLoadedReport
        ? $"{SanitizeFileName(LoadedReport.Title)}.json"
        : "nvt-fw-combiner-report.json";

    private string CreateBuildActionTip(string readinessStatus, bool canBuild)
    {
        return true switch
        {
            _ when HasLoadedReport && LoadedReport.HasPrimaryIssue =>
                $"{TrimOneLine(LoadedReport.PrimaryIssue.Detail, 150)} {Text.GetOpenReportForDetailsSentence()}",
            _ when HasLoadedReport && !LastRunResult.Succeeded =>
                $"{TrimOneLine(LastRunResult.Detail, 150)} {Text.GetOpenReportForDetailsSentence()}",
            _ when canBuild => Text.GetBuildActionTip(readinessStatus, canBuild: true),
            _ => readinessStatus,
        };
    }

    /// <summary>Command that opens the latest report modal.</summary>
    public IRelayCommand ShowReportCommand { get; }

    /// <summary>Command that closes the report modal.</summary>
    public IRelayCommand CloseReportCommand { get; }

    /// <summary>Command that dismisses the compact report notification.</summary>
    public IRelayCommand DismissReportToastCommand { get; }

    /// <summary>Loads a CLI/application run report JSON into the readable report modal.</summary>
    public void LoadReportJson(string json, string sourceName)
    {
        try
        {
            LoadedReport = ReportReviewViewModel.FromJson(json, sourceName, language: Text.Language);
        }
        catch (JsonException exception)
        {
            LoadedReport = ReportReviewViewModel.Error(sourceName, exception.Message, language: Text.Language);
        }

        LoadedReportJson = json;
        CaptureLoadedReportInHistory();
        SetReportToast(Text.FormatReportLoadedToast(sourceName));
        NotifyReportChanged();
    }

    /// <summary>Loads a report review error as the latest reopenable report.</summary>
    public void LoadReportError(string sourceName, string message)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(message);

        LoadedReport = ReportReviewViewModel.Error(sourceName, message, "Load error", "Load failed", Text.Language);
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

    /// <summary>Loads a UI-triggered run failure as the latest reopenable report.</summary>
    public void LoadRunErrorReport(
        string action,
        string profileId,
        string icId,
        string number,
        string message,
        IReadOnlyDictionary<string, string> slotPaths,
        string compositionKind = "Merge",
        string modeId = WorkbenchWorkflowIds.StandardMerge,
        string experienceId = WorkbenchWorkflowIds.StandardMerge,
        string issueCode = WorkbenchIssueCodes.UiRunFailed)
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
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? WorkbenchWorkflowIds.StandardMerge : profileId,
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
        SetShellToast(Text.ReportToastTitle, text);
    }

    private void SetShellToast(string title, string text)
    {
        ShellToastTitle = title;
        ReportToastText = text;
        HasReportToast = true;
        ReportToastOpacity = 1;
        OnPropertyChanged(nameof(ShellToastTitle));
        OnPropertyChanged(nameof(ReportToastText));
        OnPropertyChanged(nameof(HasReportToast));
        OnPropertyChanged(nameof(ReportToastOpacity));
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
        OnPropertyChanged(nameof(MergeBuildActionTip));
        OnPropertyChanged(nameof(ReplaceBuildActionTip));
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

    private static string TrimOneLine(string value, int maxLength)
    {
        string oneLine = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return oneLine.Length <= maxLength
            ? oneLine
            : string.Concat(oneLine.AsSpan(0, Math.Max(0, maxLength - 3)), "...");
    }
}
