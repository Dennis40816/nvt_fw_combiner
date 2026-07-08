using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies Replace reports surface accepted final-output CRC/header differences.</summary>
    [Fact]
    public void ReportReviewShowsAcceptedOutputDifferences()
    {
        string json = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(json, "replace-report.json");

        Assert.True(viewModel.LoadedReport.HasOutputDifferences);
        ReportLineViewModel difference = Assert.Single(viewModel.LoadedReport.OutputDifferences);
        Assert.Equal("diff-001", difference.Title);
        Assert.Equal("TP flash header / CRC fields", difference.SectionLabel);
        Assert.Equal("0x1C-0x1F (len 0x4)", difference.Range);
        Assert.Equal("4 bytes changed", difference.ChangedSummary);
        Assert.Contains("TP flash header", difference.Reason, StringComparison.Ordinal);
        Assert.Contains(difference.Badges, badge => badge.Text == "accepted");
        Assert.Contains(difference.Badges, badge => badge.Text == "CRC/header");
        Assert.Equal("Before bytes", difference.BeforeLabel);
        Assert.Equal("AA BB CC DD", difference.BeforeValue);
        Assert.Equal("After bytes", difference.AfterLabel);
        Assert.Equal("11 22 33 44", difference.AfterValue);
        Assert.Contains(difference.Facts, fact => fact.Label == "Reason" && fact.Value.Contains("TP flash header", StringComparison.Ordinal));
        Assert.DoesNotContain(difference.Facts, fact => fact.Value.Contains("...", StringComparison.Ordinal));
        Assert.True(viewModel.LoadedReport.HasOperationFlow);
        Assert.Contains(viewModel.LoadedReport.OperationFlow, node =>
            node.Title == "Refresh header and CRC" &&
            node.Number == "100" &&
            node.Meta == "details in Postbuild tab");
        Assert.DoesNotContain(viewModel.LoadedReport.StepOperations, operation => operation.HasCodeBlock);
        Assert.True(viewModel.LoadedReport.HasCommandOperations);
        Assert.Equal("Accepted changes", viewModel.LoadedReport.ByteDifferenceTitle);
        Assert.Contains("CRC/header", viewModel.LoadedReport.ByteDifferenceDetail, StringComparison.Ordinal);
        Assert.Equal("1/1 accepted", viewModel.LoadedReport.ByteDifferenceMeta);
        Assert.Contains(viewModel.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "CRC/header refresh" &&
            row.Count == "1" &&
            row.Status == "present");
        Assert.Contains(viewModel.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "Unexpected differences" &&
            row.Count == "0");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Output diff" &&
            row.Detail == "1" &&
            row.Meta == "all accepted");

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("差異", viewModel.Text.ReportTabChanges);
        Assert.Equal("可接受變更", viewModel.LoadedReport.ByteDifferenceTitle);
        Assert.Equal("1/1 可接受", viewModel.LoadedReport.ByteDifferenceMeta);
        ReportLineViewModel localizedDifference = Assert.Single(viewModel.LoadedReport.OutputDifferences);
        Assert.Contains(localizedDifference.Badges, badge => badge.Text == "可接受");
        Assert.Equal("TP flash header / CRC fields", localizedDifference.SectionLabel);
        Assert.Equal("變更前 bytes", localizedDifference.BeforeLabel);
        Assert.Equal("AA BB CC DD", localizedDifference.BeforeValue);
        Assert.Contains(viewModel.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "意外差異" &&
            row.Count == "0" &&
            row.Status == "無");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Output diff" &&
            row.Detail == "1" &&
            row.Meta == "全部可接受");
    }

    /// <summary>Verifies report inputs use readable CtrlRAM region labels instead of raw slot ids.</summary>
    [Fact]
    public void ReportReviewFormatsCtrlRamInputTitles()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(ReportJsonSamples.CtrlRamInputs(), "ctrlram-inputs.json");

        Assert.Contains(viewModel.LoadedReport.Inputs, input =>
            input.Title == "Base flash image" &&
            input.Classification == "base");
        Assert.Contains(viewModel.LoadedReport.Inputs, input =>
            input.Title == "VN CtrlRAM" &&
            input.Classification == "ctrlram");
        Assert.Contains(viewModel.LoadedReport.Inputs, input =>
            input.Title == "Normal CtrlRAM (Slave R)" &&
            input.Classification == "ctrlram");
    }
}
