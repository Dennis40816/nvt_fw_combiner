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

    /// <summary>Gets AB final output ownership directly from the compiled plan.</summary>
    public static WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(
        string icId,
        TopologySelection? abMergeTopologySelection = null)
    {
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
        CoverageSegment[] coverage =
        [
            new(
                new ByteRange(0, initialization.Capacity),
                $"Blank 0x{initialization.FillByte:X2}",
                "No AB input writes this output range.",
                "#CBD5E1",
                false,
                WorkbenchMemoryCoverageRole.Standard),
        ];
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatFullRange(initialization.Capacity),
                "No output",
                "Initialize",
                $"Blank output 0x{initialization.FillByte:X2}",
                "Initialize the compiled AB output before applying the ordered profile operations."),
        ];
        foreach (CompositionOperation operation in composition.Plan.OrderedOperations)
        {
            string targetSpace = AddressSpaceLabel(operation.TargetSpaceId);
            string sourceSpace = operation.SourceSpaceId is null
                ? operation.Kind.ToString()
                : AddressSpaceLabel(operation.SourceSpaceId);
            ExternalProcessorInvocation? invocation = operation.ExternalProcessorInvocation;
            bool isPostbuild = operation.Kind == CompositionOperationKind.RunExternalProcessor &&
                invocation is not null;
            string rangeLabel = isPostbuild
                ? $"Staging/read scope: {targetSpace} {FormatDisplayRange(operation.TargetRange)}"
                : $"{targetSpace} {FormatDisplayRange(operation.TargetRange)}";
            string detail = isPostbuild
                ? $"Sequence {operation.Sequence}: {operation.Reason} Allowed writes: " +
                  $"{string.Join(", ", invocation!.AllowedWriteRanges.Select(FormatDisplayRange))}."
                : $"Sequence {operation.Sequence}: {operation.Reason}";
            rows.Add(new WorkbenchMemoryMapRow(
                rangeLabel,
                targetSpace,
                ActionLabel(operation.Kind),
                sourceSpace,
                detail));
            if (!StringComparer.Ordinal.Equals(operation.TargetSpaceId, CompositionAddressSpaceIds.OutputImage))
            {
                continue;
            }

            coverage = ApplyCoverageWrite(
                coverage,
                new CoverageSegment(
                    operation.TargetRange,
                    sourceSpace,
                    operation.Reason,
                    CoverageFill(sourceSpace),
                    false,
                    WorkbenchMemoryCoverageRole.Standard));
        }

        return new WorkbenchMemoryDisplay(
            FormatFullRange(initialization.Capacity),
            rows,
            ToWorkbenchCoverageSegments(coverage, initialization.Capacity));
    }

    /// <summary>Gets AB output ownership from a Bootstrap-owned symbolic topology token.</summary>
    public static WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(
        string icId,
        string? abMergeTopologyToken)
    {
        return GetAbMergeMemoryDisplay(
            icId,
            AbMergeWorkbenchCompositionService.ResolveTopologySelection(abMergeTopologyToken));
    }

}
