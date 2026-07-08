namespace NvtFwCombiner.Bootstrap;

/// <summary>Stable workbench planning/report issue codes surfaced by UI and CLI adapters.</summary>
public static class WorkbenchIssueCodes
{
    /// <summary>Unexpected UI-triggered run failure.</summary>
    public const string UiRunFailed = "ui.run.failed";

    /// <summary>Required input slot is missing.</summary>
    public const string InputMissing = "ui.input.missing";

    /// <summary>Input artifact could not be read from disk.</summary>
    public const string InputArtifactReadFailed = "input.artifact.read-failed";

    /// <summary>General Merge has no explicit mapping rows.</summary>
    public const string GeneralMergeMappingRequired = "ui.general-merge.mapping-required";

    /// <summary>General Merge source range exceeds the selected source file.</summary>
    public const string GeneralMergeSourceOutOfBounds = "ui.general-merge.source-out-of-bounds";

    /// <summary>General Merge mapping range text is invalid.</summary>
    public const string GeneralMergeRangeInvalid = "ui.general-merge.range-invalid";

    /// <summary>General Merge output capacity text is invalid.</summary>
    public const string GeneralMergeCapacityInvalid = "ui.general-merge.capacity-invalid";

    /// <summary>General Merge output capacity exceeds supported in-memory composition limits.</summary>
    public const string GeneralMergeCapacityUnsupported = "ui.general-merge.capacity-unsupported";

    /// <summary>General Replace mapping range text is invalid.</summary>
    public const string GeneralReplaceRangeInvalid = "ui.general-replace.range-invalid";

    /// <summary>DP Replace profile is intentionally pending for the selected IC.</summary>
    public const string ReplaceDpProfilePending = "replace.dp.profile-pending";

    /// <summary>Replace mode is not recognized by the workbench router.</summary>
    public const string ReplaceModeUnknown = "replace.mode.unknown";

    /// <summary>CtrlRAM Replace IC-number selection is unsupported by the selected postbuild profile.</summary>
    public const string ReplaceCtrlRamIcNumberUnsupported = "replace.ctrlram.ic-number-unsupported";

    /// <summary>CtrlRAM Replace has no registered postbuild profile for the selected IC.</summary>
    public const string ReplaceCtrlRamPostbuildProfileMissing = "replace.ctrlram.postbuild-profile-missing";

    /// <summary>CtrlRAM Replace could not determine the postbuild category from base firmware metadata.</summary>
    public const string ReplaceCtrlRamPostbuildCategoryUnknown = "replace.ctrlram.postbuild-category-unknown";

    /// <summary>CtrlRAM Replace postbuild category is not supported for the selected base firmware.</summary>
    public const string ReplaceCtrlRamPostbuildCategoryUnsupported = "replace.ctrlram.postbuild-category-unsupported";

    /// <summary>CtrlRAM Replace has no postbuild-mapped CtrlRAM region for the selected IC/number.</summary>
    public const string ReplaceCtrlRamNoMappedRegion = "replace.ctrlram.no-mapped-region";

    /// <summary>CtrlRAM Replace has no selected replacement BIN for mapped regions.</summary>
    public const string ReplaceCtrlRamNoRegionInput = "replace.ctrlram.no-region-input";

    /// <summary>CtrlRAM Replace postbuild planner did not expose an approved write range.</summary>
    public const string ReplaceCtrlRamPostbuildWriteRangeMissing = "replace.ctrlram.postbuild-write-range-missing";

    /// <summary>General Replace IC-number selection is unsupported by the selected postbuild profile.</summary>
    public const string ReplaceGeneralIcNumberUnsupported = "replace.general.ic-number-unsupported";

    /// <summary>General Replace TP-touching postbuild planner did not expose an approved write range.</summary>
    public const string ReplaceGeneralPostbuildWriteRangeMissing = "replace.general.postbuild-write-range-missing";
}
