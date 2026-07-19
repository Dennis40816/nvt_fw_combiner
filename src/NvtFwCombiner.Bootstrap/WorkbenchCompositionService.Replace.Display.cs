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
            return CreateMessageDisplay(
                "Not available",
                ("Policy", "Not available", "Blocked", "No target", $"{icId} {replaceMode} Replace is Not available under the current IC workflow policy."),
                coverage: null);
        }

        if (replaceMode == WorkbenchReplaceModes.Dp)
        {
            return CreateV2DpReplaceMemoryDisplay(icId, dpBaseLength) ??
                CreateMessageDisplay(
                    "No DP Replace profile",
                    ("Catalog", "No V2 profile", "Blocked", "No target", $"No trusted V2 DP Replace profile is registered for {icId}."),
                    coverage: null);
        }

        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = BuiltInTpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile);
        if (regions.Count == 0)
        {
            string detail = $"No TP Overview flash-map profile is available for {icId}.";
            return CreateMessageDisplay(
                "No flash-map profile",
                ("Catalog", "No flash-map row", "Blocked", "No target", detail),
                ("No range", "No profile", detail, "#CBD5E1"));
        }

        IReadOnlyList<WorkbenchMemoryMapRow> rows = replaceMode switch
        {
            WorkbenchReplaceModes.CtrlRam =>
            [
                .. BuiltInTpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile)
                    .OrderBy(region => region.Range.Start)
                    .Select(region => new WorkbenchMemoryMapRow(
                        FormatDisplayRange(region.Range),
                        "Base firmware",
                        "Replace + CRC",
                        region.PostbuildFileName ?? "CtrlRAM BIN",
                        $"{region.DisplayName} at {FormatDisplayRange(region.Range)} can use its own replacement BIN; the report shows the CRC/header refresh command.")),
            ],
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
