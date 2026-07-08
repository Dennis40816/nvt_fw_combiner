using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Catalog and tool status used by the Settings page.</summary>
public sealed record WorkbenchSettingsSnapshot(
    int StandardMergeProfileCount,
    int ReplaceProfileCount,
    int FlashMapIcCount,
    int PostbuildProfileCount,
    string ToolBindingIds,
    string ToolManifestPath);

/// <summary>Firmware facts read from a flash image FWConfig block.</summary>
public sealed record WorkbenchFirmwareConfigMetadata(
    long FirmwareConfigStart,
    string CommonFwVersion,
    byte FirmwareVersion,
    byte FirmwareVersionBar,
    bool IsFirmwareVersionBarValid,
    byte FirmwareSubVersion,
    ushort ProjectId,
    string? PostbuildCategory);

/// <summary>DP version facts read using gen_flash standard-merge contiguous main/sub version-byte rules.</summary>
public sealed record WorkbenchDpVersionMetadata(
    string IcId,
    string Prefix,
    string VersionToken,
    string DisplayVersion,
    long MainInputReadOffset,
    long SubInputReadOffset,
    long OutputMainAbsoluteAddress,
    long OutputSubAbsoluteAddress,
    string EvidenceSource);

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
