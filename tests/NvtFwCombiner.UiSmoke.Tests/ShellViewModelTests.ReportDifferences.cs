using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ReportReviewHistoryTests
{
    /// <summary>Verifies Replace reports surface accepted final-output CRC/header differences.</summary>
    [Fact]
    public async Task ReportReviewShowsAcceptedOutputDifferences()
    {
        string json = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences();
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.Reports.LoadReportJson(json, "replace-report.json");

        Assert.True(viewModel.Reports.LoadedReport.HasOutputDifferences);
        ReportLineViewModel difference = Assert.Single(viewModel.Reports.LoadedReport.OutputDifferences);
        Assert.Equal("DLM CRC 0", difference.Title);
        Assert.Equal("Header", difference.SectionLabel);
        Assert.Equal("0x1C-0x1F (len 0x4)", difference.Range);
        Assert.Equal("4 bytes changed", difference.ChangedSummary);
        Assert.Equal("Expected: postbuild recalculated DLM CRC 0.", difference.Reason);
        Assert.Contains(difference.Badges, badge => badge.Text == "expected");
        Assert.Contains(difference.Badges, badge => badge.Text == "CRC/header");
        Assert.Equal("Before bytes", difference.BeforeLabel);
        Assert.Equal("AA BB CC DD", difference.BeforeValue);
        Assert.Equal("After bytes", difference.AfterLabel);
        Assert.Equal("11 22 33 44", difference.AfterValue);
        Assert.Contains(difference.Facts, fact => fact.Label == "Reason" &&
            fact.Value.Contains("DLM CRC 0", StringComparison.Ordinal));
        Assert.DoesNotContain(difference.Facts, fact => fact.Value.Contains("...", StringComparison.Ordinal));
        Assert.True(viewModel.Reports.LoadedReport.HasOperationFlow);
        Assert.Contains(viewModel.Reports.LoadedReport.OperationFlow, node =>
            node.Title == "Refresh header and CRC" &&
            node.Number == "100" &&
            node.Meta == "command details in Postbuild tab");
        Assert.DoesNotContain(viewModel.Reports.LoadedReport.StepOperations, operation => operation.HasCodeBlock);
        Assert.NotEmpty(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.True(viewModel.Reports.LoadedReport.HasPostbuildInvocations);
        ReportPostbuildInvocationViewModel invocation = Assert.Single(viewModel.Reports.LoadedReport.PostbuildInvocations);
        Assert.Equal("900.01", invocation.Number);
        Assert.Equal("Runtime invocation", invocation.Title);
        Assert.Equal("Expected changes", viewModel.Reports.LoadedReport.ByteDifferenceTitle);
        Assert.Contains("section", viewModel.Reports.LoadedReport.ByteDifferenceDetail, StringComparison.Ordinal);
        Assert.Equal("1/1 expected", viewModel.Reports.LoadedReport.ByteDifferenceMeta);
        Assert.Equal("Inspect expected changes", viewModel.Reports.LoadedReport.NextStepTitle);
        Assert.Contains("Inspect 1 expected change", viewModel.Reports.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Contains("affected data field", viewModel.Reports.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Contains(viewModel.Reports.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "Header" &&
            row.Count == "1" &&
            row.Status == "expected");
        ReportDifferenceGroupViewModel differenceGroup = Assert.Single(viewModel.Reports.LoadedReport.OutputDifferenceGroups);
        Assert.Equal("Header", differenceGroup.Title);
        Assert.Equal("1 expected field update", differenceGroup.Detail);
        viewModel.SelectedLanguage = "Traditional Chinese";
        await Assert.IsType<Task>(viewModel.Reports.RelocalizationTask, exactMatch: false);

        Assert.Equal("差異", viewModel.Text.ReportTabChanges);
        Assert.Equal("預期變更", viewModel.Reports.LoadedReport.ByteDifferenceTitle);
        Assert.Equal("1/1 預期", viewModel.Reports.LoadedReport.ByteDifferenceMeta);
        ReportLineViewModel localizedDifference = Assert.Single(viewModel.Reports.LoadedReport.OutputDifferences);
        Assert.Contains(localizedDifference.Badges, badge => badge.Text == "預期");
        Assert.Equal("Header", localizedDifference.SectionLabel);
        Assert.Equal("變更前 bytes", localizedDifference.BeforeLabel);
        Assert.Equal("AA BB CC DD", localizedDifference.BeforeValue);
        Assert.Contains(viewModel.Reports.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "Header" &&
            row.Count == "1" &&
            row.Status == "預期");
    }

    /// <summary>Verifies incomplete hex previews are not labelled as complete byte values.</summary>
    [Fact]
    public void ReportReviewLabelsIncompleteOutputDifferenceHexAsPreview()
    {
        string json = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences(
            isHexPreviewComplete: false,
            hexPreviewByteCount: 2);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.Reports.LoadReportJson(json, "replace-report.json");

        ReportLineViewModel difference = Assert.Single(viewModel.Reports.LoadedReport.OutputDifferences);
        Assert.Equal("Before preview, first 2 bytes", difference.BeforeLabel);
        Assert.Equal("After preview, first 2 bytes", difference.AfterLabel);
    }

    /// <summary>Verifies report inputs use readable CtrlRAM region labels instead of raw slot ids.</summary>
    [Fact]
    public void ReportReviewFormatsCtrlRamInputTitles()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.Reports.LoadReportJson(ReportJsonSamples.CtrlRamInputs(), "ctrlram-inputs.json");

        Assert.Contains(viewModel.Reports.LoadedReport.Inputs, input =>
            input.Title == "Base flash image" &&
            input.Classification == "base");
        Assert.Contains(viewModel.Reports.LoadedReport.Inputs, input =>
            input.Title == "VN CtrlRAM" &&
            input.Classification == "ctrlram");
        Assert.Contains(viewModel.Reports.LoadedReport.Inputs, input =>
            input.Title == "Normal CtrlRAM (Slave R)" &&
            input.Classification == "ctrlram");
    }
}
