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

    /// <summary>Gets readable memory-map rows for the selected General Merge authoring state.</summary>
    public static IReadOnlyList<MemoryMapRowViewModel> GetGeneralMergeMemoryMapRows(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappings)
    {
        return
        [
            .. WorkbenchCompositionService.GetGeneralMergeMemoryMapRows(outputLength, mappings)
                .Select(ToMemoryMapRow),
        ];
    }

    /// <summary>Gets output address coverage text for the selected General Merge output length.</summary>
    public static string GetGeneralMergeMemoryRangeLabel(string outputLength)
    {
        return WorkbenchCompositionService.GetGeneralMergeMemoryRangeLabel(outputLength);
    }

    /// <summary>Gets visual coverage segments for the selected General Merge authoring state.</summary>
    public static IReadOnlyList<MemoryCoverageSegmentViewModel> GetGeneralMergeCoverageSegments(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappings)
    {
        return
        [
            .. WorkbenchCompositionService.GetGeneralMergeCoverageSegments(outputLength, mappings)
                .Select(ToMemoryCoverageSegment),
        ];
    }

}
