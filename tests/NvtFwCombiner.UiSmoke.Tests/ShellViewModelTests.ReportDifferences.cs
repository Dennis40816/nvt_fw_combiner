using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies Replace reports surface accepted final-output CRC/header differences.</summary>
    [Fact]
    public async Task ReportReviewShowsAcceptedOutputDifferences()
    {
        string json = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(json, "replace-report.json");

        Assert.True(viewModel.LoadedReport.HasOutputDifferences);
        ReportLineViewModel difference = Assert.Single(viewModel.LoadedReport.OutputDifferences);
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
        Assert.True(viewModel.LoadedReport.HasOperationFlow);
        Assert.Contains(viewModel.LoadedReport.OperationFlow, node =>
            node.Title == "Refresh header and CRC" &&
            node.Number == "100" &&
            node.Meta == "command details in Postbuild tab");
        Assert.DoesNotContain(viewModel.LoadedReport.StepOperations, operation => operation.HasCodeBlock);
        Assert.NotEmpty(GetCommandOperations(viewModel.LoadedReport));
        Assert.True(viewModel.LoadedReport.HasPostbuildInvocations);
        ReportPostbuildInvocationViewModel invocation = Assert.Single(viewModel.LoadedReport.PostbuildInvocations);
        Assert.Equal("900.01", invocation.Number);
        Assert.Equal("Runtime invocation", invocation.Title);
        Assert.Equal("Expected changes", viewModel.LoadedReport.ByteDifferenceTitle);
        Assert.Contains("section", viewModel.LoadedReport.ByteDifferenceDetail, StringComparison.Ordinal);
        Assert.Equal("1/1 expected", viewModel.LoadedReport.ByteDifferenceMeta);
        Assert.Equal("Inspect expected changes", viewModel.LoadedReport.NextStepTitle);
        Assert.Contains("Inspect 1 expected change", viewModel.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Contains("affected data field", viewModel.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Contains(viewModel.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "Header" &&
            row.Count == "1" &&
            row.Status == "expected");
        ReportDifferenceGroupViewModel differenceGroup = Assert.Single(viewModel.LoadedReport.OutputDifferenceGroups);
        Assert.Equal("Header", differenceGroup.Title);
        Assert.Equal("1 expected field update", differenceGroup.Detail);
        viewModel.SelectedLanguage = "Traditional Chinese";
        await Assert.IsType<Task>(viewModel.ReportRelocalizationTask, exactMatch: false);

        Assert.Equal("差異", viewModel.Text.ReportTabChanges);
        Assert.Equal("預期變更", viewModel.LoadedReport.ByteDifferenceTitle);
        Assert.Equal("1/1 預期", viewModel.LoadedReport.ByteDifferenceMeta);
        ReportLineViewModel localizedDifference = Assert.Single(viewModel.LoadedReport.OutputDifferences);
        Assert.Contains(localizedDifference.Badges, badge => badge.Text == "預期");
        Assert.Equal("Header", localizedDifference.SectionLabel);
        Assert.Equal("變更前 bytes", localizedDifference.BeforeLabel);
        Assert.Equal("AA BB CC DD", localizedDifference.BeforeValue);
        Assert.Contains(viewModel.LoadedReport.OutputDifferenceSummaryRows, row =>
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
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(json, "replace-report.json");

        ReportLineViewModel difference = Assert.Single(viewModel.LoadedReport.OutputDifferences);
        Assert.Equal("Before preview, first 2 bytes", difference.BeforeLabel);
        Assert.Equal("After preview, first 2 bytes", difference.AfterLabel);
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
