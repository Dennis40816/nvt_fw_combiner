using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public MergePresentationViewModel Merge { get; }

    private void Merge_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Merge));
        if (e.PropertyName == nameof(MergePresentationViewModel.SelectedMergeMode))
        {
            RecordDebugActivity(
                SystemActivityCodes.ModeSelected,
                SystemActivityCategory.Workflow,
                Merge.SelectedMergeMode,
                "merge");
        }
        if (e.PropertyName == nameof(MergePresentationViewModel.IsAbSameTpConflictPromptOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }

}
