using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Keeps actual runtime argv visible even without a profile-declared command block.</summary>
    [Fact]
    public void ReportReviewRoutesRuntimeOnlyEvidenceToCommands()
    {
        var report = ReportReviewViewModel.FromJson(
            ReportJsonSamples.RuntimeOnlyCommandTrace(),
            "runtime-only.json");

        ReportLineViewModel command = Assert.Single(GetCommandOperations(report));
        Assert.False(command.HasCodeBlock);
        Assert.True(command.HasRuntimeCommands);
        Assert.Empty(report.StepOperations);
    }

    /// <summary>Verifies report triage points users to the first issue and command evidence.</summary>
    [Fact]
    public void ReportReviewTriagePrioritizesIssueAndCommandEvidence()
    {
        string json = ReportJsonSamples.CtrlRamCommandIssue();

        var report = ReportReviewViewModel.FromJson(json, "preview-report.json");

        Assert.Equal("Needs attention", report.OutcomeTitle);
        Assert.Equal("Start with this issue", report.NextStepTitle);
        Assert.Equal("processor.tool.missing", report.PrimaryIssue.Title);
        Assert.Equal(1, report.PostbuildInvocationCount);
        ReportPostbuildInvocationViewModel invocation = Assert.Single(report.PostbuildInvocations);
        Assert.Equal("900.01", invocation.Number);
        Assert.Equal("Runtime invocation", invocation.Title);
        Assert.Equal("900. Postbuild refresh", invocation.OperationTitle);
        Assert.Equal("planned", invocation.Status);
        Assert.Contains("argv[0]: MERGE_MODE", invocation.ArgumentListEvidence, StringComparison.Ordinal);
        Assert.Equal("Working directory: C:\\staging\\ui-smoke-command", invocation.WorkingDirectoryDetail);
        ReportLineViewModel command = Assert.Single(GetCommandOperations(report));
        Assert.Equal("run-external-processor", command.OperationKind);
        Assert.Equal("(none)", command.OperationSource);
        Assert.Equal("output-image 0x0-0x7FFFF (len 0x80000)", command.OperationTarget);
        Assert.Equal("legacy-combiner", command.OperationProcessor);
        Assert.Equal("planned", command.OperationStatus);
        Assert.Equal("Profile-declared Combiner plan", command.CodeBlockLabel);
        Assert.Contains("Combiner.exe", command.CodeBlock, StringComparison.Ordinal);
        ReportRuntimeCommandViewModel runtimeCommand = Assert.Single(command.RuntimeCommands);
        Assert.Equal("Runtime invocation 1", runtimeCommand.Title);
        Assert.Contains("exe: C:\\tools\\legacy-combiner\\Combiner.exe", runtimeCommand.ArgumentListEvidence, StringComparison.Ordinal);
        Assert.Contains("argv[0]: MERGE_MODE", runtimeCommand.ArgumentListEvidence, StringComparison.Ordinal);
        Assert.Contains("argv[2]: C:\\staging\\ui-smoke-command\\BIN\\Normal_Ctrlram.bin", runtimeCommand.ArgumentListEvidence, StringComparison.Ordinal);
        Assert.Equal("Working directory: C:\\staging\\ui-smoke-command", runtimeCommand.WorkingDirectoryDetail);
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

    /// <summary>Verifies every recorded external process call receives an independently numbered Postbuild row.</summary>
    [Fact]
    public void ReportReviewFlattensRuntimeInvocationsPerPostbuildOperation()
    {
        var report = ReportReviewViewModel.FromJson(
            ReportJsonSamples.CtrlRamCommandTrace(runtimeInvocationCount: 3),
            "runtime-trace.json");

        _ = Assert.Single(GetCommandOperations(report));
        Assert.Equal(3, report.PostbuildInvocationCount);
        Assert.Equal(
            ["900.01", "900.02", "900.03"],
            report.PostbuildInvocations.Select(invocation => invocation.Number));
        Assert.All(report.PostbuildInvocations, invocation =>
        {
            Assert.Equal("Runtime invocation", invocation.Title);
            Assert.Equal("900. Postbuild refresh", invocation.OperationTitle);
            Assert.Contains("exe: C:\\tools\\legacy-combiner\\Combiner.exe", invocation.ArgumentListEvidence, StringComparison.Ordinal);
        });
        Assert.Contains("3 postbuild commands", report.OperationFlow.Single(node =>
            node.Title == "Refresh header and CRC").Detail, StringComparison.Ordinal);
    }
}
