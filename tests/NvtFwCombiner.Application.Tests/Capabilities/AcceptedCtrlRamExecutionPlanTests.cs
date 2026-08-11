using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Capabilities;

/// <summary>Contracts for the immutable CtrlRAM execution evidence retained by one compilation.</summary>
public sealed class AcceptedCtrlRamExecutionPlanTests
{
    /// <summary>Accepted selection, draft, and advisory evidence are retained without caller-owned collections.</summary>
    [Fact]
    public void AdvisoryIssuesAreDefensivelySnapshotted()
    {
        var advisory = new CompositionIssue(
            "ctrlram.advisory",
            "Review the source metadata.",
            "reference-base",
            CompositionIssueSeverity.Warning);
        var issues = new List<CompositionIssue> { advisory };
        var draft = new CtrlRamFirmwareVersionDraftState(0x12, 0x34);

        var plan = new AcceptedCtrlRamExecutionPlan(
            IcNumberSelection.FromToken("2"),
            draft,
            issues);
        issues.Clear();

        Assert.Equal("2", Assert.Single(plan.IcNumberSelection.Parts));
        Assert.Same(draft, plan.FirmwareVersionDraft);
        Assert.Same(advisory, Assert.Single(plan.AdvisoryIssues));
    }
}
