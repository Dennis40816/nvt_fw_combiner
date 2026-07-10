using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Runs an approved external processor through an application port.</summary>
public interface IExternalProcessor
{
    /// <summary>Executes the configured processor and returns validated bytes plus completed-process audit evidence.</summary>
    ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken);
}
