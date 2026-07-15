using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static WorkbenchRunResult CreateGeneralMergeReportRunResult(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        string profileId,
        string profileVersion)
    {
        return CreateBlockedReportRunResult(
            GeneralMergeRunIdPrefix,
            profileId,
            profileVersion,
            icId,
            IcWorkflowIds.GeneralMerge,
            IcWorkflowIds.GeneralMerge,
            CompositionKind.Merge,
            slotPaths,
            build,
            operations,
            issues,
            outputFileName);
    }
}
