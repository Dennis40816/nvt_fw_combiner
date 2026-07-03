using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets structured Replace input slots for the selected mode and device context.</summary>
    public static IReadOnlyList<WorkbenchReplaceInputSlot> GetReplaceInputSlots(
        string icId,
        string number,
        string replaceMode)
    {
        return replaceMode switch
        {
            "DP" => GetDpReplaceInputSlots(icId),
            "CtrlRAM" => GetCtrlRamReplaceInputSlots(icId, number),
            _ => [],
        };
    }

    /// <summary>Gets the default output file name for a Replace build.</summary>
    public static string GetReplaceDefaultOutputFileName(string icId, string replaceMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);

        string normalizedIc = icId.ToLowerInvariant();
        return replaceMode switch
        {
            "DP" => $"{normalizedIc}-dp-replace.bin",
            "CtrlRAM" => $"{normalizedIc}-ctrlram-replace.bin",
            "General" => $"{normalizedIc}-general-replace.bin",
            _ => "nvt-fw-combiner-replace.bin",
        };
    }


    /// <summary>Gets readable memory-map rows for the selected Replace mode.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetReplaceMemoryMapRows(
        string icId,
        string number,
        string replaceMode)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        return regions.Count == 0
            ?
            [
                new WorkbenchMemoryMapRow(
                    "Catalog",
                    "No flash-map row",
                    "Blocked",
                    "No target",
                    $"No TP Overview flash-map profile is available for {icId}."),
            ]
            : replaceMode switch
            {
                "DP" => CreateDpReplaceRows(icId, regions),
                "CtrlRAM" => CreateCtrlRamReplaceRows(regions),
                "General" =>
                [
                    .. CreatePreserveRows(regions),
                    new WorkbenchMemoryMapRow(
                        "Runtime range",
                        "Base flash",
                        "Replace",
                        "General BIN",
                        "The selected explicit range must be approved by the compiled General Replace profile."),
                ],
                _ =>
                [
                    new WorkbenchMemoryMapRow(
                        "Mode",
                        "Unknown",
                        "Select",
                        "No target",
                        "Select DP, CtrlRAM, or General Replace."),
                ],
            };
    }

    /// <summary>Gets TP Overview address coverage text for the selected Replace context.</summary>
    public static string GetReplaceMemoryRangeLabel(string icId, string number)
    {
        return GetReplaceMemoryRangeLabel(icId, number, replaceMode: string.Empty);
    }

    /// <summary>Gets TP Overview address coverage text for the selected Replace context and mode.</summary>
    public static string GetReplaceMemoryRangeLabel(string icId, string number, string replaceMode)
    {
        if (replaceMode == "DP" && IsNt51950Or51(icId))
        {
            return FormatFullRange(Nt51950DpContainerLength);
        }

        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, ToIcNumberSelection(number));
        return regions.Count == 0
            ? "No flash-map profile"
            : FormatFullRange(regions.Max(region => region.Range.EndExclusive));
    }

    /// <summary>Gets final visual coverage segments for the selected Replace view.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetReplaceCoverageSegments(
        string icId,
        string number,
        string replaceMode)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        if (regions.Count == 0)
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "No range",
                    "No profile",
                    $"No TP Overview flash-map profile is available for {icId}.",
                    "#CBD5E1",
                    280,
                    false),
            ];
        }

        long capacity = replaceMode == "DP" && IsNt51950Or51(icId)
            ? Nt51950DpContainerLength
            : regions.Max(region => region.Range.EndExclusive);
        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                "Base flash",
                "Kept from the original base firmware unless a replacement covers it.",
                "#E2E8F0",
                false),
        ];

        if (replaceMode == "DP" && IsNt51950Or51(icId))
        {
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    new ByteRange(0, Nt51950DpContainerLength),
                    "Changed DP BIN",
                    $"Replacement DP fills {FormatDisplayRange(new ByteRange(0, Nt51950DpContainerLength))}; shorter inputs are padded by profile policy.",
                    "#2563EB",
                    true));
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    Nt51950TpRestoreRange,
                    "Restored TP",
                    $"Original TP FW at {FormatDisplayRange(Nt51950TpRestoreRange)} is copied back from the base firmware.",
                    "#64748B",
                    false));
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    Nt51950CustomerInfoPreserveRange,
                    "Preserved customer info",
                    $"Customer information at {FormatDisplayRange(Nt51950CustomerInfoPreserveRange)} is copied back from the base firmware.",
                    "#94A3B8",
                    false));
            return ToWorkbenchCoverageSegments(segments, capacity);
        }

        foreach (TpFlashMapRegion region in regions
            .Where(IsPreservedRegion)
            .OrderBy(region => region.Range.Start))
        {
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    region.Range,
                    "Preserve",
                    $"{region.DisplayName} stays from the original base firmware.",
                    "#94A3B8",
                    false));
        }

        IEnumerable<TpFlashMapRegion> replacementRegions = replaceMode switch
        {
            "DP" => GetDpReplaceRegions(icId, regions),
            "CtrlRAM" => regions.Where(region => region.Kind == TpFlashMapRegionKind.CtrlRam),
            _ => [],
        };

        foreach (TpFlashMapRegion region in replacementRegions.OrderBy(region => region.Range.Start))
        {
            string label = replaceMode switch
            {
                "DP" => IsLdRegion(region) ? "Changed LDC BIN" : "Changed DP BIN",
                "CtrlRAM" => region.DisplayName,
                _ => "Replacement BIN",
            };
            string detail = replaceMode == "CtrlRAM"
                ? $"{region.DisplayName} can be replaced here. Empty input keeps the original firmware; Preview lists the CRC/header refresh command."
                : $"{region.DisplayName}; {ActionSummaryForReplaceMode(replaceMode)}";
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    region.Range,
                    label,
                    detail,
                    CoverageFill(label),
                    true));
        }

        return ToWorkbenchCoverageSegments(segments, capacity);
    }


    /// <summary>Runs a Replace preview or build through the workbench Replace facade.</summary>
    public static async ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        ArgumentNullException.ThrowIfNull(slotPaths);

        return replaceMode switch
        {
            "DP" when IsNt51950Or51(icId) => await RunNt51950DpReplaceAsync(
                icId,
                slotPaths,
                build,
                outputPath,
                cancellationToken).ConfigureAwait(false),
            "DP" => CreatePlanningRunResult(
                icId,
                number,
                replaceMode,
                slotPaths,
                build,
                "replace.dp.profile-pending",
                "DP Replace output is enabled only for NT51950/NT51951 until per-IC DP source mapping and golden evidence are approved."),
            "CtrlRAM" => await RunCtrlRamReplaceAsync(
                icId,
                number,
                slotPaths,
                build,
                outputPath,
                cancellationToken).ConfigureAwait(false),
            "General" => CreatePlanningRunResult(
                icId,
                number,
                replaceMode,
                slotPaths,
                build,
                "replace.general.profile-pending",
                "General Replace UI authoring is available, but production build still needs compiled explicit mappings wired to the workbench runner."),
            _ => CreatePlanningRunResult(
                icId,
                number,
                replaceMode,
                slotPaths,
                build,
                "replace.mode.unknown",
                $"Unknown Replace mode '{replaceMode}'."),
        };
    }


}
