using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

#pragma warning disable CS1591 // Infrastructure adapter contracts are not end-user API.

namespace NvtFwCombiner.Application.Composition;

/// <summary>Stable completed health priority for one selected input.</summary>
public enum FirmwareInputInspectionSeverity
{
    /// <summary>The input satisfies its compiled policy and metadata is readable.</summary>
    Valid,

    /// <summary>The input is accepted but requires user attention.</summary>
    Warning,

    /// <summary>The input blocks Build.</summary>
    Blocking,
}

/// <summary>Informational AB version facts decoded only from the canonical accepted source view.</summary>
public sealed record AbMergeInputFacts(
    string AddressSpaceId,
    IReadOnlyList<CompiledInputVersionObservation> Versions);

/// <summary>Firmware facts read from the canonical NVT-located FWConfig Backup block.</summary>
public sealed record FirmwareConfigMetadataSnapshot(
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
public readonly record struct DpVersionMetadata(string VersionToken)
{
    /// <summary>Human-readable DP version shared by every typed input surface.</summary>
    public string DisplayValue => FormatDisplayValue(VersionToken);

    /// <summary>Formats the canonical four-hex-digit DP token for client display.</summary>
    public static string FormatDisplayValue(string versionToken)
    {
        ArgumentNullException.ThrowIfNull(versionToken);
        return versionToken.Length == 4
            ? $"D{versionToken[..2]}-{versionToken[2..]}"
            : $"D{versionToken}";
    }
}

/// <summary>CMI DP facts projected for output naming and shell display.</summary>
public readonly record struct CmiDpCodeMetadata(
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

/// <summary>
/// A verified NVT Backup FWConfig suggestion for the shared client IC-number selection.
/// It exists only when the selected image has exactly one valid NVT Backup location.
/// </summary>
public sealed record FirmwareContextSuggestion(
    string IcId,
    string NumberToken,
    byte ChipNumber,
    string CommonFwVersion,
    ushort ProjectId);

/// <summary>Compatibility shape for CtrlRAM naming; DP metadata uses it only when compiled classification is absent.</summary>
public enum BaseFirmwareArtifactKind
{
    /// <summary>The available bytes do not establish a declared TP-only or FlashCode shape.</summary>
    Unknown,

    /// <summary>A declared TP work prefix or a full container with only erased/cleared DP regions.</summary>
    TpFirmware,

    /// <summary>A declared full Flash container containing programmed DP bytes.</summary>
    FlashCode,
}

/// <summary>Non-terminal CtrlRAM Base discovery state before an exact replacement compilation exists.</summary>
public enum CtrlRamBaseDiscoveryReadiness
{
    /// <summary>The inspection did not establish a valid base-only discovery result.</summary>
    NotApplicable,

    /// <summary>The base was inspected and may declare replacement inputs, but no replacement is compiled yet.</summary>
    Inspected,
}

/// <summary>One read-only client projection decoded from one immutable firmware image read.</summary>
public sealed record FirmwareInspectionSnapshot(
    string? DetectedIcId,
    FirmwareConfigMetadataSnapshot? FirmwareConfig,
    DpVersionMetadata? DpVersion,
    CmiDpCodeMetadata? CmiDpCode,
    FirmwareContextSuggestion? ContextSuggestion,
    CtrlRamInspectionDisplay? CtrlRamDisplay,
    BaseFirmwareArtifactKind BaseFirmwareArtifactKind = BaseFirmwareArtifactKind.Unknown)
{
    /// <summary>Content identity captured from the same immutable bytes used by this inspection.</summary>
    public FileStamp? FileStamp { get; init; }

    /// <summary>Application-owned profile-declared artifact classification and its typed evidence.</summary>
    public CompiledFirmwareArtifactClassification? ArtifactClassification { get; init; }

    /// <summary>AB-specific typed inspection when the request names one compiled AB input space.</summary>
    public AbMergeInputFacts? AbMergeFacts { get; init; }

    /// <summary>Shared Application-owned terminal slot health for the current compiled input.</summary>
    public AuthoringInputSlotStatus? InputSlotStatus { get; init; }

    /// <summary>Canonical catalog owning the attached coherent input-inspection batch.</summary>
    public AuthoringCapabilityCatalogSnapshot? InputSlotCatalog { get; init; }

    /// <summary>Exact canonical prerequisite blocking DP metadata projection, when one is pending.</summary>
    public FirmwareMetadataPrerequisite? DpMetadataPrerequisite { get; init; }

    /// <summary>Exact typed authoring issues that prevented this input batch from compiling.</summary>
    public IReadOnlyList<CompositionIssue> AuthoringCompilationIssues { get; init; } = [];

    /// <summary>Typed non-terminal CtrlRAM Base discovery result.</summary>
    public CtrlRamBaseDiscoveryReadiness CtrlRamBaseDiscoveryReadiness { get; init; }
}

/// <summary>Optional CtrlRAM display context projected during firmware inspection.</summary>
public sealed record CtrlRamInspectionRequest(string NumberToken);

/// <summary>Materialized CtrlRAM shell projections derived from the inspected base firmware.</summary>
public sealed record CtrlRamInspectionDisplay(
    string NumberToken,
    IReadOnlyList<CtrlRamRegion> Regions,
    IReadOnlyList<ReplaceInputSlot> InputSlots);

/// <summary>One named firmware projection requested from a shared distinct-path read batch.</summary>
public sealed record FirmwareInspectionSnapshotInput(
    string InspectionId,
    string Path,
    string? TpPath = null,
    CtrlRamInspectionRequest? CtrlRamRequest = null,
    string? AbMergeAddressSpaceId = null,
    string? AbMergeTopologyToken = null,
    string? DpReplaceAddressSpaceId = null,
    long AuthoringRevision = 1,
    string? StandardMergeAddressSpaceId = null,
    string? CtrlRamReplaceAddressSpaceId = null,
    ResolvedCapability? ExactCapability = null);

/// <summary>One coherent compiled input-inspection batch mapped to client inspection ids.</summary>
public sealed record FirmwareInspectionStatusBatch(
    AuthoringCapabilityCatalogSnapshot? Catalog,
    IReadOnlyDictionary<string, AuthoringInputSlotStatus> Statuses,
    IReadOnlyList<CompositionIssue> Issues,
    CtrlRamBaseDiscoveryResult? CtrlRamBaseDiscovery = null)
{
    public static FirmwareInspectionStatusBatch Empty { get; } =
        new(null, new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal), []);

    /// <summary>
    /// Gets the sole publication-bound metadata plan returned by a completed
    /// exact authoring inspection; discovery-only catalogs return null.
    /// </summary>
    public ResolvedMetadataPlan? ExactMetadataPlan =>
        Catalog?.Routes.Count == 1
            ? Catalog.Routes[0].ExactCapability?.MetadataPlan
            : null;
}

/// <summary>One Application-owned base-only discovery result keyed to the caller inspection identity.</summary>
public sealed record CtrlRamBaseDiscoveryResult(
    string InspectionId,
    CtrlRamBaseDiscoveryReadiness Readiness);

/// <summary>One coherent AB Merge inspection batch mapped to client inspection ids.</summary>
public sealed record AbMergeInspectionBatch(
    AuthoringCapabilityCatalogSnapshot? Catalog,
    IReadOnlyDictionary<string, AuthoringInputSlotStatus> Statuses,
    IReadOnlyDictionary<string, AbMergeInputFacts> Facts,
    IReadOnlyList<CompositionIssue> Issues)
{
    public static AbMergeInspectionBatch Empty { get; } =
        new(
            null,
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal),
            new Dictionary<string, AbMergeInputFacts>(StringComparer.Ordinal),
            []);
}

/// <summary>One named materialized result from a shared distinct-path read batch.</summary>
public sealed record FirmwareInspectionSnapshotResult(
    string InspectionId,
    FirmwareInspectionSnapshot Inspection);

/// <summary>One distinct-path inspection batch over coherent content reads.</summary>
public sealed class FirmwareInspectionBatchResult
{
    public FirmwareInspectionBatchResult(
        IReadOnlyDictionary<string, FirmwareInspectionSnapshot> inspectionsById,
        IReadOnlyDictionary<string, FileStamp?> fileStamps,
        IEnumerable<string> unstableFilePaths)
    {
        ArgumentNullException.ThrowIfNull(inspectionsById);
        ArgumentNullException.ThrowIfNull(fileStamps);
        ArgumentNullException.ThrowIfNull(unstableFilePaths);
        InspectionsById = new ReadOnlyDictionary<string, FirmwareInspectionSnapshot>(
            new Dictionary<string, FirmwareInspectionSnapshot>(inspectionsById, StringComparer.Ordinal));
        FileStamps = new ReadOnlyDictionary<string, FileStamp?>(
            new Dictionary<string, FileStamp?>(fileStamps, StringComparer.Ordinal));
        UnstableFilePaths = Array.AsReadOnly(
        [
            .. unstableFilePaths
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal),
        ]);
    }

    /// <summary>Inspection projections keyed by the caller-supplied inspection id.</summary>
    public IReadOnlyDictionary<string, FirmwareInspectionSnapshot> InspectionsById { get; }

    /// <summary>Accepted content identities keyed by path; unreadable or changing sources have null.</summary>
    public IReadOnlyDictionary<string, FileStamp?> FileStamps { get; }

    /// <summary>Paths whose content changed during their coherent read.</summary>
    public IReadOnlyList<string> UnstableFilePaths { get; }

    /// <summary>True when no source changed during its read; unreadable paths retain null stamps.</summary>
    public bool IsContentStable => UnstableFilePaths.Count == 0;
}

/// <summary>Typed Replace presentation group derived before the UI boundary.</summary>
public enum ReplaceRegionGroup
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
public enum ReplaceInputRole
{
    /// <summary>No fixed Replace inspection workflow is declared.</summary>
    None,
    /// <summary>One DP Replace input selected by the exact compiled contract.</summary>
    Dp,
    /// <summary>One CtrlRAM Replace input selected by the exact compiled contract.</summary>
    CtrlRam,
}

/// <summary>One typed CtrlRAM section retained with an input discovery projection.</summary>
public sealed record CtrlRamInputDescriptionSection(
    string DisplayName,
    ReplaceRegionGroup RegionGroup,
    long MaximumLength,
    long TargetStart,
    string TitleStem);

/// <summary>Structured CtrlRAM input facts retained independently from display text.</summary>
public sealed record CtrlRamInputDescriptionFacts(
    string SourceFileName,
    IReadOnlyList<CtrlRamInputDescriptionSection> Sections,
    bool RequiresDiffNfMerge,
    string TitleStem,
    bool IsShared);

/// <summary>Closed CtrlRAM family role used by detailed memory presentation.</summary>
public enum CtrlRamRegionRole
{
    /// <summary>NF CtrlRAM.</summary>
    Nf,
    /// <summary>Normal CtrlRAM.</summary>
    Normal,
    /// <summary>MP CtrlRAM.</summary>
    Mp,
    /// <summary>VN CtrlRAM.</summary>
    Vn,
    /// <summary>Vector CtrlRAM.</summary>
    Vector,
    /// <summary>DiffDLM or DIFF CtrlRAM.</summary>
    DiffDlm,
    /// <summary>CtrlRAM outside the approved detailed family vocabulary.</summary>
    Other,
}

/// <summary>One file slot declared by the selected Replace workflow.</summary>
public sealed record ReplaceInputSlot(
    string SlotId,
    string Title,
    string Description,
    bool IsOptional,
    string AddressSpaceId,
    string? RegionId,
    string CompiledSlotId,
    string? SelectionGroupId = null,
    ReplaceRegionGroup RegionGroup = ReplaceRegionGroup.Common,
    ReplaceInputRole InputRole = ReplaceInputRole.None,
    CtrlRamInputDescriptionFacts? CtrlRamDescription = null);

/// <summary>One CtrlRAM region row for shell display.</summary>
public sealed record CtrlRamRegion(
    string RegionId,
    string DisplayName,
    long Start,
    long Length,
    bool IsMultiChipOnly,
    ReplaceRegionGroup RegionGroup,
    CtrlRamRegionRole Role);

/// <summary>Typed General Merge initializer resolved before Presentation creates a draft.</summary>
public sealed class GeneralMergeInitializer
{
    internal GeneralMergeInitializer(
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
