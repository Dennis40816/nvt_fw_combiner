namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Read-only localized explanation; it never decides severity or Build availability.</summary>
internal sealed record IssueCardViewModel(
    string Caption,
    string Subject,
    string Summary,
    string Impact,
    string Action,
    bool IsError,
    string DiagnosticCode = "",
    bool IsBuildStatus = false,
    int AdditionalBlockerCount = 0,
    string AdditionalBlockerText = "")
{
    public bool HasImpact => !string.IsNullOrWhiteSpace(Impact);
    public bool HasAction => !string.IsNullOrWhiteSpace(Action);
    public bool HasDiagnosticCode => !string.IsNullOrWhiteSpace(DiagnosticCode);
    public bool HasAdditionalBlockers => AdditionalBlockerCount > 0;
    public string AdditionalBlockerBadge => $"+{AdditionalBlockerCount}";

    public string AutomationText => string.Join(" ", new[] { Caption, Subject, Impact, Summary, Action, AdditionalBlockerText, DiagnosticCode }
        .Where(static value => !string.IsNullOrWhiteSpace(value)));

    public string IconPath => IsError
        ? "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01"
        : "M12 3L22 20H2L12 3 M12 9V14 M12 17H12.01";

    public string ImpactIconPath => IsError
        ? "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M7 12H17"
        : "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 11V17 M12 7H12.01";
}
