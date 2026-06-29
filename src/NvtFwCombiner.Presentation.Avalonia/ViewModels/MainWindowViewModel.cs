namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>View model for the 0.1.1 planning shell.</summary>
public sealed class MainWindowViewModel
{
    /// <summary>Initializes the planning shell view model.</summary>
    public MainWindowViewModel(
        string shellVersion,
        string workspaceTitle,
        string workspaceSummary,
        string previewActionLabel,
        string buildActionLabel,
        IReadOnlyList<NavigationItemViewModel> navigationItems,
        PlanningCardViewModel mergePreview,
        PlanningCardViewModel replacePreview,
        PlanningCardViewModel savedRulesAndReports,
        PlanningCardViewModel diagnostics,
        string footerStatus)
    {
        ShellVersion = shellVersion;
        WorkspaceTitle = workspaceTitle;
        WorkspaceSummary = workspaceSummary;
        PreviewActionLabel = previewActionLabel;
        BuildActionLabel = buildActionLabel;
        NavigationItems = navigationItems;
        MergePreview = mergePreview;
        ReplacePreview = replacePreview;
        SavedRulesAndReports = savedRulesAndReports;
        Diagnostics = diagnostics;
        FooterStatus = footerStatus;
    }

    /// <summary>Gets the shell milestone label.</summary>
    public string ShellVersion { get; }

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; }

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; }

    /// <summary>Gets the preview action label.</summary>
    public string PreviewActionLabel { get; }

    /// <summary>Gets the build action label.</summary>
    public string BuildActionLabel { get; }

    /// <summary>Gets the sidebar navigation items.</summary>
    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    /// <summary>Gets merge preview sample content.</summary>
    public PlanningCardViewModel MergePreview { get; }

    /// <summary>Gets replace preview sample content.</summary>
    public PlanningCardViewModel ReplacePreview { get; }

    /// <summary>Gets saved rules and reports sample content.</summary>
    public PlanningCardViewModel SavedRulesAndReports { get; }

    /// <summary>Gets diagnostics sample content.</summary>
    public PlanningCardViewModel Diagnostics { get; }

    /// <summary>Gets footer status content.</summary>
    public string FooterStatus { get; }
}
