using System.IO.Pipes;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedApplicationProcessTests
{
    private static StableLauncherHandoff.ImmutableBootstrapProcessLaunch CreateBootstrapLaunch(
        Process process,
        AnonymousPipeServerStream admissionPipe,
        string root,
        IManagedProcessTermination? termination = null)
    {
        ManagedProcessLifetimeLease lifetime = ManagedProcessLifetimeLease.TryAcquire(
            Path.Combine(root, "state.json"),
            ManagedProcessLifetimeKind.Bootstrap) ??
            throw new InvalidOperationException("Bootstrap lifetime was not acquired.");
#pragma warning disable CA2000 // Gate and lifetime ownership transfer into the returned receipt.
        return new(
            Task.FromResult<Process?>(process),
            admissionPipe,
            new BootstrapStartAuthorization(),
            lifetime,
            termination ?? ManagedProcessTermination.Instance);
#pragma warning restore CA2000
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Process, pipe, gate, and lifetime ownership transfer into the returned receipt.")]
    private static StableLauncherHandoff.ImmutableBootstrapProcessLaunch StartBootstrapTree(
        string root,
        string marker,
        string behavior,
        out AnonymousPipeClientStream client,
        out int processId)
    {
        StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch = StartBootstrapTreeCore(
            root,
            marker,
            behavior,
            provideAdmissionWriter: true,
            out AnonymousPipeClientStream? createdClient,
            out processId);
        client = createdClient ?? throw new InvalidOperationException(
            "Bootstrap admission writer was not created.");
        return launch;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Process, pipe, gate, and lifetime ownership transfer into the returned receipt.")]
    private static StableLauncherHandoff.ImmutableBootstrapProcessLaunch
        StartBootstrapTreeWithoutAdmissionWriter(
            string root,
            string marker,
            string behavior,
            out int processId)
    {
        return StartBootstrapTreeCore(
            root,
            marker,
            behavior,
            provideAdmissionWriter: false,
            out _,
            out processId);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Process, pipe, gate, and lifetime ownership transfer into the returned receipt.")]
    private static StableLauncherHandoff.ImmutableBootstrapProcessLaunch StartBootstrapTreeCore(
        string root,
        string marker,
        string behavior,
        bool provideAdmissionWriter,
        out AnonymousPipeClientStream? client,
        out int processId)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        string statePath = Path.Combine(root, "bootstrap-state.json");
        ManagedProcessLifetimeLease lifetime = ManagedProcessLifetimeLease.TryAcquire(
            statePath,
            ManagedProcessLifetimeKind.Bootstrap) ??
            throw new InvalidOperationException("Bootstrap lifetime was not acquired.");
        var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        client = provideAdmissionWriter
            ? new AnonymousPipeClientStream(PipeDirection.Out, pipe.GetClientHandleAsString())
            : null;
        var startGate = new BootstrapStartAuthorization();
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                AppContext.BaseDirectory,
                "ready-probe",
                "NvtFwCombiner.ReadyProbe.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--state-path");
        startInfo.ArgumentList.Add(statePath);
        startInfo.Environment[BehaviorEnvironment] = behavior;
        startInfo.Environment["NVT_READY_PROBE_TREE_MARKER"] = marker;
        lifetime.ApplyInheritedContext(startInfo);
        Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Bootstrap tree probe did not start.");
        if (!provideAdmissionWriter)
        {
            pipe.DisposeLocalCopyOfClientHandle();
        }
        processId = process.Id;
        return new(
            Task.FromResult<Process?>(process),
            pipe,
            startGate,
            lifetime,
            ManagedProcessTermination.Instance);
    }

    private static async Task<int> WaitForProcessMarkerAsync(string marker)
    {
        long deadline = Environment.TickCount64 + 5_000;
        while (Environment.TickCount64 < deadline)
        {
            try
            {
                if (File.Exists(marker))
                {
                    string value = await File.ReadAllTextAsync(
                        marker,
                        TestContext.Current.CancellationToken);
                    if (int.TryParse(
                            value,
                            System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out int processId))
                    {
                        return processId;
                    }
                }
            }
            catch (IOException)
            {
            }
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        throw new Xunit.Sdk.XunitException("Process marker was not readable before its deadline.");
    }

    private static bool IsRunning(int processId)
    {
        if (processId == 0)
        {
            return false;
        }
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TerminateProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _ = process.WaitForExit(5_000);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or Win32Exception)
        {
        }
    }

    private static async Task WaitForBootstrapLifetimeExitAsync(string statePath)
    {
        long cleanupDeadline = Environment.TickCount64 + 2_000;
        while (ManagedProcessLifetimeLease.GetStatus(
                   statePath,
                   ManagedProcessLifetimeKind.Bootstrap) != ManagedProcessLifetimeStatus.Exited &&
               Environment.TickCount64 < cleanupDeadline)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        Assert.Equal(
            ManagedProcessLifetimeStatus.Exited,
            ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeKind.Bootstrap));
    }

    private static async Task WaitForProcessExitAsync(int processId)
    {
        long deadline = Environment.TickCount64 + 2_000;
        while (IsRunning(processId) && Environment.TickCount64 < deadline)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        Assert.False(IsRunning(processId));
    }

    private sealed class SlowThenRealTermination(TimeSpan delay) : IManagedProcessTermination
    {
        public ManagedProcessTerminationResult ConfirmExited(Process process)
        {
            Thread.Sleep(delay);
            return ManagedProcessTermination.Instance.ConfirmExited(process);
        }
    }

    private static void AssertProcessExited(int processId)
    {
        _ = Assert.Throws<ArgumentException>(() => Process.GetProcessById(processId));
    }

    private static Process StartSilentProbe(string root)
    {
        string probe = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = probe,
            UseShellExecute = false,
        };
        startInfo.Environment[BehaviorEnvironment] = "tree-grandchild";
        startInfo.Environment["NVT_READY_PROBE_TREE_MARKER"] = Path.Combine(root, "silent-probe");
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Probe did not start.");
    }

    private static async ValueTask<ManagedProcessStartResult> RunAsync(
        string managedRoot,
        ManagedAppVersion version,
        string behavior,
        TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        string? previous = Environment.GetEnvironmentVariable(BehaviorEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, behavior);
            using TestExecutableLaunchLease executableLease = ExecutableLease(managedRoot, version);
            string statePath = Path.Combine(managedRoot, "state", "version-manager.v1.json");
            return await new AnonymousPipeManagedApplicationProcess(statePath).StartUntilReadyAsync(
                managedRoot,
                version,
                executableLease,
                deadline,
                cancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, previous);
        }
    }

    private static void PrepareProbe(string managedRoot, ManagedAppVersion version)
    {
        string probeRoot = Path.Combine(AppContext.BaseDirectory, "ready-probe");
        string versionRoot = Path.Combine(managedRoot, "versions", version.ToString());
        _ = Directory.CreateDirectory(versionRoot);
        foreach (string source in Directory.EnumerateFiles(probeRoot))
        {
            string fileName = Path.GetFileName(source);
            string targetName = string.Equals(fileName, "NvtFwCombiner.ReadyProbe.exe", StringComparison.OrdinalIgnoreCase)
                ? "NvtFwCombiner.exe"
                : fileName;
            File.Copy(source, Path.Combine(versionRoot, targetName));
        }
    }

    private static TestExecutableLaunchLease ExecutableLease(
        string managedRoot,
        ManagedAppVersion version)
    {
        string workingDirectory = Path.Combine(managedRoot, "versions", version.ToString());
        return new TestExecutableLaunchLease(
            Path.Combine(workingDirectory, "NvtFwCombiner.exe"),
            workingDirectory);
    }

    private sealed record TestExecutableLaunchLease(
        string ExecutablePath,
        string WorkingDirectory,
        bool IsValidForStart = true) : IManagedExecutableLaunchLease
    {
        public bool TryValidateForStart()
        {
            return IsValidForStart;
        }
        public void Dispose() { }
    }

    private sealed class FailingTerminationOperations(bool failKill, bool failWait)
        : IManagedProcessTerminationOperations
    {
        public bool HasExited(Process process)
        {
            return false;
        }

        public void Kill(Process process)
        {
            process.Kill(entireProcessTree: true);
            if (failKill)
            {
                throw new Win32Exception("Injected kill failure.");
            }
        }

        public bool WaitForExit(Process process, TimeSpan timeout)
        {
            return !failWait
                ? process.WaitForExit(checked((int)Math.Ceiling(timeout.TotalMilliseconds)))
                : throw new InvalidOperationException("Injected wait failure.");
        }

        public int GetExitCode(Process process)
        {
            return process.ExitCode;
        }
    }

    private sealed class AggregateKillFailureOperations(bool rootExitsDuringKill)
        : IManagedProcessTerminationOperations
    {
        private bool _rootExited;
        public int HasExitedCalls { get; private set; }

        public bool HasExited(Process process)
        {
            HasExitedCalls++;
            return _rootExited;
        }

        public void Kill(Process process)
        {
            _rootExited = rootExitsDuringKill;
            throw new AggregateException("Injected partial tree-kill failure.");
        }

        public bool WaitForExit(Process process, TimeSpan timeout)
        {
            throw new InvalidOperationException("Wait must not follow a failed tree kill.");
        }

        public int GetExitCode(Process process)
        {
            return 0;
        }
    }

    private sealed class ExpiringWaitOperations : IManagedProcessTerminationOperations
    {
        public TimeSpan? ObservedTimeout { get; private set; }

        public bool HasExited(Process process)
        {
            return false;
        }

        public void Kill(Process process)
        {
        }

        public bool WaitForExit(Process process, TimeSpan timeout)
        {
            ObservedTimeout = timeout;
            return false;
        }

        public int GetExitCode(Process process)
        {
            return 0;
        }
    }
}
