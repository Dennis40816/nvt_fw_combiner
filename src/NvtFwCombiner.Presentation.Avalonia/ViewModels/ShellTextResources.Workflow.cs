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

    /// <summary>Label for the confirmed safe export action in the raw-BIN Hex Editor.</summary>
    public string HexEditorSaveLabel { get; private init; } = string.Empty;

    /// <summary>Label for choosing a new output path in the raw-BIN Hex Editor.</summary>
    public string HexEditorSaveAsLabel { get; private init; } = string.Empty;

    /// <summary>Title for the safe Hex Editor export confirmation.</summary>
    public string HexEditorSaveConfirmationTitle { get; private init; } = string.Empty;

    /// <summary>Explanation for the safe Hex Editor export confirmation.</summary>
    public string HexEditorSaveConfirmationDetail { get; private init; } = string.Empty;

    /// <summary>Confirmation action label for the safe Hex Editor export dialog.</summary>
    public string HexEditorSaveConfirmationActionLabel { get; private init; } = string.Empty;

    public string GeneralMergeMappingDetail { get; private init; } = string.Empty;

    public string GeneralMergeMappingsDetail { get; private init; } = string.Empty;

    public string CtrlRamInputFilesDetail { get; private init; } = string.Empty;

    public string AbCodeMergeTitle { get; private init; } = string.Empty;

    public string AbCodeMergeDetail { get; private init; } = string.Empty;

    public string MergeModeTooltip { get; private init; } = string.Empty;

    public string WorkflowContextReplaceDetail { get; private init; } = string.Empty;

    public string WorkflowContextMergeDetail { get; private init; } = string.Empty;

    public string WorkflowContextSafetyDetail { get; private init; } = string.Empty;

    public string FirmwareIcMismatchTitle { get; private init; } = string.Empty;

    public string FirmwareIcMismatchDetail { get; private init; } = string.Empty;

    public string FirmwareIcMismatchCurrentLabel { get; private init; } = string.Empty;

    public string FirmwareIcMismatchDetectedLabel { get; private init; } = string.Empty;

    public string FirmwareIcMismatchKeepLabel { get; private init; } = string.Empty;

    public string FirmwareIcMismatchUseDetectedLabel { get; private init; } = string.Empty;

    public string ContextUpdatedToastTitle { get; private init; } = string.Empty;

    public string UtilToolsLabel { get; private init; } = string.Empty;

    public string HexEditorContextInsertZeroBeforeLabel { get; private init; } = string.Empty;

    public string HexEditorContextInsertZeroAfterLabel { get; private init; } = string.Empty;

    public string HexEditorContextDeleteByteLabel { get; private init; } = string.Empty;

    public string HexEditorContextSetToZeroLabel { get; private init; } = string.Empty;

    public string HexEditorContextSetToFfLabel { get; private init; } = string.Empty;

    public string HexEditorSourceReadyDetail { get; private init; } = string.Empty;

    public string HexEditorSourceEmptyDetail { get; private init; } = string.Empty;

    public string HexEditorSaveCompletedDetail { get; private init; } = string.Empty;

    public string HexEditorFileOperationFailedDetail { get; private init; } = string.Empty;

    public string HexEditorNothingToUndoDetail { get; private init; } = string.Empty;

    public string HexEditorNothingToRedoDetail { get; private init; } = string.Empty;

    public string HexEditorTitle { get; private init; } = string.Empty;

    public string HexEditorDetail { get; private init; } = string.Empty;

    public string HexEditorGoToAddressLabel { get; private init; } = string.Empty;

    public string HexEditorChangeTitle { get; private init; } = string.Empty;

    public string HexEditorAddressColumnLabel { get; private init; } = string.Empty;

    public string HexEditorAsciiColumnLabel { get; private init; } = string.Empty;

    public string HexEditorShowOriginalRowsLabel { get; private init; } = string.Empty;

    public string HexEditorHexBytesLabel { get; private init; } = string.Empty;

    public string HexEditorHexBytesPlaceholder { get; private init; } = string.Empty;

    public string HexEditorOverwriteRangeLabel { get; private init; } = string.Empty;

    public string HexEditorFillRangeLabel { get; private init; } = string.Empty;

    public string HexEditorUndoLabel { get; private init; } = string.Empty;

    public string HexEditorRedoLabel { get; private init; } = string.Empty;

    public string HexEditorEditByteLabel { get; private init; } = string.Empty;

    public string HexEditorInvalidAddressDetail { get; private init; } = string.Empty;

    public string HexEditorInvalidByteDetail { get; private init; } = string.Empty;

    public string HexEditorInvalidRangeDetail { get; private init; } = string.Empty;
}

#pragma warning restore CS1591
