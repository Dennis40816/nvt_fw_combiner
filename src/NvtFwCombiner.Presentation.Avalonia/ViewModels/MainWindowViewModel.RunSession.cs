namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
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

    private bool IsCompositionRunInProgress()
    {
        return RunSession.IsRunInProgress;
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
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(IsDeviceContextSelectionVisible));
        OnPropertyChanged(nameof(IsDeviceContextNumberSelectionVisible));
        OnPropertyChanged(nameof(IsDeviceContextFamilyBadgeVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
    }
}
