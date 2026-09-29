using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedApplicationProcessTests
{
    /// <summary>A blocked final validation expires without a late child or retained start handles.</summary>
    [Fact]
    public async Task BlockedValidationExpiresBeforeProcessCreationAndCleansUp()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create();
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        PrepareProbe(workspace.Root, version);
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        string processMarker = Path.Combine(workspace.Root, "unexpected-application-start.txt");
        string? previousBehavior = Environment.GetEnvironmentVariable(BehaviorEnvironment);
        string? previousMarker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        IntPtr readyHandle = IntPtr.Zero;
        IntPtr lifetimeHandle = IntPtr.Zero;
        Task<ManagedProcessStartResult>? start = null;
        try
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, "ready");
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", processMarker);
            using TestExecutableLaunchLease executableLease = ExecutableLease(workspace.Root, version);
            var adapter = new AnonymousPipeManagedApplicationProcess(
                statePath,
                ManagedProcessTermination.Instance,
                beforeStartValidation: startInfo =>
                {
                    readyHandle = new IntPtr(long.Parse(
                        startInfo.Environment[AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment]!,
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
                version,
                executableLease,
                TimeSpan.FromMilliseconds(150),
                TestContext.Current.CancellationToken));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            ManagedProcessStartResult result = await start.WaitAsync(
                TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            Assert.Equal(ManagedProcessStartOutcome.ReadyTimeout, result.Outcome);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            Assert.False(File.Exists(processMarker));
            release.Set();
            using var cleanupDeadline = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            cleanupDeadline.CancelAfter(TimeSpan.FromSeconds(2));
            while (ManagedProcessLifetimeLease.GetStatus(
                       statePath, ManagedProcessLifetimeKind.Application) != ManagedProcessLifetimeStatus.Exited ||
                   GetApplicationReadyHandleInformation(readyHandle, out _) ||
                   GetApplicationReadyHandleInformation(lifetimeHandle, out _))
            {
                await Task.Delay(20, cleanupDeadline.Token);
            }
            Assert.False(File.Exists(processMarker));
            Assert.Equal(
                ManagedProcessLifetimeStatus.Exited,
                ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeKind.Application));
            Assert.False(GetApplicationReadyHandleInformation(readyHandle, out _));
            Assert.Equal(6, Marshal.GetLastPInvokeError());
            Assert.False(GetApplicationReadyHandleInformation(lifetimeHandle, out _));
            Assert.Equal(6, Marshal.GetLastPInvokeError());
        }
        finally
        {
            release.Set();
            if (start is not null)
            {
                _ = await start.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            Environment.SetEnvironmentVariable(BehaviorEnvironment, previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", previousMarker);
        }
    }
}
