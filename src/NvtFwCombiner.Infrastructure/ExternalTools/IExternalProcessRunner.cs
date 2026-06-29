namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Runs a prepared external process without shell command construction.</summary>
public interface IExternalProcessRunner
{
    /// <summary>Runs the process and returns captured exit status.</summary>
    ValueTask<ExternalProcessResult> RunAsync(
        ExternalProcessStartInfo startInfo,
        CancellationToken cancellationToken);
}
