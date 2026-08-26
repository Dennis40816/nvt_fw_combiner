using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Starts one exact version-scoped launcher and accepts one identity-bound READY line.</summary>
internal sealed class AnonymousPipeManagedLauncherProcess : IManagedLauncherProcess
{
    internal const string ReadyPipeHandleEnvironment = "NVT_FW_COMBINER_LAUNCHER_READY_PIPE_HANDLE";
    internal const string ExpectedReadyEnvironment = "NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY";
    private const int MaximumReadyLineCharacters = 4096;

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
                ManagedPathSafety.IsReparsePoint(executable))
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
            process = Process.Start(startInfo);
            if (process is null)
            {
                return Failure(LauncherProcessStartOutcome.StartFailed);
            }
            pipe.DisposeLocalCopyOfClientHandle();

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
                    return new(LauncherProcessStartOutcome.ExitedBeforeReady, process.ExitCode);
                }
                KillIfRunning(process);
                return new(LauncherProcessStartOutcome.InvalidReadySignal, process.HasExited ? process.ExitCode : null);
            }
            if (completed == exitTask && exitTask.IsCompletedSuccessfully)
            {
                return new(LauncherProcessStartOutcome.ExitedBeforeReady, process.ExitCode);
            }
            await completed.ConfigureAwait(false);
            KillIfRunning(process);
            return new(LauncherProcessStartOutcome.ReadyTimeout, process.HasExited ? process.ExitCode : null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillIfRunning(process);
            return new(LauncherProcessStartOutcome.ReadyTimeout, process?.HasExited == true ? process.ExitCode : null);
        }
        catch (OperationCanceledException)
        {
            KillIfRunning(process);
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or InvalidOperationException or DecoderFallbackException)
        {
            KillIfRunning(process);
            return new(LauncherProcessStartOutcome.StartFailed, process?.HasExited == true ? process.ExitCode : null);
        }
        finally
        {
            process?.Dispose();
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

    private static void KillIfRunning(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited)
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

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}

/// <summary>Public host seam used only by the immutable Bootstrap executable.</summary>
public static class LauncherBootstrapRuntime
{
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
        var coordinator = new LauncherBootstrapCoordinator(
            root,
            state,
            new JsonVersionManagerStateStore(state),
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
            _ => 99,
        };
    }

    /// <summary>Reports nested app readiness only after reloading exact app and launcher identities.</summary>
    public static async ValueTask<bool> ReportNestedReadyAsync(
        string managedRoot,
        string statePath,
        CancellationToken cancellationToken)
    {
        string? handle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment);
        string? expected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment);
        Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment, null);
        Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment, null);
        if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        VersionManagerStateLoadResult app = await new JsonVersionManagerStateStore(statePath)
            .LoadAsync(cancellationToken).ConfigureAwait(false);
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

        try
        {
            await using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, handle);
            byte[] message = Encoding.UTF8.GetBytes(LauncherReadyProtocol.Create(running, admission) + "\n");
            await pipe.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
