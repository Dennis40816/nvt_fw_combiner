using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static class CompositionRunExecutionSupport
{
    internal static async ValueTask<CompositionRunResult> PreviewOrBuildAsync(
        CompositionRunService service,
        CompositionRunRequest request,
        bool build,
        CancellationToken cancellationToken)
    {
        return build
            ? await service.AutomaticBuildAsync(request, cancellationToken).ConfigureAwait(false)
            : await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
