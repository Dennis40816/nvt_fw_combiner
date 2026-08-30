using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Platform.Processes;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Strict immutable-Bootstrap launch arguments.</summary>
public sealed record LauncherBootstrapLaunchOptions(string ManagedRoot, string StatePath)
{
    /// <summary>Parses host arguments, using the canonical per-user state path when none is supplied.</summary>
    public static LauncherBootstrapLaunchOptions Parse(string[] args, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        string managedRoot = baseDirectory;
        string statePath = JsonVersionManagerStateStore.GetDefaultPath();
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            string value = index + 1 < args.Length
                ? args[++index]
                : throw new ArgumentException("Bootstrap option is missing its value.", nameof(args));
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            switch (option)
            {
                case "--managed-root":
                    managedRoot = value;
                    break;
                case "--state-path":
                    statePath = value;
                    break;
                default:
                    throw new ArgumentException("Unknown Bootstrap option.", nameof(args));
            }
        }
        return new(Path.GetFullPath(managedRoot), Path.GetFullPath(statePath));
    }
}

/// <summary>Starts one exact version-scoped launcher and accepts one identity-bound READY line.</summary>
internal sealed class AnonymousPipeManagedLauncherProcess : IManagedLauncherProcess
{
    internal const string ReadyPipeHandleEnvironment = "NVT_FW_COMBINER_LAUNCHER_READY_PIPE_HANDLE";
    internal const string ExpectedReadyEnvironment = "NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY";
    internal const string BootstrapAdmissionPipeHandleEnvironment =
        "NVT_FW_COMBINER_BOOTSTRAP_ADMISSION_PIPE_HANDLE";
    private const int MaximumReadyLineCharacters = 4096;
    private readonly BootstrapAdmissionSignal _admission;
    private readonly ManagedImmutableBootstrapIdentity? _inheritedBootstrapIdentity;
    private readonly IManagedProcessTermination _termination;
    private readonly Action? _beforeStartValidation;

    internal AnonymousPipeManagedLauncherProcess()
        : this(ManagedProcessTermination.Instance, BootstrapAdmissionSignal.Capture())
    {
    }

    internal AnonymousPipeManagedLauncherProcess(IManagedProcessTermination termination)
        : this(termination, BootstrapAdmissionSignal.Capture())
    {
    }

    internal AnonymousPipeManagedLauncherProcess(
        IManagedProcessTermination termination,
        BootstrapAdmissionSignal admission,
        Action? beforeStartValidation = null,
        ManagedImmutableBootstrapIdentity? inheritedBootstrapIdentity = null)
    {
        _termination = termination ?? throw new ArgumentNullException(nameof(termination));
        _admission = admission ?? throw new ArgumentNullException(nameof(admission));
        _beforeStartValidation = beforeStartValidation;
        _inheritedBootstrapIdentity = inheritedBootstrapIdentity;
    }

    public ValueTask<ManagedProcessLifetimeStatus> GetLifetimeStatusAsync(
        string statePath,
        ManagedProcessLifetimeKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ManagedProcessLifetimeLease.GetStatus(
            statePath,
            kind));
    }

    public async ValueTask<LauncherProcessStartResult> StartUntilReadyAsync(
        string managedRoot,
        string statePath,
        ManagedLauncherIdentity launcher,
        IManagedExecutableLaunchLease executableLease,
        TimeSpan readyDeadline,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(executableLease);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(readyDeadline, TimeSpan.Zero);
        Process? process = null;
        ManagedProcessLifetimeLease? lifetime = null;
        try
        {
            if (_admission.Outcome == BootstrapAdmissionInheritanceOutcome.Invalid)
            {
                return Failure(LauncherProcessStartOutcome.StartFailed);
            }
            lifetime = ManagedProcessLifetimeLease.TryAcquire(
                statePath,
                ManagedProcessLifetimeKind.Launcher);
            if (lifetime is null)
            {
                return Failure(LauncherProcessStartOutcome.TerminationUnconfirmed);
            }
            using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            var startInfo = new ProcessStartInfo
            {
                FileName = executableLease.ExecutablePath,
                WorkingDirectory = executableLease.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            startInfo.ArgumentList.Add("--managed-root");
            startInfo.ArgumentList.Add(Path.GetFullPath(managedRoot));
            startInfo.ArgumentList.Add("--state-path");
            startInfo.ArgumentList.Add(Path.GetFullPath(statePath));
            string expected = LauncherReadyProtocol.CreateExpectedPrefix(launcher);
            startInfo.Environment[ReadyPipeHandleEnvironment] = pipe.GetClientHandleAsString();
            startInfo.Environment[ExpectedReadyEnvironment] = expected;
            lifetime.ApplyInheritedContext(startInfo);
            _ = startInfo.Environment.Remove(
                InheritedManagedBootstrapIdentityContext.EnvironmentName);
            if (_inheritedBootstrapIdentity is not null)
            {
                InheritedManagedBootstrapIdentityContext.Apply(
                    startInfo,
                    _inheritedBootstrapIdentity);
            }
            try
            {
                process = ProcessLaunchGate.StartContained(
                    startInfo,
                    [
                        ProcessInheritedHandle.Parse(
                            ReadyPipeHandleEnvironment,
                            pipe.GetClientHandleAsString()),
                        new ProcessInheritedHandle(
                            ManagedProcessLifetimeLease.HandleEnvironment,
                            lifetime.InheritedHandleValue),
                    ],
                    () =>
                    {
                        _beforeStartValidation?.Invoke();
                        return executableLease.TryValidateForStart();
                    });
            }
            finally
            {
                DisposeLocalClientHandle(pipe);
            }
            if (process is null)
            {
                return Failure(LauncherProcessStartOutcome.StartFailed);
            }
            if (!await _admission.ReportAdmittedAsync(cancellationToken).ConfigureAwait(false))
            {
                return Terminate(process, lifetime, LauncherProcessStartOutcome.StartFailed);
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(readyDeadline);
            Task<string?> readyTask = BoundedUtf8LineReader.ReadAsync(
                pipe,
                MaximumReadyLineCharacters,
                bufferSize: 512,
                deadline.Token);
            Task exitTask = process.WaitForExitAsync(deadline.Token);
            Task completed = await Task.WhenAny(readyTask, exitTask).ConfigureAwait(false);
            if (readyTask.IsCompletedSuccessfully)
            {
                if (LauncherReadyProtocol.TryParse(
                        readyTask.Result,
                        expected,
                        out ManagedVersionAdmission? readyAdmission))
                {
                    return lifetime.TryReleaseAcceptedTree()
                        ? new(LauncherProcessStartOutcome.Ready, null, readyAdmission)
                        : Terminate(process, lifetime, LauncherProcessStartOutcome.StartFailed);
                }
                if (readyTask.Result is { Length: 0 })
                {
                    await exitTask.ConfigureAwait(false);
                    return Exited(process, lifetime);
                }
                return Terminate(process, lifetime, LauncherProcessStartOutcome.InvalidReadySignal);
            }
            if (completed == exitTask && exitTask.IsCompletedSuccessfully)
            {
                return Exited(process, lifetime);
            }
            await completed.ConfigureAwait(false);
            return Terminate(process, lifetime, LauncherProcessStartOutcome.ReadyTimeout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return process is null
                ? new(LauncherProcessStartOutcome.ReadyTimeout, null)
                : Terminate(process, lifetime!, LauncherProcessStartOutcome.ReadyTimeout);
        }
        catch (OperationCanceledException)
        {
            if (process is not null)
            {
                _ = _termination.ConfirmExited(process);
                _ = lifetime?.TerminateTreeAndConfirmEmpty(ManagedProcessTermination.DefaultWaitTimeout);
            }
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException or
            DecoderFallbackException or Win32Exception)
        {
            return process is null
                ? new(LauncherProcessStartOutcome.StartFailed, null)
                : Terminate(process, lifetime!, LauncherProcessStartOutcome.StartFailed);
        }
        finally
        {
            DisposeProcess(process);
            lifetime?.Dispose();
        }
    }

    private static LauncherProcessStartResult Failure(LauncherProcessStartOutcome outcome)
    {
        return new(outcome, null);
    }

    private LauncherProcessStartResult Exited(
        Process process,
        ManagedProcessLifetimeLease lifetime)
    {
        int exitCode = process.ExitCode;
        LauncherProcessStartResult result = Terminate(
            process,
            lifetime,
            LauncherProcessStartOutcome.ExitedBeforeReady);
        return exitCode == LauncherBootstrapRuntime.UnconfirmedTerminationExitCode
            ? new(LauncherProcessStartOutcome.TerminationUnconfirmed, exitCode)
            : result;
    }

    private LauncherProcessStartResult Terminate(
        Process process,
        ManagedProcessLifetimeLease lifetime,
        LauncherProcessStartOutcome confirmedOutcome)
    {
        ManagedProcessTerminationResult termination = _termination.ConfirmExited(process);
        bool treeExited = lifetime.TerminateTreeAndConfirmEmpty(ManagedProcessTermination.DefaultWaitTimeout);
        return termination.IsExitConfirmed && treeExited
            ? new(confirmedOutcome, termination.ExitCode)
            : new(LauncherProcessStartOutcome.TerminationUnconfirmed, null);
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
