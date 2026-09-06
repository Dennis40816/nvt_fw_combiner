using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Cards and Report translate owner facts without scanning or parsing firmware diagnostics.</summary>
public sealed class IssueCardProjectionTests
{
    /// <summary>Minimum size is concise, localized and only attributed to the source-coverage issue.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShortInputCardUsesTypedMinimumAndClearsOnFileChange(bool chinese)
    {
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(StandardMergeFeedbackTests.Status(
            "input.source-view.incomplete", AuthoringSlotLifecycle.Error, 262143, 262144), chinese);
        IssueCardViewModel card = Assert.IsType<IssueCardViewModel>(slot.IssueCard);
        Assert.Contains("≥ 256 KiB", card.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("262143", card.AutomationText, StringComparison.Ordinal);
        Assert.True(slot.BlocksBuild);
        Assert.DoesNotContain("Error: Error:", slot.SemanticStateAutomationText, StringComparison.Ordinal);
        slot.ApplyExperienceText(ShellTextResources.For(chinese ? ShellLanguage.English : ShellLanguage.ChineseTraditional));
        Assert.Contains(chinese ? "too short" : "太短", slot.IssueCard!.Summary, StringComparison.Ordinal);
        slot.FilePath = @"C:\firmware\other.bin";
        // The session still owns readiness; discard old numeric facts without locally allowing Build.
        Assert.True(slot.BlocksBuild);
        Assert.Null(slot.InspectedSlotId);
        Assert.DoesNotContain("256 KiB", slot.SemanticStateAutomationText, StringComparison.Ordinal);

        FirmwareSlotViewModel other = StandardMergeFeedbackTests.Slot(StandardMergeFeedbackTests.Status(
            "input.address-space.length-mismatch", AuthoringSlotLifecycle.Error, 2, 262144), chinese);
        Assert.DoesNotContain("≥", other.IssueCard!.Summary, StringComparison.Ordinal);
    }

    /// <summary>Every repeated byte comes from the declared-range evidence, never an assumed FF.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(165)]
    [InlineData(255)]
    public void UniformCardUsesActualByteAndKeepsWarningNonblocking(byte repeatedByte)
    {
        var evidence = new InputDiagnosticEvidence("tp-input", null, null, new ByteRange(0, 2), repeatedByte);
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(StandardMergeFeedbackTests.Status(
            "TP_UNIFORM_CONTENT_WARNING", AuthoringSlotLifecycle.Warning, evidence: evidence), false);
        Assert.Equal($"TP region contains only 0x{repeatedByte:X2}", slot.IssueCard!.Summary);
        Assert.DoesNotContain("0x0,", slot.IssueCard.AutomationText, StringComparison.Ordinal);
        Assert.False(slot.BlocksBuild);
        Assert.Contains("Does not block Build", slot.SemanticStateAutomationText, StringComparison.Ordinal);
        slot.SetInputInspectionPending("Checking");
        Assert.Null(slot.IssueCard);
    }

    /// <summary>Typed and reopened reports bind exact numeric evidence by final index, even for duplicate issue identities.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReportNumericEvidenceSurvivesReopenWithoutAmbiguousMatching(bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        CompositionIssue[] issues = [new("TP_UNIFORM_CONTENT_WARNING", "Identical original", "same-rule", "warning"),
            new("TP_UNIFORM_CONTENT_WARNING", "Identical original", "same-rule", "warning"),
            new("input.source-view.incomplete", "Opaque short diagnostic", "read")];
        InputDiagnosticSummary[] diagnostics = [
            new(1, "tp-input", new("tp-input", null, null, new ByteRange(0x7000, 0x39000), 0xFF)),
            new(2, "tp-input", new("tp-input", 262143, 262144, new ByteRange(0x7000, 0x39000), null)),
        ];
        CompositionRunReport source = ReportInputFeedbackTests.Report(issues, diagnostics);
        var live = ReportReviewViewModel.FromReportCancellable(source, false, "live", null, null, language, TestContext.Current.CancellationToken);
        var reopened = ReportReviewViewModel.FromJson(ReportInputFeedbackTests.Json(source), "saved.json", language: language);
        Assert.Equal(live.SummaryIssueDescriptions, reopened.SummaryIssueDescriptions);
        Assert.DoesNotContain("0xFF", reopened.Issues[0].Detail, StringComparison.Ordinal);
        Assert.Contains("0xFF", reopened.Issues[1].IssueSummary, StringComparison.Ordinal);
        Assert.Contains("[0x7000, 0x40000)", reopened.Issues[1].Detail, StringComparison.Ordinal);
        Assert.Contains("262,143", reopened.Issues[2].Detail, StringComparison.Ordinal);
        Assert.Contains("262,144", reopened.Issues[2].Detail, StringComparison.Ordinal);
        Assert.Contains(chinese ? "缺少：1 bytes" : "Missing: 1 bytes", reopened.Issues[2].Detail, StringComparison.Ordinal);
        Assert.Contains("≥ 256 KiB", reopened.SummaryIssueDescriptions, StringComparison.Ordinal);
        Assert.Equal(live.Issues.Select(static row => row.Detail), reopened.Issues.Select(static row => row.Detail));
        Assert.Equal(issues.Select(static issue => issue.Message), reopened.Issues.Select(static row => row.CodeBlock));
        Assert.Equal(1, reopened.BlockingIssueCount);
        Assert.Equal(2, reopened.WarningCount);
        Assert.Contains(chinese ? "沒有產生輸出" : "did not produce an output", reopened.OutcomeDetail, StringComparison.Ordinal);
    }

    /// <summary>Invalid optional evidence cannot break or reclassify a durable report.</summary>
    [Theory]
    [InlineData("duplicate")]
    [InlineData("negative")]
    [InlineData("outside")]
    [InlineData("missing")]
    [InlineData("byte")]
    [InlineData("range")]
    [InlineData("length")]
    public void InvalidReportEvidenceFallsBackToOriginalIssue(string failure)
    {
        CompositionIssue[] issues = [new("TP_UNIFORM_CONTENT_WARNING", "Original raw diagnostic", "rule", "warning")];
        CompositionRunReport source = ReportInputFeedbackTests.Report(issues,
            [new(0, "tp-input", new("tp-input", null, null, new ByteRange(0, 2), 0xFF))]);
        JsonNode root = JsonNode.Parse(ReportInputFeedbackTests.Json(source))!;
        JsonArray entries = root["InputDiagnostics"]!.AsArray();
        JsonNode entry = entries[0]!;
        switch (failure)
        {
            case "duplicate": entries.Add(entry.DeepClone()); entries.Add(entry.DeepClone()); break;
            case "negative": entry["IssueIndex"] = -1; break;
            case "outside": entry["IssueIndex"] = 1; break;
            case "missing": entry["Evidence"] = null; break;
            case "byte": entry["Evidence"]!["RepeatedByte"] = 256; break;
            case "range": entry["Evidence"]!["SourceRange"]!["Length"] = 0; break;
            case "length": entry["Evidence"]!["ActualLength"] = -1; break;
            default: throw new ArgumentOutOfRangeException(nameof(failure), failure, null);
        }
        var report = ReportReviewViewModel.FromJson(root.ToJsonString(), "invalid.json");
        Assert.Equal("Original raw diagnostic", Assert.Single(report.Issues).CodeBlock);
        Assert.DoesNotContain("only 0xFF", report.SummaryIssueDescriptions, StringComparison.Ordinal);
        Assert.Equal(0, report.BlockingIssueCount);
        Assert.Equal(1, report.WarningCount);
    }
}
