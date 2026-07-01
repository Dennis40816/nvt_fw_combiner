namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Creates the production-backed UI view model.</summary>
public static class ShellViewModelFactory
{
    /// <summary>Creates the main window view model.</summary>
    /// <param name="language">Requested shell text language.</param>
    /// <returns>A populated main window view model.</returns>
    public static MainWindowViewModel Create(ShellLanguage language = ShellLanguage.English)
    {
        var text = ShellTextResources.For(language);

        return new MainWindowViewModel(
            text.ShellVersion,
            text.WorkspaceTitle,
            text.WorkspaceSummary,
            text.PreviewActionLabel,
            text.BuildActionLabel,
            text.ReportModalActionLabel,
            text.DeviceContextTitle,
            text.IcLabel,
            text.NumberLabel,
            text.DeviceContextStatus,
            CreatePlanningCard(text.SettingsPreview),
            CreatePlanningCard(text.MergePreview),
            CreatePlanningCard(text.ReplacePreview),
            text.FooterStatus);
    }

    private static PlanningCardViewModel CreatePlanningCard(PlanningCardText text)
    {
        return new PlanningCardViewModel(
            text.Title,
            text.Subtitle,
            text.Rows,
            text.Status);
    }
}
