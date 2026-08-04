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

    /// <summary>Gets user-facing AB final input ownership from the compiled profile and selected DP length.</summary>
    public static WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(
        string icId,
        TopologySelection? abMergeTopologySelection = null,
        long? dpInputLength = null)
    {
        if (dpInputLength is < 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(dpInputLength.Value);
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
        bool selectedDpLengthMatchesCompiledCapacity =
            dpInputLength is > 0 && dpInputLength.Value == initialization.Capacity;
        long displayCapacity = selectedDpLengthMatchesCompiledCapacity
            ? dpInputLength!.Value
            : initialization.Capacity;
        string dpDetail = dpInputLength is > 0 && !selectedDpLengthMatchesCompiledCapacity
            ? $"Selected DP AB length 0x{dpInputLength.Value:X} does not match the compiled " +
                $"0x{initialization.Capacity:X} layout; Memory coverage uses the compiled capacity."
            : "Use the selected DP AB container length for the output memory layout.";
        bool transformsTpB = composition.Plan.OrderedOperations.Any(static operation =>
            operation.Kind == CompositionOperationKind.TransformScalar);
        bool postbuildsTpB = composition.Plan.OrderedOperations.Any(static operation =>
            operation.Kind == CompositionOperationKind.RunExternalProcessor);
        string tpBAction = (transformsTpB, postbuildsTpB) switch
        {
            (true, true) => "Transform + Overlay + Postbuild",
            (true, false) => "Transform + Overlay",
            (false, true) => "Overlay + Postbuild",
            _ => "Overlay",
        };
        string tpBDetail = (transformsTpB, postbuildsTpB) switch
        {
            (true, true) =>
                "Transform TPB fields, overlay TPB at the fixed profile-declared TP B range, " +
                "then apply the profile-declared postbuild effects.",
            (true, false) =>
                "Transform TPB fields, then overlay TPB at the fixed profile-declared TP B range.",
            (false, true) =>
                "Overlay TPB at the fixed profile-declared TP B range, then apply the " +
                "profile-declared postbuild effects.",
            _ => "Overlay TPB at the fixed profile-declared TP B range.",
        };
        FirmwareRegion[] tpCodeRegions =
        [
            .. composition.V2Details.Provenance.ResolvedMap.ImageMap.Regions
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
                tpBDetail,
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
                dpDetail),
            new(
                FormatDisplayRange(tpA.Range),
                "DP AB",
                "Overlay",
                "TPA",
                "Overlay TPA at the fixed profile-declared TP A range."),
            new(
                FormatDisplayRange(tpB.Range),
                "DP AB",
                tpBAction,
                "TPB",
                tpBDetail),
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
