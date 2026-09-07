using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Live and reopened reports render the same understandable input diagnostics.</summary>
public sealed class ReportInputFeedbackTests
{
    /// <summary>The primary input error offers the same actionable help in Summary and Issues.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrimaryInputFailureUsesSpecificNextAction(bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var review = ReportReviewViewModel.FromJson(Json(Report([
            new("input.artifact.read-failed", "Unable to read artifact (IOException).", "tp-input")
        ])), "failure.json", language: language);
        Assert.Equal(review.PrimaryIssue.Detail, review.NextStepDetail);
        Assert.Contains(chinese ? "讀取權限" : "read access", review.NextStepDetail, StringComparison.Ordinal);
    }

    /// <summary>All reviewed input diagnostics use human help while preserving original typed evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedAndJsonInputIssuesUseTheSameLocalizedHelp(bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        string[] codes = ["DP_UNIFORM_CONTENT_WARNING", "TP_UNIFORM_CONTENT_WARNING", "LDC_UNIFORM_CONTENT_WARNING",
            "input.inspection.extension-not-accepted", "input.inspection.source-unreadable", "input.artifact.read-failed",
            "input.artifact.content-snapshot-mismatch", "input.binding.missing", CompositionIssueCodes.InputAddressSpaceLengthMismatch,
            "input.source-view.incomplete"];
        CompositionIssue[] issues = [.. codes.Select((code, index) => new CompositionIssue(
            code, $"Original diagnostic {index}: 0x10-0x20.", $"inspect-{index}", index < 3 ? "warning" : "error"))];
        CompositionRunReport source = Report(issues);
        string json = Json(source);
        var live = ReportReviewViewModel.FromReportCancellable(source, false, "live", null, null, language, TestContext.Current.CancellationToken);
        var loaded = ReportReviewViewModel.FromJson(json, "saved.json", language: language);
        for (int index = 0; index < issues.Length; index++)
        {
            CompositionIssue original = issues[index];
            ReportLineViewModel row = live.Issues[index];
            ReportLineViewModel reopened = loaded.Issues[index];
            Assert.NotEqual(original.Message, row.Detail);
            Assert.Equal(original.Code, row.Title);
            Assert.Equal(original.Severity, row.Severity);
            Assert.Equal(original.OperationId, row.Meta);
            Assert.Equal(original.Message, row.CodeBlock);
            Assert.Equal((row.Title, row.Detail, row.Meta, row.CodeBlock),
                (reopened.Title, reopened.Detail, reopened.Meta, reopened.CodeBlock));
            Assert.Contains("Build", row.Detail, StringComparison.Ordinal);
            Assert.DoesNotContain(original.Code, row.Detail, StringComparison.Ordinal);
        }
        Assert.Equal(3, live.WarningCount);
        Assert.Equal(7, live.BlockingIssueCount);
        Assert.Equal(issues[3].Code, live.PrimaryIssue.Title);
        Assert.Equal(loaded.SummaryIssueDescriptions, live.SummaryIssueDescriptions);
        Assert.Contains(chinese ? "TP 範圍疑似為填充值" : "TP range contains repeated data", live.SummaryIssueDescriptions, StringComparison.Ordinal);
        Assert.Contains(chinese ? "另有 5 項" : "+5 more", live.SummaryIssueDescriptions, StringComparison.Ordinal);
        Assert.Equal(6, live.SummaryIssueDescriptions.Split(Environment.NewLine).Length);
        Assert.DoesNotContain("UNIFORM_CONTENT_WARNING", live.SummaryIssueDescriptions, StringComparison.Ordinal);
        Assert.Equal(json, Json(source));
    }

    /// <summary>A warning-only Summary leads with the finding and its correct Build impact.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarningSummaryDoesNotRepeatTheMisleadingRawDiagnostic(bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        CompositionRunReport source = Report([new CompositionIssue("TP_UNIFORM_CONTENT_WARNING",
            "Input range is a repeated-byte placeholder and cannot be used.", "tp-content-plausibility", "warning")]);
        var review = ReportReviewViewModel.FromJson(Json(source), "warning.json", language: language);
        Assert.True(review.HasWarningsWithoutBlockingIssues);
        Assert.Equal(chinese ? "警告: TP 範圍疑似為填充值" : "Warning: TP range contains repeated data", review.SummaryIssueDescriptions);
        Assert.Contains(chinese ? "此警告不會阻擋 Build" : "This warning does not block Build", review.NextStepDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot be used", review.NextStepDetail, StringComparison.Ordinal);
        Assert.Contains("cannot be used", Assert.Single(review.Issues).CodeBlock, StringComparison.Ordinal);
    }

    /// <summary>Unknown codes and unexpected severities are not silently reclassified or explained.</summary>
    [Fact]
    public void UnknownAndNonWarningUniformCodesPreserveFallback()
    {
        CompositionIssue[] issues = [new("OTHER", "Unknown raw message", "step", "warning"),
            new("TP_UNIFORM_CONTENT_WARNING", "Blocking raw message", "step", "error")];
        var review = ReportReviewViewModel.FromJson(Json(Report(issues)), "fallback.json");
        Assert.Equal(issues.Select(static issue => issue.Message), review.Issues.Select(static issue => issue.Detail));
        Assert.All(review.Issues, static row => Assert.False(row.HasCodeBlock));
        Assert.Equal(1, review.WarningCount);
        Assert.Equal(1, review.BlockingIssueCount);
    }

    /// <summary>The concise header keeps original diagnostic order and never hides the complete issue list.</summary>
    [Fact]
    public void SummaryRetainsMixedSeverityAndCleanState()
    {
        var review = ReportReviewViewModel.FromJson(Json(Report([
            new("TP_UNIFORM_CONTENT_WARNING", "raw warning", severity: "warning"),
            new("input.source-view.incomplete", "raw short input"),
            new("OTHER", "A custom finding"),
        ])), "mixed.json");
        Assert.Equal(["Warning: TP range contains repeated data", "Error: Input is too short", "Error: A custom finding"],
            review.SummaryIssueDescriptions.Split(Environment.NewLine));
        Assert.Equal(3, review.Issues.Count);
        Assert.Equal("input.source-view.incomplete", review.PrimaryIssue.Title);
        var clean = ReportReviewViewModel.FromJson(Json(Report([])), "clean.json");
        Assert.Empty(clean.SummaryIssueDescriptions);
    }

    /// <summary>Informational diagnostics are not mislabeled as warnings in the concise summary.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SummaryKeepsInformationDistinctFromWarning(bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        CompositionRunReport source = Report([new("TP_UNIFORM_CONTENT_WARNING", "Informational raw finding", severity: "info")]);
        var live = ReportReviewViewModel.FromReportCancellable(source, false, "live", null, null, language, TestContext.Current.CancellationToken);
        var loaded = ReportReviewViewModel.FromJson(Json(source), "info.json", language: language);
        Assert.Equal(chinese ? "資訊: Informational raw finding" : "Info: Informational raw finding", loaded.SummaryIssueDescriptions);
        Assert.Equal(loaded.SummaryIssueDescriptions, live.SummaryIssueDescriptions);
        Assert.Equal(0, loaded.BlockingIssueCount);
        Assert.False(Assert.Single(loaded.Issues).HasCodeBlock);
    }

    internal static CompositionRunReport Report(IReadOnlyList<CompositionIssue> issues,
        IReadOnlyList<InputDiagnosticSummary>? inputDiagnostics = null)
    {
        DateTimeOffset timestamp = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
        return new CompositionRunReport("feedback-test", "test-profile", "1.0.0", "NT51929",
            "standard-merge", "standard-merge", CompositionKind.Merge, timestamp, timestamp,
            [], [], [], issues, new OutputArtifactSummary("preview.bin", 0, new string('a', 64), committed: false),
            inputDiagnostics: inputDiagnostics);
    }

    internal static string Json(CompositionRunReport report)
    {
        return CompositionRunReportJson.Serialize(new CompositionRunResult(
            report.Issues.Any(static issue => issue.Severity == "error") ? CompositionExecutionStatus.Failed : CompositionExecutionStatus.Succeeded,
            ReadOnlyMemory<byte>.Empty, report, null, null, null, null, null, null));
    }
}
