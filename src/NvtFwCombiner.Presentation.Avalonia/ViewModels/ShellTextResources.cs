// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Supported shell text languages.</summary>
public enum ShellLanguage
{
    /// <summary>English UI text.</summary>
    English,

    /// <summary>Traditional Chinese UI text.</summary>
    ChineseTraditional,
}

/// <summary>Localized text bundle for the production-backed UI shell.</summary>
public sealed partial class ShellTextResources
{
    private static readonly PlanningCardText EmptyPlanningCard = new(string.Empty, string.Empty, [], string.Empty);

    private ShellTextResources()
    {
    }

    /// <summary>Gets the resource bundle for a language.</summary>
    public static ShellTextResources For(ShellLanguage language)
    {
        return language switch
        {
            ShellLanguage.English => CreateEnglish(),
            ShellLanguage.ChineseTraditional => CreateChineseTraditional(),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };
    }

    /// <summary>Converts the persisted language preference into a resource language.</summary>
    public static ShellLanguage LanguageFromPreference(string? preference)
    {
        return string.Equals(preference, "Traditional Chinese", StringComparison.Ordinal)
            ? ShellLanguage.ChineseTraditional
            : ShellLanguage.English;
    }

    /// <summary>Gets the persisted language preference token for a resource language.</summary>
    public static string PreferenceFromLanguage(ShellLanguage language)
    {
        return language == ShellLanguage.ChineseTraditional ? "Traditional Chinese" : "English";
    }

    /// <summary>Gets the language represented by this bundle.</summary>
    public ShellLanguage Language { get; private init; }

    /// <summary>Gets the product title.</summary>
    public string ProductTitle { get; private init; } = "NVT FW Combiner";

    /// <summary>Gets the Home navigation label.</summary>
    public string HomeLabel { get; private init; } = string.Empty;

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; private init; } = string.Empty;

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; private init; } = string.Empty;

    /// <summary>Gets the preview action label.</summary>
    public string PreviewActionLabel { get; private init; } = string.Empty;

    /// <summary>Gets the build action label.</summary>
    public string BuildActionLabel { get; private init; } = string.Empty;

    /// <summary>Gets the report modal action label.</summary>
    public string ReportModalActionLabel { get; private init; } = string.Empty;

    /// <summary>Gets the shared device context heading.</summary>
    public string DeviceContextTitle { get; private init; } = string.Empty;

    /// <summary>Gets the IC field label.</summary>
    public string IcLabel { get; private init; } = string.Empty;

    /// <summary>Gets the IC count/variant field label.</summary>
    public string NumberLabel { get; private init; } = string.Empty;

    /// <summary>Gets the shared device context status text.</summary>
    public string DeviceContextStatus { get; private init; } = string.Empty;

    /// <summary>Gets settings preview text.</summary>
    public PlanningCardText SettingsPreview { get; private init; } = EmptyPlanningCard;

    /// <summary>Gets merge preview text.</summary>
    public PlanningCardText MergePreview { get; private init; } = EmptyPlanningCard;

    /// <summary>Gets replace preview text.</summary>
    public PlanningCardText ReplacePreview { get; private init; } = EmptyPlanningCard;

    /// <summary>Gets footer status text.</summary>
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

    public string ReviewReplacementInputsTooltip { get; private init; } = string.Empty;

    public string InputFilesTitle { get; private init; } = string.Empty;

    public string OutputLayoutTitle { get; private init; } = string.Empty;

    public string PlanTitle { get; private init; } = string.Empty;

    public string ValidationTitle { get; private init; } = string.Empty;

    public string GeneralReplaceMappingTitle { get; private init; } = string.Empty;

    public string GeneralMergeMappingTitle { get; private init; } = string.Empty;

    public string ExplicitMappingsTitle { get; private init; } = string.Empty;

    public string AddRangeLabel { get; private init; } = string.Empty;

    public string AddMappingLabel { get; private init; } = string.Empty;

    public string StartLabel { get; private init; } = string.Empty;

    public string EndLabel { get; private init; } = string.Empty;

    public string SourceStartLabel { get; private init; } = string.Empty;

    public string TargetStartLabel { get; private init; } = string.Empty;

    public string LengthLabel { get; private init; } = string.Empty;

    public string SourceBinLabel { get; private init; } = string.Empty;

    public string ReplacementBinLabel { get; private init; } = string.Empty;

    public string OutputLengthLabel { get; private init; } = string.Empty;

    public string OutputLengthPlaceholder { get; private init; } = string.Empty;

    public string BrowseLabel { get; private init; } = string.Empty;

    public string RequiredLabel { get; private init; } = string.Empty;

    public string OptionalLabel { get; private init; } = string.Empty;

    public string NoBinSelectedLabel { get; private init; } = string.Empty;

    public string MergeDpSlotDescription { get; private init; } = string.Empty;

    public string MergeTpSlotDescription { get; private init; } = string.Empty;

    public string MergeLdSlotDescription { get; private init; } = string.Empty;

    public string BaseFlashBinTitle { get; private init; } = string.Empty;

    public string BaseFlashBinDescription { get; private init; } = string.Empty;

    public string DpReplacementBinTitle { get; private init; } = string.Empty;

    public string DpReplacementBinDescription { get; private init; } = string.Empty;

    public string TpReplacementBinTitle { get; private init; } = string.Empty;

    public string TpReplacementBinDescription { get; private init; } = string.Empty;

    public string LdReplacementBinTitle { get; private init; } = string.Empty;

    public string LdReplacementBinDescription { get; private init; } = string.Empty;

    public string CtrlRamReplacementBinDescription { get; private init; } = string.Empty;

    public string SelectReplacementBinTooltip { get; private init; } = string.Empty;

    public string SelectSourceBinTooltip { get; private init; } = string.Empty;

    public string RemoveRangeTooltip { get; private init; } = string.Empty;

    public string RemoveMappingTooltip { get; private init; } = string.Empty;

    public string GeneralReplaceRuleBaseTitle { get; private init; } = string.Empty;

    public string GeneralReplaceRuleBaseDetail { get; private init; } = string.Empty;

    public string GeneralReplaceRuleBoundsTitle { get; private init; } = string.Empty;

    public string GeneralReplaceRuleBoundsDetail { get; private init; } = string.Empty;

    public string GeneralReplaceRuleLengthTitle { get; private init; } = string.Empty;

    public string GeneralReplaceRuleLengthDetail { get; private init; } = string.Empty;

    public string GeneralReplaceValidationDetail { get; private init; } = string.Empty;

    public string GeneralReplaceMappingsDetail { get; private init; } = string.Empty;

    public string GeneralMergeMappingDetail { get; private init; } = string.Empty;

    public string GeneralMergeMappingsDetail { get; private init; } = string.Empty;

    public string CtrlRamInputFilesDetail { get; private init; } = string.Empty;

    public string AbCodeMergeTitle { get; private init; } = string.Empty;

    public string AbCodeMergeDetail { get; private init; } = string.Empty;

    public string SettingsCatalogTitle { get; private init; } = string.Empty;

    public string SettingsCatalogSubtitle { get; private init; } = string.Empty;

    public string SettingsRuntimeChecksTitle { get; private init; } = string.Empty;

    public string SettingsRuntimeChecksSubtitle { get; private init; } = string.Empty;

    public string SettingsDiagnosticsTitle { get; private init; } = string.Empty;

    public string SettingsDiagnosticsSubtitle { get; private init; } = string.Empty;

    public string SettingsPreferencesTitle { get; private init; } = string.Empty;

    public string SettingsPreferencesSubtitle { get; private init; } = string.Empty;

    public string ThemeLabel { get; private init; } = string.Empty;

    public string StrictnessLabel { get; private init; } = string.Empty;

    public string LanguageLabel { get; private init; } = string.Empty;

    public string SettingsInspectorKicker { get; private init; } = string.Empty;

    public string SettingsReadinessTitle { get; private init; } = string.Empty;

    public string ReportToastTitle { get; private init; } = string.Empty;

    public string ReplaceSelectionTitle { get; private init; } = string.Empty;

    public string CloseSelectionTooltip { get; private init; } = string.Empty;

    public string SelectedReplacementsTitle { get; private init; } = string.Empty;

    public string RequiredBeforeBuildTitle { get; private init; } = string.Empty;

    public string CloseLabel { get; private init; } = string.Empty;

    public string SaveReportLabel { get; private init; } = string.Empty;

    public string CloseReportTooltip { get; private init; } = string.Empty;

    public string ReportHistoryTitle { get; private init; } = string.Empty;

    public string BackToReportLabel { get; private init; } = string.Empty;

    public string ClearAllLabel { get; private init; } = string.Empty;

    public string ClearHistoryLabel { get; private init; } = string.Empty;

    public string ClearHistoryTooltip { get; private init; } = string.Empty;

    public string NoReportHistoryLabel { get; private init; } = string.Empty;

    public string RunLabel { get; private init; } = string.Empty;

    public string OutputLabel { get; private init; } = string.Empty;

    public string ChangeReviewTitle { get; private init; } = string.Empty;

    public string EvidenceTitle { get; private init; } = string.Empty;

    public string TraceLabel { get; private init; } = string.Empty;

    public string OpenReportHistoryTooltip { get; private init; } = string.Empty;

    public string OpenReportHistoryAutomationName { get; private init; } = string.Empty;

    public string ReportTabInputs { get; private init; } = string.Empty;

    public string ReportTabChanges { get; private init; } = string.Empty;

    public string ReportTabOperations { get; private init; } = string.Empty;

    public string ReportTabPostbuild { get; private init; } = string.Empty;

    public string ReportTabIssues { get; private init; } = string.Empty;

    public string ReportTabRaw { get; private init; } = string.Empty;

    public string RunMetadataTitle { get; private init; } = string.Empty;

    public string ReportFileLabel { get; private init; } = string.Empty;

    public string StatusLabel { get; private init; } = string.Empty;

    public string ArtifactPathLabel { get; private init; } = string.Empty;

    public string InputsAndHashesTitle { get; private init; } = string.Empty;

    public string EmptyInputsMessage { get; private init; } = string.Empty;

    public string EmptyByteChangesMessage { get; private init; } = string.Empty;

    public string OutputChangesTitle { get; private init; } = string.Empty;

    public string DiffLabel { get; private init; } = string.Empty;

    public string RangeLabel { get; private init; } = string.Empty;

    public string ResultLabel { get; private init; } = string.Empty;

    public string DetailLabel { get; private init; } = string.Empty;

    public string ExplanationLabel { get; private init; } = string.Empty;

    public string ReasonLabel { get; private init; } = string.Empty;

    public string ChangedRangesTitle { get; private init; } = string.Empty;

    public string EmptyOperationsMessage { get; private init; } = string.Empty;

    public string OperationStepsTitle { get; private init; } = string.Empty;

    public string StepLabel { get; private init; } = string.Empty;

    public string KindLabel { get; private init; } = string.Empty;

    public string SourceLabel { get; private init; } = string.Empty;

    public string TargetLabel { get; private init; } = string.Empty;

    public string ProcessorLabel { get; private init; } = string.Empty;

    public string EmptyPostbuildMessage { get; private init; } = string.Empty;

    public string HeaderRefreshTraceTitle { get; private init; } = string.Empty;

    public string EmptyIssuesMessage { get; private init; } = string.Empty;

    public string IssuesAndWarningsTitle { get; private init; } = string.Empty;

    public string RangeTableTitle { get; private init; } = string.Empty;

    public string AddressSpaceLabel { get; private init; } = string.Empty;

    public string CommandArgvLabel { get; private init; } = string.Empty;

    public string DeleteReportTooltip { get; private init; } = string.Empty;

    public string MergeModeTooltip { get; private init; } = string.Empty;

}

#pragma warning restore CS1591
