using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets structured Replace input slots for the selected mode and device context.</summary>
    public static IReadOnlyList<WorkbenchReplaceInputSlot> GetReplaceInputSlots(
        string icId,
        string number,
        string replaceMode,
        string? basePath = null)
    {
        return GetReplaceWorkflowId(replaceMode) is not null &&
            !IsReplaceWorkflowSupported(icId, replaceMode)
                ? []
                : replaceMode switch
                {
                    WorkbenchReplaceModes.Dp => GetDpReplaceInputSlots(icId),
                    WorkbenchReplaceModes.CtrlRam => GetCtrlRamReplaceInputSlots(icId, number, basePath),
                    _ => [],
                };
    }

    /// <summary>Gets one coherent Replace range, row, and coverage snapshot.</summary>
    public static WorkbenchMemoryDisplay GetReplaceMemoryDisplay(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        string? ctrlRamBasePath = null)
    {
        LegacyCombinerPostbuildProfile? postbuildProfile = replaceMode == WorkbenchReplaceModes.CtrlRam &&
            TryResolvePostbuildProfileForDisplay(icId, ctrlRamBasePath, out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        return CreateReplaceMemoryDisplay(icId, number, replaceMode, dpBaseLength, postbuildProfile);
    }

    private static WorkbenchMemoryDisplay CreateReplaceMemoryDisplay(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        if (GetReplaceWorkflowId(replaceMode) is not null &&
            !IsReplaceWorkflowSupported(icId, replaceMode))
        {
            return new WorkbenchMemoryDisplay(
                "Not available",
                [
                    new WorkbenchMemoryMapRow(
                        "Policy",
                        "Not available",
                        "Blocked",
                        "No target",
                        $"{icId} {replaceMode} Replace is Not available under the current IC workflow policy."),
                ],
                []);
        }

        if (replaceMode == WorkbenchReplaceModes.Dp &&
            CreateV2DpReplaceMemoryDisplay(icId, dpBaseLength) is { } v2Display)
        {
            return v2Display;
        }

        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = BuiltInTpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile);
        if (regions.Count == 0)
        {
            return new WorkbenchMemoryDisplay(
                "No flash-map profile",
                [
                    new WorkbenchMemoryMapRow(
                        "Catalog",
                        "No flash-map row",
                        "Blocked",
                        "No target",
                        $"No TP Overview flash-map profile is available for {icId}."),
                ],
                [
                    new WorkbenchMemoryCoverageSegment(
                        "No range",
                        "No profile",
                        $"No TP Overview flash-map profile is available for {icId}.",
                        "#CBD5E1",
                        280,
                        false),
                ]);
        }

        IReadOnlyList<WorkbenchMemoryMapRow> rows = replaceMode switch
        {
            WorkbenchReplaceModes.Dp => CreateDpReplaceRows(icId, regions),
            WorkbenchReplaceModes.CtrlRam => CreateCtrlRamReplaceRows(
                BuiltInTpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile)),
            WorkbenchReplaceModes.General =>
            [
                new WorkbenchMemoryMapRow(
                    "Runtime range",
                    "Base flash",
                    "Replace",
                    "General BIN",
                    "The selected explicit range must be approved by the compiled General Replace profile; TP ranges require Combiner CRC/header refresh."),
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
        return new WorkbenchMemoryDisplay(
            FormatFullRange(regions.Max(region => region.Range.EndExclusive)),
            rows,
            CreateReplaceCoverageSegments(icId, replaceMode, selection, postbuildProfile, regions));
    }

}
