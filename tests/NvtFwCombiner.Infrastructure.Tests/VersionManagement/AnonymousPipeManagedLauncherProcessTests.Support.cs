using System.IO.Pipes;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedLauncherProcessTests
{
    private static async ValueTask<LauncherProcessStartResult> RunAsync(
        string managedRoot,
        string statePath,
        ManagedLauncherIdentity identity,
        string behavior,
        string? argumentsPath,
        TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        string? previousBehavior = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        string? previousArguments = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH");
        string? previousVersion = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION");
        string? previousAdmission = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION");
        string? previousManifest = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST");
        try
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", behavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", argumentsPath);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", identity.OwnerAppVersion.ToString());
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", identity.OwnerAdmissionIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", identity.OwnerReleaseManifestSha256);
            using TestExecutableLaunchLease executableLease = ExecutableLease(managedRoot, identity);
            using BootstrapAdmissionSignal admission = BootstrapAdmissionSignal.Capture();
            var process = new AnonymousPipeManagedLauncherProcess(
                ManagedProcessTermination.Instance,
                admission);
            return await process.StartUntilReadyAsync(
                managedRoot,
                statePath,
                identity,
                executableLease,
                deadline,
                cancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", previousArguments);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", previousVersion);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", previousAdmission);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", previousManifest);
        }
    }

    private static TestExecutableLaunchLease ExecutableLease(
        string managedRoot,
        ManagedLauncherIdentity identity)
    {
        string workingDirectory = Path.Combine(
            managedRoot,
            "versions",
            identity.OwnerAppVersion.ToString(),
            "launcher");
        return new TestExecutableLaunchLease(
            Path.Combine(workingDirectory, "NvtFwCombiner.Launcher.exe"),
            workingDirectory);
    }

    private static bool IsRunning(int processId)
    {
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

    private static void WithOuterReadyEnvironment(string? handle, string? expected, Action action)
    {
        string? previousHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment);
        string? previousExpected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                handle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                expected);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                previousHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                previousExpected);
        }
    }

    private static string DuplicateInheritableClientHandle(AnonymousPipeServerStream pipe)
    {
        IntPtr process = GetCurrentProcess();
        IntPtr source = new(long.Parse(
            pipe.GetClientHandleAsString(),
            NumberStyles.None,
            CultureInfo.InvariantCulture));
        if (!DuplicateHandle(
                process,
                source,
                process,
                out IntPtr duplicate,
                desiredAccess: 0,
                inheritHandle: true,
                DuplicateSameAccess))
        {
            throw new InvalidOperationException(
                $"Client handle duplication failed with {Marshal.GetLastPInvokeError()}.");
        }
#pragma warning disable CA2000 // Ownership transfers to BootstrapAdmissionSignal through the raw handle.
        var owned = new SafePipeHandle(duplicate, ownsHandle: true);
#pragma warning restore CA2000
        string value = owned.DangerousGetHandle().ToInt64().ToString(CultureInfo.InvariantCulture);
        owned.SetHandleAsInvalid();
        owned.Dispose();
        return value;
    }

    private static void WithBootstrapStartEnvironment(
        string? context,
        string? handle,
        Action action)
    {
        string? priorContext = Environment.GetEnvironmentVariable(BootstrapStartGate.ContextEnvironment);
        string? priorHandle = Environment.GetEnvironmentVariable(BootstrapStartGate.HandleEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(BootstrapStartGate.ContextEnvironment, context);
            Environment.SetEnvironmentVariable(BootstrapStartGate.HandleEnvironment, handle);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(BootstrapStartGate.ContextEnvironment, priorContext);
            Environment.SetEnvironmentVariable(BootstrapStartGate.HandleEnvironment, priorHandle);
        }
    }

    private sealed class FailingTerminationOperations : IManagedProcessTerminationOperations
    {
        public bool HasExited(Process process)
        {
            return false;
        }

        public void Kill(Process process)
        {
            process.Kill(entireProcessTree: true);
        }

        public bool WaitForExit(Process process, TimeSpan timeout)
        {
            throw new InvalidOperationException("Injected wait failure.");
        }

        public int GetExitCode(Process process)
        {
            return process.ExitCode;
        }
    }

    private static ManagedLauncherIdentity PrepareProbe(string managedRoot)
    {
        ManagedAppVersion owner = ManagedAppVersion.Parse("0.10.6");
        string launcherRoot = Path.Combine(managedRoot, "versions", owner.ToString(), "launcher");
        _ = Directory.CreateDirectory(launcherRoot);
        string probeRoot = Path.Combine(AppContext.BaseDirectory, "ready-probe");
        foreach (string source in Directory.EnumerateFiles(probeRoot))
        {
            string fileName = Path.GetFileName(source);
            string targetName = string.Equals(fileName, "NvtFwCombiner.ReadyProbe.exe", StringComparison.OrdinalIgnoreCase)
                ? "NvtFwCombiner.Launcher.exe"
                : fileName;
            File.Copy(source, Path.Combine(launcherRoot, targetName));
        }
        string executable = Path.Combine(launcherRoot, "NvtFwCombiner.Launcher.exe");
        byte[] bytes = File.ReadAllBytes(executable);
        return ManagedLauncherIdentity.Create(
            owner,
            "catalog-identity-0.10.6",
            new string('a', 64),
            ManagedAppVersion.Parse("1.0.0"),
            ManagedLauncherIdentity.SupportedProtocolVersion,
            ManagedLauncherIdentity.ExecutablePath,
            bytes.LongLength,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
    }

    private const uint DuplicateSameAccess = 0x00000002;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DuplicateHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint options);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();
}
