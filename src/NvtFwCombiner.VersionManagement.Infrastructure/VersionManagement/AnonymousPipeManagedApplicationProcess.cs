using System.Buffers.Binary;
using System.ComponentModel;
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
    private readonly string _statePath;
    private readonly IManagedProcessTermination _termination;

    /// <summary>Creates a process adapter, optionally propagating an exact custom version-state path.</summary>
    public AnonymousPipeManagedApplicationProcess(string? statePath = null)
        : this(statePath, ManagedProcessTermination.Instance)
    {
    }

    internal AnonymousPipeManagedApplicationProcess(
        string? statePath,
        IManagedProcessTermination termination)
    {
        _statePath = Path.GetFullPath(statePath ?? JsonVersionManagerStateStore.GetDefaultPath());
        _termination = termination ?? throw new ArgumentNullException(nameof(termination));
    }

    /// <inheritdoc />
    public ValueTask<ManagedProcessLifetimeStatus> GetLifetimeStatusAsync(
        string managedRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ManagedProcessLifetimeLease.GetStatus(
            _statePath,
            ManagedProcessLifetimeLease.ApplicationSuffix));
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
                ManagedPathSafety.IsReparsePoint(executable) ||
                !ManagedProcessExecutable.HasPortableExecutableHeader(executable))
            {
                return new(ManagedProcessStartOutcome.StartFailed, null);
            }

            using ManagedProcessLifetimeLease? lifetime = ManagedProcessLifetimeLease.TryAcquire(
                _statePath,
                ManagedProcessLifetimeLease.ApplicationSuffix);
            if (lifetime is null)
            {
                return new(ManagedProcessStartOutcome.TerminationUnconfirmed, null);
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
            startInfo.ArgumentList.Add("--state-path");
            startInfo.ArgumentList.Add(_statePath);
            startInfo.Environment[ReadyPipeHandleEnvironment] = pipe.GetClientHandleAsString();
            startInfo.Environment[ExpectedVersionEnvironment] = version.ToString();
            startInfo.Environment[ManagedProcessLifetimeLease.HandleEnvironment] = lifetime.InheritedHandle;
            try
            {
                process = Process.Start(startInfo);
            }
            finally
            {
                DisposeLocalClientHandle(pipe);
            }
            if (process is null)
            {
                return new(ManagedProcessStartOutcome.StartFailed, null);
            }

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
                return Terminate(process, ManagedProcessStartOutcome.InvalidReadySignal);
            }
            if (completed == exitTask && exitTask.IsCompletedSuccessfully)
            {
                return new(ManagedProcessStartOutcome.ExitedBeforeReady, process.ExitCode);
            }

            await completed.ConfigureAwait(false);
            return Terminate(process, ManagedProcessStartOutcome.ReadyTimeout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return process is null
                ? new(ManagedProcessStartOutcome.ReadyTimeout, null)
                : Terminate(process, ManagedProcessStartOutcome.ReadyTimeout);
        }
        catch (OperationCanceledException)
        {
            if (process is not null)
            {
                _ = _termination.ConfirmExited(process);
            }
            throw;
        }
        catch (DecoderFallbackException)
        {
            return process is not null
                ? Terminate(process, ManagedProcessStartOutcome.InvalidReadySignal)
                : new(ManagedProcessStartOutcome.InvalidReadySignal, null);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return process is not null
                ? Terminate(process, ManagedProcessStartOutcome.StartFailed)
                : new(ManagedProcessStartOutcome.StartFailed, null);
        }
        finally
        {
            DisposeProcess(process);
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

    private ManagedProcessStartResult Terminate(
        Process process,
        ManagedProcessStartOutcome confirmedOutcome)
    {
        ManagedProcessTerminationResult termination = _termination.ConfirmExited(process);
        return termination.IsExitConfirmed
            ? new(confirmedOutcome, termination.ExitCode)
            : new(ManagedProcessStartOutcome.TerminationUnconfirmed, null);
    }

    private static void DisposeLocalClientHandle(AnonymousPipeServerStream pipe)
    {
        try
        {
            pipe.DisposeLocalCopyOfClientHandle();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
        }
    }

    private static void DisposeProcess(Process? process)
    {
        try
        {
            process?.Dispose();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
        }
    }
}

internal readonly record struct ManagedProcessTerminationResult(bool IsExitConfirmed, int? ExitCode)
{
    internal static ManagedProcessTerminationResult Unconfirmed => new(false, null);
}

internal interface IManagedProcessTermination
{
    ManagedProcessTerminationResult ConfirmExited(Process process);
}

internal interface IManagedProcessTerminationOperations
{
    bool HasExited(Process process);
    void Kill(Process process);
    bool WaitForExit(Process process, TimeSpan timeout);
    int GetExitCode(Process process);
}

internal sealed class ManagedProcessTermination : IManagedProcessTermination
{
    internal static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly IManagedProcessTerminationOperations _operations;
    private readonly TimeSpan _waitTimeout;

    internal static ManagedProcessTermination Instance { get; } = new(new ProcessTerminationOperations());

    internal ManagedProcessTermination(
        IManagedProcessTerminationOperations operations,
        TimeSpan? waitTimeout = null)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _waitTimeout = waitTimeout ?? DefaultWaitTimeout;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_waitTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            _waitTimeout,
            TimeSpan.FromMilliseconds(int.MaxValue));
    }

    public ManagedProcessTerminationResult ConfirmExited(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        try
        {
            if (_operations.HasExited(process))
            {
                return new(true, _operations.GetExitCode(process));
            }
        }
        catch (Exception exception) when (IsTerminationUncertainty(exception))
        {
            return ManagedProcessTerminationResult.Unconfirmed;
        }

        try
        {
            _operations.Kill(process);
        }
        catch (Exception exception) when (IsTerminationUncertainty(exception))
        {
            return ManagedProcessTerminationResult.Unconfirmed;
        }

        try
        {
            return _operations.WaitForExit(process, _waitTimeout) && _operations.HasExited(process)
                ? new(true, _operations.GetExitCode(process))
                : ManagedProcessTerminationResult.Unconfirmed;
        }
        catch (Exception exception) when (IsTerminationUncertainty(exception))
        {
            return ManagedProcessTerminationResult.Unconfirmed;
        }
    }

    private static bool IsTerminationUncertainty(Exception exception)
    {
        return exception is AggregateException or InvalidOperationException or Win32Exception;
    }

    private sealed class ProcessTerminationOperations : IManagedProcessTerminationOperations
    {
        public bool HasExited(Process process)
        {
            return process.HasExited;
        }

        public void Kill(Process process)
        {
            process.Kill(entireProcessTree: true);
        }

        public bool WaitForExit(Process process, TimeSpan timeout)
        {
            return process.WaitForExit(checked((int)Math.Ceiling(timeout.TotalMilliseconds)));
        }

        public int GetExitCode(Process process)
        {
            return process.ExitCode;
        }
    }
}

/// <summary>Rejects malformed PE payloads before Win32 launch; package identity remains repository-owned.</summary>
internal static class ManagedProcessExecutable
{
    internal static bool HasPortableExecutableHeader(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.RandomAccess);
            if (stream.Length < 64)
            {
                return false;
            }

            Span<byte> dosHeader = stackalloc byte[64];
            stream.ReadExactly(dosHeader);
            int peHeaderOffset = BinaryPrimitives.ReadInt32LittleEndian(dosHeader[0x3C..]);
            if (dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z' ||
                peHeaderOffset < dosHeader.Length || peHeaderOffset > stream.Length - 4)
            {
                return false;
            }

            stream.Position = peHeaderOffset;
            Span<byte> peSignature = stackalloc byte[4];
            stream.ReadExactly(peSignature);
            return peSignature.SequenceEqual("PE\0\0"u8);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
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
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return ValueTask.FromResult(false);
        }
    }
}
