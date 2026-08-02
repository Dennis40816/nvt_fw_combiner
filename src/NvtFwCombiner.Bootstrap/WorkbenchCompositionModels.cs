using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Catalog and tool status used by the Settings page.</summary>
public sealed record WorkbenchSettingsSnapshot(
    int CatalogIcCount,
    int StandardMergeProfileCount,
    int DpReplaceProfileCount,
    int CtrlRamReplaceAvailableIcCount);

/// <summary>One stable workbench IC-number choice projected from compatibility catalogs.</summary>
public sealed record WorkbenchIcNumberChoice(string Token, string DisplayLabel);

/// <summary>Golden-evidence state shown without implying a product support promise.</summary>
public enum WorkbenchWorkflowEvidenceStatus
{
    /// <summary>Direct or owner-approved fact-scoped golden parity exists.</summary>
    GoldenVerified,

    /// <summary>The workflow is available while golden/owner review remains open.</summary>
    EvidenceGated,

    /// <summary>No approved executable/safety contract exists for the selected IC/workflow.</summary>
    NotAvailable,
}

/// <summary>Selected workflow availability plus its evidence reason and opening condition.</summary>
public sealed record WorkbenchWorkflowReadiness(
    bool IsAvailable,
    WorkbenchWorkflowEvidenceStatus EvidenceStatus,
    string Reason,
    string OpenCondition);

/// <summary>Owner-defined IC-family relation projected without exposing Profiles types.</summary>
public enum WorkbenchIcFamilyRelationship
{
    /// <summary>No cross-IC family fact is declared.</summary>
    Standalone,

    /// <summary>The IC is the canonical family source.</summary>
    Canonical,

    /// <summary>All facts in the declared family scope are reusable.</summary>
    PerfectAlias,

    /// <summary>Only the explicitly declared family scope is reusable.</summary>
    PartialAlias,
}

/// <summary>Owner-defined perfect/partial IC family fact exposed to UI and audit surfaces.</summary>
public sealed record WorkbenchIcFamilySummary(
    string? FamilyId,
    string? CanonicalIcId,
    WorkbenchIcFamilyRelationship Relationship,
    string? Scope);

/// <summary>Profile-owned AB input role projected without exposing raw profile strings to Presentation.</summary>
public enum WorkbenchAbMergeInputRole
{
    /// <summary>Complete two-bank DP_AB container.</summary>
    DpAb,

    /// <summary>Touch payload for bank A.</summary>
    TpA,

    /// <summary>Touch payload for bank B.</summary>
    TpB,
}

/// <summary>One required AB slot lowered from the compiled input contract.</summary>
public sealed record WorkbenchAbMergeInputSlot(
    string SlotId,
    string AddressSpaceId,
    WorkbenchAbMergeInputRole Role,
    long RequiredEndExclusive,
    IReadOnlyList<long> ExpectedOuterLengths);

/// <summary>Stable completed health priority for one selected input.</summary>
public enum WorkbenchInputInspectionSeverity
{
    /// <summary>The input satisfies its compiled policy and metadata is readable.</summary>
    Valid,

    /// <summary>The input is accepted but requires user attention.</summary>
    Warning,

    /// <summary>The input blocks Build.</summary>
    Blocking,
}

/// <summary>Typed corrective action for a workbench input diagnostic.</summary>
public enum WorkbenchInputInspectionNextAction
{
    /// <summary>No action is required.</summary>
    None,

    /// <summary>Select a readable local BIN.</summary>
    SelectReadableInput,

    /// <summary>Select an input that reaches the compiled required end.</summary>
    SelectCompatibleInput,

    /// <summary>Review the ignored immutable source tail.</summary>
    ReviewIgnoredTrailingBytes,

    /// <summary>Review an unexpected but accepted outer length.</summary>
    ReviewUnexpectedOuterLength,

    /// <summary>Version metadata is informational; review the Unknown value.</summary>
    ReviewUnknownVersion,
}

/// <summary>One stable input diagnostic used for deterministic severity aggregation.</summary>
public sealed record WorkbenchInputInspectionIssue(
    WorkbenchInputInspectionSeverity Severity,
    string Code,
    bool BlocksBuild,
    WorkbenchInputInspectionNextAction NextAction);

/// <summary>One explicit AB bank or TP version value shown without routing authority.</summary>
public enum WorkbenchAbVersionKind
{
    /// <summary>DP bank 1 CMI value.</summary>
    Dp1,

    /// <summary>DP bank 2 CMI value.</summary>
    Dp2,

    /// <summary>TPA NVT Backup firmware value.</summary>
    TpA,

    /// <summary>TPB NVT Backup firmware value.</summary>
    TpB,
}

/// <summary>One independently decoded AB version value.</summary>
public sealed record WorkbenchAbVersionValue(
    WorkbenchAbVersionKind Kind,
    string Value,
    string? JiraBadge,
    bool IsUnknown);

/// <summary>One immutable AB input inspection projected from the compiled contract and accepted prefix.</summary>
public sealed record WorkbenchAbMergeInputInspection(
    string AddressSpaceId,
    long? ActualLength,
    long RequiredEndExclusive,
    IReadOnlyList<long> ExpectedOuterLengths,
    ByteRange? IgnoredTrailingRange,
    IReadOnlyList<WorkbenchInputInspectionIssue> Issues,
    IReadOnlyList<WorkbenchAbVersionValue> Versions)
{
    /// <summary>Highest-priority deterministic issue.</summary>
    public WorkbenchInputInspectionIssue PrimaryIssue => Issues
        .OrderByDescending(static issue => issue.Severity)
        .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
        .First();

    /// <summary>True when the current selected source cannot be built.</summary>
    public bool BlocksBuild => Issues.Any(static issue => issue.BlocksBuild);

    /// <summary>Number of immutable source bytes excluded from execution.</summary>
    public long IgnoredTrailingBytes => IgnoredTrailingRange?.Length ?? 0;
}

/// <summary>One compiled built-in profile summary exposed without compiler-internal profile data.</summary>
public sealed record WorkbenchProfileSummary(
    string ProfileId,
    string IcId,
    CompositionKind CompositionKind,
    IReadOnlyList<string> RequiredInputAddressSpaceIds,
    string DefaultOutputFileName,
    CompiledIcNumberPolicy? IcNumberPolicy,
    bool CompileSucceeded,
    IReadOnlyList<string> IssueCodes);

/// <summary>One profile-owned AB Merge topology choice exposed only when map selection requires it.</summary>
public sealed record WorkbenchAbMergeTopologyChoice(string Token, string DisplayLabel);

/// <summary>Firmware facts read from the canonical NVT-located FWConfig Backup block.</summary>
public sealed record WorkbenchFirmwareConfigMetadata(
    long FirmwareConfigBackupStart,
    string CommonFwVersion,
    byte FirmwareVersion,
    byte FirmwareVersionBar,
    bool IsFirmwareVersionBarValid,
    byte FirmwareSubVersion,
    byte ChipNumber,
    ushort ProjectId,
    string? PostbuildCategory,
    FirmwareConfigHardwareMetadata Hardware);

/// <summary>DP version token projected without exposing the application flash-map catalog type.</summary>
public readonly record struct WorkbenchDpVersionMetadata(string VersionToken)
{
    /// <summary>Human-readable DP version shared by every workbench input surface.</summary>
    public string DisplayValue => FormatDisplayValue(VersionToken);

    /// <summary>Formats the canonical four-hex-digit DP token for workbench display.</summary>
    public static string FormatDisplayValue(string versionToken)
    {
        ArgumentNullException.ThrowIfNull(versionToken);
        return versionToken.Length == 4
            ? $"D{versionToken[..2]}-{versionToken[2..]}"
            : $"D{versionToken}";
    }
}

/// <summary>CMI DP facts projected for output naming and shell display.</summary>
public readonly record struct WorkbenchCmiDpCodeMetadata(
    byte MajorVersionByte,
    byte MinorVersionNibble,
    ushort JiraNumber,
    long Register16Offset)
{
    /// <summary>Four uppercase hex digits used by FlashCode naming: DP major byte then minor nibble.</summary>
    public string VersionToken => FormattableString.Invariant($"{MajorVersionByte:X2}{MinorVersionNibble:X2}");

    /// <summary>Technical AUTO_PRJ badge, or <see langword="null"/> when Jira is zero.</summary>
    public string? JiraBadge => JiraNumber == 0 ? null : $"AUTO_PRJ-{JiraNumber}";
}

/// <summary>Build-only TP FW version override requested for a CtrlRAM Replace output.</summary>
public sealed record WorkbenchCtrlRamFirmwareVersionEdit(byte FirmwareVersion, byte FirmwareSubVersion);

/// <summary>
/// A verified NVT Backup FWConfig suggestion for the shared workbench IC-number selection.
/// It exists only when the selected image has exactly one valid NVT Backup location.
/// </summary>
public sealed record WorkbenchFirmwareContextSuggestion(
    string IcId,
    string NumberToken,
    byte ChipNumber,
    string CommonFwVersion,
    ushort ProjectId);

/// <summary>Read-only base-image shape classification used only for CtrlRAM Replace output naming.</summary>
public enum WorkbenchBaseFirmwareArtifactKind
{
    /// <summary>The available bytes do not establish a declared TP-only or FlashCode shape.</summary>
    Unknown,

    /// <summary>A declared TP work prefix or a full container with only erased/cleared DP regions.</summary>
    TpFirmware,

    /// <summary>A declared full Flash container containing programmed DP bytes.</summary>
    FlashCode,
}

/// <summary>One read-only workbench projection decoded from one immutable firmware image read.</summary>
public sealed record WorkbenchFirmwareInspection(
    string? DetectedIcId,
    WorkbenchFirmwareConfigMetadata? FirmwareConfig,
    WorkbenchDpVersionMetadata? DpVersion,
    WorkbenchCmiDpCodeMetadata? CmiDpCode,
    WorkbenchFirmwareContextSuggestion? ContextSuggestion,
    WorkbenchCtrlRamInspectionDisplay? CtrlRamDisplay,
    WorkbenchBaseFirmwareArtifactKind BaseFirmwareArtifactKind = WorkbenchBaseFirmwareArtifactKind.Unknown)
{
    /// <summary>Application-owned profile-declared artifact classification and its typed evidence.</summary>
    public CompiledFirmwareArtifactClassification? ArtifactClassification { get; init; }

    /// <summary>AB-specific typed inspection when the request names one compiled AB input space.</summary>
    public WorkbenchAbMergeInputInspection? AbMergeInput { get; init; }

    /// <summary>Shared Application-owned terminal slot health for the current compiled input.</summary>
    public AuthoringInputSlotStatus? InputSlotStatus { get; init; }
}

/// <summary>Optional CtrlRAM display context projected during firmware inspection.</summary>
public sealed record WorkbenchCtrlRamInspectionRequest(string NumberToken);

/// <summary>Materialized CtrlRAM shell projections derived from the inspected base firmware.</summary>
public sealed record WorkbenchCtrlRamInspectionDisplay(
    string NumberToken,
    IReadOnlyList<WorkbenchCtrlRamRegion> Regions,
    IReadOnlyList<WorkbenchReplaceInputSlot> InputSlots,
    WorkbenchMemoryDisplay MemoryDisplay);

/// <summary>One named firmware projection requested from a shared distinct-path read batch.</summary>
public sealed record WorkbenchFirmwareInspectionInput(
    string InspectionId,
    string Path,
    string? TpPath = null,
    WorkbenchCtrlRamInspectionRequest? CtrlRamRequest = null,
    string? AbMergeAddressSpaceId = null,
    string? AbMergeTopologyToken = null,
    string? DpReplaceAddressSpaceId = null,
    long AuthoringRevision = 1);

/// <summary>One named materialized result from a shared distinct-path read batch.</summary>
public sealed record WorkbenchFirmwareInspectionResult(
    string InspectionId,
    WorkbenchFirmwareInspection Inspection);

/// <summary>One selected firmware path candidate used by output naming metadata policy.</summary>
public sealed record WorkbenchOutputNameCandidate(
    WorkbenchOutputNameCandidateKind Kind,
    string? Path);

/// <summary>One already-inspected firmware candidate used by pure output-name projection.</summary>
public sealed record WorkbenchOutputNameInspectionCandidate(
    WorkbenchOutputNameCandidateKind Kind,
    WorkbenchFirmwareInspection? Inspection);

/// <summary>Firmware candidate role used by FlashCode output naming.</summary>
public enum WorkbenchOutputNameCandidateKind
{
    /// <summary>Unknown or generic BIN path.</summary>
    Unknown,

    /// <summary>Display/DP-family payload candidate.</summary>
    Dp,

    /// <summary>Touch-panel payload candidate.</summary>
    Tp,

    /// <summary>CtrlRAM payload candidate.</summary>
    CtrlRam,

    /// <summary>Base/reference firmware image candidate.</summary>
    Base,
}

/// <summary>Suggested FlashCode output name and the metadata tokens used to create it.</summary>
public sealed record WorkbenchOutputFileNameSuggestion(
    string FileName,
    string DpVersionToken,
    bool HasDpVersion,
    string TpVersionToken,
    bool HasTpVersion,
    string DateToken);

/// <summary>One readable before/after memory-map row for shell display.</summary>
public sealed record WorkbenchMemoryMapRow(
    string RangeLabel,
    string BeforeSource,
    string ActionLabel,
    string AfterSource,
    string Detail);

/// <summary>Typed byte provenance used only to choose coverage presentation.</summary>
public enum WorkbenchMemoryCoverageRole
{
    /// <summary>A normal initialized, input, replacement, or informational segment.</summary>
    Standard,

    /// <summary>Bytes retained or restored from the selected base firmware.</summary>
    BaseFirmware,
}

/// <summary>One visual memory coverage segment for shell display.</summary>
public sealed record WorkbenchMemoryCoverageSegment(
    string RangeLabel,
    string SourceLabel,
    string Detail,
    string Fill,
    double BarWidth,
    bool IsChanged,
    WorkbenchMemoryCoverageRole Role,
    string? RegionId = null);

/// <summary>One coherent range, row, and coverage projection from a single compiled workflow state.</summary>
public sealed record WorkbenchMemoryDisplay(
    string RangeLabel,
    IReadOnlyList<WorkbenchMemoryMapRow> MemoryMapRows,
    IReadOnlyList<WorkbenchMemoryCoverageSegment> CoverageSegments);

/// <summary>One file slot declared by the selected Replace workflow.</summary>
public sealed record WorkbenchReplaceInputSlot(
    string SlotId,
    string Title,
    string Description,
    bool IsOptional,
    string AddressSpaceId,
    string? RegionId);

/// <summary>One CtrlRAM region row for shell display.</summary>
public sealed record WorkbenchCtrlRamRegion(
    string DisplayName,
    long Start,
    long Length,
    bool IsMultiChipOnly);

/// <summary>Composition result returned to the desktop shell.</summary>
public sealed record WorkbenchRunResult(
    bool Succeeded,
    string Status,
    string ProfileId,
    long OutputSize,
    string OutputSha256,
    string OutputFileName,
    string? CommittedOutputId,
    string ReportJson)
{
    /// <summary>Non-serialized in-memory bytes available to the current desktop inspection session.</summary>
    [JsonIgnore]
    public CompositionRunInspectionSnapshot? InspectionSnapshot { get; internal init; }

    /// <summary>
    /// Non-serialized content-bound General draft accepted for this result.
    /// Desktop Preview and Build reuse this exact immutable draft until an
    /// explicit edit or Reload/Rebind replaces it.
    /// </summary>
    [JsonIgnore]
    public GeneralMappingDraftState? AcceptedGeneralMappingDraft { get; internal init; }

    /// <summary>Shared action state retained for CLI and later Presentation consumers.</summary>
    [JsonIgnore]
    public CapabilityActionReadinessSnapshot? ActionReadiness { get; internal init; }

    /// <summary>Whether this result owns a serialized run report.</summary>
    [JsonIgnore]
    public bool HasRunReport => !string.IsNullOrWhiteSpace(ReportJson);

    /// <summary>Non-serialized immutable output bytes retained only for a declared follow-up delivery artifact.</summary>
    [JsonIgnore]
    internal ReadOnlyMemory<byte> OutputBytes { get; init; }

    /// <summary>Non-serialized automatic naming provenance from the authoritative AB execution.</summary>
    [JsonIgnore]
    internal OutputNamingSummary? OutputNaming { get; init; }

    /// <summary>Additional artifacts delivered from the completed primary output.</summary>
    [JsonIgnore]
    public IReadOnlyList<WorkbenchDeliveryArtifact> DeliveryArtifacts { get; init; } = [];

    /// <summary>True when every selected delivery artifact was committed.</summary>
    [JsonIgnore]
    public bool IsDeliveryComplete { get; init; } = true;

    /// <summary>Operator-safe detail when the primary output committed but a requested additional delivery did not.</summary>
    [JsonIgnore]
    public string? DeliveryFailureMessage { get; init; }

    /// <summary>Deterministic Preview identity, retained for adapter parity and Build approval.</summary>
    [JsonIgnore]
    public string? PreviewToken { get; internal init; }
}

/// <summary>One profile-declared optional A-bank delivery proposed before a Build commits output.</summary>
public sealed class WorkbenchAbAFlashCodeDeliveryPlan
{
    internal WorkbenchAbAFlashCodeDeliveryPlan(
        string profileId,
        IReadOnlyList<string> inputPaths,
        ByteRange sourceRange,
        string suggestedFileName)
    {
        ProfileId = profileId;
        InputPaths = inputPaths;
        SourceRange = sourceRange;
        SuggestedFileName = suggestedFileName;
    }

    /// <summary>Standard FlashCode filename rendered from the same accepted AB naming tokens as the primary output.</summary>
    public string SuggestedFileName { get; }

    internal string ProfileId { get; }

    internal IReadOnlyList<string> InputPaths { get; }

    internal ByteRange SourceRange { get; }
}

/// <summary>One additional artifact committed from a primary composition output.</summary>
public sealed record WorkbenchDeliveryArtifact(
    string DeliveryKind,
    string OutputPath,
    string OutputFileName,
    long OutputSize,
    ByteRange SourceRange,
    string Sha256);

internal sealed record CoverageSegment(
    ByteRange Range,
    string SourceLabel,
    string Detail,
    string Fill,
    bool IsChanged,
    WorkbenchMemoryCoverageRole Role,
    string? RegionId = null);
