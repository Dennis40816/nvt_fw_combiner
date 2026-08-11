using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 profile spaces and logical views.</summary>
public sealed class CompositionProfileV2SpaceNormalizerTests
{
    /// <summary>Verifies immutable input spaces preserve both instance policies.</summary>
    [Fact]
    public void SpaceMapsInputArtifactInstancePolicies()
    {
        InputArtifactProfileSpace singleton = Assert.IsType<InputArtifactProfileSpace>(CompositionProfileNormalizer.NormalizeSpace(
            InputSpace("singleton")));
        InputArtifactProfileSpace perBinding = Assert.IsType<InputArtifactProfileSpace>(CompositionProfileNormalizer.NormalizeSpace(
            InputSpace("per-binding")));

        Assert.Equal(CompiledInputInstancePolicy.Singleton, singleton.InstancePolicy);
        Assert.Equal(CompiledInputInstancePolicy.PerBinding, perBinding.InstancePolicy);
        Assert.Equal("tp-input", singleton.SlotId);
    }

    /// <summary>Verifies mutable spaces map both capacity and initializer union shapes.</summary>
    [Fact]
    public void SpaceMapsMutableSpaceShapes()
    {
        MutableCompositionProfileSpace work = Assert.IsType<MutableCompositionProfileSpace>(CompositionProfileNormalizer.NormalizeSpace(
            new CompositionProfileSpaceDocument(
                "work",
                "work-buffer",
                Capacity: new CompositionProfileCapacityDocument("fixed", Number("16.0")),
                Initializer: new CompositionProfileInitializerDocument("blank", Number("255")))));
        MutableCompositionProfileSpace output = Assert.IsType<MutableCompositionProfileSpace>(CompositionProfileNormalizer.NormalizeSpace(
            new CompositionProfileSpaceDocument(
                "output",
                "output-image",
                Capacity: new CompositionProfileCapacityDocument("resolved-map"),
                Initializer: new CompositionProfileInitializerDocument(
                    "clone",
                    SourceSlotId: "reference-input"))));

        Assert.Equal(CompositionProfileSpaceKind.WorkBuffer, work.Kind);
        Assert.Equal(16, Assert.IsType<FixedProfileCapacity>(work.Capacity).Bytes);
        Assert.Equal(0xFF, Assert.IsType<BlankProfileInitializer>(work.Initializer).FillByte);
        Assert.Equal(CompositionProfileSpaceKind.OutputImage, output.Kind);
        _ = Assert.IsType<ResolvedMapProfileCapacity>(output.Capacity);
        Assert.Equal("reference-input", Assert.IsType<CloneProfileInitializer>(output.Initializer).SourceSlotId);
    }

    /// <summary>Verifies an admitted logical-output capacity maps to its canonical value.</summary>
    [Fact]
    public void SpaceMapsRuntimeRequestCapacity()
    {
        var output = new CompositionProfileSpaceDocument(
            "output",
            "output-image",
            Capacity: new CompositionProfileCapacityDocument("runtime-request"),
            Initializer: new CompositionProfileInitializerDocument("blank", Number("0")));
        MutableCompositionProfileSpace normalized = Assert.IsType<MutableCompositionProfileSpace>(
            CompositionProfileNormalizer.NormalizeSpace(output));

        _ = Assert.IsType<RuntimeRequestProfileCapacity>(normalized.Capacity);
    }

    /// <summary>Verifies unknown space tokens fail at exact source paths.</summary>
    [Fact]
    public void SpaceRejectsUnknownUnionTokensWithPaths()
    {
        CompositionProfileNormalizationException kind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeSpace(new CompositionProfileSpaceDocument("space", "future")));
        CompositionProfileNormalizationException policy = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeSpace(InputSpace("future")));

        Assert.Equal("spaces[0].kind", kind.Path);
        Assert.Equal("spaces[0].instancePolicy", policy.Path);
    }

    /// <summary>Verifies capacity and initializer scalar errors fail closed at their fields.</summary>
    [Fact]
    public void SpaceRejectsInvalidScalarValuesWithPaths()
    {
        CompositionProfileNormalizationException capacityKind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeSpace(MutableSpace(
                new CompositionProfileCapacityDocument("future"),
                new CompositionProfileInitializerDocument("blank", Number("0")))));
        CompositionProfileNormalizationException capacityBytes = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeSpace(MutableSpace(
                new CompositionProfileCapacityDocument("fixed", Number("1.5")),
                new CompositionProfileInitializerDocument("blank", Number("0")))));
        CompositionProfileNormalizationException initializerKind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeSpace(MutableSpace(
                new CompositionProfileCapacityDocument("resolved-map"),
                new CompositionProfileInitializerDocument("future"))));
        CompositionProfileNormalizationException fillByte = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeSpace(MutableSpace(
                new CompositionProfileCapacityDocument("resolved-map"),
                new CompositionProfileInitializerDocument("blank", Number("256")))));
        Assert.Equal("spaces[0].capacity.kind", capacityKind.Path);
        Assert.Equal("spaces[0].capacity.bytes", capacityBytes.Path);
        Assert.Equal("spaces[0].initializer.kind", initializerKind.Path);
        Assert.Equal("spaces[0].initializer.fillByte", fillByte.Path);
    }

    /// <summary>Verifies every logical-view selector preserves half-open range values.</summary>
    [Fact]
    public void ViewMapsEverySelectorShape()
    {
        MapRegionViewSelector region = Assert.IsType<MapRegionViewSelector>(CompositionProfileNormalizer.NormalizeView(
            View(new CompositionProfileViewSelectorDocument("map-region", RegionId: "dp-code"))).Selector);
        MapRegionSliceViewSelector slice = Assert.IsType<MapRegionSliceViewSelector>(CompositionProfileNormalizer.NormalizeView(
            View(new CompositionProfileViewSelectorDocument(
                "map-region-slice",
                RegionId: "dp-code",
                Offset: Number("16.0"),
                Length: Number("32")))).Selector);
        SpaceRangeViewSelector range = Assert.IsType<SpaceRangeViewSelector>(CompositionProfileNormalizer.NormalizeView(
            View(new CompositionProfileViewSelectorDocument(
                "space-range",
                Range: new CompositionProfileRelativeRangeDocument(Number("48"), Number("16"))))).Selector);
        RegionTemplateRangeViewSelector templateRange = Assert.IsType<RegionTemplateRangeViewSelector>(
            CompositionProfileNormalizer.NormalizeView(
                View(new CompositionProfileViewSelectorDocument(
                    "region-template-range",
                    RegionInstanceId: "b-bank",
                    TemplateRegionId: "tp-code")),
                "views[0]").Selector);

        Assert.Equal("dp-code", region.RegionId);
        Assert.Equal(16, slice.RelativeRange.Start);
        Assert.Equal(48, slice.RelativeRange.EndExclusive);
        Assert.Equal(48, range.Range.Start);
        Assert.Equal(64, range.Range.EndExclusive);
        Assert.Equal("b-bank", templateRange.RegionInstanceId);
        Assert.Equal("tp-code", templateRange.TemplateRegionId);
    }

    /// <summary>Verifies an unknown selector token retains its exact source path.</summary>
    [Fact]
    public void ViewRejectsUnknownSelectorKindWithPath()
    {
        CompositionProfileNormalizationException kind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeView(View(new CompositionProfileViewSelectorDocument("future"))));
        Assert.Equal("views[0].selector.kind", kind.Path);
    }

    /// <summary>Verifies range scalars and end arithmetic reject invalid values without wrapping.</summary>
    [Fact]
    public void ViewRejectsInvalidRangesWithPaths()
    {
        CompositionProfileNormalizationException fraction = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeView(View(new CompositionProfileViewSelectorDocument(
                "map-region-slice",
                RegionId: "dp-code",
                Offset: Number("1.5"),
                Length: Number("1")))));
        CompositionProfileNormalizationException zeroLength = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeView(View(new CompositionProfileViewSelectorDocument(
                "space-range",
                Range: new CompositionProfileRelativeRangeDocument(Number("0"), Number("0"))))));
        CompositionProfileNormalizationException overflow = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeView(View(new CompositionProfileViewSelectorDocument(
                "space-range",
                Range: new CompositionProfileRelativeRangeDocument(
                    Number("9223372036854775807"),
                    Number("1"))))));

        Assert.Equal("views[0].selector.offset", fraction.Path);
        Assert.Equal("views[0].selector.range.length", zeroLength.Path);
        Assert.Equal("views[0].selector.range", overflow.Path);
    }

    private static CompositionProfileSpaceDocument InputSpace(string instancePolicy)
    {
        return new CompositionProfileSpaceDocument(
            "source",
            "input-artifact",
            SlotId: "tp-input",
            InstancePolicy: instancePolicy);
    }

    private static CompositionProfileSpaceDocument MutableSpace(
        CompositionProfileCapacityDocument capacity,
        CompositionProfileInitializerDocument initializer)
    {
        return new CompositionProfileSpaceDocument(
            "output",
            "output-image",
            Capacity: capacity,
            Initializer: initializer);
    }

    private static CompositionProfileViewDocument View(CompositionProfileViewSelectorDocument selector)
    {
        return new CompositionProfileViewDocument("view", "source", selector);
    }

    private static JsonElement Number(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
