using NvtFwCombiner.Application.Composition;

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
        if (!slotPaths.TryGetValue("replace-base", out string? basePath) ||
            string.IsNullOrWhiteSpace(basePath))
        {
            failure = CreatePlanningRunResult(
                icId,
                number,
                "DP",
                slotPaths,
                build,
                "ui.input.missing",
                $"Base flash BIN is required before {FormatDpPerspectiveIcIds()} DP Replace can determine the DP base length.");
            return false;
        }

        string fullBasePath = Path.GetFullPath(basePath);
        if (!File.Exists(fullBasePath))
        {
            failure = CreatePlanningRunResult(
                icId,
                number,
                "DP",
                slotPaths,
                build,
                "input.artifact.read-failed",
                "Base flash BIN path does not exist.");
            return false;
        }

        long baseLength = new FileInfo(fullBasePath).Length;
        if (!IsSupportedDpPerspectiveBaseLength(baseLength))
        {
            failure = CreatePlanningRunResult(
                icId,
                number,
                "DP",
                slotPaths,
                build,
                "input.address-space.length-mismatch",
                $"{icId} DP Replace base flash BIN length must be one of {FormatSupportedDpPerspectiveBaseLengths()} (actual {FormatHexLength(baseLength)}).",
                baseLength);
            return false;
        }

        InputArtifactBinding[] bindings =
        [
            new("reference-base", "replace-base", fullBasePath),
            CreateBinding("dp-replacement", "replace-dp", slotPaths),
        ];
        context = new DpPerspectiveDpReplaceRunContext(
            ToIcNumberSelection(number),
            fullBasePath,
            baseLength,
            bindings);
        return true;
    }

    private sealed record DpPerspectiveDpReplaceRunContext(
        IcNumberSelection Selection,
        string BasePath,
        long Capacity,
        IReadOnlyList<InputArtifactBinding> Bindings);
}
