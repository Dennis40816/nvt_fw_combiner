using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal readonly record struct CompositionRunExecutionMetrics(
    int PreviewRunCount,
    int BuildRunCount,
    int SuccessfulInputReadCount,
    int ExternalProcessorSessionCount,
    int ExternalProcessInvocationCount)
{
    internal int CompositionRunCount => checked(PreviewRunCount + BuildRunCount);

    internal CompositionRunExecutionMetrics RecordPreview(CompositionRunResult result)
    {
        return Record(result, previewRuns: 1, buildRuns: 0);
    }

    internal CompositionRunExecutionMetrics RecordBuild(CompositionRunResult result)
    {
        return Record(result, previewRuns: 0, buildRuns: 1);
    }

    private CompositionRunExecutionMetrics Record(
        CompositionRunResult result,
        int previewRuns,
        int buildRuns)
    {
        ArgumentNullException.ThrowIfNull(result);

        int processorSessions = result.Report.Operations.Count(static operation =>
            operation.Kind == CompositionOperationKind.RunExternalProcessor &&
            operation.Status != OperationRunStatus.Skipped);
        int processInvocations = result.Report.Operations.Sum(static operation =>
            operation.ExecutedCommands.Count);
        return new CompositionRunExecutionMetrics(
            checked(PreviewRunCount + previewRuns),
            checked(BuildRunCount + buildRuns),
            checked(SuccessfulInputReadCount + result.Report.Inputs.Count),
            checked(ExternalProcessorSessionCount + processorSessions),
            checked(ExternalProcessInvocationCount + processInvocations));
    }
}
