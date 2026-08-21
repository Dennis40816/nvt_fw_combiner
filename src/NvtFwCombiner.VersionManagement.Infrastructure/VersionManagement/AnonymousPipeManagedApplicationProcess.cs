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
    public async ValueTask<bool> TryReportReadyAsync(
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
        if (string.IsNullOrWhiteSpace(handle) ||
            !string.Equals(expected, version.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            await using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, handle);
            byte[] message = Encoding.UTF8.GetBytes($"READY:{version}\n");
            await pipe.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>Starts only the exact stable launcher located at the managed root.</summary>
public sealed class StableLauncherHandoff : IStableLauncherHandoff
{
    private readonly string _managedRoot;

    /// <summary>Creates a launcher handoff for one exact managed root.</summary>
    /// <param name="managedRoot">Stable launcher-owned root.</param>
    public StableLauncherHandoff(string managedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        _managedRoot = Path.GetFullPath(managedRoot);
    }

    /// <inheritdoc />
    public ValueTask<bool> TryStartLauncherAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string launcher = Path.Combine(_managedRoot, "NvtFwCombiner.Launcher.exe");
            if (!ManagedPathSafety.IsSafeExistingDirectory(_managedRoot) ||
                !File.Exists(launcher) ||
                ManagedPathSafety.IsReparsePoint(launcher))
            {
                return ValueTask.FromResult(false);
            }
            Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = launcher,
                WorkingDirectory = _managedRoot,
                UseShellExecute = false,
            });
            process?.Dispose();
            return ValueTask.FromResult(process is not null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ValueTask.FromResult(false);
        }
    }
}
