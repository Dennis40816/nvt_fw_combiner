using System.Text;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Bootstrap;
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

        bool startupPublished = await startup.Reports.LoadReportFileAsync(
            token => files.ReadTextAsync(path, MaximumReportBytes, token),
            "parity.json",
            TestContext.Current.CancellationToken);
        bool manualPublished = await manual.Reports.LoadReportFileAsync(
            token => files.ReadTextAsync(
                _ => new ValueTask<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false)),
                MaximumReportBytes,
                token),
            "parity.json",
            TestContext.Current.CancellationToken);

        Assert.True(startupPublished);
        Assert.True(manualPublished);
        Assert.Equal(startup.Reports.LoadedReportJson, manual.Reports.LoadedReportJson);
        Assert.Equal(startup.Reports.LoadedReport.RunId, manual.Reports.LoadedReport.RunId);
        Assert.Equal(startup.Reports.LoadedReport.Status, manual.Reports.LoadedReport.Status);
        Assert.Equal("parity.json", Assert.Single(startup.Reports.ReportHistoryEntries).SourceName);
        Assert.Equal("parity.json", Assert.Single(manual.Reports.ReportHistoryEntries).SourceName);
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

        bool handled = await viewModel.Reports.LoadReportFileAsync(
            token => CompositionHostServices.Create().LocalFiles.ReadTextAsync(
                path,
                MaximumReportBytes,
                token),
            "oversized.json",
            TestContext.Current.CancellationToken);

        Assert.False(handled);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.Equal(currentJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal("current.json", Assert.Single(viewModel.Reports.ReportHistoryEntries).SourceName);
        Assert.Contains("10485760-byte limit", viewModel.Reports.ReportToastText, StringComparison.Ordinal);
    }
}
