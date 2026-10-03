using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorLogicalCoverageTests
{
    /// <summary>A DP field before its sibling code retains code identity, never the field's title.</summary>
    [Fact]
    public void DpFieldBeforeCodeUsesDeclaredSiblingOwnerIdentity()
    {
        IReadOnlyList<MemoryLayoutSectionLocator> sections = Sections(
            Region("field", "flash-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Command, 0, 1),
            Region("dp-code", "flash-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, 1, 7),
            Region("tp-code", "flash-image", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code, 8, 8));
        MemoryLayoutSectionLocator dp = Assert.Single(sections, section => section.ContentRole == MemoryContentRole.Dp);
        Assert.Equal("dp-code", dp.CanonicalRegion!.RegionId);
        Assert.Equal(new ByteRange(0, 8), dp.Range);
        Assert.Equal("field", Assert.Single(dp.Fields).CanonicalRegion.RegionId);
    }

    /// <summary>DP-owned parentage folds a command even when its adjacent declared region is TP.</summary>
    [Fact]
    public void DpParentFoldsFieldBesideTpAndDoesNotListDataPartitionsAsFields()
    {
        IReadOnlyList<MemoryLayoutSectionLocator> sections = Sections(
            Region("dp-image", "flash-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Image, 0, 12),
            Region("dp-code", "dp-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, 0, 4),
            Region("tp-code", "dp-image", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code, 4, 4),
            Region("field", "dp-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Command, 8, 1),
            Region("dp-data", "dp-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Data, 9, 3),
            Region("tp-tail", "flash-image", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code, 12, 4));
        MemoryLayoutSectionLocator dp = Assert.Single(sections, section => section.Range.Contains(new ByteRange(8, 1)));
        Assert.Equal("dp-image", dp.CanonicalRegion!.RegionId);
        Assert.Equal(MemoryContentRole.Tp, sections[1].ContentRole);
        MemoryLayoutSectionField field = Assert.Single(dp.Fields);
        Assert.Equal("field", field.CanonicalRegion.RegionId);
        Assert.Equal(new ByteRange(8, 1), field.Range);
        Assert.DoesNotContain(sections, section => section.CanonicalRegion?.RegionId == "field");
    }

    /// <summary>Adjacent DP sections declared under different parents are not the same section.</summary>
    [Fact]
    public void AdjacentDpSectionsKeepDistinctDeclaredParents()
    {
        IReadOnlyList<MemoryLayoutSectionLocator> sections = Sections(
            Region("dp-image", "flash-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Image, 0, 8),
            Region("other-image", "flash-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Image, 8, 8));
        Assert.Equal(["dp-image", "other-image"], sections.Select(section => section.CanonicalRegion!.RegionId));
        Assert.Equal([new ByteRange(0, 8), new ByteRange(8, 8)], sections.Select(section => section.Range));
    }

    /// <summary>Incomplete companion declarations retain neutral context until a DP owner is declared.</summary>
    [Fact]
    public void OrphanDpCommandDoesNotInventStandaloneDpSection()
    {
        IReadOnlyList<MemoryLayoutSectionLocator> sections = Sections(
            Region("tp-code", "flash-image", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code, 0, 8),
            Region("field", "flash-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Command, 8, 1),
            Region("context", "flash-image", FirmwareRegionOwner.System, FirmwareRegionKind.Data, 9, 7));
        Assert.Equal([MemoryContentRole.Tp, MemoryContentRole.General], sections.Select(section => section.ContentRole));
        Assert.Empty(sections[1].Fields);
    }

    /// <summary>A field nested inside a later code sibling follows the parent's final section identity.</summary>
    [Fact]
    public void NestedDpFieldUsesParentsFinalCoalescedIdentity()
    {
        IReadOnlyList<MemoryLayoutSectionLocator> sections = Sections(
            Region("dp-code", "flash-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, 0, 4),
            Region("dp-next", "flash-image", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, 4, 4),
            Region("before-field", "dp-next", FirmwareRegionOwner.Dp, FirmwareRegionKind.Data, 4, 1),
            Region("field", "dp-next", FirmwareRegionOwner.Dp, FirmwareRegionKind.Command, 5, 1),
            Region("after-field", "dp-next", FirmwareRegionOwner.Dp, FirmwareRegionKind.Data, 6, 2),
            Region("tp-code", "flash-image", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code, 8, 8));
        Assert.Equal("dp-code", sections[0].CanonicalRegion!.RegionId);
        Assert.Equal(new ByteRange(0, 8), sections[0].Range);
        Assert.Equal("field", Assert.Single(sections[0].Fields).CanonicalRegion.RegionId);
    }

    private static IReadOnlyList<MemoryLayoutSectionLocator> Sections(params FirmwareRegion[] children)
    {
        FirmwareImageMap map = CreateResolvedMap(ExperienceIds.CtrlRamReplace, ctrlRamMap: false, customRegions:
            [Region("flash-image", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image, 0, Capacity), .. children]).ImageMap;
        return MemoryLayoutProjector.ProjectMapSections(map, "flash", Capacity);
    }

    private static FirmwareRegion Region(string id, string? parent, FirmwareRegionOwner owner, FirmwareRegionKind kind, long start, long length)
    {
        return new(id, parent, owner, kind, new ByteRange(start, length), FirmwareWriteConstraint.ExplicitRange);
    }
}
