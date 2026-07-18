using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static class CompositionRunExecutionSupport
{
    internal static async ValueTask<CompositionRunResult> PreviewOrBuildAsync(
        CompositionRunService service,
        CompositionRunRequest request,
        bool build,
        CancellationToken cancellationToken)
    {
        CompositionRunExecutionOutcome outcome = await PreviewOrBuildWithMetricsAsync(
                service,
                request,
                build,
                cancellationToken)
            .ConfigureAwait(false);
        return outcome.Result;
    }

    internal static async ValueTask<CompositionRunExecutionOutcome> PreviewOrBuildWithMetricsAsync(
        CompositionRunService service,
        CompositionRunRequest request,
        bool build,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(request);

        CompositionRunExecutionMetrics metrics = default;
        if (!build)
        {
            CompositionRunResult previewOnly = await service
                .PreviewAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return new CompositionRunExecutionOutcome(
                previewOnly,
                metrics.RecordPreview(previewOnly));
        }

        CompositionRunResult preview = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        metrics = metrics.RecordPreview(preview);
        if (preview.Status != CompositionExecutionStatus.Succeeded)
        {
            return new CompositionRunExecutionOutcome(preview, metrics);
        }

        CompositionRunResult result = await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), cancellationToken)
            .ConfigureAwait(false);
        return new CompositionRunExecutionOutcome(result, metrics.RecordBuild(result));
    }
}
