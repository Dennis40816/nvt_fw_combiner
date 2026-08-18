namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private void ApplyTextResources(ShellLanguage language, bool notify = true)
    {
        Text = ShellTextResources.For(language);
        WorkspaceTitle = Text.WorkspaceTitle;
        WorkspaceSummary = Text.WorkspaceSummary;
        SettingsPreview = Text.SettingsPreview;
        if (WorkflowSession.IsCanonicalCatalogReady)
        {
            ApplyCatalogBackedTextResources();
        }

        LoadedHexEditorWorkspace?.ApplyTextResources(Text);
        RunSession.ApplyLanguageChanged(language);
        MessageCenter.ApplyLanguageChanged();

        if (!notify)
        {
            return;
        }

        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(SettingsPreview));
        OnPropertyChanged(nameof(Merge.MergePreview));
        OnPropertyChanged(nameof(Merge.MergeMemorySummary));
        OnPropertyChanged(nameof(Merge.StandardMergeSupportSummary));
        OnPropertyChanged(nameof(Merge.MergeReadinessStatus));
        RefreshNavigationTrail();
        OnPropertyChanged(nameof(NavigationPath));
        OnPropertyChanged(nameof(NavigationClearRoute));
        Reports.ApplyLanguageChanged();
        if (_deferredState.IsSettingsLoaded)
        {
            RefreshSettingsState();
        }

    }

    private void ApplyCatalogBackedTextResources()
    {
        WorkflowSession.ApplyLanguageChanged();
        Merge.ApplyLanguageChanged();
        Replace.ApplyLanguageChanged();
        Replace.NotifyCommandStateChanged();
    }
}
