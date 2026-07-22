using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static WorkbenchRunResult CreatePlanningRunResult(
        string icId,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string issueCode,
        string issueMessage)
    {
        return CreateReplaceReportRunResult(
            icId,
            replaceMode,
            slotPaths,
            build,
            [],
            [new CompositionIssue(issueCode, issueMessage, replaceMode.ToLowerInvariant())],
            GetReplaceDefaultOutputFileName(icId, replaceMode));
    }

    private static List<InputArtifactSummary> CreateInputSummaries(
        IReadOnlyDictionary<string, string> slotPaths)
    {
        List<InputArtifactSummary> summaries = [];
        foreach ((string slotId, string path) in slotPaths.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                summaries.Add(new InputArtifactSummary(slotId, Path.GetFileName(path), 0, EmptySha256));
                continue;
            }

            FileInfo file = new(fullPath);
            summaries.Add(new InputArtifactSummary(
                slotId,
                Path.GetFileName(fullPath),
                file.Length,
                WorkbenchArtifactIdentity.Sha256File(fullPath)));
        }

        return summaries;
    }
}
