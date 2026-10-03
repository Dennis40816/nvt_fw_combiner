using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ReportReviewHistoryTests
{
    /// <summary>Current and reopened cards retain the best recorded cause, independently of acceptance.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ReportDifferenceCausesPreserveSemanticThenLegacyExplanation(bool accepted, bool semantic)
    {
        const string legacyCause = "Postbuild refreshed the TP header CRC fields.";
        const string fieldCause = "Expected: postbuild recalculated DLM CRC 0.";
        var difference = new OutputDifferenceSummary(
            "diff-001", new ByteRange(28, 4), 4, OutputDifferenceClassifications.PostbuildCrcHeader,
            accepted, "recorded processor evidence", legacyCause, "Header", "before-hash", "after-hash",
            semantic: semantic ? new OutputDifferenceSemantic(
                "tp-flash-header", "TP Flash Header", "header-0-dlm-crc", "DLM CRC 0", fieldCause) : null);
        ReportReviewViewModel.OutputDifferenceProjection live = ReportReviewViewModel.ProjectOutputDifferences(
            [difference], "reported-output", 32, ShellLanguage.English, TestContext.Current.CancellationToken);
        JsonNode root = JsonNode.Parse(ReportJsonSamples.ReplaceWithAcceptedOutputDifferences())!;
        root["OutputDifferences"] = new JsonArray(JsonSerializer.SerializeToNode(difference));
        ReportReviewViewModel reopened = ReportReviewViewModel.FromJson(root.ToJsonString(), "legacy-cause.json");
        string expected = semantic ? fieldCause : legacyCause;
        foreach (ReportHexDiffRangeViewModel card in new[]
            { Assert.Single(live.HexDiffSource.NavigatorRows), Assert.Single(reopened.HexDiff.Ranges) })
        {
            Assert.Equal(expected, card.Reason);
            Assert.Equal(accepted, card.IsAccepted);
            Assert.Equal(accepted ? "Expected" : "Review required", card.Status);
        }
    }

    /// <summary>Legacy reports without a usable cause describe only their recorded classification.</summary>
    [Theory]
    [InlineData(OutputDifferenceClassifications.PostbuildCrcHeader, "Header / CRC fields", "Postbuild updated Header / CRC fields.", "Postbuild 已更新 Header / CRC fields。")]
    [InlineData(OutputDifferenceClassifications.DeclaredReplacement, "Normal CtrlRAM", "Source bytes copied into Normal CtrlRAM.", "來源 bytes 已複製至Normal CtrlRAM。")]
    [InlineData(OutputDifferenceClassifications.PreservedReference, "Reference base", "Bytes changed in a range declared to remain unchanged.", "宣告應保持不變的區段中出現 byte 變更。")]
    [InlineData(OutputDifferenceClassifications.Unexpected, "DLM CRC 0", "No specific cause was recorded for this change.", "此變更未記錄具體原因。")]
    [InlineData("future-classification", "DLM CRC 0", "No specific cause was recorded for this change.", "此變更未記錄具體原因。")]
    [InlineData(OutputDifferenceClassifications.PostbuildCrcHeader, "", "Postbuild updated CRC/header.", "Postbuild 已更新 CRC/header。")]
    [InlineData(OutputDifferenceClassifications.DeclaredReplacement, "", "Source bytes copied into the declared replacement range.", "來源 bytes 已複製至宣告的替換區段。")]
    public void ReportDifferenceCauseFallbackDoesNotInferFromAcceptanceOrSection(
        string classification, string section, string expected, string chineseExpected)
    {
        foreach (bool accepted in new[] { false, true })
        {
            foreach (JsonNode? unusableCause in new JsonNode?[] { null, JsonValue.Create(" "), JsonValue.Create(42), new JsonObject() })
            {
                JsonNode root = JsonNode.Parse(ReportJsonSamples.ReplaceWithAcceptedOutputDifferences())!;
                JsonNode difference = root["OutputDifferences"]![0]!;
                difference["Semantic"] = null;
                difference["Explanation"] = unusableCause;
                difference["Classification"] = classification;
                difference["SectionLabel"] = section;
                difference["IsAccepted"] = accepted;
                var report = ReportReviewViewModel.FromJson(root.ToJsonString(), "legacy-no-cause.json");
                ReportLineViewModel row = Assert.Single(report.OutputDifferences);
                Assert.Equal(expected, row.Reason);
                Assert.Equal(accepted && classification != OutputDifferenceClassifications.Unexpected, row.IsAccepted);
                var chinese = ReportReviewViewModel.FromJson(root.ToJsonString(), "legacy-no-cause.json",
                    language: ShellLanguage.ChineseTraditional);
                Assert.Equal(chineseExpected, Assert.Single(chinese.OutputDifferences).Reason);
                if (unusableCause is null)
                {
                    Assert.True(difference.AsObject().Remove("Explanation"));
                    var missing = ReportReviewViewModel.FromJson(root.ToJsonString(), "missing-cause.json");
                    Assert.Equal(expected, Assert.Single(missing.OutputDifferences).Reason);
                }
            }
        }
    }

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
