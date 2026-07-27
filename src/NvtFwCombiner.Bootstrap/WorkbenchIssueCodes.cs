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

    /// <summary>An accepted AB input has no readable informational version metadata.</summary>
    public const string AbInputVersionUnknown = "ab.input.version-unknown";

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

    /// <summary>Standard Merge DP Perspective input length is not approved by the profile.</summary>
    public const string StandardMergeDpLengthUnsupported = "standard-merge.dp-length-unsupported";

    /// <summary>General Replace mapping range text is invalid.</summary>
    public const string GeneralReplaceRangeInvalid = "ui.general-replace.range-invalid";

    /// <summary>General Replace patch id cannot be safely shown in reports.</summary>
    public const string GeneralReplacePatchIdInvalid = "ui.general-replace.patch-id-invalid";

    /// <summary>General Replace patch ids must be unique across mappings and patches.</summary>
    public const string GeneralReplacePatchIdDuplicate = "ui.general-replace.patch-id-duplicate";

    /// <summary>General Replace overwrite bytes are not valid hexadecimal byte pairs.</summary>
    public const string GeneralReplacePatchHexInvalid = "ui.general-replace.patch-hex-invalid";

    /// <summary>General Replace overwrite bytes must exactly cover the selected target range.</summary>
    public const string GeneralReplacePatchLengthMismatch = "ui.general-replace.patch-length-mismatch";

    /// <summary>General Replace fill values must contain exactly one hexadecimal byte.</summary>
    public const string GeneralReplacePatchFillByteInvalid = "ui.general-replace.patch-fill-byte-invalid";

    /// <summary>DP Replace profile is intentionally pending for the selected IC.</summary>
    public const string ReplaceDpProfilePending = "replace.dp.profile-pending";

    /// <summary>Replace mode is not recognized by the workbench router.</summary>
    public const string ReplaceModeUnknown = "replace.mode.unknown";

    /// <summary>The selected IC does not expose the requested Replace workflow.</summary>
    public const string ReplaceWorkflowNotSupported = "replace.workflow.not-supported";

    /// <summary>CtrlRAM Replace IC-number selection is unsupported by the selected postbuild profile.</summary>
    public const string ReplaceCtrlRamIcNumberUnsupported = "replace.ctrlram.ic-number-unsupported";

    /// <summary>Selected CtrlRAM IC Number conflicts with the readable FWConfig chip count.</summary>
    public const string ReplaceCtrlRamIcNumberMismatch = "replace.ctrlram.ic-number-mismatch";

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

    /// <summary>A Cascade route with embedded Diff NF does not accept the legacy independent NF input.</summary>
    public const string ReplaceCtrlRamCascadeNfInputUnsupported = "replace.ctrlram.cascade-nf-input-unsupported";

    /// <summary>DiffDLM does not satisfy the active-record mask contract.</summary>
    public const string ReplaceCtrlRamDiffDlmSourceInvalid = "replace.ctrlram.diffdlm-source-invalid";

    /// <summary>CtrlRAM Replace postbuild planner did not expose an approved write range.</summary>
    public const string ReplaceCtrlRamPostbuildWriteRangeMissing = "replace.ctrlram.postbuild-write-range-missing";

    /// <summary>CtrlRAM Replace cannot edit TP FW version because the canonical NVT Backup has invalid version metadata.</summary>
    public const string ReplaceCtrlRamFirmwareVersionSourceInvalid = "replace.ctrlram.fw-version-source-invalid";

    /// <summary>CtrlRAM Replace has no unambiguous approved Combiner path from a source FWConfig to the canonical Backup.</summary>
    public const string ReplaceCtrlRamFirmwareVersionPropagationUnavailable = "replace.ctrlram.fw-version-propagation-unavailable";

    /// <summary>CtrlRAM Replace postbuild did not leave one readable canonical TP FW version Backup.</summary>
    public const string ReplaceCtrlRamFirmwareVersionOutputInvalid = "replace.ctrlram.fw-version-output-invalid";

    /// <summary>CtrlRAM Replace postbuild did not propagate the confirmed TP FW version to the canonical Backup.</summary>
    public const string ReplaceCtrlRamFirmwareVersionOutputMismatch = "replace.ctrlram.fw-version-output-mismatch";

    /// <summary>General Replace IC-number selection is unsupported by the selected postbuild profile.</summary>
    public const string ReplaceGeneralIcNumberUnsupported = "replace.general.ic-number-unsupported";

    /// <summary>General Replace TP-touching postbuild planner did not expose an approved write range.</summary>
    public const string ReplaceGeneralPostbuildWriteRangeMissing = "replace.general.postbuild-write-range-missing";
}
