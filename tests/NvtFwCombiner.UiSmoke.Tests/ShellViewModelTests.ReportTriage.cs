using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies report triage points users to the first issue and command evidence.</summary>
    [Fact]
    public void ReportReviewTriagePrioritizesIssueAndCommandEvidence()
    {
        string json = ReportJsonSamples.CtrlRamCommandIssue();

        var report = ReportReviewViewModel.FromJson(json, "preview-report.json");

        Assert.Equal("Needs attention", report.OutcomeTitle);
        Assert.Equal("Start with this issue", report.NextStepTitle);
        Assert.Equal("processor.tool.missing", report.PrimaryIssue.Title);
        Assert.Contains(report.TriageRows, row =>
            row.Title == "1. First issue" &&
            row.Detail == "processor.tool.missing" &&
            row.Meta == "run-ctrlram-postbuild");
        Assert.Contains(report.TriageRows, row =>
            row.Title == "4. Evidence" &&
            row.Detail == "Refresh commands" &&
            row.Meta == "1 command(s)");
        Assert.Contains(report.EvidenceRows, row =>
            row.Title == "Commands" &&
            row.Detail == "1" &&
            row.Meta == "external processors");
        Assert.True(report.ShouldExpandIssues);
        Assert.True(report.ShouldExpandCommandOperations);
        Assert.False(report.ShouldExpandStepOperations);
        ReportLineViewModel command = Assert.Single(report.CommandOperations);
        Assert.Equal("run-external-processor", command.OperationKind);
        Assert.Equal("(none)", command.OperationSource);
        Assert.Equal("output-image 0x0-0x7FFFF (len 0x80000)", command.OperationTarget);
        Assert.Equal("legacy-combiner", command.OperationProcessor);
        Assert.Equal("planned", command.OperationStatus);
        Assert.Contains("Combiner.exe", command.CodeBlock, StringComparison.Ordinal);
        Assert.Contains(command.Badges, badge => badge.Text == "planned");
        Assert.Contains(command.Badges, badge => badge.Text == "overlap reject");
        Assert.Contains(command.Badges, badge => badge.Text == "built-in-profile");
        Assert.Contains(command.Facts, fact => fact.Label == "Operation source" && fact.Value == "built-in-profile");
        Assert.Contains(command.Facts, fact => fact.Label == "Processor" && fact.Value == "legacy-combiner");
        Assert.Contains(command.Facts, fact => fact.Label == "Tool" && fact.Value == "legacy-combiner-1.13.0");
        Assert.True(command.HasRangeRows);
        Assert.Contains(command.RangeRows, row =>
            row.Kind == "Target" &&
            row.AddressSpace == "output-image" &&
            row.Range == "0x0-0x7FFFF (len 0x80000)" &&
            row.Source == "work image");
        Assert.Contains(command.RangeRows, row =>
            row.Kind == "Processor read" &&
            row.AddressSpace == "output-image" &&
            row.Range == "0x0-0x7FFFF (len 0x80000)" &&
            row.Source == "postbuild read policy");
        Assert.Contains(command.RangeRows, row =>
            row.Kind == "Processor write" &&
            row.AddressSpace == "output-image" &&
            row.Range == "0x7100-0x7103 (len 0x4)" &&
            row.Source == "postbuild write policy");
        Assert.Contains(command.RangeRows, row =>
            row.Kind == "Processor write" &&
            row.AddressSpace == "output-image" &&
            row.Range == "0x7118-0x711B (len 0x4)" &&
            row.Source == "postbuild write policy");
    }
}
