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
        using var expiry = new CancellationTokenSource();
        Task<ManagedProcessStartResult>? start = null;
        try
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, "ready");
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", processMarker);
            using TestExecutableLaunchLease executableLease = ExecutableLease(workspace.Root, version);
            var adapter = new AnonymousPipeManagedApplicationProcess(
                statePath,
                ManagedProcessTermination.Instance,
                beforeStartValidation: _ =>
                {
                    entered.Set();
                    release.Wait(TestContext.Current.CancellationToken);
                },
                deadlineSignal: expiry.Token);
            start = adapter.StartUntilReadyAsync(
                workspace.Root,
                version,
                executableLease,
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken).AsTask();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            expiry.Cancel();
            Assert.False(start.IsCompleted);
            release.Set();
            ManagedProcessStartResult result = await start.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(ManagedProcessStartOutcome.ReadyTimeout, result.Outcome);
            Assert.False(File.Exists(processMarker));
            Assert.Equal(
                ManagedProcessLifetimeStatus.Exited,
                ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeKind.Application));
            ManagedProcessStartResult rollback = await new AnonymousPipeManagedApplicationProcess(statePath)
                .StartUntilReadyAsync(workspace.Root, version, executableLease,
                    TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(ManagedProcessStartOutcome.Ready, rollback.Outcome);
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
