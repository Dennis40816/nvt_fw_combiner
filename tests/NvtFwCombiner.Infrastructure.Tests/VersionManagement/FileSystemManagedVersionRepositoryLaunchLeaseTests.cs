using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Runs repository-owned stable executable leases across the real process boundary.</summary>
public sealed partial class FileSystemManagedVersionRepositoryTests
{
    /// <summary>The repository's real verified lease remains write/delete-denying across the adapter's Process.Start.</summary>
    [Fact]
    public async Task RepositoryLeaseRemainsStableThroughManagedProcessStart()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = workspace.PathFor("managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6", useReadyProbe: true);
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        Assert.True(installed.IsSuccess, installed.Issue.ToString());
        ManagedExecutableLaunchLeaseResult acquired = await repository.AcquireApplicationLaunchLeaseAsync(
            managedRoot,
            installed.Admission!,
            TestContext.Current.CancellationToken);
        Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
        string? previousBehavior = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        string executable = acquired.Lease!.ExecutablePath;
        string displaced = executable + ".displaced";
        try
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "ready");
            ManagedProcessStartResult started = await new AnonymousPipeManagedApplicationProcess(
                    workspace.PathFor("state/version-manager.v1.json"))
                .StartUntilReadyAsync(
                    managedRoot,
                    installed.Admission!.Version,
                    acquired.Lease,
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            Assert.Equal(ManagedProcessStartOutcome.Ready, started.Outcome);
            _ = Assert.Throws<IOException>(() => File.Move(executable, displaced));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", previousBehavior);
            acquired.Lease.Dispose();
        }
        await Task.Delay(500, TestContext.Current.CancellationToken);
        File.Move(executable, displaced);
        File.Move(displaced, executable);
    }
}
