using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Runs repository-owned stable executable leases across the real process boundary.</summary>
public sealed partial class FileSystemManagedVersionRepositoryTests
{
    /// <summary>A child added at the final start gate is rejected before Process.Start.</summary>
    [Fact]
    public async Task RepositoryLeaseRejectsLateChildAtFinalProcessStartGate()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        var repository = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6", useReadyProbe: true),
            TestContext.Current.CancellationToken);
        ManagedExecutableLaunchLeaseResult acquired = await repository
            .AcquireApplicationLaunchLeaseAsync(
                managedRoot,
                installed.Admission!,
                TestContext.Current.CancellationToken);
        Assert.True(acquired.IsAcquired, acquired.Issue.ToString());
        string unexpected = Path.Combine(managedRoot, "versions", "0.10.6", "late.dll");
        var process = new AnonymousPipeManagedApplicationProcess(
            workspace.PathFor("state/version-manager.v1.json"),
            ManagedProcessTermination.Instance,
            beforeStartValidation: () => File.WriteAllText(unexpected, "foreign"));

        ManagedProcessStartResult started = await process.StartUntilReadyAsync(
            managedRoot,
            installed.Admission!.Version,
            acquired.Lease!,
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedProcessStartOutcome.StartFailed, started.Outcome);
        Assert.True(File.Exists(unexpected));
        acquired.Lease!.Dispose();
    }

    /// <summary>Cancellation after full package proof releases custody and preserves healthy inventory.</summary>
    [Fact]
    public async Task CancellationAfterClosedPackageProofReleasesTreeAndPreservesInventory()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6", useReadyProbe: true);
        var installer = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await installer.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        Assert.True(installed.IsSuccess, installed.Issue.ToString());
        using var cancellation = new CancellationTokenSource();
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            Directory.EnumerateDirectories,
            Directory.Exists,
            custodyHook: null,
            beforeLeaseCreation: cancellation.Cancel);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await repository.AcquireApplicationLaunchLeaseAsync(
                managedRoot,
                installed.Admission!,
                cancellation.Token));

        string executable = Path.Combine(managedRoot, "versions", "0.10.6", "NvtFwCombiner.exe");
        File.AppendAllText(executable, string.Empty);
        ManagedVersionInventory inventory = RequireInventory(await installer.InventoryAsync(
            managedRoot,
            [installed.Admission!],
            installed.Admission!.Version,
            installed.Admission.Version,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, inventory.HealthyCount);
        Assert.Equal(0, inventory.DamagedCount);
    }

    /// <summary>Late package topology mutation fails before launch and inventory reports damage.</summary>
    [Fact]
    public async Task LateClosedPackageAdditionFailsLeaseAndIsVisibleToInventory()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6", useReadyProbe: true);
        var installer = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await installer.InstallAsync(
            managedRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);
        Assert.True(installed.IsSuccess, installed.Issue.ToString());
        string unexpected = Path.Combine(managedRoot, "versions", "0.10.6", "unexpected.dll");
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            Directory.EnumerateDirectories,
            Directory.Exists,
            custodyHook: null,
            beforeLeaseCreation: () => File.WriteAllText(unexpected, "foreign"));

        ManagedExecutableLaunchLeaseResult acquired = await repository
            .AcquireApplicationLaunchLeaseAsync(
                managedRoot,
                installed.Admission!,
                TestContext.Current.CancellationToken);
        ManagedVersionInventory inventory = RequireInventory(await installer.InventoryAsync(
            managedRoot,
            [installed.Admission!],
            installed.Admission!.Version,
            installed.Admission.Version,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken));

        Assert.Null(acquired.Lease);
        Assert.Equal(ManagedExecutableLaunchIssue.UnsafePath, acquired.Issue);
        Assert.Equal(1, inventory.DamagedCount);
    }

    /// <summary>The repository's real verified lease remains write/delete-denying across the adapter's Process.Start.</summary>
    [Fact]
    public async Task RepositoryLeaseRemainsStableThroughManagedProcessStart()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
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
