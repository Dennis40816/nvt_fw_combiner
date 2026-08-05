using NvtFwCombiner.Application.ExternalTools;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionMemoryProjection
{
    /// <summary>Gets current profile-derived DP Replace Reference FlashCode capacities.</summary>
    public static string? GetDpReplaceReferenceCapacityLabel(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return CanonicalCapabilityProjection.TryResolveBuiltInV2DpReplaceDisplay(
                icId,
                baseCapacity: null,
                out BuiltInV2DpReplaceDisplay? display) &&
            display.Issues.Count == 0
                ? BuiltInV2Bundle.FormatCapacities(display.SupportedBaseCapacities)
                : null;
    }

    /// <summary>Gets structured Replace input slots for the selected mode and device context.</summary>
    public static IReadOnlyList<WorkbenchReplaceInputSlot> GetReplaceInputSlots(
        string icId,
        string number,
        string replaceMode,
        string? basePath = null)
    {
        return CanonicalCapabilityProjection.GetReplaceWorkflowId(replaceMode) is not null &&
            !CanonicalCapabilityProjection.IsReplaceWorkflowAvailable(icId, replaceMode)
                ? []
                : replaceMode switch
                {
                    WorkbenchReplaceModes.Dp => DpReplaceInputSlotProjection.GetInputSlots(icId),
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
            FirmwareInspectionAdapter.TryResolvePostbuildProfileFromBasePathForDisplay(icId, ctrlRamBasePath, out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        return CreateReplaceMemoryDisplay(icId, number, replaceMode, dpBaseLength, postbuildProfile);
    }

    internal static WorkbenchMemoryDisplay CreateReplaceMemoryDisplay(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        if (CanonicalCapabilityProjection.GetReplaceWorkflowId(replaceMode) is not null &&
            !CanonicalCapabilityProjection.IsReplaceWorkflowAvailable(icId, replaceMode))
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

        IcNumberSelection selection = WorkbenchIcNumberSelections.FromNumberToken(number);
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
                ("No range", "No profile", detail));
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
