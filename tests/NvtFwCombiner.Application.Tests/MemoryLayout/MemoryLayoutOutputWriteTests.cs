// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Output-coordinate declarations remain distinct from execution steps and final byte provenance.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed class MemoryLayoutOutputWriteTests
{
    /// <summary>Standard Merge publishes the exact direct writes and selected source slices.</summary>
    [Fact]
    public void StandardMergePublishesDirectWritesWithInputSources()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.OutputWrites.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.None, layout.OutputWrites.UnavailableReason);
        Assert.Equal(
            [("copy-dp", "flash", new ByteRange(0, 4), "dp-input", (ByteRange?)new ByteRange(0, 4)),
             ("copy-tp", "flash", new ByteRange(12, 4), "tp-input", (ByteRange?)new ByteRange(12, 4))],
            layout.OutputWrites.Values.Select(static write =>
                (write.OperationId, write.OutputRange.AddressSpaceId, write.OutputRange.Range, write.SourceSlotId, write.SourceRange)));
        Assert.All(layout.OutputWrites.Values, static write => Assert.Null(write.BankId));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
        _ = Assert.Throws<NotSupportedException>(() => ((IList<MemoryLayoutOutputWrite>)layout.OutputWrites.Values).Clear());
    }

    /// <summary>Ordinary Replace retains its input source and does not list reference initialization as a step.</summary>
    [Fact]
    public void OrdinaryReplacePublishesOnlyItsDeclaredReplacement()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Replace);

        MemoryLayoutSnapshot layout = Project(fixture);

        MemoryLayoutOutputWrite write = Assert.Single(layout.OutputWrites.Values);
        Assert.Equal(("replace-dp", "flash", new ByteRange(0, 4), "dp-replacement", new ByteRange(0, 4)),
            (write.OperationId, write.OutputRange.AddressSpaceId, write.OutputRange.Range, write.SourceSlotId, write.SourceRange));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>One content write can be placed twice; the two publishing copies remain only in Steps.</summary>
    [Fact]
    public void ScratchInputWritePublishesEveryPlacementWithoutTransportSteps()
    {
        CompositionOperation content = CompositionOperation.CopyRange("content", 10, "dp-input", new ByteRange(3, 4),
            "scratch", new ByteRange(1, 4), OverlapPolicy.Reject, "Copy selected content.");
        ProjectionFixture fixture = ScratchFixture(
            [content, Publish("place-first", 20, new ByteRange(1, 4), new ByteRange(0, 4)),
             Publish("place-second", 30, new ByteRange(1, 4), new ByteRange(12, 4))]);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal([("content", new ByteRange(0, 4), (ByteRange?)new ByteRange(3, 4)),
                      ("content", new ByteRange(12, 4), (ByteRange?)new ByteRange(3, 4))],
            layout.OutputWrites.Values.Select(static write => (write.OperationId, write.OutputRange.Range, write.SourceRange)));
        Assert.All(layout.OutputWrites.Values, static write => Assert.Equal("dp-input", write.SourceSlotId));
        Assert.Equal(fixture.Composition.Plan.OrderedOperations, layout.Steps.Values);
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>Partial transports translate both target and selected-input offsets without dropping any declared byte.</summary>
    [Fact]
    public void SplitPlacementPreservesTheCorrespondingInputSlices()
    {
        CompositionOperation content = CompositionOperation.CopyRange("content", 10, "dp-input", new ByteRange(3, 4),
            "scratch", new ByteRange(2, 4), OverlapPolicy.Reject, "Copy selected content.");
        ProjectionFixture fixture = ScratchFixture(
            [content, Publish("place-left", 20, new ByteRange(2, 2), new ByteRange(4, 2)),
             Publish("place-right", 30, new ByteRange(4, 2), new ByteRange(12, 2))]);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal([(new ByteRange(4, 2), (ByteRange?)new ByteRange(3, 2)),
                      (new ByteRange(12, 2), (ByteRange?)new ByteRange(5, 2))],
            layout.OutputWrites.Values.Select(static write => (write.OutputRange.Range, write.SourceRange)));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>Projection follows ordered multi-buffer copies rather than assuming a single bank offset.</summary>
    [Fact]
    public void MultipleScratchHopsUseEachDeclaredCopyOffset()
    {
        CompositionPlan plan = new(
            [ImageInitialization.Blank("output-image", Capacity, 0), ImageInitialization.Blank("scratch", 8, 0),
             ImageInitialization.Blank("second", 8, 0)], "output-image",
            [.. MergePlan().AddressSpaces, new AddressSpace("scratch", 8, AddressSpaceMutability.Mutable),
             new AddressSpace("second", 8, AddressSpaceMutability.Mutable)],
            [CompositionOperation.FillRange("content", 10, "scratch", new ByteRange(1, 2), 0xA5, OverlapPolicy.Reject, "Fill content."),
             CompositionOperation.CopyRange("hop", 20, "scratch", new ByteRange(1, 2), "second", new ByteRange(5, 2), OverlapPolicy.Reject, "Move content."),
             CompositionOperation.CopyRange("publish", 30, "second", new ByteRange(4, 4), "output-image", new ByteRange(10, 4), OverlapPolicy.Reject, "Publish content.")]);
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge, plan);

        MemoryLayoutSnapshot layout = Project(fixture);

        MemoryLayoutOutputWrite write = Assert.Single(layout.OutputWrites.Values);
        Assert.Equal(("content", new ByteRange(11, 2)), (write.OperationId, write.OutputRange.Range));
        Assert.Null(write.SourceSlotId);
        Assert.Null(write.SourceRange);
        AssertOutputWriteInvariants(layout, plan);
    }

    /// <summary>A direct write cannot hide a scratch declaration with no complete output placement.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnprovenScratchWriteMakesTheWholeFactSetUnavailable(bool partialPlacement)
    {
        CompositionOperation fill = CompositionOperation.FillRange("unplaced", 10, "scratch", new ByteRange(2, 4),
            0xA5, OverlapPolicy.Reject, "Fill content.");
        CompositionOperation[] operations = partialPlacement
            ? [MergePlan().OrderedOperations[0], fill, Publish("partial", 200, new ByteRange(2, 2), new ByteRange(12, 2))]
            : [MergePlan().OrderedOperations[0], fill];
        ProjectionFixture fixture = ScratchFixture(operations);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.OutputWrites.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, layout.OutputWrites.UnavailableReason);
        Assert.Empty(layout.OutputWrites.Values);
        Assert.Equal(fixture.Composition.Plan.OrderedOperations, layout.Steps.Values);
    }

    /// <summary>A copy before the content write cannot prove that write's final location.</summary>
    [Fact]
    public void EarlierTransportDoesNotLocateALaterScratchWrite()
    {
        ProjectionFixture fixture = ScratchFixture(
            [Publish("too-early", 10, new ByteRange(0, 4), new ByteRange(0, 4)),
             CompositionOperation.FillRange("late-content", 20, "scratch", new ByteRange(0, 4), 0xA5, OverlapPolicy.Reject, "Fill later.")]);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, layout.OutputWrites.UnavailableReason);
        Assert.Empty(layout.OutputWrites.Values);
    }

    /// <summary>Reusing scratch bytes for a new placement must not attribute the old content to the new output.</summary>
    [Fact]
    public void ScratchReuseDoesNotPublishAnEarlierWriteIntoTheLaterPlacement()
    {
        ProjectionFixture fixture = ScratchFixture(
            [CompositionOperation.FillRange("first", 10, "scratch", new ByteRange(0, 2), 0xA5, OverlapPolicy.Reject, "First content."),
             Publish("publish-first", 20, new ByteRange(0, 2), new ByteRange(0, 2)),
             CompositionOperation.CopyRange("second", 30, "dp-input", new ByteRange(4, 2), "scratch", new ByteRange(0, 2),
                 OverlapPolicy.ReplaceExisting, "Reuse the workspace."),
             Publish("publish-second", 40, new ByteRange(0, 2), new ByteRange(12, 2))]);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal([("first", new ByteRange(0, 2)), ("second", new ByteRange(12, 2))],
            layout.OutputWrites.Values.Select(static write => (write.OperationId, write.OutputRange.Range)));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>Processors publish only declared writes, and neither processors nor patches inherit an input source.</summary>
    [Fact]
    public void ScratchProcessorAndPatchPublishOnlyTheirDeclaredRangesWithoutSources()
    {
        ByteRange staged = new(0, 8);
        ExternalProcessorInvocation invocation = new("processor", "tool", [staged], [new ByteRange(0, 1), new ByteRange(6, 1)]);
        ProjectionFixture fixture = ScratchFixture(
            [CompositionOperation.FillRange("fill", 10, "scratch", staged, 0xA5, OverlapPolicy.Reject, "Fill content."),
             CompositionOperation.PatchScalar("patch", 20, "scratch", new ByteRange(2, 1), [0x7E], OverlapPolicy.ReplaceExisting, "Patch content."),
             CompositionOperation.RunExternalProcessor("processor", 30, "scratch", staged, invocation, OverlapPolicy.ReplaceExisting, "Process content."),
             Publish("publish", 40, staged, new ByteRange(4, 8))]);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal([("fill", new ByteRange(4, 8)), ("patch", new ByteRange(6, 1)),
                      ("processor", new ByteRange(4, 1)), ("processor", new ByteRange(10, 1))],
            layout.OutputWrites.Values.Select(static write => (write.OperationId, write.OutputRange.Range)));
        Assert.All(layout.OutputWrites.Values, static write =>
        {
            Assert.Null(write.SourceSlotId);
            Assert.Null(write.SourceRange);
        });
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>An input-cloned scratch image keeps its selected source around a scalar edit, which stays a separate write.</summary>
    [Fact]
    public void InputCloneCopyRetainsItsSelectedSourceAndSeparatePatchWrite()
    {
        CompositionOperation patch = CompositionOperation.PatchScalar("patch", 10, "scratch", new ByteRange(3, 1),
            [0x7E], OverlapPolicy.Reject, "Patch the selected image.");
        ProjectionFixture fixture = ScratchFixture(
            [CompositionOperation.FillRange("base", 0, "output-image", new ByteRange(8, 4), 0xFF, OverlapPolicy.Reject, "Initialize output content."),
             patch, CompositionOperation.CopyRange("copy-input-clone", 20, "scratch", new ByteRange(2, 4), "output-image",
                 new ByteRange(8, 4), OverlapPolicy.ReplaceExisting, "Overlay the selected image.")],
            ImageInitialization.Reference("scratch", "dp-input", Capacity));

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal([("base", new ByteRange(8, 4), null, null), ("patch", new ByteRange(9, 1), null, null),
                      ("copy-input-clone", new ByteRange(8, 1), "dp-input", (ByteRange?)new ByteRange(2, 1)),
                      ("copy-input-clone", new ByteRange(10, 2), "dp-input", (ByteRange?)new ByteRange(4, 2))],
            layout.OutputWrites.Values.Select(static write => (write.OperationId, write.OutputRange.Range, write.SourceSlotId, write.SourceRange)));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>A partly overlaid clone copy still carries every untouched selected-input slice.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialInputCloneOverlayRetainsTheOtherSelectedSourceSlices(bool copyOverlay)
    {
        CompositionOperation overlay = copyOverlay
            ? CompositionOperation.CopyRange("overlay", 10, "tp-input", new ByteRange(9, 1), "scratch", new ByteRange(4, 1),
                OverlapPolicy.Reject, "Overlay one selected input byte.")
            : CompositionOperation.FillRange("overlay", 10, "scratch", new ByteRange(4, 1), 0xA5, OverlapPolicy.Reject, "Overlay one fill byte.");
        ProjectionFixture fixture = ScratchFixture(
            [CompositionOperation.FillRange("base", 0, "output-image", new ByteRange(8, 6), 0xFF, OverlapPolicy.Reject, "Initialize output content."),
             overlay, CompositionOperation.CopyRange("copy-clone", 20, "scratch", new ByteRange(2, 6), "output-image",
                 new ByteRange(8, 6), OverlapPolicy.ReplaceExisting, "Copy the partly overlaid input image.")],
            ImageInitialization.Reference("scratch", "dp-input", Capacity));

        MemoryLayoutSnapshot layout = Project(fixture);

        string? overlaySlot = copyOverlay ? "tp-input" : null;
        ByteRange? overlaySource = copyOverlay ? new ByteRange(9, 1) : null;
        Assert.Equal([("base", new ByteRange(8, 6), null, null), ("overlay", new ByteRange(10, 1), overlaySlot, overlaySource),
                      ("copy-clone", new ByteRange(8, 2), "dp-input", (ByteRange?)new ByteRange(2, 2)),
                      ("copy-clone", new ByteRange(11, 3), "dp-input", (ByteRange?)new ByteRange(5, 3))],
            layout.OutputWrites.Values.Select(static write => (write.OperationId, write.OutputRange.Range, write.SourceSlotId, write.SourceRange)));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>A scratch byte overwritten before publish is consumed, so the other writes stay known.</summary>
    [Fact]
    public void ScratchByteOverwrittenBeforePublishKeepsTheWritesKnown()
    {
        ByteRange staged = new(0, 8);
        ProjectionFixture fixture = ScratchFixture(
            [CompositionOperation.FillRange("fill", 10, "scratch", staged, 0xA5, OverlapPolicy.Reject, "Fill content."),
             CompositionOperation.CopyRange("overlay", 20, "tp-input", new ByteRange(9, 1), "scratch", new ByteRange(4, 1),
                 OverlapPolicy.ReplaceExisting, "Overlay one selected input byte."),
             Publish("publish", 30, staged, new ByteRange(4, 8))]);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.OutputWrites.Status);
        Assert.Equal([("fill", new ByteRange(4, 4), null, null), ("fill", new ByteRange(9, 3), null, null),
                      ("overlay", new ByteRange(8, 1), "tp-input", (ByteRange?)new ByteRange(9, 1))],
            layout.OutputWrites.Values.Select(static write => (write.OperationId, write.OutputRange.Range, write.SourceSlotId, write.SourceRange)));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>A copy from a clone whose immutable reference is not a bound input slot makes the set unresolved.</summary>
    [Fact]
    public void CloneOfAnUnboundSpaceMakesTheOutputWritesUnresolved()
    {
        CompositionPlan plan = new(
            [ImageInitialization.Blank("output-image", Capacity, 0),
             ImageInitialization.Reference("scratch", "unbound-reference", Capacity)],
            "output-image",
            [.. MergePlan().AddressSpaces,
             new AddressSpace("unbound-reference", Capacity, AddressSpaceMutability.Immutable),
             new AddressSpace("scratch", Capacity, AddressSpaceMutability.Mutable)],
            [CompositionOperation.CopyRange("copy-clone", 10, "scratch", new ByteRange(0, 4), "output-image",
                 new ByteRange(8, 4), OverlapPolicy.ReplaceExisting, "Copy a clone of an unbound space.")]);
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge, plan);

        MemoryLayoutSnapshot layout = Project(fixture);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.OutputWrites.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, layout.OutputWrites.UnavailableReason);
    }

    /// <summary>No overlay is unavailable; a supplied zero-operation plan is known empty.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutputWritesDistinguishMissingOverlayFromAnEmptyPlan(bool withOverlay)
    {
        CompositionPlan original = MergePlan();
        CompositionPlan plan = new(original.Initializations, original.OutputSpaceId, original.AddressSpaces, []);
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge, plan);
        ActiveSessionSnapshot session = Session(fixture);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, withOverlay ? fixture.Composition : null);

        Assert.Equal(withOverlay ? MemoryLayoutFactStatus.KnownEmpty : MemoryLayoutFactStatus.Unavailable, layout.OutputWrites.Status);
        Assert.Equal(withOverlay ? MemoryLayoutFactUnavailableReason.None : MemoryLayoutFactUnavailableReason.CompiledPlanNotResolved,
            layout.OutputWrites.UnavailableReason);
        Assert.Empty(layout.OutputWrites.Values);
    }

    private static ProjectionFixture ScratchFixture(CompositionOperation[] operations, ImageInitialization? scratch = null)
    {
        CompositionPlan plan = new(
            [ImageInitialization.Blank("output-image", Capacity, 0), scratch ?? ImageInitialization.Blank("scratch", 8, 0)],
            "output-image", [.. MergePlan().AddressSpaces, new AddressSpace("scratch", scratch?.Capacity ?? 8, AddressSpaceMutability.Mutable)], operations);
        return CreateFixture(CompositionKind.Merge, plan);
    }

    private static CompositionOperation Publish(string id, int sequence, ByteRange source, ByteRange target)
    {
        return CompositionOperation.CopyRange(id, sequence, "scratch", source, "output-image", target, OverlapPolicy.Reject, "Publish scratch content.");
    }

    private static MemoryLayoutSnapshot Project(ProjectionFixture fixture)
    {
        return MemoryLayoutProjector.Project(fixture.Capability, Session(fixture), fixture.Composition);
    }

    private static ActiveSessionSnapshot Session(ProjectionFixture fixture)
    {
        return fixture.Composition.V2Details.CompositionKind == CompositionKind.Merge
            ? CreateSession(fixture, Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity), Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity))
            : CreateSession(fixture, Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity), Slot("dp-replacement", AuthoringSlotLifecycle.Verified, Capacity));
    }
}
