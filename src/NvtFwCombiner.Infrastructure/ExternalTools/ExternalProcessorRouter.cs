using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Routes catalog-owned postbuild ids to their adapter and all other declared ids to the manifest adapter.</summary>
public sealed class ExternalProcessorRouter : IExternalProcessor
{
    private readonly IExternalProcessor _legacyPostbuildProcessor;
    private readonly IExternalProcessor _manifestProcessor;
    private readonly HashSet<string> _legacyPostbuildProcessorIds;

    /// <summary>Creates one closed dispatch boundary over the existing external-processor port.</summary>
    public ExternalProcessorRouter(
        IExternalProcessor legacyPostbuildProcessor,
        IExternalProcessor manifestProcessor,
        IEnumerable<string> legacyPostbuildProcessorIds)
    {
        ArgumentNullException.ThrowIfNull(legacyPostbuildProcessor);
        ArgumentNullException.ThrowIfNull(manifestProcessor);
        ArgumentNullException.ThrowIfNull(legacyPostbuildProcessorIds);

        _legacyPostbuildProcessorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string processorId in legacyPostbuildProcessorIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
            if (!_legacyPostbuildProcessorIds.Add(processorId))
            {
                throw new ArgumentException(
                    $"Legacy postbuild processor id '{processorId}' is declared more than once.",
                    nameof(legacyPostbuildProcessorIds));
            }
        }

        _legacyPostbuildProcessor = legacyPostbuildProcessor;
        _manifestProcessor = manifestProcessor;
    }

    /// <inheritdoc />
    public ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _legacyPostbuildProcessorIds.Contains(request.ProcessorId)
            ? _legacyPostbuildProcessor.TransformAsync(request, cancellationToken)
            : _manifestProcessor.TransformAsync(request, cancellationToken);
    }
}
