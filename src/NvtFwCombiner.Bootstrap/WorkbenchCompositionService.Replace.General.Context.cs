using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralReplaceRunContext(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patchInputs,
        bool build,
        out GeneralReplaceRunContext? context,
        out WorkbenchRunResult? failure)
    {
        Dictionary<string, string> reportSlotPaths = CreateGeneralReplaceReportSlotPaths(slotPaths, mappingInputs);
        IcNumberSelection selection = ToIcNumberSelection(number);

        if (!TpFlashMapCatalog.IsNumberSelectionSupported(icId, selection))
        {
            context = null;
            failure = CreatePlanningRunResult(
                icId,
                number,
                WorkbenchReplaceModes.General,
                reportSlotPaths,
                build,
                WorkbenchIssueCodes.ReplaceGeneralIcNumberUnsupported,
                $"IC number selection '{number}' is not supported for {icId} General Replace.");
            return false;
        }

        if (!slotPaths.TryGetValue(WorkbenchSlotIds.ReplaceBase, out string? basePath) ||
            string.IsNullOrWhiteSpace(basePath))
        {
            context = null;
            failure = CreatePlanningRunResult(
                icId,
                number,
                WorkbenchReplaceModes.General,
                reportSlotPaths,
                build,
                WorkbenchIssueCodes.InputMissing,
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
                WorkbenchReplaceModes.General,
                reportSlotPaths,
                build,
                WorkbenchIssueCodes.InputArtifactReadFailed,
                "Base flash BIN path does not exist.");
            return false;
        }

        WorkbenchGeneralReplaceMappingInput[] selectedMappings =
        [
            .. mappingInputs.Where(mapping => !string.IsNullOrWhiteSpace(mapping.FilePath)),
        ];
        WorkbenchGeneralReplacePatchInput[] selectedPatches = [.. patchInputs];
        if (selectedMappings.Length == 0 && selectedPatches.Length == 0)
        {
            context = null;
            failure = CreatePlanningRunResult(
                icId,
                number,
                WorkbenchReplaceModes.General,
                reportSlotPaths,
                build,
                WorkbenchIssueCodes.InputMissing,
                "At least one General Replace mapping row or hexadecimal patch is required.");
            return false;
        }

        long capacity = new FileInfo(fullBasePath).Length;
        if (capacity <= 0)
        {
            context = null;
            failure = CreatePlanningRunResult(
                icId,
                number,
                WorkbenchReplaceModes.General,
                reportSlotPaths,
                build,
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                "Base flash BIN must not be empty.");
            return false;
        }

        context = new GeneralReplaceRunContext(
            selection,
            reportSlotPaths,
            fullBasePath,
            capacity,
            selectedMappings,
            selectedPatches);
        failure = null;
        return true;
    }
}
