using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
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

/// <summary>One compiled built-in profile summary exposed without its legacy profile model.</summary>
public sealed record WorkbenchProfileSummary(
    string ProfileId,
    string IcId,
    CompositionKind CompositionKind,
    IReadOnlyList<string> RequiredInputAddressSpaceIds,
    string DefaultOutputFileName,
    CompiledIcNumberPolicy? IcNumberPolicy,
    bool CompileSucceeded,
    IReadOnlyList<string> IssueCodes);

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
public readonly record struct WorkbenchDpVersionMetadata(string VersionToken);

/// <summary>CMI DP facts projected for output naming and shell display.</summary>
public readonly record struct WorkbenchCmiDpCodeMetadata(
    byte MajorVersionByte,
    byte MinorVersionNibble,
    ushort JiraNumber,
    long Register16Offset)
{
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

/// <summary>One read-only workbench projection decoded from one immutable firmware image read.</summary>
public sealed record WorkbenchFirmwareInspection(
    string? DetectedIcId,
    WorkbenchFirmwareConfigMetadata? FirmwareConfig,
    WorkbenchDpVersionMetadata? DpVersion,
    WorkbenchCmiDpCodeMetadata? CmiDpCode,
    WorkbenchFirmwareContextSuggestion? ContextSuggestion,
    WorkbenchCtrlRamInspectionDisplay? CtrlRamDisplay);

/// <summary>Optional CtrlRAM display context projected during firmware inspection.</summary>
public sealed record WorkbenchCtrlRamInspectionRequest(string NumberToken);

/// <summary>Materialized CtrlRAM shell projections derived from the inspected base firmware.</summary>
public sealed record WorkbenchCtrlRamInspectionDisplay(
    string NumberToken,
    IReadOnlyList<WorkbenchCtrlRamRegion> Regions,
    IReadOnlyList<WorkbenchReplaceInputSlot> InputSlots,
    WorkbenchMemoryDisplay MemoryDisplay);

/// <summary>One selected firmware path candidate used by output naming metadata policy.</summary>
public sealed record WorkbenchOutputNameCandidate(
    WorkbenchOutputNameCandidateKind Kind,
    string? Path);

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

/// <summary>One visual memory coverage segment for shell display.</summary>
public sealed record WorkbenchMemoryCoverageSegment(
    string RangeLabel,
    string SourceLabel,
    string Detail,
    string Fill,
    double BarWidth,
    bool IsChanged);

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
}

internal sealed record CoverageSegment(
    ByteRange Range,
    string SourceLabel,
    string Detail,
    string Fill,
    bool IsChanged);
