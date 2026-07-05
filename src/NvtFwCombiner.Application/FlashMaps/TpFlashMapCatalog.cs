using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>TP flash-map region category from the owner-maintained TP Overview workbook.</summary>
public enum TpFlashMapRegionKind
{
    /// <summary>Display/DP payload area.</summary>
    Dp,

    /// <summary>CtrlRAM payload area replace can target.</summary>
    CtrlRam,

    /// <summary>Customer or production information that must be preserved unless explicitly allowed.</summary>
    CustomerInfo,

    /// <summary>Project ID area.</summary>
    ProjectId,

    /// <summary>Other documented firmware region.</summary>
    Other,
}

/// <summary>IC-count visibility for a TP flash-map row.</summary>
public enum TpFlashMapRegionVisibility
{
    /// <summary>Visible for single and multi-chip selections.</summary>
    Always,

    /// <summary>Visible only for cascade or numeric selections larger than one.</summary>
    MultiChipOnly,

    /// <summary>Visible only for numeric selections of two or larger.</summary>
    TwoChipAndAbove,

    /// <summary>Visible only for numeric selections of three or larger.</summary>
    ThreeChipAndAbove,
}

/// <summary>One TP Overview-derived flash-map region.</summary>
public sealed class TpFlashMapRegion
{
    /// <summary>Creates a TP flash-map region.</summary>
    public TpFlashMapRegion(
        string regionId,
        string displayName,
        TpFlashMapRegionKind kind,
        ByteRange range,
        TpFlashMapRegionVisibility visibility = TpFlashMapRegionVisibility.Always,
        string? postbuildFileName = null,
        IReadOnlyList<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        RegionId = regionId;
        DisplayName = displayName;
        Kind = kind;
        Range = range;
        Visibility = visibility;
        PostbuildFileName = string.IsNullOrWhiteSpace(postbuildFileName) ? null : postbuildFileName;
        Tags = tags ?? [];
    }

    /// <summary>Stable id for reports and UI state.</summary>
    public string RegionId { get; }

    /// <summary>Human-facing region label from TP Overview wording.</summary>
    public string DisplayName { get; }

    /// <summary>Region category.</summary>
    public TpFlashMapRegionKind Kind { get; }

    /// <summary>Absolute flash range in the TP image.</summary>
    public ByteRange Range { get; }

    /// <summary>IC-count visibility policy for this row.</summary>
    public TpFlashMapRegionVisibility Visibility { get; }

    /// <summary>Postbuild BIN file name when the legacy command sequence consumes this row as an external BIN.</summary>
    public string? PostbuildFileName { get; }

    /// <summary>Additional row tags such as diff, slave, or preserve.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>True when the region is hidden for single-chip selections.</summary>
    public bool IsHiddenInSingle => Visibility != TpFlashMapRegionVisibility.Always;
}

/// <summary>TP Overview flash-map profile for one selectable IC id.</summary>
public sealed class TpFlashMapProfile
{
    private readonly TpFlashMapRegion[] _regions;

    /// <summary>Creates a TP flash-map profile.</summary>
    public TpFlashMapProfile(
        string icId,
        string overviewSource,
        long firmwareConfigStart,
        IEnumerable<TpFlashMapRegion> regions,
        string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(overviewSource);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        _regions = [.. regions];
        if (_regions.Length == 0)
        {
            throw new ArgumentException("Flash-map profile must contain at least one region.", nameof(regions));
        }

        IcId = icId;
        OverviewSource = overviewSource;
        FirmwareConfigStart = firmwareConfigStart;
        Evidence = evidence;
    }

    /// <summary>Selectable NT-prefixed IC id.</summary>
    public string IcId { get; }

    /// <summary>TP Overview source section label.</summary>
    public string OverviewSource { get; }

    /// <summary>Primary FLASHMAP_FW_REGISTER address used for FWConfig metadata reads.</summary>
    public long FirmwareConfigStart { get; }

    /// <summary>Reference evidence used to create this profile.</summary>
    public string Evidence { get; }

    /// <summary>All documented regions in stable TP Overview order.</summary>
    public IReadOnlyList<TpFlashMapRegion> Regions => _regions;
}

/// <summary>Production flash-map catalog normalized from TP Overview and postbuild naming.</summary>
public static class TpFlashMapCatalog
{
    private static readonly Dictionary<string, TpFlashMapProfile> ProfilesByIc = BuildProfiles()
        .ToDictionary(profile => profile.IcId, StringComparer.Ordinal);

    private static readonly Dictionary<string, LegacyCombinerPostbuildProfile> PostbuildProfilesByIc =
        LegacyCombinerPostbuildCatalog.All
            .GroupBy(profile => profile.IcId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    /// <summary>Supported IC ids in stable order.</summary>
    public static IReadOnlyList<string> IcIds { get; } =
    [
        .. ProfilesByIc.Keys.Order(StringComparer.Ordinal),
    ];

    /// <summary>Returns the primary FWConfig flash start for the selected IC, when documented.</summary>
    public static bool TryGetFirmwareConfigStart(string icId, out long start)
    {
        if (ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? profile))
        {
            start = profile.FirmwareConfigStart;
            return true;
        }

        start = 0;
        return false;
    }

    /// <summary>Returns true when the catalog has a flash-map profile for <paramref name="icId"/>.</summary>
    public static bool TryFind(string icId, out TpFlashMapProfile? profile)
    {
        return ProfilesByIc.TryGetValue(icId, out profile);
    }

    /// <summary>Gets UI number choices from the postbuild branches available for an IC.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return !PostbuildProfilesByIc.TryGetValue(icId, out LegacyCombinerPostbuildProfile? profile)
            ? ["single"]
            : profile.TwoChipCommands is not null || profile.ThreeChipCommands is not null
            ? ["single", "2", "3"]
            : ["single", "cascade"];
    }

    /// <summary>Gets TP Overview CtrlRAM regions visible for the selected IC and IC-count context.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetCtrlRamRegions(
        string icId,
        IcNumberSelection? selection)
    {
        return GetRegions(icId, selection, TpFlashMapRegionKind.CtrlRam);
    }

    /// <summary>Gets TP Overview regions visible for the selected IC, IC-count context, and optional kind.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetRegions(
        string icId,
        IcNumberSelection? selection,
        TpFlashMapRegionKind? kind = null)
    {
        if (!ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? profile))
        {
            return [];
        }

        int? count = TryGetNumericCount(selection);
        bool isSingle = IsSingle(selection, count);
        return [
            .. profile.Regions
                .Where(region => kind is null || region.Kind == kind)
                .Where(region => IsVisible(region.Visibility, isSingle, count))
        ];
    }

    /// <summary>Gets visible CtrlRAM regions that are consumed by the selected postbuild command plan.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetPostbuildMappedCtrlRamRegions(
        string icId,
        IcNumberSelection? selection)
    {
        if (!ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? profile) ||
            !PostbuildProfilesByIc.TryGetValue(icId, out LegacyCombinerPostbuildProfile? postbuildProfile))
        {
            return [];
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            postbuildProfile,
            selection);
        IReadOnlyList<LegacyCombinerBlockArgument> blocks = LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan);
        return [
            .. GetCtrlRamRegions(icId, selection)
                .Where(region => blocks.Any(block => IsMappedBlock(region, block)))
        ];
    }

    private static bool IsMappedBlock(TpFlashMapRegion region, LegacyCombinerBlockArgument block)
    {
        return string.Equals(region.PostbuildFileName, block.SourceFileName, StringComparison.Ordinal) &&
            region.Range.Overlaps(block.FirmwareRange);
    }

    private static bool IsVisible(TpFlashMapRegionVisibility visibility, bool isSingle, int? count)
    {
        return visibility switch
        {
            TpFlashMapRegionVisibility.Always => true,
            TpFlashMapRegionVisibility.MultiChipOnly => !isSingle,
            TpFlashMapRegionVisibility.TwoChipAndAbove => !isSingle && (count is null || count >= 2),
            TpFlashMapRegionVisibility.ThreeChipAndAbove => !isSingle && (count is null || count >= 3),
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unsupported visibility."),
        };
    }

    private static bool IsSingle(IcNumberSelection? selection, int? count)
    {
        if (selection is null)
        {
            return true;
        }

        if (selection.Mode == IcNumberInputMode.SingleSelector || count == 1)
        {
            return true;
        }

        string? lastPart = selection.Parts.Count == 0 ? null : selection.Parts[^1];
        return string.Equals(lastPart, "single", StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryGetNumericCount(IcNumberSelection? selection)
    {
        return selection?.Mode != IcNumberInputMode.NumericSelector || selection.Parts.Count == 0
            ? null
            : int.TryParse(selection.Parts[^1], out int count) ? count : null;
    }

    private static IReadOnlyList<TpFlashMapProfile> BuildProfiles()
    {
        TpFlashMapRegion[] nt51927Regions = Nt51927Regions();
        TpFlashMapRegion[] nt51929Regions = Nt51929Regions();
        TpFlashMapRegion[] nt51950Regions = Nt51950Regions();
        return
        [
            Profile("NT51917", "51927 & 51928 (Not NB)", 0x16000, nt51927Regions, "Owner confirmation: NT51917 follows NT51927."),
            Profile("NT51919", "51929 & 51932", 0x1F200, nt51929Regions, "Owner confirmation: NT51919 follows NT51929."),
            Profile("NT51920", "51920", 0x22000, Nt51920Regions()),
            Profile("NT51923", "51923", 0x22000, Nt51923Regions()),
            Profile("NT51926", "51926", 0x22000, Nt51926Regions()),
            Profile("NT51927", "51927 & 51928 (Not NB)", 0x16000, nt51927Regions),
            Profile("NT51928", "51927 & 51928 (Not NB)", 0x16000, nt51927Regions, "NT51928 non-NB follows NT51927. NT51928 NB is not covered."),
            Profile("NT51929", "51929 & 51932", 0x1F200, nt51929Regions, "Owner confirmation: NT51929 follows NT51932 postbuild."),
            Profile("NT51930", "51930", 0x1F200, Nt51930Regions()),
            Profile("NT51931", "51931", 0x16000, Nt51931Regions()),
            Profile("NT51932", "51929 & 51932", 0x1F200, nt51929Regions),
            Profile("NT51950", "51950 (No Backup EN)", 0x22200, nt51950Regions),
            Profile("NT51951", "51951 (No Back Up EN)", 0x22200, nt51950Regions, "Owner confirmation: NT51951 follows NT51950 postbuild."),
        ];
    }

    private static TpFlashMapProfile Profile(
        string icId,
        string overviewSource,
        long firmwareConfigStart,
        IEnumerable<TpFlashMapRegion> regions,
        string? note = null)
    {
        string evidence = string.IsNullOrWhiteSpace(note)
            ? "IC_FlashMap.xlsx TP Overview and postbuild naming."
            : $"IC_FlashMap.xlsx TP Overview and postbuild naming. {note}";
        return new TpFlashMapProfile(icId, overviewSource, firmwareConfigStart, regions, evidence);
    }

    private static TpFlashMapRegion[] Nt51920Regions()
    {
        return
        [
            Ctrl("normal-master", "Normal CtrlRAM (Master)", 0x22780, 0x02800, "Normal_Ctrlram.bin"),
            Ctrl("mp-master", "MP CtrlRAM (Master)", 0x24F80, 0x01700, "MP_Ctrlram.bin"),
            Ctrl("normal-slave", "Normal CtrlRAM (Slave)", 0x26780, 0x02800, "Normal_Ctrlram_S.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["slave"]),
            Ctrl("mp-slave", "MP CtrlRAM (Slave)", 0x28F80, 0x01700, "MP_Ctrlram_S.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["slave"]),
            Ctrl("nf", "NF CtrlRAM", 0x2A780, 0x01F90, "NF_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x2C710, 0x01018, "VN_Ctrlram.bin"),
            Ctrl("vector", "Vector CtrlRAM", 0x2D728, 0x00258, "Vector_Ctrlram.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["vector"]),
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x3E000, 0x02000),
        ];
    }

    private static TpFlashMapRegion[] Nt51923Regions()
    {
        return
        [
            Ctrl("normal", "Normal CtrlRAM", 0x22800, 0x03800, "Normal_Ctrlram.bin"),
            Ctrl("mp", "MP CtrlRAM", 0x26000, 0x02800, "MP_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x28800, 0x01800, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff"]),
            Ctrl("nf", "NF CtrlRAM", 0x2A000, 0x044B0, "NF_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x2E800, 0x01660, "VN_Ctrlram.bin"),
            Region("project-id", "Project ID", TpFlashMapRegionKind.ProjectId, 0x3C000, 0x01000),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x3D000, 0x01000, tags: ["preserve"]),
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x3E000, 0x02000),
        ];
    }

    private static TpFlashMapRegion[] Nt51926Regions()
    {
        return
        [
            Ctrl("normal", "Normal CtrlRAM", 0x22800, 0x02C00, "Normal_Ctrlram.bin"),
            Ctrl("mp", "MP CtrlRAM", 0x25400, 0x02400, "MP_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x27800, 0x02800, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff"]),
            Ctrl("nf", "NF CtrlRAM", 0x2C800, 0x02DD0, "NF_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x315D0, 0x0149E, "VN_Ctrlram.bin"),
            Region("fw-config-backup", "FW Config Backup", TpFlashMapRegionKind.Other, 0x3B000, 0x00780, tags: ["backup", "postbuild"]),
            Region("project-id", "Project ID", TpFlashMapRegionKind.ProjectId, 0x3C000, 0x01000),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x3D000, 0x01000, tags: ["preserve"]),
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x3E000, 0x02000),
        ];
    }

    private static TpFlashMapRegion[] Nt51927Regions()
    {
        return
        [
            Ctrl("nf-master", "NF CtrlRAM (Master)", 0x16800, 0x00FD0, "NF_Ctrlram.bin"),
            Ctrl("normal-master", "Normal CtrlRAM (Master)", 0x177D0, 0x03000, "Normal_Ctrlram.bin"),
            Ctrl("mp-master", "MP CtrlRAM (Master)", 0x1A7D0, 0x02400, "MP_Ctrlram.bin"),
            Ctrl("vn-master", "VN CtrlRAM (Master)", 0x1CBD0, 0x01660, "VN_Ctrlram.bin"),
            Ctrl("nf-slave-r", "NF CtrlRAM (Slave R)", 0x1F800, 0x00FD0, "NF_Ctrlram.bin", TpFlashMapRegionVisibility.TwoChipAndAbove, ["slave"]),
            Ctrl("normal-slave-r", "Normal CtrlRAM (Slave R)", 0x207D0, 0x03000, "Normal_Ctrlram_R.bin", TpFlashMapRegionVisibility.TwoChipAndAbove, ["slave"]),
            Ctrl("mp-slave-r", "MP CtrlRAM (Slave R)", 0x237D0, 0x02400, "MP_Ctrlram_R.bin", TpFlashMapRegionVisibility.TwoChipAndAbove, ["slave"]),
            Ctrl("vn-slave-r", "VN CtrlRAM (Slave R)", 0x25BD0, 0x01660, "VN_Ctrlram.bin", TpFlashMapRegionVisibility.TwoChipAndAbove, ["slave"]),
            Ctrl("nf-slave-l", "NF CtrlRAM (Slave L)", 0x28800, 0x00FD0, "NF_Ctrlram.bin", TpFlashMapRegionVisibility.ThreeChipAndAbove, ["slave"]),
            Ctrl("normal-slave-l", "Normal CtrlRAM (Slave L)", 0x297D0, 0x03000, "Normal_Ctrlram_L.bin", TpFlashMapRegionVisibility.ThreeChipAndAbove, ["slave"]),
            Ctrl("mp-slave-l", "MP CtrlRAM (Slave L)", 0x2C7D0, 0x02400, "MP_Ctrlram_L.bin", TpFlashMapRegionVisibility.ThreeChipAndAbove, ["slave"]),
            Ctrl("vn-slave-l", "VN CtrlRAM (Slave L)", 0x2EBD0, 0x01660, "VN_Ctrlram.bin", TpFlashMapRegionVisibility.ThreeChipAndAbove, ["slave"]),
            Region("header-backup", "FW Header Backup", TpFlashMapRegionKind.Other, 0x32DC0, 0x00460, tags: ["backup", "postbuild"]),
            Region("fw-config-reg-backup", "FW Config/Reg Backup", TpFlashMapRegionKind.Other, 0x34000, 0x00800, tags: ["backup", "postbuild"]),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x3B000, 0x01000, tags: ["preserve"]),
            Region("dp-initial-code", "DP Region (Initial Code)", TpFlashMapRegionKind.Dp, 0x3C000, 0x04000),
            Region("dp-ldc-51928", "DP Region (LDC) - NT51928 only", TpFlashMapRegionKind.Dp, 0x40000, 0x22000),
        ];
    }

    private static TpFlashMapRegion[] Nt51929Regions()
    {
        return
        [
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x00000, 0x06000),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x06000, 0x01000, tags: ["preserve"]),
            Ctrl("nf", "NF CtrlRAM", 0x1FC00, 0x01F90, "NF_Ctrlram.bin"),
            Ctrl("normal", "Normal CtrlRAM", 0x21B90, 0x04A00, "Normal_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x26590, 0x01960, "VN_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x2D100, 0x08C00, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff"]),
        ];
    }

    private static TpFlashMapRegion[] Nt51930Regions()
    {
        return
        [
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x00000, 0x06000),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x06000, 0x01000, tags: ["preserve"]),
            Ctrl("normal", "Normal CtrlRAM", 0x21650, 0x02C00, "Normal_Ctrlram.bin"),
            Ctrl("mp", "MP CtrlRAM", 0x24250, 0x03400, "MP_Ctrlram.bin", tags: ["overview-only"]),
            Ctrl("vn", "VN CtrlRAM", 0x27650, 0x01960, "VN_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x2F200, 0x0FE00, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff", "<=13ic"]),
            Ctrl("nf", "NF CtrlRAM", 0x1FC00, 0x01A50, "NF_Ctrlram.bin"),
        ];
    }

    private static TpFlashMapRegion[] Nt51931Regions()
    {
        return
        [
            Ctrl("nf", "NF CtrlRAM", 0x16800, 0x00FD0, "NF_Ctrlram.bin"),
            Ctrl("normal", "Normal CtrlRAM", 0x177D0, 0x02800, "Normal_Ctrlram.bin"),
            Ctrl("mp", "MP CtrlRAM", 0x19FD0, 0x02400, "MP_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x1C3D0, 0x01660, "VN_Ctrlram.bin"),
            Ctrl("dlm", "DLM CtrlRAM", 0x22800, 0x17C00, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff", "dlm"]),
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x3E000, 0x02000),
        ];
    }

    private static TpFlashMapRegion[] Nt51950Regions()
    {
        return
        [
            Region("dp-initial-code", "DP Region (Initial Code)", TpFlashMapRegionKind.Dp, 0x00000, 0x0A000),
            Ctrl("nf", "NF CtrlRAM", 0x22C00, 0x02A10, "NF_Ctrlram.bin"),
            Ctrl("normal", "Normal CtrlRAM", 0x25610, 0x05C00, "Normal_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x2B210, 0x020FC, "VN_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x33200, 0x01400, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff"]),
            // 0x37000-0x37FFF is customer information. Merge/Replace must preserve it unless
            // a future explicit customer-info workflow authorizes writes to this region.
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x37000, 0x01000, tags: ["preserve"]),
            Region("dp-1-or-2ic", "DP Region (1IC or 2IC)", TpFlashMapRegionKind.Dp, 0x38000, 0x08000),
            Region("dp-2ic-only", "DP Region (2IC only)", TpFlashMapRegionKind.Dp, 0x40000, 0x0A000, TpFlashMapRegionVisibility.TwoChipAndAbove),
            Region("dp-ldc-51951", "DP Region (LDC) - NT51951", TpFlashMapRegionKind.Dp, 0x41000, 0x1C000, TpFlashMapRegionVisibility.TwoChipAndAbove),
        ];
    }

    private static TpFlashMapRegion Ctrl(
        string id,
        string displayName,
        long start,
        long length,
        string postbuildFileName,
        TpFlashMapRegionVisibility visibility = TpFlashMapRegionVisibility.Always,
        IReadOnlyList<string>? tags = null)
    {
        return Region(id, displayName, TpFlashMapRegionKind.CtrlRam, start, length, visibility, postbuildFileName, tags);
    }

    private static TpFlashMapRegion Region(
        string id,
        string displayName,
        TpFlashMapRegionKind kind,
        long start,
        long length,
        TpFlashMapRegionVisibility visibility = TpFlashMapRegionVisibility.Always,
        string? postbuildFileName = null,
        IReadOnlyList<string>? tags = null)
    {
        return new TpFlashMapRegion(
            id,
            displayName,
            kind,
            new ByteRange(start, length),
            visibility,
            postbuildFileName,
            tags);
    }
}
