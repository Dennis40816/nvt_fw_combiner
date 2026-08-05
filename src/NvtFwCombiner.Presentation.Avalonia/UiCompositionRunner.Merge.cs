using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Projects one Standard Merge memory display snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetStandardMergeMemoryDisplay(
        PresentationCompositionServices services,
        string icId,
        long? dpInputLength = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        WorkbenchMemoryDisplay display =
            services.Memory.GetStandardMergeMemoryDisplay(icId, dpInputLength);
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
        PresentationCompositionServices services,
        string icId,
        string outputLength,
        string? outputFillByte)
    {
        ArgumentNullException.ThrowIfNull(services);
        WorkbenchMemoryDisplay display =
            services.Memory.GetGeneralMergeMemoryDisplay(
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
        PresentationCompositionServices services,
        string icId,
            string? abMergeTopologyToken = null,
            long? dpInputLength = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        WorkbenchMemoryDisplay display = services.Memory.GetAbMergeMemoryDisplay(
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
        PresentationCompositionServices services,
        string icId,
        WorkbenchGeneralMergeInitializer initializer,
        IReadOnlyList<AuthoringMappingState> states,
        GeneralAuthoringAdmissionResult? admission)
    {
        ArgumentNullException.ThrowIfNull(services);
        WorkbenchMemoryDisplay display =
            services.Memory.GetGeneralMergeMemoryDisplay(
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
