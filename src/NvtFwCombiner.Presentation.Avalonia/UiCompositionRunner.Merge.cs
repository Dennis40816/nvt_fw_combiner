using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<MemoryMapRowViewModel> GetStandardMergeMemoryMapRows(
        string icId,
        long? dpInputLength = null)
    {
        return
        [
            .. WorkbenchCompositionService.GetStandardMergeMemoryMapRows(icId, dpInputLength)
                .Select(ToMemoryMapRow),
        ];
    }

    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<MemoryCoverageSegmentViewModel> GetStandardMergeCoverageSegments(
        string icId,
        long? dpInputLength = null)
    {
        return
        [
            .. WorkbenchCompositionService.GetStandardMergeCoverageSegments(icId, dpInputLength)
                .Select(segment => new MemoryCoverageSegmentViewModel(
                    segment.RangeLabel,
                    segment.SourceLabel,
                    segment.Detail,
                    segment.Fill,
                    segment.BarWidth)),
        ];
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
                .Select(segment => new MemoryCoverageSegmentViewModel(
                    segment.RangeLabel,
                    segment.SourceLabel,
                    segment.Detail,
                    segment.Fill,
                    segment.BarWidth,
                    segment.IsChanged)),
        ];
    }

}
