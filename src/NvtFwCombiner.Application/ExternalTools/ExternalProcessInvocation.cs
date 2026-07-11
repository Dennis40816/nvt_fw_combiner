namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>One completed external process invocation captured for run-report audit evidence.</summary>
public sealed class ExternalProcessInvocation
{
    private readonly string[] _arguments;

    /// <summary>Creates an immutable record of the executable, working directory, and expanded argv.</summary>
    public ExternalProcessInvocation(
        string executablePath,
        string workingDirectory,
        IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        ExecutablePath = executablePath;
        WorkingDirectory = workingDirectory;
        _arguments = [.. arguments];
    }

    /// <summary>Resolved executable path supplied to <c>ProcessStartInfo.FileName</c>.</summary>
    public string ExecutablePath { get; }

    /// <summary>Host-created staging directory supplied to <c>ProcessStartInfo.WorkingDirectory</c>.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Expanded values supplied, in order, to <c>ProcessStartInfo.ArgumentList</c>.</summary>
    public IReadOnlyList<string> Arguments => _arguments;
}
