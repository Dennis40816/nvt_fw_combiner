// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string SettingsOverviewTitle { get; private init; } = string.Empty;

    public string SettingsOverviewSubtitle { get; private init; } = string.Empty;

    public string SettingsCapabilitiesTitle { get; private init; } = string.Empty;

    public string SettingsCapabilitiesSubtitle { get; private init; } = string.Empty;

    public string SettingsPreferencesTitle { get; private init; } = string.Empty;

    public string SettingsPreferencesSubtitle { get; private init; } = string.Empty;

    public string ThemeLabel { get; private init; } = string.Empty;

    public string LanguageLabel { get; private init; } = string.Empty;

    public string ReducedMotionLabel { get; private init; } = string.Empty;

    public string ReducedMotionDescription { get; private init; } = string.Empty;

    public string SupportMatrixOpenLabel { get; private init; } = string.Empty;

    public string SupportMatrixTitle { get; private init; } = string.Empty;

    public string SupportMatrixSubtitle { get; private init; } = string.Empty;

    public string SupportMatrixBackLabel { get; private init; } = string.Empty;

    public string SupportMatrixIcLabel { get; private init; } = string.Empty;

    public string SupportMatrixWorkflowLabel { get; private init; } = string.Empty;

    public string SupportMatrixIcCountLabel { get; private init; } = string.Empty;

    public string SupportMatrixMapVariantLabel { get; private init; } = string.Empty;

    public string SupportMatrixAuthoringLabel { get; private init; } = string.Empty;

    public string SupportMatrixExecutionLabel { get; private init; } = string.Empty;

    public string SupportMatrixPublicationLabel { get; private init; } = string.Empty;

    public string SupportMatrixEvidenceLabel { get; private init; } = string.Empty;

    public string SupportMatrixBlockerLabel { get; private init; } = string.Empty;

    public string SupportMatrixCatalogVersionLabel { get; private init; } = string.Empty;

    public string SupportMatrixSourceHashLabel { get; private init; } = string.Empty;

    public string SupportMatrixResolutionTokenLabel { get; private init; } = string.Empty;

    public string SupportMatrixFingerprintLabel { get; private init; } = string.Empty;

    public string SupportMatrixNoBlockerLabel { get; private init; } = string.Empty;

    public string SupportMatrixLegendTitle { get; private init; } = string.Empty;

    public string SupportMatrixReviewedEvidenceLabel { get; private init; } = string.Empty;

    public string SupportMatrixContractOnlyLabel { get; private init; } = string.Empty;

    public string SupportMatrixReviewRequiredLabel { get; private init; } = string.Empty;

    public string SupportMatrixBlockedLabel { get; private init; } = string.Empty;

    public string SupportMatrixNotDeclaredLabel { get; private init; } = string.Empty;

    public string SupportMatrixHoverHint { get; private init; } = string.Empty;

    public string SupportMatrixLoadingTitle { get; private init; } = string.Empty;

    public string SupportMatrixLoadingDetail { get; private init; } = string.Empty;

    public string SupportMatrixEmptyTitle { get; private init; } = string.Empty;

    public string SupportMatrixEmptyDetail { get; private init; } = string.Empty;

    public string SupportMatrixColdStartTitle { get; private init; } = string.Empty;

    public string SupportMatrixColdStartDetail { get; private init; } = string.Empty;

    public string SupportMatrixLastKnownGoodTitle { get; private init; } = string.Empty;

    public string SupportMatrixLastKnownGoodDetail { get; private init; } = string.Empty;

    public string NotAvailableLabel { get; private init; } = string.Empty;

    public string FormatSupportMatrixRouteCount(int count)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? $"{count} 條路徑"
            : $"{count} {(count == 1 ? "route" : "routes")}";
    }

    public string SupportMatrixCatalogStateValue(
        CanonicalSupportMatrixCatalogState state)
    {
        return state switch
        {
            CanonicalSupportMatrixCatalogState.Loading =>
                Language == ShellLanguage.ChineseTraditional ? "載入中" : "Loading",
            CanonicalSupportMatrixCatalogState.Current =>
                Language == ShellLanguage.ChineseTraditional ? "目前版本" : "Current",
            CanonicalSupportMatrixCatalogState.LastKnownGood =>
                Language == ShellLanguage.ChineseTraditional ? "最後可用版本" : "Last known good",
            CanonicalSupportMatrixCatalogState.ColdStartBlocked =>
                Language == ShellLanguage.ChineseTraditional ? "啟動受阻" : "Cold start blocked",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
    }

    public string SupportMatrixAuthoringValue(
        CapabilityAuthoringAvailability value)
    {
        return value switch
        {
            CapabilityAuthoringAvailability.Available =>
                Language == ShellLanguage.ChineseTraditional ? "可用" : "Available",
            CapabilityAuthoringAvailability.Unavailable =>
                Language == ShellLanguage.ChineseTraditional ? "不可用" : "Unavailable",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public string SupportMatrixPublicationValue(CapabilityPublicationStatus value)
    {
        return value switch
        {
            CapabilityPublicationStatus.Supported =>
                Language == ShellLanguage.ChineseTraditional ? "已支援" : "Supported",
            CapabilityPublicationStatus.Candidate =>
                Language == ShellLanguage.ChineseTraditional ? "候選" : "Candidate",
            CapabilityPublicationStatus.Internal =>
                Language == ShellLanguage.ChineseTraditional ? "內部" : "Internal",
            CapabilityPublicationStatus.TestOnly =>
                Language == ShellLanguage.ChineseTraditional ? "僅測試" : "Test only",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public string SupportMatrixExecutionValue(CanonicalSupportMatrixExecutionState value)
    {
        return value switch
        {
            CanonicalSupportMatrixExecutionState.Admitted =>
                Language == ShellLanguage.ChineseTraditional ? "已准入" : "Admitted",
            CanonicalSupportMatrixExecutionState.RequiresAuthoringCompilation =>
                Language == ShellLanguage.ChineseTraditional
                    ? "需依 authoring 編譯"
                    : "Requires authoring compilation",
            CanonicalSupportMatrixExecutionState.Unavailable =>
                Language == ShellLanguage.ChineseTraditional ? "不可用" : "Unavailable",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public string SupportMatrixEvidenceValue(CapabilityEvidenceStatus value)
    {
        return value switch
        {
            CapabilityEvidenceStatus.DirectGolden => "Direct golden",
            CapabilityEvidenceStatus.ApprovedAlias =>
                Language == ShellLanguage.ChineseTraditional ? "已核准別名" : "Approved alias",
            CapabilityEvidenceStatus.SyntheticOracle =>
                Language == ShellLanguage.ChineseTraditional ? "合成 oracle" : "Synthetic oracle",
            CapabilityEvidenceStatus.ContractOnly =>
                Language == ShellLanguage.ChineseTraditional ? "僅契約" : "Contract only",
            CapabilityEvidenceStatus.Missing =>
                Language == ShellLanguage.ChineseTraditional ? "缺少" : "Missing",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public string SupportMatrixBlockerValue(
        CanonicalSupportMatrixBlockerKind value)
    {
        return value switch
        {
            CanonicalSupportMatrixBlockerKind.AuthoringUnavailable =>
                Language == ShellLanguage.ChineseTraditional
                    ? "Authoring 不可用"
                    : "Authoring unavailable",
            CanonicalSupportMatrixBlockerKind.ExecutionUnavailable =>
                Language == ShellLanguage.ChineseTraditional
                    ? "Execution 不可用"
                    : "Execution unavailable",
            CanonicalSupportMatrixBlockerKind.CertificationInconsistency =>
                Language == ShellLanguage.ChineseTraditional
                    ? "Certification 不一致"
                    : "Certification inconsistency",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public string SupportMatrixCellStatusValue(SupportMatrixCellStatus value)
    {
        return value switch
        {
            SupportMatrixCellStatus.ReviewedEvidence => SupportMatrixReviewedEvidenceLabel,
            SupportMatrixCellStatus.ContractOnly => SupportMatrixContractOnlyLabel,
            SupportMatrixCellStatus.ReviewRequired => SupportMatrixReviewRequiredLabel,
            SupportMatrixCellStatus.Blocked => SupportMatrixBlockedLabel,
            SupportMatrixCellStatus.NotDeclared => SupportMatrixNotDeclaredLabel,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public static string SupportMatrixWorkflowValue(string workflowId)
    {
        return workflowId switch
        {
            ExperienceIds.StandardMerge => "Standard Merge",
            ExperienceIds.AbMerge => "AB Merge",
            ExperienceIds.GeneralMerge => "General Merge",
            ExperienceIds.DpReplace => "DP Replace",
            ExperienceIds.CtrlRamReplace => "CtrlRAM Replace",
            ExperienceIds.GeneralReplace => "General Replace",
            _ => workflowId,
        };
    }

    public static string SupportMatrixIcCountValue(string icCountVariant)
    {
        return icCountVariant switch
        {
            "selector-free" or "not-applicable" => "—",
            "1-ic" => "1 IC",
            "2-ic" => "2 IC",
            "3-ic" => "3 IC",
            "2-8-ic" => "2–8 IC",
            "2-plus-ic" => "2+ IC",
            _ => icCountVariant,
        };
    }
}

#pragma warning restore CS1591
