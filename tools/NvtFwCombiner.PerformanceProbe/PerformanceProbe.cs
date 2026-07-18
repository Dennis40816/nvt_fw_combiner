using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.PerformanceProbe;

internal static class PerformanceProbe
{
    private const string SchemaVersion = "nvt-fw-combiner-performance-probe-v1";

    internal static async Task<PerformanceProbeReport> RunAsync(
        ProbeOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        string repositoryRoot = RepositoryLocator.FindRoot();
        var process = Process.GetCurrentProcess();
        List<ReportCaseEvidence> reportCases =
        [
            MeasureReportCase(process, options, "small", differenceCount: 24, sectionCount: 4),
            MeasureReportCase(process, options, "large", differenceCount: 1_000, sectionCount: 40),
            MeasureReportCase(process, options, "very-large", differenceCount: 10_000, sectionCount: 200),
        ];
        using var fixture = StandardMergePerformanceFixture.Load(repositoryRoot, "51926");
        UiBuildEvidence uiBuild = await MeasureUiBuildAsync(
            process,
            options,
            fixture,
            cancellationToken).ConfigureAwait(false);
        process.Refresh();
        return new PerformanceProbeReport(
            SchemaVersion,
            DateTimeOffset.UtcNow,
            CaptureSourceEvidence(repositoryRoot),
            CaptureEnvironmentEvidence(process),
            new ProbeSettings(
                options.WarmupCount,
                options.IterationCount,
                "First in-process sample after a full blocking GC; warm statistics follow the configured warm-up runs.",
                options.HeartbeatIntervalMilliseconds),
            reportCases,
            uiBuild,
            [
                "Timing, allocation, working-set, and heartbeat values are local evidence, not pass/fail thresholds.",
                "The UI Build case uses the tracked NT51926 Standard Merge golden only to measure shared scheduling and dispatcher responsiveness; it does not imply Replace support.",
                "Deterministic composition/read/session/launch/commit counts and firmware parity remain owned by the repository test suite.",
                "Code size is intentionally excluded from the performance result.",
            ]);
    }

    private static ReportCaseEvidence MeasureReportCase(
        Process process,
        ProbeOptions options,
        string name,
        int differenceCount,
        int sectionCount)
    {
        SyntheticReport input = SyntheticReportFactory.Create(differenceCount, sectionCount);
        ForceFullCollection();
        ReportIteration cold = MeasureReportIteration(process, input.Json);
        for (int index = 0; index < options.WarmupCount; index++)
        {
            _ = MeasureReportIteration(process, input.Json);
        }

        var warm = new List<ReportIteration>(options.IterationCount);
        for (int index = 0; index < options.IterationCount; index++)
        {
            warm.Add(MeasureReportIteration(process, input.Json));
        }

        return new ReportCaseEvidence(
            name,
            differenceCount,
            sectionCount,
            input.Utf8ByteCount,
            input.Sha256,
            cold.InitialSummaryRows,
            cold.InitialGroupHeaders,
            cold.InitialVisibleDetailRows,
            cold.FirstExpandedGroupRows,
            CreateOperationEvidence(cold.SummaryReady, warm.Select(static sample => sample.SummaryReady)),
            CreateOperationEvidence(cold.FirstDetailReady, warm.Select(static sample => sample.FirstDetailReady)));
    }

    private static ReportIteration MeasureReportIteration(Process process, string json)
    {
        long summaryWorkingSetBefore = ReadWorkingSet(process);
        long summaryAllocationBefore = GC.GetAllocatedBytesForCurrentThread();
        long summaryStart = Stopwatch.GetTimestamp();
        ReportReviewViewModel report = ReportReviewViewModel.FromJson(json, "performance-probe.json");
        double summaryElapsed = Stopwatch.GetElapsedTime(summaryStart).TotalMilliseconds;
        long summaryAllocated = GC.GetAllocatedBytesForCurrentThread() - summaryAllocationBefore;
        long summaryWorkingSetDelta = ReadWorkingSet(process) - summaryWorkingSetBefore;

        int initialVisibleDetailRows = report.OutputDifferenceGroups.Sum(static group => group.RowsPage.VisibleCount);
        ReportDifferenceGroupViewModel firstGroup = report.OutputDifferenceGroups[0];
        long detailWorkingSetBefore = ReadWorkingSet(process);
        long detailAllocationBefore = GC.GetAllocatedBytesForCurrentThread();
        long detailStart = Stopwatch.GetTimestamp();
        firstGroup.IsExpanded = true;
        double detailElapsed = Stopwatch.GetElapsedTime(detailStart).TotalMilliseconds;
        long detailAllocated = GC.GetAllocatedBytesForCurrentThread() - detailAllocationBefore;
        long detailWorkingSetDelta = ReadWorkingSet(process) - detailWorkingSetBefore;
        return new ReportIteration(
            new OperationSample(summaryElapsed, summaryAllocated, summaryWorkingSetDelta),
            new OperationSample(detailElapsed, detailAllocated, detailWorkingSetDelta),
            report.OutputDifferenceSummaryPage.VisibleCount,
            report.OutputDifferenceGroupPage.VisibleCount,
            initialVisibleDetailRows,
            firstGroup.RowsPage.VisibleCount);
    }

    private static OperationEvidence CreateOperationEvidence(
        OperationSample cold,
        IEnumerable<OperationSample> warm)
    {
        OperationSample[] samples = [.. warm];
        return new OperationEvidence(
            cold,
            NumericDistribution.Create(samples.Select(static sample => sample.ElapsedMilliseconds)),
            NumericDistribution.Create(samples.Select(static sample => (double)sample.AllocatedBytes)),
            NumericDistribution.Create(samples.Select(static sample => (double)sample.WorkingSetDeltaBytes)));
    }

    private static async Task<UiBuildEvidence> MeasureUiBuildAsync(
        Process process,
        ProbeOptions options,
        StandardMergePerformanceFixture fixture,
        CancellationToken cancellationToken)
    {
        ForceFullCollection();
        UiBuildSample cold = await MeasureUiBuildIterationAsync(
            process,
            options.HeartbeatIntervalMilliseconds,
            fixture,
            cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < options.WarmupCount; index++)
        {
            _ = await MeasureUiBuildIterationAsync(
                process,
                options.HeartbeatIntervalMilliseconds,
                fixture,
                cancellationToken).ConfigureAwait(false);
        }

        var warm = new List<UiBuildSample>(options.IterationCount);
        for (int index = 0; index < options.IterationCount; index++)
        {
            warm.Add(await MeasureUiBuildIterationAsync(
                process,
                options.HeartbeatIntervalMilliseconds,
                fixture,
                cancellationToken).ConfigureAwait(false));
        }

        return new UiBuildEvidence(
            "NT51926 Standard Merge shared UI scheduling path",
            fixture.InputSha256,
            fixture.ExpectedOutputSha256,
            cold,
            NumericDistribution.Create(warm.Select(static sample => sample.TotalMilliseconds)),
            NumericDistribution.Create(warm.Select(static sample => sample.ClickToActiveMilliseconds)),
            NumericDistribution.Create(warm.Select(static sample => sample.MaximumHeartbeatGapMilliseconds)),
            NumericDistribution.Create(warm.Select(static sample => (double)sample.WorkingSetDeltaBytes)),
            warm.Min(static sample => sample.HeartbeatCount),
            warm.All(static sample => sample.ProgressNotificationUsedDispatcherThread),
            warm.All(static sample => sample.HeartbeatsUsedDispatcherThread));
    }

    private static async Task<UiBuildSample> MeasureUiBuildIterationAsync(
        Process process,
        int heartbeatIntervalMilliseconds,
        StandardMergePerformanceFixture fixture,
        CancellationToken cancellationToken)
    {
        using var workspace = TemporaryProbeWorkspace.Create();
        IReadOnlyDictionary<string, string> inputs = fixture.CopyInputsTo(workspace.Root);
        using var uiThread = new UiThreadProbeContext();
        var viewModel = new MainWindowViewModel("0.9.10-performance-probe", "0.9.10-performance-probe");
        await uiThread.InvokeAsync(() =>
        {
            viewModel.SelectedIc = fixture.FullIcId;
            foreach ((string slotId, string path) in inputs)
            {
                viewModel.SetSlotFile(slotId, path);
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);

        string outputPath = Path.Combine(workspace.Root, "output.bin");
        var heartbeatTimes = new ConcurrentQueue<double>();
        int progressThreadId = 0;
        long progressTimestamp = 0;
        int heartbeatWrongThread = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.IsRunInProgress) && viewModel.IsRunInProgress)
            {
                _ = Interlocked.CompareExchange(ref progressThreadId, Environment.CurrentManagedThreadId, 0);
                _ = Interlocked.CompareExchange(ref progressTimestamp, Stopwatch.GetTimestamp(), 0);
            }
        };

        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        long clickTimestamp = Stopwatch.GetTimestamp();
        var stopwatch = Stopwatch.StartNew();
        Task heartbeatTask = PublishHeartbeatsAsync(
            uiThread,
            stopwatch,
            heartbeatTimes,
            heartbeatIntervalMilliseconds,
            heartbeatCancellation.Token);
        long workingSetBefore = ReadWorkingSet(process);
        await uiThread.InvokeAsync(() => viewModel.BuildStandardMergeAsync(outputPath)).ConfigureAwait(false);
        stopwatch.Stop();
        heartbeatCancellation.Cancel();
        await heartbeatTask.ConfigureAwait(false);
        await uiThread.InvokeAsync(static () => Task.CompletedTask).ConfigureAwait(false);
        long workingSetDelta = ReadWorkingSet(process) - workingSetBefore;

        if (!viewModel.LastRunResult.Succeeded)
        {
            throw new InvalidOperationException($"UI Build probe failed: {viewModel.LastRunResult.Detail}");
        }

        if (!string.Equals(
                viewModel.LoadedReport.OutputSha256,
                fixture.ExpectedOutputSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("UI Build probe output SHA-256 does not match the tracked golden.");
        }

        double[] timestamps = [.. heartbeatTimes.Order()];
        double clickToActiveMilliseconds = progressTimestamp == 0
            ? stopwatch.Elapsed.TotalMilliseconds
            : Stopwatch.GetElapsedTime(clickTimestamp, progressTimestamp).TotalMilliseconds;
        return new UiBuildSample(
            stopwatch.Elapsed.TotalMilliseconds,
            clickToActiveMilliseconds,
            MaximumHeartbeatGap(timestamps, stopwatch.Elapsed.TotalMilliseconds),
            timestamps.Length,
            workingSetDelta,
            progressThreadId == uiThread.ThreadId,
            heartbeatWrongThread == 0);

        async Task PublishHeartbeatsAsync(
            UiThreadProbeContext context,
            Stopwatch clock,
            ConcurrentQueue<double> times,
            int intervalMilliseconds,
            CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(intervalMilliseconds, token).ConfigureAwait(false);
                    context.Post(
                        _ =>
                        {
                            if (Environment.CurrentManagedThreadId != context.ThreadId)
                            {
                                _ = Interlocked.Exchange(ref heartbeatWrongThread, 1);
                            }

                            times.Enqueue(clock.Elapsed.TotalMilliseconds);
                        },
                        null);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static double MaximumHeartbeatGap(IReadOnlyList<double> timestamps, double totalMilliseconds)
    {
        double maximum = 0;
        double previous = 0;
        foreach (double timestamp in timestamps)
        {
            maximum = Math.Max(maximum, timestamp - previous);
            previous = timestamp;
        }

        return Math.Round(Math.Max(maximum, totalMilliseconds - previous), 3, MidpointRounding.AwayFromZero);
    }

    private static SourceEvidence CaptureSourceEvidence(string repositoryRoot)
    {
        string branch = RunGit(repositoryRoot, "branch", "--show-current") ?? "unknown";
        string commit = RunGit(repositoryRoot, "rev-parse", "HEAD") ?? "unknown";
        string? status = RunGit(repositoryRoot, "status", "--porcelain", "--untracked-files=normal");
        string manifestPath = Path.Combine(
            repositoryRoot,
            "external-tools",
            "legacy-combiner",
            "1.13.0",
            "manifest.json");
        return new SourceEvidence(
            branch,
            commit,
            status is null ? null : status.Length > 0,
            ComputeFileSha256(manifestPath));
    }

    private static EnvironmentEvidence CaptureEnvironmentEvidence(Process process)
    {
        process.Refresh();
        return new EnvironmentEvidence(
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown",
            Environment.ProcessorCount,
            GCSettings.IsServerGC,
            process.PeakWorkingSet64);
    }

    private static string? RunGit(string repositoryRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process? git = Process.Start(startInfo);
        if (git is null)
        {
            return null;
        }

        string output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode == 0 ? output.Trim() : null;
    }

    private static long ReadWorkingSet(Process process)
    {
        process.Refresh();
        return process.WorkingSet64;
    }

    private static string ComputeFileSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private sealed record ReportIteration(
        OperationSample SummaryReady,
        OperationSample FirstDetailReady,
        int InitialSummaryRows,
        int InitialGroupHeaders,
        int InitialVisibleDetailRows,
        int FirstExpandedGroupRows);
}
