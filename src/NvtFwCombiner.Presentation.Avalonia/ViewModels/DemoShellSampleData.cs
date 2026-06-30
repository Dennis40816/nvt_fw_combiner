namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Provides separated synthetic data for the 0.1.1 planning shell.</summary>
public static class DemoShellSampleData
{
    /// <summary>Creates the sample view model used before application-core wiring.</summary>
    /// <param name="language">Requested demo-shell text language.</param>
    /// <returns>A populated planning shell view model.</returns>
    public static MainWindowViewModel Create(DemoShellLanguage language = DemoShellLanguage.English)
    {
        var text = DemoShellTextResources.For(language);

        return new MainWindowViewModel(
            text.ShellVersion,
            text.WorkspaceTitle,
            text.WorkspaceSummary,
            text.PreviewActionLabel,
            text.BuildActionLabel,
            text.ReportModalActionLabel,
            CreateNavigationItems(text),
            CreatePlanningCard(text.MergePreview),
            CreatePlanningCard(text.ReplacePreview),
            CreatePlanningCard(text.SavedRulesAndReports),
            CreatePlanningCard(text.ReportModalPreview),
            text.FooterStatus);
    }

    private static IReadOnlyList<NavigationItemViewModel> CreateNavigationItems(DemoShellTextResources text)
    {
        return [.. text.NavigationItems.Select(label => new NavigationItemViewModel(label))];
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
