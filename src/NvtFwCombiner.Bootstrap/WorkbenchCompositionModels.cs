using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Catalog and tool status used by the Settings page.</summary>
public sealed record WorkbenchSettingsSnapshot(
    int StandardMergeProfileCount,
    int ReplaceProfileCount,
    int FlashMapIcCount,
    int PostbuildProfileCount,
    string ToolBindingIds,
    string ToolManifestPath);

/// <summary>One stable workbench IC-number choice projected from compatibility catalogs.</summary>
public sealed record WorkbenchIcNumberChoice(string Token, string DisplayLabel);

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
    string ReportJson);

internal sealed record CoverageSegment(
    ByteRange Range,
    string SourceLabel,
    string Detail,
    string Fill,
    bool IsChanged);
