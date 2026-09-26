using System.Diagnostics;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises the OS-backed, exact-identity version-manager writer lease.</summary>
public sealed class FileSystemVersionManagerWriteLeaseTests
{
    /// <summary>Cold PowerShell start on a CI runner is measured separately from lease readiness.</summary>
    private static readonly TimeSpan LeaseHolderStartBudget = TimeSpan.FromSeconds(60);

    /// <summary>Readiness after the helper reports <c>STARTED</c> keeps the original bound.</summary>
    private static readonly TimeSpan LeaseHolderReadinessDeadline = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);

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
        using Process process = CreateLeaseHolderProcess(
            "[Console]::Out.WriteLine('STARTED');" +
            "$stream=[IO.File]::Open($env:NVT_LEASE_PATH,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);" +
            "[IO.File]::WriteAllText($env:NVT_LEASE_READY,'ready');[Console]::Out.WriteLine('LEASE_HELD');Start-Sleep -Seconds 30");
        process.StartInfo.Environment["NVT_LEASE_PATH"] = lockPath;
        process.StartInfo.Environment["NVT_LEASE_READY"] = readyPath;
        Assert.True(process.Start());
        var output = new LeaseHolderOutput(process);
        try
        {
            await WaitForLeaseHolderReadyAsync(
                readyPath,
                process,
                output,
                LeaseHolderStartBudget,
                LeaseHolderReadinessDeadline,
                TestContext.Current.CancellationToken);
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
            await StopLeaseHolderAsync(process);
            _ = await output.CompleteAsync();
        }
    }

    /// <summary>Time spent before the helper reports STARTED is not charged to the readiness deadline.</summary>
    [Fact]
    public async Task WindowsSlowLeaseHolderStartWithPromptReadinessPasses()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create("nfc-version-lease-slow-start");
        string readyPath = Path.Combine(workspace.Root, "lease-ready.txt");
        // The start delay exceeds the readiness deadline, which a single shared deadline would reject.
        TimeSpan readinessDeadline = TimeSpan.FromSeconds(2);
        using Process process = CreateLeaseHolderProcess(
            "Start-Sleep -Seconds 3;[Console]::Out.WriteLine('STARTED');" +
            "[IO.File]::WriteAllText($env:NVT_LEASE_READY,'ready');Start-Sleep -Seconds 30");
        process.StartInfo.Environment["NVT_LEASE_READY"] = readyPath;
        Assert.True(process.Start());
        var output = new LeaseHolderOutput(process);
        try
        {
            var elapsed = Stopwatch.StartNew();
            await WaitForLeaseHolderReadyAsync(
                readyPath,
                process,
                output,
                LeaseHolderStartBudget,
                readinessDeadline,
                TestContext.Current.CancellationToken);

            Assert.True(File.Exists(readyPath));
            Assert.True(elapsed.Elapsed > readinessDeadline, $"ElapsedMs={elapsed.ElapsedMilliseconds}");
        }
        finally
        {
            await StopLeaseHolderAsync(process);
            _ = await output.CompleteAsync();
        }
    }

    /// <summary>Start and readiness failures explain the child phase and error, and always reap the helper.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task WindowsLeaseHolderFailureReportsChildDiagnostics(bool reportsStarted, bool exits)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create("nfc-version-lease-diagnostics");
        string readyPath = Path.Combine(workspace.Root, "lease-ready.txt");
        string marker = reportsStarted ? "STARTED" : "BOOTING";
        using Process process = CreateLeaseHolderProcess(
            $"[Console]::Out.WriteLine('{marker}');" +
            (exits ? "throw 'lease-probe-error'" : "Start-Sleep -Seconds 30"));
        // Only a never-starting child uses a short start budget; readiness keeps the production deadline.
        TimeSpan startBudget = reportsStarted || exits ? LeaseHolderStartBudget : TimeSpan.FromSeconds(2);
        Assert.True(process.Start());
        var output = new LeaseHolderOutput(process);
        try
        {
            Xunit.Sdk.XunitException failure = await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() =>
                WaitForLeaseHolderReadyAsync(
                    readyPath,
                    process,
                    output,
                    startBudget,
                    LeaseHolderReadinessDeadline,
                    TestContext.Current.CancellationToken));
            string expectedFailure = (reportsStarted, exits) switch
            {
                (true, false) => "readiness timed out after 10s",
                (true, true) => "child exited before READY",
                (false, false) => "child start timed out after 2s",
                (false, true) => "child exited before STARTED",
            };
            Assert.Contains(expectedFailure, failure.Message, StringComparison.Ordinal);
            Assert.Contains($"Started={reportsStarted}", failure.Message, StringComparison.Ordinal);
            Assert.Contains("ReadyExists=False", failure.Message, StringComparison.Ordinal);
            Assert.Contains("CallerCancelled=False", failure.Message, StringComparison.Ordinal);
            if (reportsStarted || exits)
            {
                // A timed-out cold start may be reaped before its first line, so only these cases pin stdout.
                Assert.Contains(marker, failure.Message, StringComparison.Ordinal);
            }
            if (exits)
            {
                Assert.Contains("lease-probe-error", failure.Message, StringComparison.Ordinal);
            }
            Assert.True(process.HasExited);
        }
        finally
        {
            await StopLeaseHolderAsync(process);
            _ = await output.CompleteAsync();
        }
    }

    /// <summary>
    /// Waits for the helper's STARTED marker within <paramref name="startBudget"/>, then for the ready file
    /// within <paramref name="readinessDeadline"/> measured from that marker.
    /// </summary>
    private static async Task WaitForLeaseHolderReadyAsync(
        string path,
        Process process,
        LeaseHolderOutput output,
        TimeSpan startBudget,
        TimeSpan readinessDeadline,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        string? failure = await WaitForLeaseHolderStartAsync(process, output, startBudget, cancellationToken);
        long startMs = elapsed.ElapsedMilliseconds;
        failure ??= await WaitForReadyFileAsync(path, process, readinessDeadline, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (failure is null)
        {
            return;
        }

        string state = $"PID={process.Id}; ElapsedMs={elapsed.ElapsedMilliseconds}; " +
            $"Started={output.Started.IsCompleted}; StartMs={startMs}; " +
            $"HasExited={process.HasExited}; ReadyExists={File.Exists(path)}; CallerCancelled={cancellationToken.IsCancellationRequested}";
        // A sleeping child keeps both pipes open: reap it before awaiting complete diagnostic output.
        try
        {
            await StopLeaseHolderAsync(process);
            string[] streams = await output.CompleteAsync();
            throw new Xunit.Sdk.XunitException(
                $"Lease-holder {failure}. {state}; ExitCode={process.ExitCode}\nstdout/stages:\n{streams[0]}\nstderr:\n{streams[1]}");
        }
        catch (Exception cleanupError) when (cleanupError is TimeoutException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new Xunit.Sdk.XunitException($"Lease-holder {failure}. {state}; diagnostic cleanup failed: {cleanupError}");
        }
    }

    private static async Task<string?> WaitForLeaseHolderStartAsync(
        Process process,
        LeaseHolderOutput output,
        TimeSpan startBudget,
        CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task expired = Task.Delay(startBudget, budget.Token);
        Task exited = process.WaitForExitAsync(budget.Token);
        Task completed = await Task.WhenAny(output.Started, exited, expired);
        await budget.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (completed == exited)
        {
            // STARTED can precede a fast exit while still buffered in the pipe: drain before deciding.
            _ = await Task.WhenAny(output.Stdout, Task.Delay(OutputDrainTimeout, CancellationToken.None));
        }
        return output.Started.IsCompleted
            ? null
            : completed == exited
                ? "child exited before STARTED"
                : $"child start timed out after {(int)startBudget.TotalSeconds}s";
    }

    private static async Task<string?> WaitForReadyFileAsync(
        string path,
        Process process,
        TimeSpan readinessDeadline,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(readinessDeadline);
        try
        {
            while (!File.Exists(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    return "child exited before READY";
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return $"readiness timed out after {(int)readinessDeadline.TotalSeconds}s";
        }
        return null;
    }

    private static Process CreateLeaseHolderProcess(string script)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add("$ErrorActionPreference='Stop';" + script);
        return process;
    }

    private static async Task StopLeaseHolderAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    /// <summary>Captures both helper streams and observes the STARTED line as soon as it arrives.</summary>
    private sealed class LeaseHolderOutput
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LeaseHolderOutput(Process process)
        {
            Stdout = ReadStdoutAsync(process.StandardOutput);
            Stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        }

        public Task Started => started.Task;

        public Task<string> Stdout { get; }

        public Task<string> Stderr { get; }

        public Task<string[]> CompleteAsync()
        {
            return Task.WhenAll(Stdout, Stderr).WaitAsync(OutputDrainTimeout, CancellationToken.None);
        }

        private async Task<string> ReadStdoutAsync(StreamReader reader)
        {
            var text = new StringBuilder();
            while (await reader.ReadLineAsync(CancellationToken.None) is { } line)
            {
                _ = text.AppendLine(line);
                if (string.Equals(line, "STARTED", StringComparison.Ordinal))
                {
                    _ = started.TrySetResult();
                }
            }
            return text.ToString();
        }
    }

    private sealed class DisposableStub : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
