using System.Diagnostics;
using System.Globalization;

namespace NvtFwCombiner.Platform.Processes;

/// <summary>Names one handle that a contained Windows child may inherit.</summary>
public readonly record struct ProcessInheritedHandle
{
    /// <summary>Creates one environment-bound inherited handle.</summary>
    public ProcessInheritedHandle(string environmentVariable, IntPtr handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariable);
        if (environmentVariable.Contains('=', StringComparison.Ordinal))
        {
            throw new ArgumentException("Environment variable names cannot contain '='.", nameof(environmentVariable));
        }
        if (handle.ToInt64() <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(handle));
        }
        EnvironmentVariable = environmentVariable;
        Handle = handle;
    }

    /// <summary>Environment variable that receives the short-lived duplicate value.</summary>
    public string EnvironmentVariable { get; }

    /// <summary>Non-inheritable original handle retained by the parent.</summary>
    public IntPtr Handle { get; }

    /// <summary>Parses a decimal Windows handle returned by an anonymous pipe.</summary>
    public static ProcessInheritedHandle Parse(string environmentVariable, string handle)
    {
        return long.TryParse(handle, NumberStyles.None, CultureInfo.InvariantCulture, out long value)
            ? new(environmentVariable, new IntPtr(value))
            : throw new ArgumentException("Inherited handle must be a positive decimal value.", nameof(handle));
    }
}

/// <summary>Serializes every production process start in this process.</summary>
public static class ProcessLaunchGate
{
    private static readonly Lock StartLock = new();

    /// <summary>Starts a normal child while excluding concurrent managed-handle duplication.</summary>
    public static Process? Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        lock (StartLock)
        {
            return Process.Start(startInfo);
        }
    }

    /// <summary>Starts a managed child with exactly the declared Windows handle allowlist.</summary>
    public static Process? StartContained(
        ProcessStartInfo startInfo,
        IReadOnlyList<ProcessInheritedHandle> inheritedHandles)
    {
        return StartContained(startInfo, inheritedHandles, static () => true);
    }

    /// <summary>
    /// Starts a managed child only when its final custody validation succeeds while the
    /// global start gate is held, immediately before native process creation.
    /// </summary>
    public static Process? StartContained(
        ProcessStartInfo startInfo,
        IReadOnlyList<ProcessInheritedHandle> inheritedHandles,
        Func<bool> validateImmediatelyBeforeStart)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(inheritedHandles);
        ArgumentNullException.ThrowIfNull(validateImmediatelyBeforeStart);
        lock (StartLock)
        {
            return OperatingSystem.IsWindows()
                ? WindowsContainedProcessStarter.Start(
                    startInfo,
                    inheritedHandles,
                    validateImmediatelyBeforeStart)
                : validateImmediatelyBeforeStart()
                    ? Process.Start(startInfo)
                    : null;
        }
    }

    /// <summary>Clears child inheritance from one captured Windows handle.</summary>
    public static bool TryClearInheritance(IntPtr handle)
    {
        return !OperatingSystem.IsWindows() ||
            WindowsContainedProcessStarter.TryClearInheritance(handle);
    }
}
