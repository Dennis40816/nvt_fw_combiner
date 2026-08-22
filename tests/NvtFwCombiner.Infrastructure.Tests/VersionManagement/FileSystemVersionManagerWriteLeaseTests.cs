using System.Diagnostics;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises the OS-backed, exact-identity version-manager writer lease.</summary>
public sealed class FileSystemVersionManagerWriteLeaseTests
{
    /// <summary>Exact state and managed-root identity has one writer across store instances.</summary>
    [Fact]
    public async Task ExactStateAndManagedRootHaveOneCrossInstanceWriter()
    {
        using var workspace = TempWorkspace.Create("nfc-version-lease");
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        string managedRoot = Path.Combine(workspace.Root, "managed");
        var firstStore = new JsonVersionManagerStateStore(statePath);
        var secondStore = new JsonVersionManagerStateStore(statePath);

        using VersionManagerWriteLeaseResult first = await firstStore.TryAcquireWriteLeaseAsync(
            managedRoot,
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);
        using VersionManagerWriteLeaseResult contended = await secondStore.TryAcquireWriteLeaseAsync(
            managedRoot,
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);
        using VersionManagerWriteLeaseResult otherRoot = await secondStore.TryAcquireWriteLeaseAsync(
            managedRoot + "-other",
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsAcquired);
        Assert.Equal(VersionManagerWriteLeaseIssue.Busy, contended.Issue);
        Assert.True(otherRoot.IsAcquired);
    }

    /// <summary>Disposing the writer handle makes the exact identity immediately available.</summary>
    [Fact]
    public async Task DisposedWriterCanBeAcquiredAgain()
    {
        using var workspace = TempWorkspace.Create("nfc-version-lease");
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        string managedRoot = Path.Combine(workspace.Root, "managed");
        var store = new JsonVersionManagerStateStore(statePath);
        using (VersionManagerWriteLeaseResult first = await store.TryAcquireWriteLeaseAsync(
                   managedRoot,
                   TimeSpan.Zero,
                   TestContext.Current.CancellationToken))
        {
            Assert.True(first.IsAcquired);
        }

        using VersionManagerWriteLeaseResult second = await store.TryAcquireWriteLeaseAsync(
            managedRoot,
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
        string managedRoot = Path.Combine(workspace.Root, "managed");
        string lockPath = FileSystemVersionManagerWriteLease.GetLockPath(statePath, managedRoot);
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
                managedRoot,
                TimeSpan.Zero,
                TestContext.Current.CancellationToken);
            Assert.Equal(VersionManagerWriteLeaseIssue.Busy, held.Issue);

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            using VersionManagerWriteLeaseResult recovered = await store.TryAcquireWriteLeaseAsync(
                managedRoot,
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
}
