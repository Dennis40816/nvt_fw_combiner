using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedLauncherProcessTests
{
    /// <summary>A blocked Launcher validation expires without a late child or retained handles.</summary>
    [Fact]
    public async Task BlockedValidationExpiresBeforeLauncherCreationAndCleansUp()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        string processMarker = Path.Combine(workspace.Root, "unexpected-launcher-start.txt");
        string? previousBehavior = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        string? previousMarker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        IntPtr readyHandle = IntPtr.Zero;
        IntPtr lifetimeHandle = IntPtr.Zero;
        Task<LauncherProcessStartResult>? start = null;
        try
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "ready");
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", processMarker);
            using TestExecutableLaunchLease executableLease = ExecutableLease(workspace.Root, identity);
            using BootstrapAdmissionSignal admission = BootstrapAdmissionSignal.Capture();
            var adapter = new AnonymousPipeManagedLauncherProcess(
                ManagedProcessTermination.Instance,
                admission,
                beforeStartValidation: startInfo =>
                {
                    readyHandle = new IntPtr(long.Parse(
                        startInfo.Environment[AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment]!,
                        CultureInfo.InvariantCulture));
                    lifetimeHandle = new IntPtr(long.Parse(
                        startInfo.Environment[ManagedProcessLifetimeLease.HandleEnvironment]!,
                        CultureInfo.InvariantCulture));
                    entered.Set();
                    release.Wait(TestContext.Current.CancellationToken);
                });
            var stopwatch = Stopwatch.StartNew();
            start = Task.Run(async () => await adapter.StartUntilReadyAsync(
                workspace.Root,
                statePath,
                identity,
                executableLease,
                TimeSpan.FromMilliseconds(150),
                TestContext.Current.CancellationToken));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            LauncherProcessStartResult result = await start.WaitAsync(
                TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            Assert.Equal(LauncherProcessStartOutcome.ReadyTimeout, result.Outcome);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            Assert.False(File.Exists(processMarker));
            release.Set();
            using var cleanupDeadline = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            cleanupDeadline.CancelAfter(TimeSpan.FromSeconds(2));
            while (ManagedProcessLifetimeLease.GetStatus(
                       statePath, ManagedProcessLifetimeKind.Launcher) != ManagedProcessLifetimeStatus.Exited ||
                   GetHandleInformation(readyHandle, out _) ||
                   GetHandleInformation(lifetimeHandle, out _))
            {
                await Task.Delay(20, cleanupDeadline.Token);
            }
            Assert.False(File.Exists(processMarker));
            Assert.Equal(
                ManagedProcessLifetimeStatus.Exited,
                ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeKind.Launcher));
            Assert.False(GetHandleInformation(readyHandle, out _));
            Assert.Equal(6, Marshal.GetLastPInvokeError());
            Assert.False(GetHandleInformation(lifetimeHandle, out _));
            Assert.Equal(6, Marshal.GetLastPInvokeError());
        }
        finally
        {
            release.Set();
            if (start is not null)
            {
                _ = await start.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", previousMarker);
        }
    }
}
