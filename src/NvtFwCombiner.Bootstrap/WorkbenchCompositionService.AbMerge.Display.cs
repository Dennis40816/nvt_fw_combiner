using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets required AB input cards directly from the compiled profile contract.</summary>
    public static IReadOnlyList<WorkbenchAbMergeInputSlot> GetAbMergeInputSlots(string icId)
    {
        return WorkbenchAbMergeInputProjection.GetInputSlots(icId);
    }

    /// <summary>Reads one selected AB file once and projects compiled health plus informational versions.</summary>
    public static WorkbenchAbMergeInputInspection InspectAbMergeInput(
        string icId,
        string addressSpaceId,
        string path)
    {
        return WorkbenchAbMergeInputProjection.Inspect(
            icId,
            addressSpaceId,
            TryReadFirmwareImage(path));
    }

    internal static WorkbenchAbMergeInputInspection InspectAbMergeInput(
        string icId,
        string addressSpaceId,
        byte[]? image)
    {
        return WorkbenchAbMergeInputProjection.Inspect(icId, addressSpaceId, image);
    }

    /// <summary>Gets AB final output ownership directly from the compiled plan.</summary>
    public static WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(string icId)
    {
        if (!AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                icId,
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
            rows.Add(new WorkbenchMemoryMapRow(
                $"{targetSpace} {FormatDisplayRange(operation.TargetRange)}",
                targetSpace,
                ActionLabel(operation.Kind),
                sourceSpace,
                $"Sequence {operation.Sequence}: {operation.Reason}"));
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

}
