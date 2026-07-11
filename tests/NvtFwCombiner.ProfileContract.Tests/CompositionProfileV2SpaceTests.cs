using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable v2 spaces, views, metadata bindings, and access policy.</summary>
public sealed class CompositionProfileV2SpaceTests
{
    /// <summary>Verifies closed capacity and initializer values retain exact typed data.</summary>
    [Fact]
    public void CapacityAndInitializerValuesAreClosed()
    {
        var resolved = new ResolvedMapProfileCapacity();
        var fixedCapacity = new FixedProfileCapacity(4096);
        var blank = new BlankProfileInitializer(0xFF);
        var clone = new CloneProfileInitializer("reference-input");

        Assert.Equal(CompositionProfileCapacityKind.ResolvedMap, resolved.Kind);
        Assert.Equal(4096, fixedCapacity.Bytes);
        Assert.Equal(0xFF, blank.FillByte);
        Assert.Equal("reference-input", clone.SourceSlotId);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FixedProfileCapacity(0));
        _ = Assert.Throws<ArgumentException>(() => new CloneProfileInitializer("Reference"));
    }

    /// <summary>Verifies input and mutable spaces cannot enter one another's state shape.</summary>
    [Fact]
    public void SpaceShapesSeparateImmutableInputsFromMutableBuffers()
    {
        var input = new InputArtifactProfileSpace(
            "source",
            "source-input",
            CompositionProfileInstancePolicy.PerBinding);
        var work = new MutableCompositionProfileSpace(
            "work",
            CompositionProfileSpaceKind.WorkBuffer,
            new FixedProfileCapacity(64),
            new BlankProfileInitializer(0));
        var output = new MutableCompositionProfileSpace(
            "output",
            CompositionProfileSpaceKind.OutputImage,
            new ResolvedMapProfileCapacity(),
            new CloneProfileInitializer("reference-input"));

        Assert.Equal(CompositionProfileSpaceKind.InputArtifact, input.Kind);
        Assert.Equal(CompositionProfileInstancePolicy.PerBinding, input.InstancePolicy);
        Assert.Equal(CompositionProfileSpaceKind.WorkBuffer, work.Kind);
        Assert.Equal(CompositionProfileInitializerKind.Blank, work.Initializer.Kind);
        Assert.Equal(CompositionProfileSpaceKind.OutputImage, output.Kind);
        Assert.Equal(CompositionProfileCapacityKind.ResolvedMap, output.Capacity.Kind);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new MutableCompositionProfileSpace(
            "invalid",
            CompositionProfileSpaceKind.InputArtifact,
            new ResolvedMapProfileCapacity(),
            new BlankProfileInitializer(0)));
    }

    /// <summary>Verifies view selector ranges remain checked and relative to their declared basis.</summary>
    [Fact]
    public void ViewSelectorsRetainCheckedRangeBasis()
    {
        var region = new MapRegionViewSelector("dp-code");
        var slice = new MapRegionSliceViewSelector("dp-code", new ByteRange(4, 8));
        var range = new SpaceRangeViewSelector(new ByteRange(12, 4));
        var view = new CompositionProfileView("source-view", "source", slice);

        Assert.Equal(CompositionProfileViewSelectorKind.MapRegion, region.Kind);
        Assert.Equal(new ByteRange(4, 8), slice.RelativeRange);
        Assert.Equal(new ByteRange(12, 4), range.Range);
        Assert.Equal("source", view.SpaceId);
        Assert.Same(slice, view.Selector);
        _ = Assert.Throws<ArgumentException>(() => new MapRegionViewSelector("DP-Code"));
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileView("source_view", "source", range));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new MapRegionSliceViewSelector(
            "dp-code",
            default));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SpaceRangeViewSelector(default));
    }

    /// <summary>Verifies metadata binding fields and purposes are immutable deterministic sets.</summary>
    [Fact]
    public void MetadataBindingSnapshotsFieldsAndPurposes()
    {
        var fieldIds = new List<string> { "version-minor", "version-major" };
        var purposes = new List<CompositionProfileMetadataPurpose>
        {
            CompositionProfileMetadataPurpose.Version,
            CompositionProfileMetadataPurpose.Validation,
        };
        var binding = new CompositionProfileMetadataBinding(
            "cmd-version",
            "dp-source",
            "cmd",
            fieldIds,
            purposes);
        fieldIds.Clear();
        purposes.Clear();

        Assert.Equal(["version-major", "version-minor"], binding.FieldIds);
        Assert.Equal(
            [CompositionProfileMetadataPurpose.Validation, CompositionProfileMetadataPurpose.Version],
            binding.Purposes);
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileMetadataBinding(
            "cmd-version",
            "dp-source",
            "cmd",
            [],
            [CompositionProfileMetadataPurpose.Validation]));
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileMetadataBinding(
            "cmd-version",
            "dp-source",
            "cmd",
            ["version-major"],
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileMetadataBinding(
            "cmd-version",
            "dp-source",
            "cmd",
            ["version-major"],
            [CompositionProfileMetadataPurpose.Validation, CompositionProfileMetadataPurpose.Validation]));
    }

    /// <summary>Verifies parts access alone owns an immutable non-empty subregion set.</summary>
    [Fact]
    public void RegionAccessKeepsPartsExplicitAndOtherModesClosed()
    {
        var subregions = new List<string> { "part-b", "part-a" };
        var parts = new CompositionProfileRegionAccess(
            "dp-code",
            RegionAccessKind.Parts,
            "Only declared DP partitions are authorable.",
            subregions);
        subregions.Clear();
        var readOnly = new CompositionProfileRegionAccess(
            "header",
            RegionAccessKind.ReadOnly,
            "Header is visible evidence only.");

        Assert.Equal(["part-a", "part-b"], parts.AllowedSubregionIds);
        Assert.Empty(readOnly.AllowedSubregionIds);
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileRegionAccess(
            "dp-code",
            RegionAccessKind.Parts,
            "reason"));
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileRegionAccess(
            "dp-code",
            RegionAccessKind.Whole,
            "reason",
            ["part-a"]));
    }

    /// <summary>Verifies enum carriers and null semantic values fail before root graph validation.</summary>
    [Fact]
    public void SpaceValuesRejectUnknownEnumsAndNulls()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new InputArtifactProfileSpace(
            "source",
            "source-input",
            (CompositionProfileInstancePolicy)99));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new MutableCompositionProfileSpace(
            "output",
            (CompositionProfileSpaceKind)99,
            new ResolvedMapProfileCapacity(),
            new BlankProfileInitializer(0)));
        _ = Assert.Throws<ArgumentNullException>(() => new MutableCompositionProfileSpace(
            "output",
            CompositionProfileSpaceKind.OutputImage,
            null!,
            new BlankProfileInitializer(0)));
        _ = Assert.Throws<ArgumentNullException>(() => new CompositionProfileView(
            "view",
            "source",
            null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompositionProfileRegionAccess(
            "dp-code",
            (RegionAccessKind)99,
            "reason"));
    }
}
