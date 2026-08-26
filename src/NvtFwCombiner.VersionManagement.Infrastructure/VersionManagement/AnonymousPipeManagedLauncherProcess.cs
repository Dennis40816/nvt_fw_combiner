using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;

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
    private const int MaximumReadyLineCharacters = 4096;
    private readonly IManagedProcessTermination _termination;

    internal AnonymousPipeManagedLauncherProcess()
        : this(ManagedProcessTermination.Instance)
    {
    }

    internal AnonymousPipeManagedLauncherProcess(IManagedProcessTermination termination)
    {
        _termination = termination ?? throw new ArgumentNullException(nameof(termination));
    }

    public async ValueTask<LauncherProcessStartResult> StartUntilReadyAsync(
        string managedRoot,
        string statePath,
        ManagedLauncherIdentity launcher,
        TimeSpan readyDeadline,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(readyDeadline, TimeSpan.Zero);
        Process? process = null;
        try
        {
            string versionRoot = ManagedPathSafety.GetExactVersionDirectory(
                Path.Combine(Path.GetFullPath(managedRoot), FileSystemManagedVersionRepository.VersionsDirectoryName),
                launcher.OwnerAppVersion);
            string executable = ManagedPathSafety.ResolvePayloadPath(versionRoot, launcher.ExecutableRelativePath);
            if (!ManagedPathSafety.IsSafeOwnedTree(versionRoot) ||
                !File.Exists(executable) ||
                ManagedPathSafety.IsReparsePoint(executable) ||
                !ManagedProcessExecutable.HasPortableExecutableHeader(executable))
            {
                return Failure(LauncherProcessStartOutcome.StartFailed);
            }

            using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable),
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
                return Failure(LauncherProcessStartOutcome.StartFailed);
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(readyDeadline);
            Task<string?> readyTask = ReadBoundedLineAsync(pipe, deadline.Token);
            Task exitTask = process.WaitForExitAsync(deadline.Token);
            Task completed = await Task.WhenAny(readyTask, exitTask).ConfigureAwait(false);
            if (readyTask.IsCompletedSuccessfully)
            {
                if (LauncherReadyProtocol.TryParse(
                        readyTask.Result,
                        expected,
                        out ManagedVersionAdmission? readyAdmission))
                {
                    return new(LauncherProcessStartOutcome.Ready, null, readyAdmission);
                }
                if (readyTask.Result is { Length: 0 })
                {
                    await exitTask.ConfigureAwait(false);
                    return Exited(process.ExitCode);
                }
                return Terminate(process, LauncherProcessStartOutcome.InvalidReadySignal);
            }
            if (completed == exitTask && exitTask.IsCompletedSuccessfully)
            {
                return Exited(process.ExitCode);
            }
            await completed.ConfigureAwait(false);
            return Terminate(process, LauncherProcessStartOutcome.ReadyTimeout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return process is null
                ? new(LauncherProcessStartOutcome.ReadyTimeout, null)
                : Terminate(process, LauncherProcessStartOutcome.ReadyTimeout);
        }
        catch (OperationCanceledException)
        {
            if (process is not null)
            {
                _ = _termination.ConfirmExited(process);
            }
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException or
            DecoderFallbackException or Win32Exception)
        {
            return process is null
                ? new(LauncherProcessStartOutcome.StartFailed, null)
                : Terminate(process, LauncherProcessStartOutcome.StartFailed);
        }
        finally
        {
            DisposeProcess(process);
        }
    }

    private static async Task<string?> ReadBoundedLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 512,
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

    private static LauncherProcessStartResult Failure(LauncherProcessStartOutcome outcome)
    {
        return new(outcome, null);
    }

    private static LauncherProcessStartResult Exited(int exitCode)
    {
        return exitCode == LauncherBootstrapRuntime.UnconfirmedTerminationExitCode
            ? new(LauncherProcessStartOutcome.TerminationUnconfirmed, exitCode)
            : new(LauncherProcessStartOutcome.ExitedBeforeReady, exitCode);
    }

    private LauncherProcessStartResult Terminate(
        Process process,
        LauncherProcessStartOutcome confirmedOutcome)
    {
        ManagedProcessTerminationResult termination = _termination.ConfirmExited(process);
        return termination.IsExitConfirmed
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

internal static class LauncherReadyProtocol
{
    internal static string CreateExpectedPrefix(ManagedLauncherIdentity launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        string admissionDigest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(launcher.OwnerAdmissionIdentity)));
        return string.Join(
            ':',
            "READY-LAUNCHER",
            launcher.ProtocolVersion,
            launcher.OwnerAppVersion,
            admissionDigest,
            launcher.OwnerReleaseManifestSha256,
            launcher.Sha256);
    }

    internal static string Create(
        ManagedLauncherIdentity launcher,
        ManagedVersionAdmission readyAdmission)
    {
        ArgumentNullException.ThrowIfNull(readyAdmission);
        return string.Join(
            ':',
            CreateExpectedPrefix(launcher),
            readyAdmission.Version,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(readyAdmission.AdmissionIdentity)),
            readyAdmission.ReleaseManifestSha256);
    }

    internal static bool TryParse(
        string? value,
        string expectedPrefix,
        out ManagedVersionAdmission? admission)
    {
        admission = null;
        if (value is null || !value.StartsWith(expectedPrefix + ":", StringComparison.Ordinal))
        {
            return false;
        }
        string[] fields = value[(expectedPrefix.Length + 1)..].Split(':');
        if (fields.Length != 3 ||
            !ManagedAppVersion.TryParse(fields[0], out ManagedAppVersion version) ||
            !IsLowerSha256(fields[2]))
        {
            return false;
        }
        try
        {
            string identity = Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
            if (string.IsNullOrWhiteSpace(identity) || identity.Length > 2048)
            {
                return false;
            }
            admission = new(version, identity, fields[2]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static bool IsExpectedPrefix(string? value)
    {
        if (value is null)
        {
            return false;
        }
        string[] fields = value.Split(':');
        return fields.Length == 6 &&
               string.Equals(fields[0], "READY-LAUNCHER", StringComparison.Ordinal) &&
               int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int protocol) &&
               protocol == ManagedLauncherIdentity.SupportedProtocolVersion &&
               ManagedAppVersion.TryParse(fields[2], out _) &&
               IsLowerSha256(fields[3]) &&
               IsLowerSha256(fields[4]) &&
               IsLowerSha256(fields[5]);
    }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}

/// <summary>Classification of one consumed outer launcher READY inheritance context.</summary>
public enum LauncherReadyInheritanceOutcome
{
    /// <summary>The Launcher was started without immutable-Bootstrap supervision.</summary>
    NotInherited,
    /// <summary>Both inherited values are syntactically valid and may be used once.</summary>
    Inherited,
    /// <summary>The inherited pair was partial, blank, or malformed.</summary>
    InvalidInheritedContext,
}

/// <summary>One consumed outer READY inheritance context with no public handshake material.</summary>
public sealed class LauncherReadyInheritance
{
    private LauncherReadyInheritance(
        LauncherReadyInheritanceOutcome outcome,
        string? handle,
        string? expected)
    {
        Outcome = outcome;
        Handle = handle;
        Expected = expected;
    }

    /// <summary>Gets the complete inherited-context classification.</summary>
    public LauncherReadyInheritanceOutcome Outcome { get; }

    internal string? Handle { get; }
    internal string? Expected { get; }

    internal static LauncherReadyInheritance NotInherited { get; } =
        new(LauncherReadyInheritanceOutcome.NotInherited, null, null);

    internal static LauncherReadyInheritance Invalid { get; } =
        new(LauncherReadyInheritanceOutcome.InvalidInheritedContext, null, null);

    internal static LauncherReadyInheritance CreateInherited(string handle, string expected)
    {
        return new(LauncherReadyInheritanceOutcome.Inherited, handle, expected);
    }
}

/// <summary>Public host seam used only by the immutable Bootstrap executable.</summary>
public static class LauncherBootstrapRuntime
{
    private const string SeedStateFileName = "version-manager.seed.v1.json";

    /// <summary>Launcher exit code preserving an unconfirmed nested-process cleanup journal.</summary>
    public const int UnconfirmedTerminationExitCode = 17;

    /// <summary>Runs exact launcher selection and rollback under the existing app-state writer lease.</summary>
    public static async ValueTask<int> RunAsync(
        string managedRoot,
        string statePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        string root = Path.GetFullPath(managedRoot);
        string state = Path.GetFullPath(statePath);
        var stateStore = new JsonVersionManagerStateStore(state);
        ManagedVersionSeedOutcome seedOutcome = await new ManagedVersionSeedBootstrapper(
                root,
                stateStore,
                new JsonVersionManagerStateStore(Path.Combine(root, SeedStateFileName)),
                new FileSystemManagedVersionRepository())
            .EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (seedOutcome is not ManagedVersionSeedOutcome.ExistingState and
            not ManagedVersionSeedOutcome.Seeded)
        {
            return seedOutcome is ManagedVersionSeedOutcome.StateUnavailable
                ? 18
                : seedOutcome is ManagedVersionSeedOutcome.ManagedRootMismatch
                    ? 11
                    : 10;
        }
        var coordinator = new LauncherBootstrapCoordinator(
            root,
            state,
            stateStore,
            new JsonLauncherBootstrapStateStore(state),
            new FileSystemInstalledLauncherRepository(),
            new AnonymousPipeManagedLauncherProcess());
        LauncherBootstrapResult result = await coordinator.RunAsync(cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            LauncherBootstrapOutcome.Ready => 0,
            LauncherBootstrapOutcome.RolledBack => 1,
            LauncherBootstrapOutcome.Busy => 2,
            LauncherBootstrapOutcome.InvalidState => 10,
            LauncherBootstrapOutcome.ManagedRootMismatch => 11,
            LauncherBootstrapOutcome.AppMutationPending => 12,
            LauncherBootstrapOutcome.DamagedLauncher => 13,
            LauncherBootstrapOutcome.ProtocolMismatch => 14,
            LauncherBootstrapOutcome.StartFailed => 15,
            LauncherBootstrapOutcome.RollbackUnavailable => 16,
            LauncherBootstrapOutcome.StateChanged => 17,
            LauncherBootstrapOutcome.StateUnavailable => 18,
            LauncherBootstrapOutcome.TerminationUnconfirmed => 19,
            _ => 99,
        };
    }

    /// <summary>Consumes and classifies the outer READY environment before a nested process can inherit it.</summary>
    public static LauncherReadyInheritance CaptureNestedReadyContext()
    {
        string? handle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment);
        string? expected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment);
        Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment, null);
        Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment, null);
        return handle is null && expected is null
            ? LauncherReadyInheritance.NotInherited
            : !string.IsNullOrWhiteSpace(handle) &&
               long.TryParse(handle, NumberStyles.None, CultureInfo.InvariantCulture, out long pipeHandle) &&
               pipeHandle > 0 &&
               LauncherReadyProtocol.IsExpectedPrefix(expected)
                ? LauncherReadyInheritance.CreateInherited(handle, expected!)
                : LauncherReadyInheritance.Invalid;
    }

    /// <summary>Reports nested app readiness only after reloading exact app and launcher identities.</summary>
    public static async ValueTask<bool> ReportNestedReadyAsync(
        LauncherReadyInheritance context,
        string managedRoot,
        string statePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Outcome != LauncherReadyInheritanceOutcome.Inherited)
        {
            return false;
        }
        string handle = context.Handle!;
        string expected = context.Expected!;

        try
        {
            var appStateStore = new JsonVersionManagerStateStore(statePath);
            using VersionManagerWriteLeaseResult lease = await appStateStore.TryAcquireWriteLeaseAsync(
                ManagedActivationCoordinator.DefaultWriterLeaseTimeout,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return false;
            }

            VersionManagerStateLoadResult app = await appStateStore.LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            LauncherBootstrapStateLoadResult launcher = await new JsonLauncherBootstrapStateStore(statePath)
                .LoadAsync(cancellationToken).ConfigureAwait(false);
            ManagedLauncherIdentity? running = new[]
            {
                launcher.State?.Pending?.Candidate,
                launcher.State?.Pending?.PreviousLastKnownGood,
                launcher.State?.Active,
            }.FirstOrDefault(identity =>
                identity is not null &&
                string.Equals(expected, LauncherReadyProtocol.CreateExpectedPrefix(identity), StringComparison.Ordinal));
            ManagedVersionAdmission? admission = app.State?.Admissions.SingleOrDefault(
                value => value.Version == app.State.ActiveVersion);
            if (!app.IsSuccess || !launcher.IsSuccess ||
                !app.State!.IsBoundToManagedRoot(managedRoot) ||
                !launcher.State!.IsBoundToManagedRoot(managedRoot) ||
                app.State!.PendingActivation is not null || app.State.PendingMutation is not null ||
                running is null || admission is null)
            {
                return false;
            }

            await using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, handle);
            byte[] message = Encoding.UTF8.GetBytes(LauncherReadyProtocol.Create(running, admission) + "\n");
            await pipe.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or Win32Exception)
        {
            return false;
        }
    }
}
