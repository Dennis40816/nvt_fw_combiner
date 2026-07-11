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
        if (!build)
        {
            return await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        }

        CompositionRunResult preview = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        return preview.Status == CompositionExecutionStatus.Succeeded
            ? await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), cancellationToken)
                .ConfigureAwait(false)
            : preview;
    }
}
