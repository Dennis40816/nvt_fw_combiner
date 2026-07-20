namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Loads page-specific shell projections once without weakening their source contracts.</summary>
internal sealed class DeferredShellState
{
    internal bool IsSettingsLoaded { get; private set; }

    internal bool IsWorkflowLoaded { get; private set; }

    internal bool IsLoadingWorkflow { get; private set; }

    internal void EnsureSettings(Action load)
    {
        if (IsSettingsLoaded)
        {
            return;
        }

        load();
        IsSettingsLoaded = true;
    }

    internal void EnsurePage(ShellPage page, Action loadSettings, Action loadWorkflow)
    {
        if (page == ShellPage.Settings)
        {
            EnsureSettings(loadSettings);
        }
        else if (page is ShellPage.Merge or ShellPage.Replace && !IsWorkflowLoaded)
        {
            loadWorkflow();
        }
    }

    internal void EnsureWorkflow(
        Action loadNumberChoices,
        Func<string> loadGeneralMergeOutputLength,
        Action<string> applyGeneralMergeOutputLength)
    {
        if (IsWorkflowLoaded)
        {
            return;
        }

        IsLoadingWorkflow = true;
        try
        {
            loadNumberChoices();
            applyGeneralMergeOutputLength(loadGeneralMergeOutputLength());
            IsWorkflowLoaded = true;
        }
        finally
        {
            IsLoadingWorkflow = false;
        }
    }

    internal void RefreshLoaded(
        Action refreshSettings,
        Action refreshWorkflow,
        Action refreshWorkflowInspection,
        Action refreshWorkflowSelection)
    {
        if (IsSettingsLoaded)
        {
            refreshSettings();
        }

        if (IsWorkflowLoaded)
        {
            refreshWorkflow();
            refreshWorkflowInspection();
            refreshWorkflowSelection();
        }
    }
}
