using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Routes catalog-owned postbuild ids to their adapter and all other declared ids to the manifest adapter.</summary>
public sealed class ExternalProcessorRouter : IExternalProcessor
{
    private readonly IExternalProcessor _legacyPostbuildProcessor;
    private readonly IExternalProcessor _manifestProcessor;

    /// <summary>Creates one closed dispatch boundary over the existing external-processor port.</summary>
    public ExternalProcessorRouter(
        IExternalProcessor legacyPostbuildProcessor,
        IExternalProcessor manifestProcessor)
    {
        ArgumentNullException.ThrowIfNull(legacyPostbuildProcessor);
        ArgumentNullException.ThrowIfNull(manifestProcessor);

        _legacyPostbuildProcessor = legacyPostbuildProcessor;
        _manifestProcessor = manifestProcessor;
    }

    /// <inheritdoc />
    public ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ExternalProcessorProtocolPlan? plan = request.ProtocolPlan;
        return plan is null
            ? _manifestProcessor.TransformAsync(request, cancellationToken)
            : StringComparer.Ordinal.Equals(
                plan.ProtocolId,
                LegacyCombinerPostbuildPlanCompiler.ProtocolId)
            ? _legacyPostbuildProcessor.TransformAsync(request, cancellationToken)
            : ValueTask.FromResult(ExternalProcessorResult.Failed([
                new CompositionIssue(
                    "external-processor.protocol.unsupported",
                    $"No external-processor adapter is registered for compiled protocol '{plan.ProtocolId}'."),
            ]));
    }
}
