using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralReplaceRunContext(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs,
        bool build,
        out GeneralReplaceRunContext? context,
        out WorkbenchRunResult? failure)
    {
        Dictionary<string, string> reportSlotPaths = CreateGeneralReplaceReportSlotPaths(slotPaths, mappingInputs);
        IcNumberSelection selection = ToIcNumberSelection(number);

        if (!slotPaths.TryGetValue("replace-base", out string? basePath) ||
            string.IsNullOrWhiteSpace(basePath))
        {
            context = null;
            failure = CreatePlanningRunResult(
                icId,
                number,
                "General",
                reportSlotPaths,
                build,
                "ui.input.missing",
                "Base flash BIN is required before General Replace can compile explicit mappings.");
            return false;
        }

        string fullBasePath = Path.GetFullPath(basePath);
        if (!File.Exists(fullBasePath))
        {
            context = null;
            failure = CreatePlanningRunResult(
                icId,
                number,
                "General",
                reportSlotPaths,
                build,
                "input.artifact.read-failed",
                "Base flash BIN path does not exist.");
            return false;
        }

        WorkbenchGeneralReplaceMappingInput[] selectedMappings =
        [
            .. mappingInputs.Where(mapping => !string.IsNullOrWhiteSpace(mapping.FilePath)),
        ];
        if (selectedMappings.Length == 0)
        {
            context = null;
            failure = CreatePlanningRunResult(
                icId,
                number,
                "General",
                reportSlotPaths,
                build,
                "ui.input.missing",
                "At least one General Replace mapping row must select a replacement BIN.");
            return false;
        }

        long capacity = new FileInfo(fullBasePath).Length;
        if (capacity <= 0)
        {
            context = null;
            failure = CreatePlanningRunResult(
                icId,
                number,
                "General",
                reportSlotPaths,
                build,
                "input.address-space.length-mismatch",
                "Base flash BIN must not be empty.");
            return false;
        }

        context = new GeneralReplaceRunContext(
            selection,
            reportSlotPaths,
            fullBasePath,
            capacity,
            selectedMappings);
        failure = null;
        return true;
    }

    private sealed record GeneralReplaceRunContext(
        IcNumberSelection Selection,
        IReadOnlyDictionary<string, string> ReportSlotPaths,
        string BasePath,
        long Capacity,
        WorkbenchGeneralReplaceMappingInput[] SelectedMappings);
}
