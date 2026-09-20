using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Slots identify selected inputs; only the accepted path and stamp identify the BIN.</summary>
    [Theory]
    [InlineData("shared.bin", "shared.bin", 'a', 16, true)]
    [InlineData("shared.bin", "other.bin", 'a', 16, false)]
    [InlineData("shared.bin", "shared.bin", 'b', 16, false)]
    [InlineData("shared.bin", "shared.bin", 'a', 15, false)]
    [InlineData("shared.bin", "folder/../shared.bin", 'a', 16, true)]
    public void ContentIdentityUsesAcceptedArtifactInsteadOfSlotOrHash(
        string firstPath, string secondPath, char secondHash, long secondLength, bool sameArtifact)
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot session = CreateSession(fixture,
            AcceptedContentSlot("dp-input", firstPath, 'a'),
            new AuthoringSlotState("tp-input", secondPath, new FileStamp(secondLength, new string(secondHash, 64)),
                AuthoringSlotLifecycle.Warning));
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);
        MemoryLayoutContentSource first = Assert.IsType<MemoryLayoutContentSource>(layout.AfterSegments[0].ContentSource);
        MemoryLayoutContentSource second = Assert.IsType<MemoryLayoutContentSource>(layout.AfterSegments[^1].ContentSource);
        Assert.Equal(sameArtifact, first.ArtifactIdentity == second.ArtifactIdentity);
        Assert.Equal("dp-input", first.SourceSlotId);
        Assert.Equal("tp-input", second.SourceSlotId);
        Assert.Equal("dp-input", layout.AfterSegments[0].SourceSpaceId);
        Assert.All(layout.AfterSegments.Where(segment => segment.ContributingOperations.Count == 0),
            segment => Assert.Null(segment.ContentSource));
    }

    /// <summary>Case follows the same host path comparison as accepted execution inputs.</summary>
    [Fact]
    public void ContentIdentityUsesHostPathCaseComparison()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability,
            CreateSession(fixture, AcceptedContentSlot("dp-input", "shared.bin", 'a'),
                AcceptedContentSlot("tp-input", "SHARED.bin", 'a')), fixture.Composition);
        Assert.Equal(OperatingSystem.IsWindows(),
            layout.AfterSegments[0].ContentSource!.ArtifactIdentity == layout.AfterSegments[^1].ContentSource!.ArtifactIdentity);
    }

    /// <summary>Unaccepted inputs never gain grouping identity, while Replace initialization retains its BIN.</summary>
    [Fact]
    public void ReferenceIdentityIsRetainedAndUnacceptedInputsHaveNoIdentity()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Replace);
        ActiveSessionSnapshot session = CreateSession(fixture,
            AcceptedContentSlot("reference-base", "reference.bin", 'a'),
            Slot("dp-replacement", AuthoringSlotLifecycle.Empty));
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);
        Assert.Null(layout.AfterSegments[0].ContentSource);
        MemoryLayoutContentSource reference = Assert.IsType<MemoryLayoutContentSource>(layout.BeforeSegments[0].ContentSource);
        Assert.All(layout.BeforeSegments.Concat(layout.AfterSegments.Skip(1)), segment =>
        {
            Assert.Equal(reference, segment.ContentSource);
            Assert.Equal("reference-base", segment.SourceSlotId);
            Assert.Equal(MemoryWorkflowDisposition.Kept, segment.Disposition);
        });
    }

    private static AuthoringSlotState AcceptedContentSlot(string slotId, string path, char hash)
    {
        return new(slotId, path, new FileStamp(Capacity, new string(hash, 64)), AuthoringSlotLifecycle.Verified);
    }

    /// <summary>Only a proven processor round trip keeps TP ownership; exact writer facts never change.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("no-processor", false)]
    [InlineData("wrong-offset", false)]
    [InlineData("changed-output", false)]
    [InlineData("changed-work", false)]
    [InlineData("no-owner", false)]
    [InlineData("other-workflow", false)]
    [InlineData("partial-processor", false)]
    public void StagedImportRequiresUninterruptedProcessorRoundTrip(string scenario, bool inherits)
    {
        var output = new ByteRange(12, 4);
        var work = new ByteRange(0, 4);
        var imported = new ByteRange(12, 2);
        List<CompositionOperation> operations =
        [
            scenario == "no-owner"
                ? CompositionOperation.FillRange("base", 10, "output-image", output, 0, OverlapPolicy.Reject, "unowned output")
                : CompositionOperation.CopyRange("base", 10, "tp-input", output, "output-image", output, OverlapPolicy.Reject, "TP content"),
            CompositionOperation.CopyRange("stage", 20, "output-image", output, "work", work, OverlapPolicy.Reject, "stage output"),
        ];
        if (scenario != "no-processor")
        {
            operations.Add(CompositionOperation.RunExternalProcessor("process", 30, "work", work,
                new ExternalProcessorInvocation("processor", "binding", [work],
                    [new ByteRange(0, scenario == "partial-processor" ? 1 : 2)]),
                OverlapPolicy.ReplaceExisting, "declared processing"));
        }

        if (scenario is "changed-output" or "changed-work")
        {
            operations.Add(CompositionOperation.CopyRange("intervening", 40, "dp-input", new ByteRange(0, 2),
                scenario == "changed-output" ? "output-image" : "work",
                scenario == "changed-output" ? imported : new ByteRange(0, 2),
                OverlapPolicy.ReplaceExisting, "different content"));
        }

        operations.Add(CompositionOperation.CopyRange("import", 50, "work",
            new ByteRange(scenario == "wrong-offset" ? 1 : 0, 2), "output-image", imported,
            OverlapPolicy.ReplaceExisting, "import processed bytes"));
        var plan = new CompositionPlan(
            [ImageInitialization.Blank("output-image", Capacity, 0), ImageInitialization.Blank("work", Capacity, 0)],
            "output-image",
            [new("dp-input", Capacity, AddressSpaceMutability.Immutable), new("tp-input", Capacity, AddressSpaceMutability.Immutable),
             new("output-image", Capacity, AddressSpaceMutability.Mutable), new("work", Capacity, AddressSpaceMutability.Mutable)],
            operations);
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge, customPlan: plan,
            customWorkflowId: scenario == "other-workflow" ? ExperienceIds.StandardMerge : ExperienceIds.AbMerge);
        ActiveSessionSnapshot session = CreateSession(fixture,
            AcceptedContentSlot("dp-input", "dp.bin", 'a'), AcceptedContentSlot("tp-input", "tp.bin", 'b'));
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);
        MemoryLayoutSegment patch = Assert.Single(layout.AfterSegments, segment => segment.Range == imported);
        Assert.Equal("work", patch.SourceSpaceId);
        Assert.Null(patch.SourceSlotId);
        Assert.Equal(scenario == "changed-output" ? ["base", "intervening", "import"] : ["base", "import"],
            patch.ContributingOperations.Select(operation => operation.OperationId));
        if (inherits)
        {
            Assert.Equal("tp-input", patch.ContentSource?.SourceSpaceId);
            Assert.Equal(layout.AfterSegments[^1].ContentSource, patch.ContentSource);
        }
        else if (scenario == "changed-work")
        {
            // A direct input overwrite has its own proven owner, never the old TP owner.
            Assert.Equal("dp-input", patch.ContentSource?.SourceSpaceId);
        }
        else
        {
            Assert.Null(patch.ContentSource);
        }
    }

    /// <summary>Input-cloned work buffers retain content identity through declared processing.</summary>
    [Fact]
    public void InputClonedWorkRetainsInputContentAndExactWorkWriter()
    {
        var range = new ByteRange(12, 4);
        var plan = new CompositionPlan(
            [ImageInitialization.Blank("output-image", Capacity, 0), ImageInitialization.Reference("work", "tp-input", Capacity)],
            "output-image",
            [new("dp-input", Capacity, AddressSpaceMutability.Immutable), new("tp-input", Capacity, AddressSpaceMutability.Immutable),
             new("output-image", Capacity, AddressSpaceMutability.Mutable), new("work", Capacity, AddressSpaceMutability.Mutable)],
            [CompositionOperation.RunExternalProcessor("process", 10, "work", new ByteRange(0, Capacity),
                new ExternalProcessorInvocation("processor", "binding", [range], [new ByteRange(12, 1)]), OverlapPolicy.Reject, "process clone"),
             CompositionOperation.CopyRange("import", 20, "work", range, "output-image", range, OverlapPolicy.Reject, "copy clone")]);
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge, customPlan: plan);
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability,
            CreateSession(fixture, AcceptedContentSlot("dp-input", "dp.bin", 'a'), AcceptedContentSlot("tp-input", "tp.bin", 'b')),
            fixture.Composition);
        MemoryLayoutSegment tp = layout.AfterSegments[^1];
        Assert.Equal("work", tp.SourceSpaceId);
        Assert.Equal("tp-input", tp.ContentSource?.SourceSpaceId);
        Assert.Equal("tp-input", tp.ContentSource?.SourceSlotId);
        Assert.Equal(["import"], tp.ContributingOperations.Select(operation => operation.OperationId));
    }
}
