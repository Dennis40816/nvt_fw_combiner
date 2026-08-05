using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class CompositionPlanningAdapter
{
    internal static WorkbenchRunResult CreatePlanningRunResult(
        string icId,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string issueCode,
        string issueMessage)
    {
        return CompositionExecutionAdapter.CreateReplaceReportRunResult(
            icId,
            replaceMode,
            slotPaths,
            build,
            [],
            [new CompositionIssue(issueCode, issueMessage, replaceMode.ToLowerInvariant())],
            CompositionExecutionAdapter.GetReplaceDefaultOutputFileName(icId, replaceMode));
    }

}
