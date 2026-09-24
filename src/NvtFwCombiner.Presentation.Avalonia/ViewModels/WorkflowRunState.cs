using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Run presentation owned by one page instance and one mode, independent of navigation.</summary>
internal sealed class WorkflowRunState : ObservableObject
{
    private bool _hasResult;

    public UiRunResultViewModel LastRunResult { get; private set; } = new(
        "No run yet", "Drop required BIN files, then run Build.", "No output", succeeded: true);

    internal Guid? ActiveAttemptId { get; set; }
    internal CompositionRunContext? CompletedContext { get; private set; }

    internal void Publish(UiRunResultViewModel result, CompositionRunContext? completedContext = null)
    {
        _hasResult = true;
        LastRunResult = result;
        CompletedContext = completedContext;
        OnPropertyChanged(nameof(LastRunResult));
    }

    internal void ApplyLanguage(ShellTextResources text)
    {
        if (_hasResult || string.Equals(LastRunResult.Title, text.InitialRunTitle, StringComparison.Ordinal))
        {
            return;
        }

        LastRunResult = new(text.InitialRunTitle, text.InitialRunDetail, text.NoOutputLabel, succeeded: true);
        OnPropertyChanged(nameof(LastRunResult));
    }
}
