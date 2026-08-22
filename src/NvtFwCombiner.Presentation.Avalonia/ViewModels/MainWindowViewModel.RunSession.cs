using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    /// <summary>Focused Preview/Build lifetime and progress presentation.</summary>
    public CompositionRunPresentationViewModel RunSession { get; }

    private async Task RunCompositionAsync(
        bool build,
        CompositionRunWork run,
        Action<string, string> loadErrorReport)
    {
        string mode = GetSelectedRunMode();
        UiRunResultViewModel? previous = RunSession.LastRunResult;
        RecordDebugActivity(
            build ? SystemActivityCodes.BuildStarted : SystemActivityCodes.PreviewStarted,
            SystemActivityCategory.Composition,
            mode,
            GetWorkflowSelectedIc());
        await RunSession.RunCompositionAsync(build, run, loadErrorReport);
        UiRunResultViewModel? result = RunSession.LastRunResult;
        bool succeeded = !ReferenceEquals(previous, result) && result?.Succeeded == true;
        RecordSystemActivity(new SystemActivityDraft(
            succeeded
                ? build ? SystemActivityCodes.BuildCompleted : SystemActivityCodes.PreviewCompleted
                : build ? SystemActivityCodes.BuildFailed : SystemActivityCodes.PreviewFailed,
            SystemActivityImportance.Important,
            SystemActivityCategory.Composition,
            succeeded ? SystemActivitySeverity.Success : SystemActivitySeverity.Error,
            mode,
            GetWorkflowSelectedIc()));
    }

    private async Task ShowDiagnosticPreviewAsync(CompositionRunReport report)
    {
        string mode = GetSelectedRunMode();
        RecordDebugActivity(
            SystemActivityCodes.PreviewStarted,
            SystemActivityCategory.Composition,
            mode,
            GetWorkflowSelectedIc());
        await RunSession.ShowDiagnosticPreviewAsync(report);
        RecordSystemActivity(new SystemActivityDraft(
            SystemActivityCodes.PreviewFailed,
            SystemActivityImportance.Important,
            SystemActivityCategory.Composition,
            SystemActivitySeverity.Warning,
            mode,
            GetWorkflowSelectedIc()));
    }

    private void ShowActionReadiness(
        CapabilityActionReadinessSnapshot readiness,
        bool build)
    {
        RunSession.ShowActionReadiness(readiness, build);
    }

    private bool IsCompositionRunInProgress()
    {
        return RunSession.IsRunInProgress;
    }

    private bool ActiveRunShowsNumberSelector()
    {
        return RunSession.ActiveRunShowsNumberSelector;
    }

    private string GetDisplayedDeviceIc()
    {
        return RunSession.DisplayedDeviceIc;
    }

    private string GetDisplayedDeviceNumber()
    {
        return RunSession.DisplayedDeviceNumber;
    }

    private string GetDisplayedDeviceContextRefreshSummary()
    {
        return RunSession.DisplayedDeviceContextRefreshSummary;
    }

    private void NotifyRunContextChanged()
    {
        RunSession.NotifyContextChanged();
    }

    private void ResetRunResultForContextChange()
    {
        RunSession.ResetRunResultForContextChange();
    }

    private void PublishLastRunResult(UiRunResultViewModel result)
    {
        RunSession.PublishRunResult(result);
    }

    private string GetSelectedRunMode()
    {
        return IsMergeVisible ? Merge.SelectedMergeMode : Replace.SelectedReplaceMode;
    }

    private void RunSession_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RunSession));
        if (e.PropertyName == nameof(CompositionRunPresentationViewModel.IsRunInProgress))
        {
            NotifyShellRunStateChanged();
        }
    }

    private void NotifyShellRunStateChanged()
    {
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        WorkflowSession.NotifyRunStateChanged();
    }
}
