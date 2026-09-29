using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Protected untouched blank ranges retain initialization independently of their disposition.</summary>
    [Fact]
    public void ProtectedBlankAndSourceLessFillHaveDifferentInitializationFacts()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot session = CreateSession(fixture, Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);
        MemoryLayoutSegment protectedRange = Assert.Single(layout.AfterSegments, item => item.ContentRole == MemoryContentRole.Reserved);
        Assert.Equal(MemoryWorkflowDisposition.Resolved, protectedRange.Disposition);
        Assert.Equal((byte)0, protectedRange.InitializationFillByte);
        Assert.Equal(MemoryPlanDetailKind.Initialization, protectedRange.DetailKind);
        CompositionPlan plan = MergePlan();
        var filledPlan = new CompositionPlan(plan.OutputInitialization, plan.AddressSpaces,
            [CompositionOperation.FillRange("fill", 0, plan.OutputSpaceId, new ByteRange(0, 4), 0xA5, OverlapPolicy.Reject, "source-less write")]);
        ProjectionFixture filled = CreateFixture(CompositionKind.Merge, customPlan: filledPlan);
        MemoryLayoutSnapshot output = MemoryLayoutProjector.Project(filled.Capability,
            CreateSession(filled, Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity), Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity)), filled.Composition);
        MemoryLayoutSegment first = output.AfterSegments[0];
        Assert.Null(first.SourceSpaceId);
        Assert.Null(first.InitializationFillByte);
        Assert.Equal(MemoryPlanDetailKind.Unassigned, first.DetailKind);
        Assert.Equal(MemoryPlanActionKind.Copy, first.Action);
    }

    /// <summary>Reference admission and replacement remain distinct from blank initialization and map content.</summary>
    [Fact]
    public void ReferenceFactsPreserveKeptGroupsAndDoNotInventInitialization()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Replace);
        foreach (AuthoringSlotLifecycle state in new[] { AuthoringSlotLifecycle.Empty, AuthoringSlotLifecycle.Verified })
        {
            ActiveSessionSnapshot session = CreateSession(fixture, Slot("reference-base", state, Capacity),
                Slot("dp-replacement", AuthoringSlotLifecycle.Verified, Capacity));
            MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);
            Assert.All(layout.BeforeSegments, item => Assert.Null(item.InitializationFillByte));
            MemoryLayoutSegment retained = layout.AfterSegments[^1];
            Assert.Equal(state == AuthoringSlotLifecycle.Verified, retained.IsReferenceContent);
            Assert.Equal(state == AuthoringSlotLifecycle.Verified ? MemoryPlanDetailKind.ReferenceKept : MemoryPlanDetailKind.Unassigned, retained.DetailKind);
            if (retained.IsReferenceContent)
            {
                Assert.Equal(MemoryPlanActionKind.Preserve, retained.Action);
                Assert.Equal(ReplaceRegionGroup.Base, MemoryLayoutProjector.GetDisplayGroup([retained]));
            }
            MemoryLayoutSegment replacement = layout.AfterSegments[0];
            Assert.Equal(MemoryArtifactKind.DpReplacement, replacement.SourceKind);
            Assert.Equal(MemoryPlanActionKind.Replace, replacement.Action);
            Assert.Equal(MemoryContentRole.Dp, replacement.ContentRole);
        }
    }

    /// <summary>No exact capability is needed to preserve distinct workflow prerequisites.</summary>
    [Theory]
    [InlineData(MemoryPendingPrerequisite.DpBin)]
    [InlineData(MemoryPendingPrerequisite.BaseBin)]
    [InlineData(MemoryPendingPrerequisite.CtrlRamReplacement)]
    [InlineData(MemoryPendingPrerequisite.GeneralMergeSourceMapping)]
    public void UnresolvedPrerequisitesAreTypedAndNonGeometric(MemoryPendingPrerequisite prerequisite)
    {
        MemoryLayoutPendingDisplay pending = MemoryLayoutProjector.ProjectPending(null, [], prerequisite);
        Assert.Equal(prerequisite, pending.FallbackPrerequisite);
        Assert.Equal(MemoryPlanActionKind.Browse, pending.Action);
        Assert.Equal(MemoryDiagnosticSeverity.Information, pending.Severity);
        Assert.Null(pending.SlotId);
    }

    /// <summary>Unresolved pending uses original lifecycle and rejects a stale selected-path state.</summary>
    [Fact]
    public void UnresolvedReferencePrecedesBlockedInputAndStaleHealthIsNotReused()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Replace);
        var issue = new AuthoringSlotIssueReference(AuthoringDerivedResultKind.Inspection, "inspection-1", "invalid");
        ActiveSessionSnapshot session = CreateSession(fixture, Slot("reference-base", AuthoringSlotLifecycle.Empty),
            Slot("dp-replacement", AuthoringSlotLifecycle.Error, Capacity, issue));
        MemoryLayoutPendingInput[] inputs = [new("reference-base", "reference-base", true, null),
            new("dp-replacement", "dp-replacement", true, "dp-replacement.bin")];
        Assert.Null(session.ExactCapability);
        Assert.Equal("reference-base", MemoryLayoutProjector.ProjectPending(session, inputs, MemoryPendingPrerequisite.CtrlRamReplacement).SlotId);
        inputs[0] = inputs[0] with { SelectedPath = "reference.bin" };
        MemoryLayoutPendingDisplay blocked = MemoryLayoutProjector.ProjectPending(session, inputs, MemoryPendingPrerequisite.CtrlRamReplacement);
        Assert.Equal("dp-replacement", blocked.SlotId);
        Assert.Equal(MemoryPlanActionKind.Blocked, blocked.Action);
        Assert.Equal(MemoryDiagnosticSeverity.Error, blocked.Severity);
        inputs[1] = inputs[1] with { SelectedPath = "different.bin" };
        Assert.Equal(MemoryPlanActionKind.Browse, MemoryLayoutProjector.ProjectPending(session, inputs, MemoryPendingPrerequisite.CtrlRamReplacement).Action);
    }
}
