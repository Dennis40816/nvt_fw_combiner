using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    /// <summary>Focused Preview/Build lifetime and progress presentation.</summary>
    public CompositionRunPresentationViewModel RunSession { get; }

    private Task RunCompositionAsync(
        bool build,
        CompositionRunWork run,
        Action<string, string> loadErrorReport)
    {
        return RunSession.RunCompositionAsync(build, run, loadErrorReport);
    }

    private Task ShowDiagnosticPreviewAsync(CompositionRunReport report)
    {
        return RunSession.ShowDiagnosticPreviewAsync(report);
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
