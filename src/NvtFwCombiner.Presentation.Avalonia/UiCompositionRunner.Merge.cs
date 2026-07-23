using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Projects one Standard Merge memory display snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetStandardMergeMemoryDisplay(
        string icId,
        long? dpInputLength = null)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetStandardMergeMemoryDisplay(icId, dpInputLength);
        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(ToMemoryCoverageSegment)]);
    }

    /// <summary>Projects one compiled AB Merge memory display snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetAbMergeMemoryDisplay(
            string icId,
            string? abMergeTopologyToken = null)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetAbMergeMemoryDisplay(
            icId,
            abMergeTopologyToken);
        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(ToMemoryCoverageSegment)]);
    }

    /// <summary>Projects one General Merge memory display snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetGeneralMergeMemoryDisplay(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappings)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetGeneralMergeMemoryDisplay(outputLength, mappings);
        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(ToMemoryCoverageSegment)]);
    }

}
