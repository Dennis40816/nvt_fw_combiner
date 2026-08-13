using System.Text;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Tests startup and storage-provider report loading through one admitted file path.</summary>
public sealed class ReportFileLoadingTests
{
    private const long MaximumReportBytes = 10L * 1024 * 1024;

    /// <summary>Startup-path and manual-stream sources publish the same existing report projection.</summary>
    [Fact]
    public async Task StartupPathAndManualStreamHaveProjectionParity()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-file-parity");
        string json = ReportJsonSamples.Succeeded(runId: "report-file-parity");
        string path = workspace.PathFor("parity.json");
        await File.WriteAllTextAsync(path, json, TestContext.Current.CancellationToken);
        ILocalFileStore files = CompositionHostServices.Create().LocalFiles;
        MainWindowViewModel startup = PresentationTestHost.CreateViewModel();
        MainWindowViewModel manual = PresentationTestHost.CreateViewModel();
        startup.Reports.ReportHistoryEntries.CollectionChanged +=
            static (_, _) => throw new InvalidOperationException("History observer failed.");
        startup.Reports.PropertyChanged +=
            static (_, _) => throw new InvalidOperationException("Report observer failed.");
        var progress = new List<(long Completed, long Total)>();

        ReportPublicationResult startupResult = await startup.Reports.LoadReportFileAsync(
            token => files.ReadTextAsync(
                path,
                MaximumReportBytes,
                token,
                update => progress.Add((update.BytesRead, update.TotalBytes))),
            "parity.json",
            TestContext.Current.CancellationToken);
        ReportPublicationResult manualResult = await manual.Reports.LoadReportFileAsync(
            token => files.ReadTextAsync(
                _ => new ValueTask<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false)),
                MaximumReportBytes,
                token),
            "parity.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(ReportPublicationOutcome.Published, startupResult.Outcome);
        Assert.Equal(ReportPublicationOutcome.Published, manualResult.Outcome);
        Assert.Equal(startup.Reports.LoadedReportJson, manual.Reports.LoadedReportJson);
        Assert.Equal(startup.Reports.LoadedReport.RunId, manual.Reports.LoadedReport.RunId);
        Assert.Equal(startup.Reports.LoadedReport.Status, manual.Reports.LoadedReport.Status);
        Assert.Equal("parity.json", Assert.Single(startup.Reports.ReportHistoryEntries).SourceName);
        Assert.Equal("parity.json", Assert.Single(manual.Reports.ReportHistoryEntries).SourceName);
        Assert.Equal((0, new FileInfo(path).Length), progress[0]);
        Assert.Equal((new FileInfo(path).Length, new FileInfo(path).Length), progress[^1]);
        Assert.Equal(progress.Select(static update => update.Completed).Order(),
            progress.Select(static update => update.Completed));
    }

    /// <summary>An oversized startup report reports the typed failure without replacing current state.</summary>
    [Fact]
    public async Task OversizedStartupReportDoesNotPublishOrEnterHistory()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-file-oversize");
        string path = workspace.PathFor("oversized.json");
        await using (FileStream stream = File.Create(path))
        {
            stream.SetLength((10L * 1024 * 1024) + 1);
        }

        string currentJson = ReportJsonSamples.Succeeded(runId: "current-report");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.Reports.LoadReportJson(currentJson, "current.json");

        ReportPublicationResult result = await viewModel.Reports.LoadReportFileAsync(
            token => CompositionHostServices.Create().LocalFiles.ReadTextAsync(
                path,
                MaximumReportBytes,
                token),
            "oversized.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(ReportPublicationOutcome.Failed, result.Outcome);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.Equal(currentJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal("current.json", Assert.Single(viewModel.Reports.ReportHistoryEntries).SourceName);
        Assert.Contains("10485760-byte limit", viewModel.Reports.ReportToastText, StringComparison.Ordinal);
        _ = Assert.Throws<InvalidOperationException>(() => MainWindow.RequireStartupPublication(default));
        _ = Assert.Throws<InvalidOperationException>(() => MainWindow.RequireStartupPublication(
            new(ReportPublicationOutcome.Failed)));

        ILocalFileStore files = CompositionHostServices.Create().LocalFiles;
        MainWindowViewModel invalidArguments = PresentationTestHost.CreateViewModel();
        InvalidOperationException invalidArgumentsFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MainWindow.ApplyStartupReportAsync(
                invalidArguments,
                files,
                UiLaunchOptions.Parse(["--report"]),
                static (_, _) => { },
                TestContext.Current.CancellationToken));
        Assert.Contains("--report requires a value", invalidArgumentsFailure.Message, StringComparison.Ordinal);
        Assert.True(invalidArguments.Reports.HasLoadedReport);
        Assert.Contains("--report requires a value", invalidArguments.Reports.LoadedReport.PrimaryIssue.Detail,
            StringComparison.Ordinal);

        MainWindowViewModel missingReport = PresentationTestHost.CreateViewModel();
        InvalidOperationException missingReportFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MainWindow.ApplyStartupReportAsync(
                missingReport,
                files,
                UiLaunchOptions.Parse(["--open-report"]),
                static (_, _) => { },
                TestContext.Current.CancellationToken));
        Assert.Contains("requires a loaded report", missingReportFailure.Message, StringComparison.Ordinal);
        Assert.True(missingReport.Reports.HasLoadedReport);
        Assert.False(missingReport.Reports.IsReportModalOpen);

        string mixedJson = ReportJsonSamples.Succeeded(runId: "mixed-launch-options");
        MainWindowViewModel mixed = PresentationTestHost.CreateViewModel();
        mixed.Reports.PropertyChanged +=
            static (_, _) => throw new InvalidOperationException("Report observer failed.");
        InvalidOperationException mixedFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MainWindow.ApplyStartupReportAsync(
                mixed,
                new StubLocalFileStore(mixedJson),
                UiLaunchOptions.Parse(["--page", "invalid", "--report", "mixed.json", "--open-report"]),
                static (_, _) => { },
                TestContext.Current.CancellationToken));
        Assert.Contains("Unsupported --page value", mixedFailure.Message, StringComparison.Ordinal);
        Assert.Equal(mixedJson, mixed.Reports.LoadedReportJson);
        Assert.True(mixed.Reports.IsReportModalOpen);
        Assert.Contains(mixed.Reports.ReportHistoryEntries, static entry => entry.SourceName == "mixed.json");
    }

    private sealed class StubLocalFileStore(string text) : ILocalFileStore
    {
        public ValueTask<string> ReadTextAsync(
            string path,
            long maximumBytes,
            CancellationToken cancellationToken,
            Action<LocalFileReadProgress>? progress = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(text);
        }

        public ValueTask<T> ReadAsync<T>(
            string path,
            long maximumBytes,
            Func<Stream, CancellationToken, ValueTask<T>> project,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromException<T>(new NotSupportedException());
        }

        public ValueTask<string> ReadTextAsync(
            Func<CancellationToken, ValueTask<Stream>> openReadAsync,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromException<string>(new NotSupportedException());
        }

        public ValueTask WriteAsync(
            string path,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromException(new NotSupportedException());
        }
    }
}
