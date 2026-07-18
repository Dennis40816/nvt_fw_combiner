namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateDpPerspectiveDpReplaceRunContext(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        out DpPerspectiveDpReplaceRunContext? context,
        out WorkbenchRunResult? failure)
    {
        context = null;
        failure = null;
        if (!slotPaths.TryGetValue(WorkbenchSlotIds.ReplaceBase, out string? basePath) ||
            string.IsNullOrWhiteSpace(basePath))
        {
            failure = CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.InputMissing,
                $"Reference FlashCode is required before {FormatBuiltInV2DpReplaceIcIds()} DP Replace can determine the output capacity.");
            return false;
        }

        string fullBasePath = Path.GetFullPath(basePath);
        if (!File.Exists(fullBasePath))
        {
            failure = CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.InputArtifactReadFailed,
                "Reference FlashCode path does not exist.");
            return false;
        }

        long baseLength = new FileInfo(fullBasePath).Length;
        context = new DpPerspectiveDpReplaceRunContext(
            ToIcNumberSelection(number),
            fullBasePath,
            baseLength,
            slotPaths);
        return true;
    }
}
