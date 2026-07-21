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
        long firmwareConfigPrimaryStart,
        long tpPrefixLength,
        IEnumerable<long> fullFlashCapacities,
        string baseShapeEvidence,
        IEnumerable<TpFlashMapRegion> regions,
        string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(overviewSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tpPrefixLength);
        ArgumentNullException.ThrowIfNull(fullFlashCapacities);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseShapeEvidence);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        long[] capacities = [.. fullFlashCapacities.Distinct().Order()];
        if (capacities.Length == 0 || capacities.Any(capacity => capacity < tpPrefixLength))
        {
            throw new ArgumentException(
                "Flash-map profile must declare full-Flash capacities no shorter than its TP prefix.",
                nameof(fullFlashCapacities));
        }
        _regions = [.. regions];
        if (_regions.Length == 0)
        {
            throw new ArgumentException("Flash-map profile must contain at least one region.", nameof(regions));
        }

        IcId = icId;
        OverviewSource = overviewSource;
        FirmwareConfigPrimaryStart = firmwareConfigPrimaryStart;
        TpPrefixLength = tpPrefixLength;
        FullFlashCapacities = Array.AsReadOnly(capacities);
        BaseShapeEvidence = baseShapeEvidence;
        Evidence = evidence;
    }

    /// <summary>Selectable NT-prefixed IC id.</summary>
    public string IcId { get; }

    /// <summary>TP Overview source section label.</summary>
    public string OverviewSource { get; }

    /// <summary>
    /// Primary FLASHMAP_FW_REGISTER address from TP Overview. Runtime mutation requires a separately declared
    /// processor propagation capability and an exact source/Backup metadata cross-check.
    /// </summary>
    public long FirmwareConfigPrimaryStart { get; }

    /// <summary>Zero-based TP work prefix passed to the CtrlRAM Postbuild processor.</summary>
    public long TpPrefixLength { get; }

    /// <summary>Declared full-Flash container capacities that preserve bytes after the TP prefix.</summary>
    public IReadOnlyList<long> FullFlashCapacities { get; }

    /// <summary>Evidence scope for the TP/full-Flash artifact shapes; it does not imply runtime promotion.</summary>
    public string BaseShapeEvidence { get; }

    /// <summary>Reference evidence used to create this profile.</summary>
    public string Evidence { get; }

    /// <summary>All documented regions in stable TP Overview order.</summary>
    public IReadOnlyList<TpFlashMapRegion> Regions => _regions;
}
