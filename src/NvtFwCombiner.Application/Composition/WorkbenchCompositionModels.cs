using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

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

/// <summary>Informational AB version facts decoded only from the canonical accepted source view.</summary>
public sealed record WorkbenchAbMergeInputFacts(
    string AddressSpaceId,
    IReadOnlyList<CompiledInputVersionObservation> Versions);

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

/// <summary>TP FW version values requested for one CtrlRAM authoring transition.</summary>
public sealed record WorkbenchCtrlRamFirmwareVersionEdit(byte FirmwareVersion, byte FirmwareSubVersion);

/// <summary>Result of compiling and re-inspecting one typed CtrlRAM authoring transition.</summary>
public sealed record WorkbenchCtrlRamAuthoringTransitionResult(
    ActiveSessionSnapshot? Session,
    IReadOnlyList<CompositionIssue> Issues)
{
    /// <summary>True only when the new exact compilation owns current accepted input inspection.</summary>
    public bool Succeeded =>
        Session?.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection) is not null &&
        Issues.Count == 0;
}

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
    /// <summary>Content identity captured from the same immutable bytes used by this inspection.</summary>
    public FileStamp? FileStamp { get; init; }

    /// <summary>Application-owned profile-declared artifact classification and its typed evidence.</summary>
    public CompiledFirmwareArtifactClassification? ArtifactClassification { get; init; }

    /// <summary>AB-specific typed inspection when the request names one compiled AB input space.</summary>
    public WorkbenchAbMergeInputFacts? AbMergeFacts { get; init; }

    /// <summary>Shared Application-owned terminal slot health for the current compiled input.</summary>
    public AuthoringInputSlotStatus? InputSlotStatus { get; init; }

    /// <summary>Canonical catalog owning the attached coherent input-inspection batch.</summary>
    public AuthoringCapabilityCatalogSnapshot? InputSlotCatalog { get; init; }
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
    long AuthoringRevision = 1,
    string? StandardMergeAddressSpaceId = null,
    string? CtrlRamReplaceAddressSpaceId = null,
    ResolvedCapability? ExactCapability = null);

/// <summary>One coherent compiled input-inspection batch mapped to workbench inspection ids.</summary>
internal sealed record WorkbenchCompiledAuthoringInspectionBatch(
    AuthoringCapabilityCatalogSnapshot? Catalog,
    IReadOnlyDictionary<string, AuthoringInputSlotStatus> Statuses,
    IReadOnlyList<CompositionIssue> Issues)
{
    internal static WorkbenchCompiledAuthoringInspectionBatch Empty { get; } =
        new(null, new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal), []);
}

/// <summary>One coherent AB Merge inspection batch mapped to workbench inspection ids.</summary>
internal sealed record WorkbenchAbMergeInspectionBatch(
    AuthoringCapabilityCatalogSnapshot? Catalog,
    IReadOnlyDictionary<string, AuthoringInputSlotStatus> Statuses,
    IReadOnlyDictionary<string, WorkbenchAbMergeInputFacts> Facts,
    IReadOnlyList<CompositionIssue> Issues)
{
    internal static WorkbenchAbMergeInspectionBatch Empty { get; } =
        new(
            null,
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal),
            new Dictionary<string, WorkbenchAbMergeInputFacts>(StringComparer.Ordinal),
            []);
}

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

/// <summary>Typed Replace presentation group derived before the UI boundary.</summary>
public enum WorkbenchReplaceRegionGroup
{
    /// <summary>Cascade-only or DiffDLM content.</summary>
    Cascade,
    /// <summary>Shared or unpartitioned content.</summary>
    Common,
    /// <summary>Master-controller content.</summary>
    Master,
    /// <summary>Right-slave content.</summary>
    SlaveRight,
    /// <summary>Left-slave content.</summary>
    SlaveLeft,
    /// <summary>Retained base-firmware content.</summary>
    Base,
    /// <summary>Content outside the reviewed grouping vocabulary.</summary>
    Other,
}

/// <summary>Typed workflow role used to admit a Replace input to canonical inspection.</summary>
public enum WorkbenchReplaceInputRole
{
    /// <summary>No fixed Replace inspection workflow is declared.</summary>
    None,
    /// <summary>One DP Replace input selected by the exact compiled contract.</summary>
    Dp,
    /// <summary>One CtrlRAM Replace input selected by the exact compiled contract.</summary>
    CtrlRam,
}

/// <summary>One immutable semantic memory-coverage segment for a client projection.</summary>
public sealed record WorkbenchMemoryCoverageSegment(
    ByteRange? Range,
    string? UnresolvedRangeLabel,
    string SourceLabel,
    string Detail,
    long DisplayCapacity,
    bool IsChanged,
    WorkbenchMemoryCoverageRole Role,
    string? RegionId = null,
    bool IsDiffDlm = false,
    IReadOnlyList<MemoryLayoutPreservationDetail>? PreservationDetails = null,
    WorkbenchReplaceRegionGroup RegionGroup = WorkbenchReplaceRegionGroup.Common);

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
    string? RegionId,
    string CompiledSlotId,
    string? SelectionGroupId = null,
    WorkbenchReplaceRegionGroup RegionGroup = WorkbenchReplaceRegionGroup.Common,
    WorkbenchReplaceInputRole InputRole = WorkbenchReplaceInputRole.None);

/// <summary>One explicit General Replace action selected by a Presentation command boundary.</summary>
public delegate ValueTask<WorkbenchRunResult> WorkbenchGeneralReplaceAcceptedSessionRunner(
    string icId,
    string number,
    IReadOnlyDictionary<string, string> slotPaths,
    ActiveSessionSnapshot acceptedSession,
    CompositionRunProgressFeed progress,
    CancellationToken cancellationToken);

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

    /// <summary>Exact immutable capability consumed by this in-memory run.</summary>
    [JsonIgnore]
    public ResolvedCapability? ResolvedCapability { get; internal init; }

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

/// <summary>Typed General Merge initializer resolved before Presentation creates a draft.</summary>
public sealed class WorkbenchGeneralMergeInitializer
{
    internal WorkbenchGeneralMergeInitializer(
        GeneralMergeOutputInitializer value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>Exact positive output capacity.</summary>
    public long Capacity => Value.Capacity;

    /// <summary>Exact blank-output fill byte.</summary>
    public byte FillByte => Value.FillByte;

    internal GeneralMergeOutputInitializer Value { get; }
}
