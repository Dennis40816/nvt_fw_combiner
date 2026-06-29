namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Observed result from an external process invocation.</summary>
public sealed record ExternalProcessResult(
    int ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError);
