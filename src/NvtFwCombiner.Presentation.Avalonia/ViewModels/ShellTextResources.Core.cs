// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
{
    public ShellLanguage Language { get; private init; }

    public string ProductTitle { get; private init; } = "NVT FW Combiner";

    public string HomeLabel { get; private init; } = string.Empty;

    public string WorkspaceTitle { get; private init; } = string.Empty;

    public string WorkspaceSummary { get; private init; } = string.Empty;

    public string PreviewActionLabel { get; private init; } = string.Empty;

    public string BuildActionLabel { get; private init; } = string.Empty;

    public string ReportModalActionLabel { get; private init; } = string.Empty;

    public string DeviceContextTitle { get; private init; } = string.Empty;

    public string IcLabel { get; private init; } = string.Empty;

    public string NumberLabel { get; private init; } = string.Empty;

    public string DeviceContextStatus { get; private init; } = string.Empty;

    public PlanningCardText SettingsPreview { get; private init; } = EmptyPlanningCard;

    public PlanningCardText MergePreview { get; private init; } = EmptyPlanningCard;

    public PlanningCardText ReplacePreview { get; private init; } = EmptyPlanningCard;

    public string FooterStatus { get; private init; } = string.Empty;

    public string InitialRunTitle { get; private init; } = string.Empty;

    public string InitialRunDetail { get; private init; } = string.Empty;

    public string NoOutputLabel { get; private init; } = string.Empty;

    public string ConfigureKicker { get; private init; } = string.Empty;

    public string WorkflowKicker { get; private init; } = string.Empty;

    public string OpenSettingsLabel { get; private init; } = string.Empty;

    public string OpenLabel { get; private init; } = string.Empty;

    public string PendingLabel { get; private init; } = string.Empty;

    public string LoadJsonLabel { get; private init; } = string.Empty;

    public string LoadJsonTooltip { get; private init; } = string.Empty;

    public string BackTooltip { get; private init; } = string.Empty;

    public string ModeLabel { get; private init; } = string.Empty;

    public string TargetsLabel { get; private init; } = string.Empty;
}

#pragma warning restore CS1591
