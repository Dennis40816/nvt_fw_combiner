using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Returns true when the selected IC has a built-in standard merge profile.</summary>
    public static bool IsStandardMergeSupported(string icId)
    {
        return WorkbenchCompositionService.IsStandardMergeSupported(icId);
    }

    /// <summary>Gets the built-in standard merge profile id for the selected IC, if any.</summary>
    public static string? GetStandardMergeProfileId(string icId)
    {
        return WorkbenchCompositionService.GetStandardMergeProfileId(icId);
    }

    /// <summary>Gets required standard merge input address spaces for the selected IC.</summary>
    public static IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId)
    {
        return WorkbenchCompositionService.GetStandardMergeRequiredAddressSpaces(icId);
    }

    /// <summary>Gets the profile-owned default Standard Merge output file name for the selected IC.</summary>
    public static string GetStandardMergeDefaultOutputFileName(string icId)
    {
        return WorkbenchCompositionService.GetStandardMergeDefaultOutputFileName(icId);
    }

    /// <summary>Gets the default General Merge output length text for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputLength(string icId)
    {
        return WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(icId);
    }

    /// <summary>Gets the default General Merge output file name for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputFileName(string icId)
    {
        return WorkbenchCompositionService.GetGeneralMergeDefaultOutputFileName(icId);
    }

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

    /// <summary>Gets output address coverage text for the selected Standard Merge profile.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId, long? dpInputLength = null)
    {
        return WorkbenchCompositionService.GetStandardMergeMemoryRangeLabel(icId, dpInputLength);
    }

    /// <summary>Gets catalog-backed Standard Merge policy text.</summary>
    public static string GetStandardMergePolicySummary(string icId)
    {
        return WorkbenchCompositionService.GetStandardMergePolicySummary(icId);
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

    /// <summary>Runs Standard Merge preview or build through the application core.</summary>
    public static ValueTask<WorkbenchRunResult> RunStandardMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return WorkbenchCompositionService.RunStandardMergeAsync(icId, slotPaths, build, cancellationToken, outputPath);
    }

    /// <summary>Runs General Merge preview or build through the Bootstrap workbench facade.</summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeAsync(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappings,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return WorkbenchCompositionService.RunGeneralMergeAsync(
            icId,
            outputLength,
            mappings,
            build,
            cancellationToken,
            outputPath);
    }
}
