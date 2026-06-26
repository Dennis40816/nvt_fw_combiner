namespace NvtFwCombiner.Application.Ports;

/// <summary>Runs an approved external processor through an application port.</summary>
public interface IExternalProcessor
{
    /// <summary>Executes the configured processor without exposing process details to application code.</summary>
    ValueTask ExecuteAsync(string processorId, CancellationToken cancellationToken);
}
