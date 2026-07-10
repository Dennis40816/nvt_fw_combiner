// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
{
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

    public string GeneralReplacePatchOverwriteLabel { get; private init; } = string.Empty;

    public string GeneralReplacePatchFillLabel { get; private init; } = string.Empty;

    public string GeneralReplacePatchValueLabel { get; private init; } = string.Empty;

    public string GeneralReplacePatchValuePlaceholder { get; private init; } = string.Empty;

    public string GeneralReplaceApplyPatchLabel { get; private init; } = string.Empty;

    public string GeneralReplaceUndoPatchLabel { get; private init; } = string.Empty;

    public string GeneralReplaceRedoPatchLabel { get; private init; } = string.Empty;

    public string GeneralReplaceCommittedPatchesTitle { get; private init; } = string.Empty;

    public string GeneralReplaceNoPatchesLabel { get; private init; } = string.Empty;

    /// <summary>Visible when the hexadecimal viewport has no base BIN to inspect.</summary>
    public string GeneralReplaceHexViewportNoBaseDetail { get; private init; } = string.Empty;

    /// <summary>Visible when the requested hexadecimal viewport address is invalid.</summary>
    public string GeneralReplaceHexViewportAddressInvalidDetail { get; private init; } = string.Empty;

    /// <summary>Formats base length and the visible hexadecimal address window.</summary>
    public string GeneralReplaceHexViewportReadyDetail { get; private init; } = string.Empty;

    /// <summary>Title for the interactive General Replace hexadecimal editor.</summary>
    public string GeneralReplaceHexEditorTitle { get; private init; } = string.Empty;

    /// <summary>Describes how selection and virtual staged changes work in the hexadecimal editor.</summary>
    public string GeneralReplaceHexEditorDetail { get; private init; } = string.Empty;

    /// <summary>Label for hexadecimal viewport navigation.</summary>
    public string GeneralReplaceHexGoToLabel { get; private init; } = string.Empty;

    /// <summary>Label for the compact General Replace patch inspector.</summary>
    public string GeneralReplaceHexInspectorTitle { get; private init; } = string.Empty;

    /// <summary>Label for profile-authorized range selection in the hexadecimal editor.</summary>
    public string GeneralReplaceHexApprovedRegionLabel { get; private init; } = string.Empty;

    /// <summary>Column label for hexadecimal viewport addresses.</summary>
    public string GeneralReplaceHexAddressColumnLabel { get; private init; } = string.Empty;

    /// <summary>Column label for hexadecimal viewport ASCII rendering.</summary>
    public string GeneralReplaceHexAsciiColumnLabel { get; private init; } = string.Empty;

    /// <summary>Label for the experimental Hex Editor section.</summary>
    public string HexEditorExperimentalLabel { get; private init; } = string.Empty;

    /// <summary>Opens the experimental Hex Editor section.</summary>
    public string HexEditorOpenLabel { get; private init; } = string.Empty;

    /// <summary>Closes the experimental Hex Editor section.</summary>
    public string HexEditorCloseLabel { get; private init; } = string.Empty;

    /// <summary>Build label for the experimental Hex Editor.</summary>
    public string HexEditorBuildLabel { get; private init; } = string.Empty;

    /// <summary>Readiness state when the Hex Editor needs a base BIN.</summary>
    public string HexEditorBaseRequiredDetail { get; private init; } = string.Empty;

    /// <summary>Readiness state when the Hex Editor needs one staged patch.</summary>
    public string HexEditorPatchRequiredDetail { get; private init; } = string.Empty;

    /// <summary>Readiness state when Hex Editor can build a new complete BIN.</summary>
    public string HexEditorReadyDetail { get; private init; } = string.Empty;

    public string GeneralMergeMappingDetail { get; private init; } = string.Empty;

    public string GeneralMergeMappingsDetail { get; private init; } = string.Empty;

    public string CtrlRamInputFilesDetail { get; private init; } = string.Empty;

    public string AbCodeMergeTitle { get; private init; } = string.Empty;

    public string AbCodeMergeDetail { get; private init; } = string.Empty;

    public string MergeModeTooltip { get; private init; } = string.Empty;
}

#pragma warning restore CS1591
