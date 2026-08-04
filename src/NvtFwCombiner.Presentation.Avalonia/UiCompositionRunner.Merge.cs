using NvtFwCombiner.Application.Authoring;
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
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment))]);
    }

    /// <summary>Projects editable General Merge initializer text, including typed validation failures.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetGeneralMergeMemoryDisplay(
        string icId,
        string outputLength,
        string? outputFillByte)
    {
        WorkbenchMemoryDisplay display =
            WorkbenchCompositionService.GetGeneralMergeMemoryDisplay(
                icId,
                outputLength,
                outputFillByte);
        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment))]);
    }

    /// <summary>Projects one compiled AB Merge memory display snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetAbMergeMemoryDisplay(
            string icId,
            string? abMergeTopologyToken = null,
            long? dpInputLength = null)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetAbMergeMemoryDisplay(
            icId,
            abMergeTopologyToken,
            dpInputLength);
        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment))]);
    }

    /// <summary>Projects one parsed General Merge authoring snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetGeneralMergeMemoryDisplay(
        string icId,
        WorkbenchGeneralMergeInitializer initializer,
        IReadOnlyList<AuthoringMappingState> states,
        GeneralAuthoringAdmissionResult? admission)
    {
        WorkbenchMemoryDisplay display =
            WorkbenchCompositionService.GetGeneralMergeMemoryDisplay(
                icId,
                initializer,
                states,
                admission);
        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment))]);
    }

}
