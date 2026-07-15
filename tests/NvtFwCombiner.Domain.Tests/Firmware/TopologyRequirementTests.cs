using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests topology requirements independently from runtime selections.</summary>
public sealed class TopologyRequirementTests
{
    /// <summary>Verifies topology-independent facts do not require or filter a selection.</summary>
    [Fact]
    public void NoneMatchesMissingAndPresentSelections()
    {
        var requirement = TopologyRequirement.NoTopologyConstraint();

        Assert.Equal("none", requirement.CanonicalId);
        Assert.True(requirement.Matches(null));
        Assert.True(requirement.Matches(Selection(3, "cascade")));
    }

    /// <summary>Verifies single-chip facts require exactly one selected chip.</summary>
    [Fact]
    public void SingleMatchesOnlyOneChip()
    {
        var requirement = TopologyRequirement.RequireSingleChip();

        Assert.Equal("single", requirement.CanonicalId);
        Assert.False(requirement.Matches(null));
        Assert.True(requirement.Matches(Selection(1, "single")));
        Assert.False(requirement.Matches(Selection(2, "cascade")));
    }

    /// <summary>Verifies bounded cascade requirements enforce both inclusive limits.</summary>
    [Fact]
    public void CascadeMatchesInclusiveBounds()
    {
        var requirement = TopologyRequirement.RequireCascade(2, 3);

        Assert.Equal("cascade", requirement.CanonicalId);
        Assert.False(requirement.Matches(Selection(1, "single")));
        Assert.True(requirement.Matches(Selection(2, "cascade")));
        Assert.True(requirement.Matches(Selection(3, "cascade")));
        Assert.False(requirement.Matches(Selection(4, "cascade")));
    }

    /// <summary>Verifies an unbounded cascade has no inferred upper chip limit.</summary>
    [Fact]
    public void UnboundedCascadeAcceptsLargerCounts()
    {
        var requirement = TopologyRequirement.RequireCascade();

        Assert.True(requirement.Matches(Selection(2, "cascade")));
        Assert.True(requirement.Matches(Selection(32, "cascade")));
    }

    /// <summary>Verifies exact-count requirements distinguish two- and three-chip maps.</summary>
    [Fact]
    public void ExactCountMatchesOnlyDeclaredCount()
    {
        var requirement = TopologyRequirement.RequireExactCount(3);

        Assert.Equal("exact-count", requirement.CanonicalId);
        Assert.False(requirement.Matches(Selection(2, "2ic")));
        Assert.True(requirement.Matches(Selection(3, "3ic")));
    }

    /// <summary>Verifies invalid cascade bounds fail before map resolution.</summary>
    [Fact]
    public void CascadeRejectsInvalidBounds()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => TopologyRequirement.RequireCascade(1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => TopologyRequirement.RequireCascade(3, 2));
    }

    /// <summary>Verifies an exact-count requirement must be positive.</summary>
    [Fact]
    public void ExactCountRejectsNonPositiveCount()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => TopologyRequirement.RequireExactCount(0));
    }

    /// <summary>Verifies topology selection records exact metadata provenance.</summary>
    [Fact]
    public void SelectionPreservesMetadataSource()
    {
        var selection = new TopologySelection(
            2,
            "cascade",
            TopologySelectionSource.Derived,
            "firmware-config-chip-number");

        Assert.Equal(2, selection.ChipCount);
        Assert.Equal("cascade", selection.Label);
        Assert.Equal(TopologySelectionSource.Derived, selection.Source);
        Assert.Equal("firmware-config-chip-number", selection.SourceId);
    }

    /// <summary>Verifies requested topology retains its independent label and request provenance.</summary>
    [Fact]
    public void SelectionPreservesRequestedSource()
    {
        TopologySelection selection = Selection(1, "single");

        Assert.Equal("single", selection.Label);
        Assert.Equal(TopologySelectionSource.Requested, selection.Source);
        Assert.Equal("compile-request", selection.SourceId);
    }

    /// <summary>Verifies invalid selection counts, sources, labels, and source ids fail closed.</summary>
    [Fact]
    public void SelectionRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new TopologySelection(
            0,
            "single",
            TopologySelectionSource.Requested,
            "compile-request"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new TopologySelection(
            1,
            "single",
            (TopologySelectionSource)int.MaxValue,
            "compile-request"));
        _ = Assert.Throws<ArgumentException>(() => new TopologySelection(
            1,
            " ",
            TopologySelectionSource.Requested,
            "compile-request"));
        _ = Assert.Throws<ArgumentException>(() => new TopologySelection(
            1,
            "single",
            TopologySelectionSource.Requested,
            " "));
    }

    private static TopologySelection Selection(int chipCount, string label)
    {
        return new TopologySelection(
            chipCount,
            label,
            TopologySelectionSource.Requested,
            "compile-request");
    }
}
