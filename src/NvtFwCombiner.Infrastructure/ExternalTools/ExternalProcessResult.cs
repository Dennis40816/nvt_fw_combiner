namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Observed result from an external process invocation.</summary>
public sealed record ExternalProcessResult(
    int ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError)
{
    /// <summary>
    /// Cleanup fact the runner observed within its host cleanup deadline (ADR 0081). A consumer may treat the
    /// captured output as a successful result or as protocol input only when this is
    /// <see cref="ExternalProcessCleanup.Complete"/>; any other value fails closed before that use. The captured
    /// text may still be quoted as error diagnostics for a timeout or a non-zero exit regardless of this value.
    /// </summary>
    public ExternalProcessCleanup Cleanup { get; init; } = ExternalProcessCleanup.Complete;
}

/// <summary>
/// Cleanup outcome observed for one external process invocation. The runner observes only the direct child's
/// exit and the end of its two redirected streams; no value proves that every descendant has stopped, because a
/// descendant that holds neither stream is not observable (ADR 0081, owner decision 85).
/// </summary>
public enum ExternalProcessCleanup
{
    /// <summary>The direct child's exit was observed and both redirected streams reported their end.</summary>
    Complete,

    /// <summary>The termination request failed or did not finish, or the direct child's exit was not observed.</summary>
    TerminationUnconfirmed,

    /// <summary>A redirected output stream did not reach its end within the allowed drain period.</summary>
    OutputStreamHeldOpen,

    /// <summary>Reading a redirected stream failed, so its end was not observed.</summary>
    OutputReadFailed,
}

/// <summary>
/// The runner refused to start a new external process because the capacity of invocations that are running or still
/// cleaning up is full (ADR 0081, owner decision 92). The refusal happens before any process starts.
/// </summary>
public sealed class ExternalProcessCleanupCapacityException(int inUseInvocations, int limit)
    : Exception(
        $"The external process runner is at its limit of {limit} invocations that are running or still cleaning up " +
        $"({inUseInvocations} in use when the reservation was refused); a new run is refused. Restart the application.")
{
    /// <summary>
    /// In-use count (running or still cleaning up) that the atomic reservation observed when it refused; at or above
    /// <see cref="Limit"/>. It is not re-read later.
    /// </summary>
    public int InUseInvocations { get; } = inUseInvocations;

    /// <summary>The fixed limit that was reached.</summary>
    public int Limit { get; } = limit;
}

/// <summary>Shared fail-closed issue text for staged processors that consume a runner cleanup fact.</summary>
internal static class ExternalProcessCleanupText
{
    /// <summary>Issue code when the tool exited with code 0 but its cleanup was observed incomplete.</summary>
    internal const string IssueCode = "external-tool.process.cleanup-incomplete";

    /// <summary>Issue code when the runner refuses a new run because its invocation capacity is full.</summary>
    internal const string CapacityIssueCode = "external-tool.process.cleanup-capacity";

    /// <summary>
    /// Message for a capacity refusal: the invocations that are running or still cleaning up fill the capacity. It
    /// keeps the restart guidance and states no count, since only the refusal itself is certain.
    /// </summary>
    internal static string CapacityMessage(ExternalProcessCleanupCapacityException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return "The external processor could not start because external-tool runs that are still running or still " +
            "cleaning up have filled the available capacity. Restart the application and try again.";
    }

    /// <summary>Returns a sentence fragment that describes only the observed incomplete cleanup.</summary>
    internal static string Describe(ExternalProcessCleanup cleanup)
    {
        return cleanup switch
        {
            ExternalProcessCleanup.TerminationUnconfirmed =>
                "its termination or exit was not confirmed within the cleanup deadline.",
            ExternalProcessCleanup.OutputStreamHeldOpen =>
                "a redirected output stream did not reach its end within the allowed drain period.",
            ExternalProcessCleanup.OutputReadFailed =>
                "a redirected output stream could not be read to its end.",
            ExternalProcessCleanup.Complete or _ =>
                throw new ArgumentOutOfRangeException(nameof(cleanup), cleanup, "Cleanup is complete."),
        };
    }

    /// <summary>Returns an empty suffix for complete cleanup, otherwise one explanatory sentence.</summary>
    internal static string Suffix(ExternalProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Cleanup == ExternalProcessCleanup.Complete
            ? string.Empty
            : " In addition, " + Describe(result.Cleanup);
    }
}
