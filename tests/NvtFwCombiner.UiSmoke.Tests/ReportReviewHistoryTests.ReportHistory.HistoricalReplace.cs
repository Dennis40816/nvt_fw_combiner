using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ReportReviewHistoryTests
{
    /// <summary>Historical diagnostic data and unknown fields survive Raw, export and reopen without preparation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalGeneralReplaceDiagnosticKeepsExactRawAndExport(bool chinese)
    {
        string json = ReportJsonSamples.HistoricalGeneralReplaceDiagnostic;
        MainWindowViewModel shell = PresentationTestHost.CreateProductViewModel();
        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
        _ = await shell.Reports.LoadReportFileAsync(
            _ => ValueTask.FromResult(json),
            "historical-diagnostic.json",
            TestContext.Current.CancellationToken);

        Assert.Equal("general-replace", shell.Reports.LoadedReport.ModeId);
        Assert.Equal("general-replace", shell.Reports.LoadedReport.ExperienceId);
        Assert.Equal("NT51926", shell.Reports.LoadedReport.IcId);
        Assert.Equal("historical-general-replace-diagnostic", shell.Reports.LoadedReport.RunId);
        Assert.False(shell.Reports.LoadedReport.HasOutputArtifactPath);
        Assert.False(shell.Reports.LoadedReport.IsOutputCommitted);
        Assert.False(shell.Reports.LoadedReport.HexDiff.IsAvailable);
        Assert.Equal(json, shell.Reports.LoadedReportJson);
        ReportHistorySnapshot exported = Assert.Single(shell.Reports.ExportReportHistory());
        Assert.Equal(json, exported.ReportJson);
        Assert.Equal("historical-diagnostic.json", exported.SourceName);
        using JsonDocument raw = JsonDocument.Parse(exported.ReportJson);
        JsonElement diagnostic = raw.RootElement.GetProperty("DiagnosticPreview");
        Assert.Equal("diagnostic-plan-only", diagnostic.GetProperty("Mode").GetString());
        Assert.False(diagnostic.GetProperty("OutputProduced").GetBoolean());
        Assert.False(diagnostic.GetProperty("ClaimsFinalIntegrity").GetBoolean());
        Assert.Equal("Changed", diagnostic.GetProperty("Coverage")[1].GetProperty("Disposition").GetString());
        Assert.Equal("原始證據", diagnostic.GetProperty("FutureDiagnosticField").GetProperty("text").GetString());
        Assert.Equal(42, raw.RootElement.GetProperty("FutureReportField")[1].GetInt32());

        MainWindowViewModel restored = PresentationTestHost.CreateProductViewModel();
        restored.SelectedLanguage = shell.SelectedLanguage;
        LoadHistory(restored, [exported]);

        Assert.Equal(json, restored.Reports.LoadedReportJson);
        Assert.Equal(json, Assert.Single(restored.Reports.ExportReportHistory()).ReportJson);
        Assert.Equal("general-replace", restored.Reports.LoadedReport.ExperienceId);
        Assert.False(restored.Reports.LoadedReport.HasOutputArtifactPath);
        Assert.False(restored.Reports.LoadedReport.HexDiff.IsAvailable);
        Assert.Equal(1, restored.Reports.ReportHistoryCount);
    }
}
