// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Checks exact compiled step facts without deriving them from display geometry.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed class MemoryLayoutStepFactTests
{
    /// <summary>Logical output retains ordered operation identities and source/target coordinates without a physical map.</summary>
    [Fact]
    public void GeneralMergeStepFactsRetainExactOperationsWithoutPhysicalRegions()
    {
        CompiledComposition composition = CreateLogicalGeneralMergeComposition();
        ResolvedCapability capability = CreateLogicalGeneralMergeCapability(composition);
        ActiveSessionSnapshot session = LogicalSession(capability);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, capability.CompiledComposition);

        Assert.Equal(MemoryLayoutGeometryKind.LogicalOutput, layout.GeometryKind);
        Assert.Null(layout.MapId);
        Assert.Empty(layout.CanonicalRegions);
        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.Steps.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.None, layout.Steps.UnavailableReason);
        Assert.Equal(
            [
                ("copy-a", 10, "source-a", (ByteRange?)new ByteRange(0, 2), "output-image", new ByteRange(0, 2)),
                ("copy-b", 20, "source-b", (ByteRange?)new ByteRange(0, 2), "output-image", new ByteRange(4, 2)),
            ],
            layout.Steps.Values.Select(static step =>
                (step.OperationId, step.Sequence, step.SourceSpaceId, step.SourceRange, step.TargetSpaceId, step.TargetRange)));
        Assert.All(layout.Steps.Values, (step, index) => Assert.Same(capability.CompiledComposition.Plan.OrderedOperations[index], step));
        _ = Assert.Throws<NotSupportedException>(() => ((IList<CompositionOperation>)layout.Steps.Values).Clear());
    }

    /// <summary>A missing overlay cannot masquerade as an admitted empty operation plan.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingCompiledOverlayReportsStepFactsUnavailable(bool logicalOutput)
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ResolvedCapability capability = logicalOutput
            ? CreateLogicalGeneralMergeCapability(CreateLogicalGeneralMergeComposition())
            : fixture.Capability;
        ActiveSessionSnapshot session = logicalOutput
            ? LogicalSession(capability)
            : StandardSession(fixture);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, compiledOverlay: null);

        Assert.NotEmpty(capability.CompiledComposition.Plan.OrderedOperations);
        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.Steps.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.CompiledPlanNotResolved, layout.Steps.UnavailableReason);
        Assert.Empty(layout.Steps.Values);
        Assert.Same(layout.BeforeSegments, layout.AfterSegments);
    }

    /// <summary>A compiled plan with no operations has known-empty step facts rather than missing authority.</summary>
    [Fact]
    public void CompiledEmptyPlanReportsKnownEmptyStepFacts()
    {
        CompositionPlan original = CreateLogicalGeneralMergeComposition().Plan;
        CompositionPlan plan = new(original.Initializations, original.OutputSpaceId, original.AddressSpaces, []);
        CompiledComposition composition = CreateLogicalGeneralMergeComposition(plan);
        ResolvedCapability capability = CreateLogicalGeneralMergeCapability(composition);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, LogicalSession(capability), capability.CompiledComposition);

        Assert.Equal(MemoryLayoutFactStatus.KnownEmpty, layout.Steps.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.None, layout.Steps.UnavailableReason);
        Assert.Empty(layout.Steps.Values);
    }

    /// <summary>Scratch writes remain steps even when they have no input source or direct output segment.</summary>
    [Fact]
    public void StepFactsRetainSourceLessScratchWritesInTheirOriginalAddressSpace()
    {
        CompositionOperation fill = CompositionOperation.FillRange(
            "fill-scratch", 10, "scratch", new ByteRange(0, 4), 0xA5, OverlapPolicy.Reject, "seed scratch");
        CompositionOperation copy = CompositionOperation.CopyRange(
            "publish-scratch", 20, "scratch", new ByteRange(0, 4), "output-image", new ByteRange(0, 4),
            OverlapPolicy.Reject, "publish scratch bytes");
        CompositionPlan plan = new(
            [ImageInitialization.Blank("output-image", Capacity, 0), ImageInitialization.Blank("scratch", 4, 0)],
            "output-image",
            [.. MergePlan().AddressSpaces, new AddressSpace("scratch", 4, AddressSpaceMutability.Mutable)],
            [copy, fill]);
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge, plan);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, StandardSession(fixture), fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.Steps.Status);
        Assert.Equal([fill, copy], layout.Steps.Values);
        Assert.Same(fill, layout.Steps.Values[0]);
        Assert.Same(copy, layout.Steps.Values[1]);
        Assert.Null(fill.SourceSpaceId);
        Assert.Null(fill.SourceRange);
        Assert.Equal(("scratch", new ByteRange(0, 4)), (fill.TargetSpaceId, fill.TargetRange));
        Assert.Equal(("scratch", (ByteRange?)new ByteRange(0, 4)), (copy.SourceSpaceId, copy.SourceRange));
        Assert.DoesNotContain(layout.AfterSegments, segment => segment.ContributingOperations.Contains(fill));
    }

    /// <summary>The existing Domain issue reference resolves directly to the projected failing step.</summary>
    [Fact]
    public void StepIssueUsesTheExistingOperationIdAndProcessorWriteAuthority()
    {
        ByteRange stagedRange = new(0, Capacity);
        ByteRange[] writes = [new ByteRange(0, 1), new ByteRange(12, 1)];
        ExternalProcessorInvocation invocation = new("processor", "tool", [stagedRange], writes);
        CompositionOperation operation = CompositionOperation.RunExternalProcessor(
            "finalize-output", 10, "output-image", stagedRange, invocation, OverlapPolicy.Reject, "finalize output");
        CompositionPlan plan = new(ImageInitialization.Blank("output-image", Capacity, 0), MergePlan().AddressSpaces, [operation]);
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge, plan);
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, StandardSession(fixture), fixture.Composition);

        CompositionExecutionResult result = CompositionEngine.Execute(plan, new CompositionExecutionInput(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-input"] = new byte[checked((int)Capacity)],
                ["tp-input"] = new byte[checked((int)Capacity)],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.ExecutionExternalProcessorUnavailable, issue.Code);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        CompositionOperation step = Assert.Single(layout.Steps.Values, candidate => candidate.OperationId == issue.OperationId);
        Assert.Same(operation, step);
        Assert.Equal("finalize-output", issue.OperationId);
        Assert.Equal(stagedRange, step.TargetRange);
        Assert.Equal(writes, step.DeclaredWriteRanges);
        Assert.Null(step.SourceSpaceId);
        Assert.Null(step.SourceRange);
    }

    private static ActiveSessionSnapshot StandardSession(ProjectionFixture fixture)
    {
        return CreateSession(fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));
    }

    private static ActiveSessionSnapshot LogicalSession(ResolvedCapability capability)
    {
        CapabilityRouteIdentity route = capability.Identity;
        return new ActiveSessionSnapshot(route.WorkflowId, Token, new AuthoringRevision(3), route.RouteId,
            capability.CapabilityFingerprint, true, route.IcId, route.IcCountVariant, route.MapVariant,
            [route.IcId], [route.IcCountVariant],
            [Slot("source-a", AuthoringSlotLifecycle.Verified, 2), Slot("source-b", AuthoringSlotLifecycle.Verified, 2)],
            null, null, []);
    }
}
