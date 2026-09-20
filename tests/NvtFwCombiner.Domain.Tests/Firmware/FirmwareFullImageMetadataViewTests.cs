using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Pure canonical full-image metadata declaration invariants.</summary>
public sealed class FirmwareFullImageMetadataViewTests
{
    /// <summary>Snapshots are immutable and preserve map, structure, definition, and artifact identities.</summary>
    [Fact]
    public void ViewSnapshotsCanonicalReferences()
    {
        FirmwareMetadataStructure structure = Structure("config");
        (FirmwareImageMap map, FirmwareMetadataSet set) = Map(structure);
        FirmwareMetadataReferenceTarget[] targets = [new(FirmwareMetadataReferenceTargetKind.Field, "value")];
        string[] members = ["NT00001"];
        FirmwareFullImageMetadataBinding[] bindings = [new("config", structure, targets, ["evidence"])];
        var view = new FirmwareFullImageMetadataView("full", map, members, bindings, ["evidence"]);
        FirmwareFullImageMetadataView[] views = [view];
        FirmwareFamilyResolutionDefinition family = Family(map, set, views);
        members[0] = "NT99999";
        targets[0] = new(FirmwareMetadataReferenceTargetKind.Field, "changed");
        bindings[0] = new("other", structure, [new(FirmwareMetadataReferenceTargetKind.Field, "value")], ["evidence"]);
        views[0] = new("empty", map, ["NT00001"], [], ["evidence"]);
        Assert.Same(view, Assert.Single(family.FullImageMetadataViews!));
        Assert.Same(map, view.ImageMap);
        Assert.Same(structure, Assert.Single(view.MetadataBindings).Structure);
        Assert.Equal("canonical-artifact", view.MetadataBindings[0].Structure.ArtifactBindingId);
        Assert.Equal("value", Assert.Single(view.MetadataBindings[0].TargetReferences).TargetId);
        Assert.Equal("NT00001", Assert.Single(view.MemberIds));
        Assert.True(Assert.IsType<IList<FirmwareFullImageMetadataView>>(family.FullImageMetadataViews, exactMatch: false).IsReadOnly);
        Assert.True(Assert.IsType<IList<FirmwareFullImageMetadataBinding>>(view.MetadataBindings, exactMatch: false).IsReadOnly);
        Assert.True(Assert.IsType<IList<FirmwareMetadataReferenceTarget>>(view.MetadataBindings[0].TargetReferences, exactMatch: false).IsReadOnly);
    }

    /// <summary>Every selected dependency edge must remain inside the view, including transitive edges.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ViewRequiresCompletePrerequisiteClosure(bool complete)
    {
        FirmwareMetadataStructure root = Structure("root");
        FirmwareMetadataStructure middle = Structure("middle", "root");
        FirmwareMetadataStructure leaf = Structure("leaf", "middle");
        (FirmwareImageMap map, FirmwareMetadataSet set) = Map(root, middle, leaf);
        var view = new FirmwareFullImageMetadataView("full", map, ["NT00001"],
            (complete ? new[] { root, middle, leaf } : [middle, leaf]).Select(Binding), ["evidence"]);
        if (complete)
        {
            Assert.Equal(3, Assert.Single(Family(map, set, [view]).FullImageMetadataViews!).MetadataBindings.Count);
        }
        else
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => Family(map, set, [view]));
            Assert.Contains("prerequisite", error.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>Existing canonical graph validation rejects cycles even when all view ids are present.</summary>
    [Fact]
    public void ViewCannotBypassCanonicalCycleValidation()
    {
        FirmwareMetadataStructure first = Structure("first", "second");
        FirmwareMetadataStructure second = Structure("second", "first");
        (FirmwareImageMap map, FirmwareMetadataSet set) = Map(first, second);
        var view = new FirmwareFullImageMetadataView("full", map, ["NT00001"], [Binding(first), Binding(second)], ["evidence"]);
        Assert.Contains("cycle", Assert.Throws<ArgumentException>(() => Family(map, set, [view])).Message, StringComparison.Ordinal);
    }

    /// <summary>A structurally equivalent map or metadata structure is not a canonical reference.</summary>
    [Theory]
    [InlineData("map")]
    [InlineData("structure")]
    [InlineData("member")]
    public void ViewRejectsNoncanonicalReferences(string mutation)
    {
        FirmwareMetadataStructure structure = Structure("config");
        (FirmwareImageMap map, FirmwareMetadataSet set) = Map(structure);
        if (mutation == "map")
        {
            (FirmwareImageMap other, _) = Map(structure);
            var view = new FirmwareFullImageMetadataView("full", other, ["NT00001"], [Binding(structure)], ["evidence"]);
            _ = Assert.Throws<ArgumentException>(() => Family(map, set, [view]));
        }
        else
        {
            _ = Assert.Throws<ArgumentException>(() => new FirmwareFullImageMetadataView("full", map,
                mutation == "member" ? ["NT99999"] : ["NT00001"],
                [Binding(mutation == "structure" ? Structure("config") : structure)], ["evidence"]));
        }
    }

    /// <summary>Map predicates are separate selection facts and do not invent view binding requirements.</summary>
    [Fact]
    public void EmptyViewRemainsExplicitWithoutEvaluatingMapPredicates()
    {
        FirmwareMetadataStructure structure = Structure("config");
        (FirmwareImageMap map, FirmwareMetadataSet set) = Map(structure);
        Assert.Null(Family(map, set, null).FullImageMetadataViews);
        Assert.Empty(Family(map, set, []).FullImageMetadataViews!);
        var view = new FirmwareFullImageMetadataView("empty", map, ["NT00001"], [], ["evidence"]);
        Assert.Empty(Assert.Single(Family(map, set, [view]).FullImageMetadataViews!).MetadataBindings);
    }

    /// <summary>A nonempty view need not expose unrelated metadata used only for map selection.</summary>
    [Fact]
    public void ViewDoesNotAddUnrelatedMapPredicateTargets()
    {
        FirmwareMetadataStructure selector = Structure("selector");
        FirmwareMetadataStructure display = Structure("display");
        (FirmwareImageMap map, FirmwareMetadataSet set) = Map(selector, display);
        var view = new FirmwareFullImageMetadataView("full", map, ["NT00001"], [Binding(display)], ["evidence"]);
        Assert.Same(display, Assert.Single(Assert.Single(Family(map, set, [view]).FullImageMetadataViews!).MetadataBindings).Structure);
    }

    private static FirmwareFullImageMetadataBinding Binding(FirmwareMetadataStructure structure)
    {
        return new(structure.StructureId, structure, [new(FirmwareMetadataReferenceTargetKind.Field, "value")], ["evidence"]);
    }

    private static FirmwareFamilyResolutionDefinition Family(FirmwareImageMap map, FirmwareMetadataSet set,
        IEnumerable<FirmwareFullImageMetadataView>? views)
    {
        return new("synthetic", "1.0.0", new string('a', 64), [map], [set], [], [], fullImageMetadataViews: views);
    }

    private static FirmwareMetadataStructure Structure(string id, string? prerequisite = null)
    {
        var range = new FirmwareAddressedRange("flash", new ByteRange(0, 4));
        FirmwareMetadataLocator locator = prerequisite is null
            ? new FirmwareAbsoluteRangeLocator(range, "image")
            : new FirmwareMetadataFieldSelectedLocator(prerequisite, "value",
                [new FirmwareMetadataFieldSelectedBranch(0, 255, range)], 0, "image");
        return new(id, "canonical-artifact", 4, locator,
            [new FirmwareMetadataField("value", 0, 1, FirmwareMetadataEncoding.UnsignedInteger, FirmwareMetadataByteOrder.LittleEndian)], []);
    }

    private static (FirmwareImageMap Map, FirmwareMetadataSet Set) Map(params FirmwareMetadataStructure[] structures)
    {
        var set = new FirmwareMetadataSet("metadata", structures, ["evidence"]);
        var regions = new FirmwareRegionSet("regions", "flash",
            [new FirmwareRegion("image", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image, new ByteRange(0, 16), FirmwareWriteConstraint.Forbidden)], ["evidence"]);
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect("map", "flash",
            new FirmwareMapApplicability(["NT00001"], ["standard"], TopologyRequirement.NoTopologyConstraint(), 16,
                metadataPredicates: [new FirmwareMetadataPredicate(structures[0].StructureId, "value",
                    FirmwareMetadataPredicateOperator.Equal, [FirmwareMetadataValue.FromUnsignedInteger(1)])]),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps, [regions], [set], ["evidence"]);
        return (map, set);
    }
}
