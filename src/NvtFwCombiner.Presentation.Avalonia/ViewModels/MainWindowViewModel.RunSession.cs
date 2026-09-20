using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    /// <summary>Focused Preview/Build lifetime and progress presentation.</summary>
    public CompositionRunPresentationViewModel RunSession { get; }

    private async Task RunCompositionAsync(
        CompositionRunContext context,
        bool build,
        CompositionRunWork run,
        Action<string, string> loadErrorReport)
    {
        string mode = context.Mode;
        string icId = context.Ic;
        RecordDebugActivity(
            build ? SystemActivityCodes.BuildStarted : SystemActivityCodes.PreviewStarted,
            SystemActivityCategory.Composition,
            mode,
            icId);
        UiRunResultViewModel? result = await RunSession.RunCompositionAsync(context, build, run, loadErrorReport);
        bool succeeded = result?.Succeeded == true;
        RecordSystemActivity(new SystemActivityDraft(
            succeeded
                ? build ? SystemActivityCodes.BuildCompleted : SystemActivityCodes.PreviewCompleted
                : build ? SystemActivityCodes.BuildFailed : SystemActivityCodes.PreviewFailed,
            SystemActivityImportance.Important,
            SystemActivityCategory.Composition,
            succeeded ? SystemActivitySeverity.Success : SystemActivitySeverity.Error,
            mode,
            icId));
    }

    private async Task ShowDiagnosticPreviewAsync(CompositionRunContext context, CompositionRunReport report)
    {
        string mode = context.Mode;
        string icId = context.Ic;
        RecordDebugActivity(
            SystemActivityCodes.PreviewStarted,
            SystemActivityCategory.Composition,
            mode,
            icId);
        await RunSession.ShowDiagnosticPreviewAsync(context, report);
        RecordSystemActivity(new SystemActivityDraft(
            SystemActivityCodes.PreviewFailed,
            SystemActivityImportance.Important,
            SystemActivityCategory.Composition,
            SystemActivitySeverity.Warning,
            mode,
            icId));
    }

    private void ShowActionReadiness(
        CompositionRunContext context,
        CapabilityActionReadinessSnapshot readiness,
        bool build)
    {
        RunSession.ShowActionReadiness(context, readiness, build);
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

    private void ResetRunResultForContextChange(CompositionRunContext context)
    {
        RunSession.ResetRunResultForContextChange(context);
    }

    private void PublishLastRunResult(WorkflowRunState owner, UiRunResultViewModel result)
    {
        if (!RunSession.IsRunInProgress)
        {
            RunSession.PublishRunResult(owner, result);
        }
    }

    private WorkflowRunState GetDisplayedRunOwner()
    {
        return IsMergeVisible ? Merge.RunState : Replace.RunState;
    }

    private void RunSession_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(RunSession)));
        if (e.PropertyName == nameof(CompositionRunPresentationViewModel.IsRunInProgress))
        {
            NotifyShellRunStateChanged();
        }
    }

    private void NotifyShellRunStateChanged()
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IsDeviceContextVisible)));
        WorkflowSession.NotifyRunStateChanged();
    }
}
