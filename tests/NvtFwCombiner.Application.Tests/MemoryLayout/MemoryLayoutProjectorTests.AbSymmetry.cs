using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Even equal two-bank geometry cannot supply a missing profile declaration.</summary>
    [Fact]
    public void TwoEqualBankLocatorsDoNotImplyFullAbSymmetry()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge, customWorkflowId: ExperienceIds.AbMerge);
        ActiveSessionSnapshot session = CreateSession(fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Empty));
        MemoryLayoutSnapshot original = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);
        Assert.Null(fixture.Composition.V2Details.Ab);
        var layout = new MemoryLayoutSnapshot(fixture.Capability, session, fixture.ResolvedMap.ImageMap,
            original.Capacity, original.BeforeSegments, original.AfterSegments, original.PendingItems, [],
            [new("a-bank", original.AddressSpaceId, new ByteRange(0, Capacity / 2)),
             new("b-bank", original.AddressSpaceId, new ByteRange(Capacity / 2, Capacity / 2))]);
        Assert.Equal(2, layout.Banks.Count);
        Assert.False(layout.CanViewIndividualBanks);
    }
}
