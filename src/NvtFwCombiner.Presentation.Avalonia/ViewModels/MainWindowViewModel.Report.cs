using System.Text.Json;
using CommunityToolkit.Mvvm.Input;

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

    /// <summary>True when the latest report modal is open.</summary>
    public bool IsReportModalOpen { get; private set; }

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
        SetReportToast($"Report loaded: {sourceName}");
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
        string experienceId = "standard-merge")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(slotPaths);

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
                    Code = "ui.run.failed",
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
        HasReportToast = false;
        ReportToastOpacity = 0;
        OnPropertyChanged(nameof(IsReportModalOpen));
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
        OnPropertyChanged(nameof(IsReportModalOpen));
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

    private void NotifyReportChanged()
    {
        OnPropertyChanged(nameof(LoadedReport));
        OnPropertyChanged(nameof(LoadedReportJson));
        OnPropertyChanged(nameof(HasLoadedReport));
        OnPropertyChanged(nameof(CanOpenReport));
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
}
