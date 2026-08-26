using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Windows process adapter with an inherited one-use anonymous ready pipe.</summary>
public sealed class AnonymousPipeManagedApplicationProcess : IManagedApplicationProcess
{
    /// <summary>Environment key carrying the inherited client handle.</summary>
    public const string ReadyPipeHandleEnvironment = "NVT_FW_COMBINER_READY_PIPE_HANDLE";

    /// <summary>Environment key carrying the expected non-secret version.</summary>
    public const string ExpectedVersionEnvironment = "NVT_FW_COMBINER_EXPECTED_VERSION";

    private const int MaximumReadyLineCharacters = 128;
    private readonly string? _statePath;

    /// <summary>Creates a process adapter, optionally propagating an exact custom version-state path.</summary>
    public AnonymousPipeManagedApplicationProcess(string? statePath = null)
    {
        _statePath = statePath is null ? null : Path.GetFullPath(statePath);
    }

    /// <inheritdoc />
    public async ValueTask<ManagedProcessStartResult> StartUntilReadyAsync(
        string managedRoot,
        ManagedAppVersion version,
        TimeSpan readyDeadline,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(readyDeadline, TimeSpan.Zero);
        Process? process = null;
        try
        {
            string versionsRoot = Path.Combine(Path.GetFullPath(managedRoot), FileSystemManagedVersionRepository.VersionsDirectoryName);
            string versionRoot = ManagedPathSafety.GetExactVersionDirectory(versionsRoot, version);
            string executable = Path.Combine(versionRoot, "NvtFwCombiner.exe");
            if (!ManagedPathSafety.IsSafeOwnedTree(versionRoot) ||
                !File.Exists(executable) ||
                ManagedPathSafety.IsReparsePoint(executable))
            {
                return new(ManagedProcessStartOutcome.StartFailed, null);
            }

            using var pipe = new AnonymousPipeServerStream(
                PipeDirection.In,
                HandleInheritability.Inheritable);
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = versionRoot,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            startInfo.ArgumentList.Add("--managed-root");
            startInfo.ArgumentList.Add(Path.GetFullPath(managedRoot));
            if (_statePath is not null)
            {
                startInfo.ArgumentList.Add("--state-path");
                startInfo.ArgumentList.Add(_statePath);
            }
            startInfo.Environment[ReadyPipeHandleEnvironment] = pipe.GetClientHandleAsString();
            startInfo.Environment[ExpectedVersionEnvironment] = version.ToString();
            process = Process.Start(startInfo);
            if (process is null)
            {
                return new(ManagedProcessStartOutcome.StartFailed, null);
            }
            pipe.DisposeLocalCopyOfClientHandle();

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(readyDeadline);
            Task<string?> readyTask = ReadBoundedLineAsync(pipe, deadline.Token);
            Task exitTask = process.WaitForExitAsync(deadline.Token);
            Task completed = await Task.WhenAny(readyTask, exitTask).ConfigureAwait(false);
            if (readyTask.IsCompletedSuccessfully)
            {
                string expected = $"READY:{version}";
                if (string.Equals(readyTask.Result, expected, StringComparison.Ordinal))
                {
                    return new(ManagedProcessStartOutcome.Ready, null);
                }
                if (readyTask.Result is { Length: 0 })
                {
                    await exitTask.ConfigureAwait(false);
                    return new(ManagedProcessStartOutcome.ExitedBeforeReady, process.ExitCode);
                }
                KillIfRunning(process);
                return new(ManagedProcessStartOutcome.InvalidReadySignal, process.HasExited ? process.ExitCode : null);
            }
            if (completed == exitTask && exitTask.IsCompletedSuccessfully)
            {
                return new(ManagedProcessStartOutcome.ExitedBeforeReady, process.ExitCode);
            }

            await completed.ConfigureAwait(false);
            KillIfRunning(process);
            return new(ManagedProcessStartOutcome.ReadyTimeout, process.HasExited ? process.ExitCode : null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (process is not null)
            {
                KillIfRunning(process);
            }
            return new(ManagedProcessStartOutcome.ReadyTimeout, process?.HasExited == true ? process.ExitCode : null);
        }
        catch (OperationCanceledException)
        {
            if (process is not null)
            {
                KillIfRunning(process);
            }
            throw;
        }
        catch (DecoderFallbackException)
        {
            if (process is not null)
            {
                KillIfRunning(process);
            }
            return new(ManagedProcessStartOutcome.InvalidReadySignal, process?.HasExited == true ? process.ExitCode : null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (process is not null)
            {
                KillIfRunning(process);
            }
            return new(ManagedProcessStartOutcome.StartFailed, process?.HasExited == true ? process.ExitCode : null);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static async Task<string?> ReadBoundedLineAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 256,
            leaveOpen: true);
        var result = new StringBuilder();
        char[] character = new char[1];
        while (result.Length <= MaximumReadyLineCharacters)
        {
            int read = await reader.ReadAsync(character, cancellationToken).ConfigureAwait(false);
            if (read == 0 || character[0] == '\n')
            {
                return result.ToString().TrimEnd('\r');
            }
            _ = result.Append(character[0]);
        }
        return null;
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}

/// <summary>Consumes and clears the inherited one-use anonymous ready-pipe handle.</summary>
public sealed class InheritedPipeApplicationReadySignal : IApplicationReadySignal
{
    /// <inheritdoc />
    public async ValueTask<ApplicationReadySignalOutcome> ReportReadyAsync(
        ManagedAppVersion version,
        CancellationToken cancellationToken)
    {
        string? handle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment);
        string? expected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment);
        Environment.SetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
            null);
        Environment.SetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
            null);
        if (handle is null && expected is null)
        {
            return ApplicationReadySignalOutcome.NotInherited;
        }
        if (string.IsNullOrWhiteSpace(handle) ||
            !string.Equals(expected, version.ToString(), StringComparison.Ordinal))
        {
            return ApplicationReadySignalOutcome.InvalidInheritedContext;
        }

        try
        {
            await using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, handle);
            byte[] message = Encoding.UTF8.GetBytes($"READY:{version}\n");
            await pipe.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            return ApplicationReadySignalOutcome.Reported;
        }
        catch (ArgumentException)
        {
            return ApplicationReadySignalOutcome.InvalidInheritedContext;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ApplicationReadySignalOutcome.WriteFailed;
        }
    }
}

/// <summary>Starts only the exact stable launcher located at the managed root.</summary>
public sealed class StableLauncherHandoff : IStableLauncherHandoff
{
    private readonly string _managedRoot;
    private readonly string _statePath;

    /// <summary>Creates a launcher handoff for one exact managed root.</summary>
    /// <param name="managedRoot">Stable launcher-owned root.</param>
    /// <param name="statePath">Exact custom version-state path to propagate to Bootstrap.</param>
    public StableLauncherHandoff(string managedRoot, string? statePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        _managedRoot = Path.GetFullPath(managedRoot);
        _statePath = Path.GetFullPath(statePath ?? JsonVersionManagerStateStore.GetDefaultPath());
    }

    /// <inheritdoc />
    public ValueTask<bool> TryStartLauncherAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string launcher = Path.Combine(_managedRoot, "NvtFwCombiner.Bootstrap.exe");
            if (!ManagedPathSafety.IsSafeExistingDirectory(_managedRoot) ||
                !File.Exists(launcher) ||
                ManagedPathSafety.IsReparsePoint(launcher))
            {
                return ValueTask.FromResult(false);
            }
            var startInfo = new ProcessStartInfo
            {
                FileName = launcher,
                WorkingDirectory = _managedRoot,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("--managed-root");
            startInfo.ArgumentList.Add(_managedRoot);
            startInfo.ArgumentList.Add("--state-path");
            startInfo.ArgumentList.Add(_statePath);
            Process? process = Process.Start(startInfo);
            process?.Dispose();
            return ValueTask.FromResult(process is not null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ValueTask.FromResult(false);
        }
    }
}
