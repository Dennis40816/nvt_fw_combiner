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
                number,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.InputMissing,
                $"Base flash BIN is required before {FormatBuiltInV2DpReplaceIcIds()} DP Replace can determine the DP base length.");
            return false;
        }

        string fullBasePath = Path.GetFullPath(basePath);
        if (!File.Exists(fullBasePath))
        {
            failure = CreatePlanningRunResult(
                icId,
                number,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.InputArtifactReadFailed,
                "Base flash BIN path does not exist.");
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
