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
        string? previousVersion = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION");
        string? previousAdmission = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION");
        string? previousManifest = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var expiry = new CancellationTokenSource();
        Task<LauncherProcessStartResult>? start = null;
        try
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "ready");
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", processMarker);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", identity.OwnerAppVersion.ToString());
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", identity.OwnerAdmissionIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", identity.OwnerReleaseManifestSha256);
            using TestExecutableLaunchLease executableLease = ExecutableLease(workspace.Root, identity);
            using BootstrapAdmissionSignal admission = BootstrapAdmissionSignal.Capture();
            var adapter = new AnonymousPipeManagedLauncherProcess(
                ManagedProcessTermination.Instance,
                admission,
                beforeStartValidation: _ =>
                {
                    entered.Set();
                    release.Wait(TestContext.Current.CancellationToken);
                },
                deadlineSignal: expiry.Token);
            start = adapter.StartUntilReadyAsync(
                workspace.Root,
                statePath,
                identity,
                executableLease,
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken).AsTask();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            expiry.Cancel();
            Assert.False(start.IsCompleted);
            release.Set();
            LauncherProcessStartResult result = await start.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(LauncherProcessStartOutcome.ReadyTimeout, result.Outcome);
            Assert.False(File.Exists(processMarker));
            Assert.Equal(
                ManagedProcessLifetimeStatus.Exited,
                ManagedProcessLifetimeLease.GetStatus(statePath, ManagedProcessLifetimeKind.Launcher));
            LauncherProcessStartResult rollback = await new AnonymousPipeManagedLauncherProcess(
                    ManagedProcessTermination.Instance, admission)
                .StartUntilReadyAsync(workspace.Root, statePath, identity, executableLease,
                    TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(LauncherProcessStartOutcome.Ready, rollback.Outcome);
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
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", previousVersion);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", previousAdmission);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", previousManifest);
        }
    }
}
