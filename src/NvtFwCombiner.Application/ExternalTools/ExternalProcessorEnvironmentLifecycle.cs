namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Observable state of the one host-owned external-tool environment.</summary>
public enum ExternalProcessorEnvironmentState
{
    /// <summary>No discovery request has completed.</summary>
    NotLoaded,

    /// <summary>The current request is materializing a complete candidate.</summary>
    Loading,

    /// <summary>The latest request published a complete immutable environment.</summary>
    Current,

    /// <summary>The latest request failed while a prior publication remains active.</summary>
    LastKnownGood,

    /// <summary>The latest request failed and no publication exists.</summary>
    Unavailable,
}

/// <summary>Terminal outcome of one never-reused external discovery request.</summary>
public enum ExternalProcessorEnvironmentLoadOutcome
{
    /// <summary>Default invalid value; no request may publish it.</summary>
    Unknown,

    /// <summary>A complete candidate was atomically published.</summary>
    Succeeded,

    /// <summary>A typed failure retained the prior publication when one existed.</summary>
    Failed,

    /// <summary>A newer request replaced this request before publication.</summary>
    Superseded,
}

/// <summary>Stable external environment issue without private filesystem paths.</summary>
public sealed record ExternalProcessorEnvironmentIssue(string Code, string Message);

/// <summary>Stable issue codes emitted by bounded external environment discovery.</summary>
public static class ExternalProcessorEnvironmentIssueCodes
{
    /// <summary>No immutable external environment has been published.</summary>
    public const string EnvironmentUnavailable = "external-environment.unavailable";

    /// <summary>Filesystem discovery could not complete.</summary>
    public const string DiscoveryFailed = "external-environment.discovery-failed";

    /// <summary>A closed traversal or manifest byte bound was exceeded.</summary>
    public const string BoundsExceeded = "external-environment.bounds-exceeded";

    /// <summary>A discovered manifest could not be parsed or validated.</summary>
    public const string ManifestInvalid = "external-environment.manifest-invalid";

    /// <summary>The complete candidate failed executable identity or trust validation.</summary>
    public const string CandidateInvalid = "external-environment.candidate-invalid";
}

/// <summary>Latest immutable state of the one external environment lifecycle.</summary>
public sealed record ExternalProcessorEnvironmentStatus(
    ExternalProcessorEnvironmentState State,
    long RequestGeneration,
    long PublicationGeneration,
    int ManifestCount,
    IReadOnlyList<ExternalProcessorEnvironmentIssue> Issues);

/// <summary>Exactly one terminal result for a never-reused external discovery request.</summary>
public sealed record ExternalProcessorEnvironmentLoadResult(
    ExternalProcessorEnvironmentLoadOutcome Outcome,
    long RequestGeneration,
    long PublicationGeneration,
    int ManifestCount,
    bool RetainedLastKnownGood,
    IReadOnlyList<ExternalProcessorEnvironmentIssue> Issues)
{
    /// <summary>True only after a complete candidate was atomically published.</summary>
    public bool Succeeded => Outcome == ExternalProcessorEnvironmentLoadOutcome.Succeeded;
}

/// <summary>Exact manifest work or the one final typed result for a discovery request.</summary>
public sealed record ExternalProcessorEnvironmentLoadUpdate(
    long? CompletedWork,
    long? TotalWork,
    ExternalProcessorEnvironmentLoadResult? Result);

/// <summary>Loads one bounded external environment and exposes its latest safe state.</summary>
public interface IExternalProcessorEnvironmentLoader
{
    /// <summary>Latest immutable lifecycle observation.</summary>
    ExternalProcessorEnvironmentStatus Current { get; }

    /// <summary>Starts a fresh request; a newer request supersedes and drains the prior request.</summary>
    IAsyncEnumerable<ExternalProcessorEnvironmentLoadUpdate> LoadAsync(
        CancellationToken cancellationToken);

    /// <summary>Drains one request while projecting optional exact manifest work.</summary>
    async Task<ExternalProcessorEnvironmentLoadResult> LoadToCompletionAsync(
        Action<long, long>? progress,
        CancellationToken cancellationToken)
    {
        ExternalProcessorEnvironmentLoadResult? terminal = null;
        long completedWork = -1;
        long totalWork = -1;
        await foreach (ExternalProcessorEnvironmentLoadUpdate update in LoadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (terminal is not null)
            {
                throw new InvalidOperationException(
                    "External environment loading produced an update after its terminal result.");
            }
            if (update.Result is { } result)
            {
                if (update.CompletedWork is not null || update.TotalWork is not null ||
                    result.Outcome == ExternalProcessorEnvironmentLoadOutcome.Unknown)
                {
                    throw new InvalidOperationException(
                        "External environment loading produced an invalid terminal result.");
                }
                terminal = result;
            }
            else if (update.CompletedWork is { } completed && update.TotalWork is { } total)
            {
                if (total <= 0 || completed < 0 || completed > total ||
                    (totalWork >= 0 && total != totalWork) || completed < completedWork)
                {
                    throw new InvalidOperationException(
                        "External environment progress must be monotonic with one positive total.");
                }
                completedWork = completed;
                totalWork = total;
                try
                {
                    progress?.Invoke(completed, total);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Trace.TraceError(
                        "External environment progress observer failed: {0}",
                        exception);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "External environment loading produced an invalid progress update.");
            }
        }
        return terminal ?? throw new InvalidOperationException(
            "External environment loading completed without a terminal result.");
    }
}
