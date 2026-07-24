using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets required AB input cards from a Bootstrap-owned symbolic topology token.</summary>
    public static IReadOnlyList<WorkbenchAbMergeInputSlot> GetAbMergeInputSlots(
        string icId,
        string? abMergeTopologyToken)
    {
        return GetAbMergeInputSlots(
            icId,
            AbMergeWorkbenchCompositionService.ResolveTopologySelection(abMergeTopologyToken));
    }

    /// <summary>Gets required AB input cards directly from the compiled profile contract.</summary>
    public static IReadOnlyList<WorkbenchAbMergeInputSlot> GetAbMergeInputSlots(
        string icId,
        TopologySelection? abMergeTopologySelection = null)
    {
        return WorkbenchAbMergeInputProjection.GetInputSlots(icId, abMergeTopologySelection);
    }

    /// <summary>Reads one selected AB file once and projects compiled health plus informational versions.</summary>
    public static WorkbenchAbMergeInputInspection InspectAbMergeInput(
        string icId,
        string addressSpaceId,
        string path,
        TopologySelection? abMergeTopologySelection = null)
    {
        return WorkbenchAbMergeInputProjection.Inspect(
            icId,
            addressSpaceId,
            TryReadFirmwareImage(path),
            abMergeTopologySelection);
    }

    /// <summary>Reads one selected AB file using a Bootstrap-owned symbolic topology token.</summary>
    public static WorkbenchAbMergeInputInspection InspectAbMergeInput(
        string icId,
        string addressSpaceId,
        string path,
        string? abMergeTopologyToken)
    {
        return InspectAbMergeInput(
            icId,
            addressSpaceId,
            path,
            AbMergeWorkbenchCompositionService.ResolveTopologySelection(abMergeTopologyToken));
    }

    internal static WorkbenchAbMergeInputInspection InspectAbMergeInput(
        string icId,
        string addressSpaceId,
        byte[]? image,
        TopologySelection? abMergeTopologySelection = null)
    {
        return WorkbenchAbMergeInputProjection.Inspect(
            icId,
            addressSpaceId,
            image,
            abMergeTopologySelection);
    }

    /// <summary>Gets user-facing AB final input ownership from the compiled profile and selected DP length.</summary>
    public static WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(
        string icId,
        TopologySelection? abMergeTopologySelection = null,
        long? dpInputLength = null)
    {
        if (dpInputLength.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpInputLength.Value);
        }

        if (!AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                icId,
                abMergeTopologySelection,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            string detail = issues.Count == 0 ? $"AB Merge is not available for {icId}." : FormatIssues(issues);
            return CreateMessageDisplay(
                detail,
                ("Profile", "No output", "Blocked", "No output", detail),
                ("No range", "AB Merge unavailable", detail, "#CBD5E1"));
        }

        ImageInitialization initialization = composition.Plan.OutputInitialization;
        long displayCapacity = dpInputLength ?? initialization.Capacity;
        FirmwareRegion[] tpCodeRegions =
        [
            .. composition.V2Details!.Provenance.ResolvedMap.ImageMap.Regions
                .Where(static region =>
                    region.Owner == FirmwareRegionOwner.Tp &&
                    region.Kind == FirmwareRegionKind.Code)
                .OrderBy(static region => region.Range.Start),
        ];
        if (tpCodeRegions.Length != 2)
        {
            const string detail = "The compiled AB map must declare exactly one TPA and one TPB code region.";
            return CreateMessageDisplay(
                detail,
                ("Profile", "No output", "Blocked", "No output", detail),
                ("No range", "AB Merge unavailable", detail, "#CBD5E1"));
        }

        ByteRange fullDpRange = new(0, displayCapacity);
        FirmwareRegion tpA = tpCodeRegions[0];
        FirmwareRegion tpB = tpCodeRegions[1];
        CoverageSegment[] coverage =
        [
            new(
                fullDpRange,
                "DP AB",
                "Use the selected DP AB container as the complete output base.",
                CoverageFill("DP AB"),
                false,
                WorkbenchMemoryCoverageRole.Standard),
        ];
        coverage = ApplyCoverageWrite(
            coverage,
            new CoverageSegment(
                tpA.Range,
                "TPA",
                "Overlay TPA at the fixed profile-declared TP A range.",
                CoverageFill("TPA"),
                false,
                WorkbenchMemoryCoverageRole.Standard));
        coverage = ApplyCoverageWrite(
            coverage,
            new CoverageSegment(
                tpB.Range,
                "TPB",
                "Overlay TPB at the fixed profile-declared TP B range.",
                CoverageFill("TPB"),
                false,
                WorkbenchMemoryCoverageRole.Standard));
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatDisplayRange(fullDpRange),
                "No output",
                "Copy",
                "DP AB",
                "Use the selected DP AB container length for the output memory layout."),
            new(
                FormatDisplayRange(tpA.Range),
                "DP AB",
                "Overlay",
                "TPA",
                "Overlay TPA at the fixed profile-declared TP A range."),
            new(
                FormatDisplayRange(tpB.Range),
                "DP AB",
                "Overlay",
                "TPB",
                "Overlay TPB at the fixed profile-declared TP B range."),
        ];

        return new WorkbenchMemoryDisplay(
            FormatFullRange(displayCapacity),
            rows,
            ToWorkbenchCoverageSegments(coverage, displayCapacity));
    }

    /// <summary>Gets AB output ownership from a Bootstrap-owned symbolic topology token.</summary>
    public static WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(
        string icId,
        string? abMergeTopologyToken,
        long? dpInputLength = null)
    {
        return GetAbMergeMemoryDisplay(
            icId,
            AbMergeWorkbenchCompositionService.ResolveTopologySelection(abMergeTopologyToken),
            dpInputLength);
    }

}
