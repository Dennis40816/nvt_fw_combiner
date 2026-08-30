using System.Diagnostics;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises the OS-backed, exact-identity version-manager writer lease.</summary>
public sealed class FileSystemVersionManagerWriteLeaseTests
{
    /// <summary>The recovery executor can prove only the live exact-path production lease.</summary>
    [Fact]
    public async Task RecoveryCapabilityIsLiveExactAndNotForgeable()
    {
        using var workspace = TempWorkspace.Create("nfc-version-lease");
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        string otherPath = Path.Combine(workspace.Root, "state", "other.v1.json");
        var store = new JsonVersionManagerStateStore(statePath);

        VersionManagerWriteLeaseResult lease = await store.TryAcquireWriteLeaseAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        Assert.True(lease.HoldsStatePath(statePath));
        Assert.True(lease.HoldsStatePath(Path.Combine(
            Path.GetDirectoryName(statePath)!,
            "unused",
            "..",
            Path.GetFileName(statePath))));
        Assert.False(lease.HoldsStatePath(otherPath));
        using (var disposable = new DisposableStub())
        using (var forged = new VersionManagerWriteLeaseResult(
                   VersionManagerWriteLeaseIssue.None,
                   disposable))
        {
            Assert.False(forged.HoldsStatePath(statePath));
        }

        lease.Dispose();

        Assert.False(lease.HoldsStatePath(statePath));
    }

    /// <summary>One canonical state file has one writer across store instances.</summary>
    [Fact]
    public async Task SameStateHasOneCrossInstanceWriter()
    {
        using var workspace = TempWorkspace.Create("nfc-version-lease");
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        var firstStore = new JsonVersionManagerStateStore(statePath);
        var secondStore = new JsonVersionManagerStateStore(statePath);

        using VersionManagerWriteLeaseResult first = await firstStore.TryAcquireWriteLeaseAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);
        using VersionManagerWriteLeaseResult contended = await secondStore.TryAcquireWriteLeaseAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsAcquired);
        Assert.Equal(VersionManagerWriteLeaseIssue.Busy, contended.Issue);
    }

    /// <summary>Independent state files retain independent writer leases.</summary>
    [Fact]
    public async Task DifferentStatePathsDoNotContend()
    {
        using var workspace = TempWorkspace.Create("nfc-version-lease");
        var firstStore = new JsonVersionManagerStateStore(
            Path.Combine(workspace.Root, "state-a", "version-manager.v1.json"));
        var secondStore = new JsonVersionManagerStateStore(
            Path.Combine(workspace.Root, "state-b", "version-manager.v1.json"));

        using VersionManagerWriteLeaseResult first = await firstStore.TryAcquireWriteLeaseAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);
        using VersionManagerWriteLeaseResult second = await secondStore.TryAcquireWriteLeaseAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
    }

    /// <summary>Lexically different paths resolving to the same state file share one writer.</summary>
    [Fact]
    public async Task CanonicallyEquivalentStatePathsContend()
    {
        using var workspace = TempWorkspace.Create("nfc-version-lease");
        string stateDirectory = Path.Combine(workspace.Root, "state");
        var directStore = new JsonVersionManagerStateStore(
            Path.Combine(stateDirectory, "version-manager.v1.json"));
        var equivalentStore = new JsonVersionManagerStateStore(
            Path.Combine(stateDirectory, "unused", "..", "version-manager.v1.json"));

        using VersionManagerWriteLeaseResult first = await directStore.TryAcquireWriteLeaseAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);
        using VersionManagerWriteLeaseResult contended = await equivalentStore.TryAcquireWriteLeaseAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsAcquired);
        Assert.Equal(VersionManagerWriteLeaseIssue.Busy, contended.Issue);
    }

    /// <summary>Disposing the writer handle makes the exact identity immediately available.</summary>
    [Fact]
    public async Task DisposedWriterCanBeAcquiredAgain()
    {
        using var workspace = TempWorkspace.Create("nfc-version-lease");
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        var store = new JsonVersionManagerStateStore(statePath);
        using (VersionManagerWriteLeaseResult first = await store.TryAcquireWriteLeaseAsync(
                   TimeSpan.Zero,
                   TestContext.Current.CancellationToken))
        {
            Assert.True(first.IsAcquired);
        }

        using VersionManagerWriteLeaseResult second = await store.TryAcquireWriteLeaseAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        Assert.True(second.IsAcquired);
    }

    /// <summary>A terminated Windows process abandons its file lease for restart convergence.</summary>
    [Fact]
    public async Task WindowsAbandonedProcessReleasesWriterForRestartConvergence()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-version-lease");
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        string lockPath = FileSystemVersionManagerWriteLease.GetLockPath(statePath);
        string readyPath = Path.Combine(workspace.Root, "lease-ready.txt");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add(
            "$stream=[IO.File]::Open($env:NVT_LEASE_PATH,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);" +
            "[IO.File]::WriteAllText($env:NVT_LEASE_READY,'ready');Start-Sleep -Seconds 30");
        process.StartInfo.Environment["NVT_LEASE_PATH"] = lockPath;
        process.StartInfo.Environment["NVT_LEASE_READY"] = readyPath;
        Assert.True(process.Start());
        try
        {
            await WaitForFileAsync(readyPath, process, TestContext.Current.CancellationToken);
            var store = new JsonVersionManagerStateStore(statePath);
            using VersionManagerWriteLeaseResult held = await store.TryAcquireWriteLeaseAsync(
                TimeSpan.Zero,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionManagerWriteLeaseIssue.Busy, held.Issue);

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            using VersionManagerWriteLeaseResult recovered = await store.TryAcquireWriteLeaseAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            Assert.True(recovered.IsAcquired);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private static async Task WaitForFileAsync(
        string path,
        Process process,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!File.Exists(path))
        {
            Assert.False(process.HasExited, "Lease-holder process exited before acquiring the file lease.");
            await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
        }
    }

    private sealed class DisposableStub : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
