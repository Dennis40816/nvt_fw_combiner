using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact settings status row.</summary>
public sealed class SettingSummaryViewModel
{
    /// <summary>Initializes a settings status row.</summary>
    public SettingSummaryViewModel(
        string title,
        string value,
        string description,
        string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Title = title;
        Value = value;
        Description = description;
        Status = status;
    }

    /// <summary>Gets the row title.</summary>
    public string Title { get; }

    /// <summary>Gets the primary value.</summary>
    public string Value { get; }

    /// <summary>Gets the short supporting description.</summary>
    public string Description { get; }

    /// <summary>Gets the row status.</summary>
    public string Status { get; }
}

/// <summary>One read-only exact-route Support Matrix row for the Settings reporting surface.</summary>
public sealed class SupportMatrixSettingsRowViewModel
{
    /// <summary>Projects one already-materialized matrix row without creating support or execution facts.</summary>
    public SupportMatrixSettingsRowViewModel(SupportMatrixRow row, ShellLanguage language)
    {
        ArgumentNullException.ThrowIfNull(row);

        RouteId = row.Route.RouteId;
        IcId = row.Route.IcId;
        WorkflowId = row.Route.WorkflowId;
        IcCountVariant = row.Route.IcCountVariant;
        MapVariant = row.Route.MapVariant;
        (AuthoringMark, AuthoringLabel, AuthoringBadgeClasses) = AuthoringPresentation(
            row.Route.AuthoringAvailability,
            language);
        (ExecutionMark, ExecutionLabel, ExecutionBadgeClasses) = ExecutionPresentation(
            row.Route.ExecutionAdmitted,
            language);
        (PublicationLabel, PublicationBadgeClasses) = PublicationPresentation(
            row.PublicationStatus,
            language);
        (EvidenceLabel, EvidenceBadgeClasses) = EvidencePresentation(row.Evidence.Status, language);
        PublicationTooltip = PublicationTooltipFor(row, language);
        EvidenceTooltip = EvidenceTooltipFor(row, language);
        TraceabilityHelpText = language == ShellLanguage.ChineseTraditional
            ? $"發布 provenance：{PublicationTooltip}。證據 provenance：{EvidenceTooltip}。"
            : $"Publication provenance: {PublicationTooltip}. Evidence provenance: {EvidenceTooltip}.";
        AccessibleName = language == ShellLanguage.ChineseTraditional
            ? $"{IcId}、{WorkflowId}、IC 數 {IcCountVariant}、地圖 {MapVariant}；建立：{AuthoringLabel}；執行：{ExecutionLabel}；發布：{PublicationLabel}；證據：{EvidenceLabel}。"
            : $"{IcId}, {WorkflowId}, IC count {IcCountVariant}, map {MapVariant}; authoring {AuthoringLabel}; execution {ExecutionLabel}; publication {PublicationLabel}; evidence {EvidenceLabel}.";
    }

    /// <summary>Stable exact route identifier for diagnostics and traceability.</summary>
    public string RouteId { get; }

    /// <summary>Canonical IC identifier.</summary>
    public string IcId { get; }

    /// <summary>Canonical workflow identifier.</summary>
    public string WorkflowId { get; }

    /// <summary>Canonical IC Count applicability token.</summary>
    public string IcCountVariant { get; }

    /// <summary>Canonical map variant token.</summary>
    public string MapVariant { get; }

    /// <summary>Compact authoring status mark.</summary>
    public string AuthoringMark { get; }

    /// <summary>Accessible, localized authoring status.</summary>
    public string AuthoringLabel { get; }

    /// <summary>Shared badge classes for authoring status.</summary>
    public string AuthoringBadgeClasses { get; }

    /// <summary>Whether authoring uses the shared success treatment.</summary>
    public bool IsAuthoringSuccess => AuthoringBadgeClasses == "reportBadge success";

    /// <summary>Whether authoring uses the shared review treatment.</summary>
    public bool IsAuthoringReview => AuthoringBadgeClasses == "reportBadge review";

    /// <summary>Whether authoring uses the shared informational treatment.</summary>
    public bool IsAuthoringInfo => AuthoringBadgeClasses == "reportBadge info";

    /// <summary>Compact execution status mark.</summary>
    public string ExecutionMark { get; }

    /// <summary>Accessible, localized execution status.</summary>
    public string ExecutionLabel { get; }

    /// <summary>Shared badge classes for execution status.</summary>
    public string ExecutionBadgeClasses { get; }

    /// <summary>Whether execution uses the shared success treatment.</summary>
    public bool IsExecutionSuccess => ExecutionBadgeClasses == "reportBadge success";

    /// <summary>Whether execution uses the shared review treatment.</summary>
    public bool IsExecutionReview => ExecutionBadgeClasses == "reportBadge review";

    /// <summary>Whether execution uses the shared informational treatment.</summary>
    public bool IsExecutionInfo => ExecutionBadgeClasses == "reportBadge info";

    /// <summary>Localized publication status.</summary>
    public string PublicationLabel { get; }

    /// <summary>Shared badge classes for publication status.</summary>
    public string PublicationBadgeClasses { get; }

    /// <summary>Whether publication uses the shared success treatment.</summary>
    public bool IsPublicationSuccess => PublicationBadgeClasses == "reportBadge success";

    /// <summary>Whether publication uses the shared review treatment.</summary>
    public bool IsPublicationReview => PublicationBadgeClasses == "reportBadge review";

    /// <summary>Whether publication uses the shared informational treatment.</summary>
    public bool IsPublicationInfo => PublicationBadgeClasses == "reportBadge info";

    /// <summary>Localized evidence status.</summary>
    public string EvidenceLabel { get; }

    /// <summary>Shared badge classes for evidence status.</summary>
    public string EvidenceBadgeClasses { get; }

    /// <summary>Whether evidence uses the shared success treatment.</summary>
    public bool IsEvidenceSuccess => EvidenceBadgeClasses == "reportBadge success";

    /// <summary>Whether evidence uses the shared review treatment.</summary>
    public bool IsEvidenceReview => EvidenceBadgeClasses == "reportBadge review";

    /// <summary>Whether evidence uses the shared informational treatment.</summary>
    public bool IsEvidenceInfo => EvidenceBadgeClasses == "reportBadge info";

    /// <summary>Traceable publication-decision detail shown on hover.</summary>
    public string PublicationTooltip { get; }

    /// <summary>Traceable evidence-resolution detail shown on hover.</summary>
    public string EvidenceTooltip { get; }

    /// <summary>Publication and evidence provenance exposed to assistive technology.</summary>
    public string TraceabilityHelpText { get; }

    /// <summary>Complete row text for assistive technology.</summary>
    public string AccessibleName { get; }

    private static (string Mark, string Label, string BadgeClasses) AuthoringPresentation(
        SupportAuthoringAvailability availability,
        ShellLanguage language)
    {
        return availability switch
        {
            SupportAuthoringAvailability.Available => (
                "●",
                language == ShellLanguage.ChineseTraditional ? "可建立" : "Available",
                "reportBadge success"),
            SupportAuthoringAvailability.Unavailable => (
                "×",
                language == ShellLanguage.ChineseTraditional ? "不可建立" : "Unavailable",
                "reportBadge review"),
            SupportAuthoringAvailability.Unknown => (
                "?",
                language == ShellLanguage.ChineseTraditional ? "未解析" : "Unresolved",
                "reportBadge review"),
            _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null),
        };
    }

    private static (string Mark, string Label, string BadgeClasses) ExecutionPresentation(
        bool executionAdmitted,
        ShellLanguage language)
    {
        return executionAdmitted
            ? (
                "●",
                language == ShellLanguage.ChineseTraditional ? "已允許" : "Admitted",
                "reportBadge success")
            : (
                "×",
                language == ShellLanguage.ChineseTraditional ? "未允許" : "Not admitted",
                "reportBadge review");
    }

    private static (string Label, string BadgeClasses) PublicationPresentation(
        SupportPublicationStatus status,
        ShellLanguage language)
    {
        return status switch
        {
            SupportPublicationStatus.Supported => (
                language == ShellLanguage.ChineseTraditional ? "已支援" : "Supported",
                "reportBadge success"),
            SupportPublicationStatus.Candidate => (
                language == ShellLanguage.ChineseTraditional ? "候選" : "Candidate",
                "reportBadge review"),
            SupportPublicationStatus.Internal => (
                language == ShellLanguage.ChineseTraditional ? "內部" : "Internal",
                "reportBadge info"),
            SupportPublicationStatus.TestOnly => (
                language == ShellLanguage.ChineseTraditional ? "僅測試" : "Test only",
                "reportBadge info"),
            SupportPublicationStatus.Unclassified => (
                language == ShellLanguage.ChineseTraditional ? "未分類" : "Unclassified",
                "reportBadge review"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    private static (string Label, string BadgeClasses) EvidencePresentation(
        SupportEvidenceStatus status,
        ShellLanguage language)
    {
        return status switch
        {
            SupportEvidenceStatus.DirectGolden => (
                language == ShellLanguage.ChineseTraditional ? "直接 Golden" : "Direct golden",
                "reportBadge success"),
            SupportEvidenceStatus.ApprovedAlias => (
                language == ShellLanguage.ChineseTraditional ? "核准別名" : "Approved alias",
                "reportBadge success"),
            SupportEvidenceStatus.SyntheticOracle => (
                language == ShellLanguage.ChineseTraditional ? "合成 Oracle" : "Synthetic oracle",
                "reportBadge info"),
            SupportEvidenceStatus.ContractOnly => (
                language == ShellLanguage.ChineseTraditional ? "僅契約" : "Contract only",
                "reportBadge info"),
            SupportEvidenceStatus.Missing => (
                language == ShellLanguage.ChineseTraditional ? "缺少" : "Missing",
                "reportBadge review"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    private static string PublicationTooltipFor(SupportMatrixRow row, ShellLanguage language)
    {
        SupportPublicationDecision? decision = row.PublicationDecision;
        return decision is null
            ? language == ShellLanguage.ChineseTraditional
                ? "沒有此 exact route 的 owner publication decision；此狀態不會改變執行權限。"
                : "No owner publication decision exists for this exact route; this status does not change execution admission."
            : language == ShellLanguage.ChineseTraditional
                ? $"決策 {decision.DecisionId}；來源 {decision.Provenance.RecordRef}；{decision.Provenance.Rationale}"
                : $"Decision {decision.DecisionId}; source {decision.Provenance.RecordRef}; {decision.Provenance.Rationale}";
    }

    private static string EvidenceTooltipFor(SupportMatrixRow row, ShellLanguage language)
    {
        SupportEvidenceResolution evidence = row.Evidence;
        return evidence.SourceDeclarationId is null
            ? language == ShellLanguage.ChineseTraditional
                ? "沒有符合此 exact route 的 evidence declaration。"
                : "No evidence declaration applies to this exact route."
            : language == ShellLanguage.ChineseTraditional
                ? $"來源 {evidence.SourceDeclarationId}" +
                    (evidence.TargetRouteId is null ? string.Empty : $"；目標 {evidence.TargetRouteId}") +
                    (evidence.FactScopeId is null ? string.Empty : $"；事實範圍 {evidence.FactScopeId}")
                : $"Source {evidence.SourceDeclarationId}" +
                    (evidence.TargetRouteId is null ? string.Empty : $"; target {evidence.TargetRouteId}") +
                    (evidence.FactScopeId is null ? string.Empty : $"; fact scope {evidence.FactScopeId}");
    }
}
