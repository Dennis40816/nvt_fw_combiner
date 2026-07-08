using System.Globalization;
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
public static partial class TpFlashMapCatalog
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
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
        return profiles.Any(profile => profile.BranchRules.Values.Contains(LegacyCombinerPostbuildBranch.CascadeExtended))
            ? GetExtendedNumberChoices(profiles)
            : !PostbuildProfilesByIc.TryGetValue(icId, out LegacyCombinerPostbuildProfile? profile)
            ? ["single"]
            : profile.TwoChipCommands is not null || profile.ThreeChipCommands is not null
            ? ["single", "2", "3"]
            : ["single", "cascade"];
    }

    private static IReadOnlyList<string> GetExtendedNumberChoices(IEnumerable<LegacyCombinerPostbuildProfile> profiles)
    {
        return
        [
            "single",
            .. profiles
                .SelectMany(profile => profile.BranchRules.Keys)
                .Where(token => int.TryParse(token, out int value) && value > 1)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(token => int.Parse(token, CultureInfo.InvariantCulture)),
        ];
    }

    /// <summary>Gets TP Overview CtrlRAM regions visible for the selected IC and IC-count context.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetCtrlRamRegions(
        string icId,
        IcNumberSelection? selection)
    {
        return GetRegions(icId, selection, TpFlashMapRegionKind.CtrlRam);
    }

    /// <summary>Gets TP Overview CtrlRAM regions adjusted to the selected postbuild category.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetCtrlRamRegions(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        return GetRegions(icId, selection, postbuildProfile, TpFlashMapRegionKind.CtrlRam);
    }

    /// <summary>Gets TP Overview regions visible for the selected IC, IC-count context, and optional kind.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetRegions(
        string icId,
        IcNumberSelection? selection,
        TpFlashMapRegionKind? kind = null)
    {
        return GetRegions(icId, selection, postbuildProfile: null, kind);
    }

    /// <summary>Gets TP Overview regions adjusted to the selected postbuild category.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetRegions(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        TpFlashMapRegionKind? kind = null)
    {
        if (!ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? profile))
        {
            return [];
        }

        int? count = TryGetNumericCount(selection);
        bool isSingle = IsSingle(selection, count);
        return [
            .. ApplyPostbuildRangeOverrides(
                    profile.Regions
                        .Where(region => kind is null || region.Kind == kind)
                        .Where(region => IsVisible(region.Visibility, isSingle, count)),
                    postbuildProfile,
                    selection)
                .Where(region => kind is null || region.Kind == kind)
        ];
    }

    private static TpFlashMapRegion[] ApplyPostbuildRangeOverrides(
        IEnumerable<TpFlashMapRegion> regions,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        IcNumberSelection? selection)
    {
        TpFlashMapRegion[] visibleRegions =
        [
            .. regions,
        ];
        if (postbuildProfile is null)
        {
            return visibleRegions;
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            postbuildProfile,
            selection);
        IReadOnlyList<LegacyCombinerBlockArgument> blocks =
        [
            .. plan.Commands.SelectMany(command => command.Blocks),
        ];
        return [
            .. visibleRegions.Select(region => TryResolvePostbuildRange(region, blocks, out ByteRange range)
                ? new TpFlashMapRegion(
                    region.RegionId,
                    region.DisplayName,
                    region.Kind,
                    range,
                    region.Visibility,
                    region.PostbuildFileName,
                    region.Tags)
                : region),
        ];
    }

    private static bool TryResolvePostbuildRange(
        TpFlashMapRegion region,
        IReadOnlyList<LegacyCombinerBlockArgument> blocks,
        out ByteRange range)
    {
        LegacyCombinerBlockArgument[] candidates =
        [
            .. blocks.Where(block => IsPostbuildRangeOverrideCandidate(region, block)),
        ];
        if (candidates.Length == 0)
        {
            range = default;
            return false;
        }

        long start = candidates.Min(block => block.FirmwareRange.Start);
        long endExclusive = candidates.Max(block => block.FirmwareRange.EndExclusive);
        range = ByteRange.FromStartEndExclusive(start, endExclusive);
        return true;
    }

    private static bool IsPostbuildRangeOverrideCandidate(
        TpFlashMapRegion region,
        LegacyCombinerBlockArgument block)
    {
        return region.Range.Overlaps(block.FirmwareRange) &&
            (block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile
            ? string.Equals(region.PostbuildFileName, block.SourceFileName, StringComparison.Ordinal)
            : region.RegionId.Contains("fw-config", StringComparison.OrdinalIgnoreCase) &&
            block.BlockId.Contains("fw-config", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets visible CtrlRAM regions that are consumed by the selected postbuild command plan.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetPostbuildMappedCtrlRamRegions(
        string icId,
        IcNumberSelection? selection)
    {
        return !PostbuildProfilesByIc.TryGetValue(icId, out LegacyCombinerPostbuildProfile? postbuildProfile)
            ? []
            : GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile);
    }

    /// <summary>Gets visible CtrlRAM regions that are consumed by a selected postbuild command plan.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetPostbuildMappedCtrlRamRegions(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        if (!ProfilesByIc.ContainsKey(icId) || postbuildProfile is null)
        {
            return [];
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            postbuildProfile,
            selection);
        IReadOnlyList<LegacyCombinerBlockArgument> blocks = LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan);
        return [
            .. GetCtrlRamRegions(icId, selection, postbuildProfile)
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

}
