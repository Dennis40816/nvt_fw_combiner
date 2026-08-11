namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Process launch request prepared by infrastructure from an approved manifest.</summary>
public sealed class ExternalProcessStartInfo
{
    private readonly string[] _arguments;

    /// <summary>Creates a process launch request with an argument list, not a shell command line.</summary>
    public ExternalProcessStartInfo(
        string executablePath,
        string workingDirectory,
        IEnumerable<string> arguments,
        TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
        }

        ExecutablePath = executablePath;
        WorkingDirectory = workingDirectory;
        _arguments = [.. arguments];
        Timeout = timeout;
    }

    /// <summary>Resolved executable path after manifest SHA-256 verification.</summary>
    public string ExecutablePath { get; }

    /// <summary>Private staging directory used as process working directory.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Expanded arguments passed through ProcessStartInfo.ArgumentList.</summary>
    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>Creates immutable audit evidence for this exact executable, working directory, and argument list.</summary>
    public ExternalProcessInvocation ToExecutedCommand()
    {
        return new ExternalProcessInvocation(ExecutablePath, WorkingDirectory, Arguments);
    }

    /// <summary>Maximum process execution time.</summary>
    public TimeSpan Timeout { get; }
}
