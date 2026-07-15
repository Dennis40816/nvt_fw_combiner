using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 metadata bindings and authoring access.</summary>
public sealed class CompositionProfileV2MetadataNormalizerTests
{
    /// <summary>Verifies every metadata purpose maps and normalized sets are deterministic.</summary>
    [Fact]
    public void MetadataBindingMapsEveryPurpose()
    {
        var document = new CompositionProfileMetadataBindingDocument(
            "fwconfig",
            "source",
            "firmware-config",
            ["pid", "chip-number"],
            ["version", "display", "output-naming", "validation", "map-resolution"]);

        CompositionProfileMetadataBinding binding = CompositionProfileNormalizer.NormalizeMetadataBinding(document);

        Assert.Equal(["chip-number", "pid"], binding.FieldIds);
        Assert.Equal(Enum.GetValues<CompositionProfileMetadataPurpose>(), binding.Purposes);
    }

    /// <summary>Verifies unknown purpose tokens and missing arrays retain their source paths.</summary>
    [Fact]
    public void MetadataBindingRejectsInvalidMembersWithPaths()
    {
        CompositionProfileNormalizationException purpose = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeMetadataBinding(
                MetadataBinding(["future"], ["pid"])));
        CompositionProfileNormalizationException purposes = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeMetadataBinding(
                MetadataBinding(null!, ["pid"])));
        CompositionProfileNormalizationException fields = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeMetadataBinding(
                MetadataBinding(["validation"], null!)));

        Assert.Equal("metadataBindings[0].purposes[0]", purpose.Path);
        Assert.Equal("metadataBindings[0].purposes", purposes.Path);
        Assert.Equal("metadataBindings[0].fieldIds", fields.Path);
    }

    /// <summary>Verifies every authoring access token maps without becoming execution policy.</summary>
    [Fact]
    public void RegionAccessMapsEveryAccessKind()
    {
        CompositionProfileRegionAccess[] rules =
        [
            NormalizeAccess("hidden"),
            NormalizeAccess("read-only"),
            NormalizeAccess("whole"),
            NormalizeAccess("parts", ["subregion-z", "subregion-a"]),
            NormalizeAccess("explicit-range"),
        ];

        Assert.Equal(Enum.GetValues<RegionAccessKind>(), rules.Select(static rule => rule.Access));
        Assert.Equal(["subregion-a", "subregion-z"], rules[3].AllowedSubregionIds);
        Assert.All(
            rules.Where(static rule => rule.Access != RegionAccessKind.Parts),
            static rule => Assert.Empty(rule.AllowedSubregionIds));
    }

    /// <summary>Verifies unknown access tokens fail at the discriminator path.</summary>
    [Fact]
    public void RegionAccessRejectsUnknownAccessWithPath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            NormalizeAccess("future"));

        Assert.Equal("regionAccessRules[0].access", exception.Path);
    }

    /// <summary>Verifies parts require an allowlist and other access kinds reject one.</summary>
    [Fact]
    public void RegionAccessRejectsInvalidAllowlistPolicyAtRulePath()
    {
        CompositionProfileNormalizationException missing = Assert.Throws<CompositionProfileNormalizationException>(() =>
            NormalizeAccess("parts"));
        CompositionProfileNormalizationException forbidden = Assert.Throws<CompositionProfileNormalizationException>(() =>
            NormalizeAccess("whole", ["subregion"]));

        Assert.Equal("regionAccessRules[0]", missing.Path);
        Assert.Equal("regionAccessRules[0]", forbidden.Path);
        _ = Assert.IsType<ArgumentException>(missing.InnerException, exactMatch: false);
        _ = Assert.IsType<ArgumentException>(forbidden.InnerException, exactMatch: false);
    }

    private static CompositionProfileMetadataBindingDocument MetadataBinding(
        IReadOnlyList<string> purposes,
        IReadOnlyList<string> fieldIds)
    {
        return new CompositionProfileMetadataBindingDocument(
            "fwconfig",
            "source",
            "firmware-config",
            fieldIds,
            purposes);
    }

    private static CompositionProfileRegionAccess NormalizeAccess(
        string access,
        IReadOnlyList<string>? allowedSubregionIds = null)
    {
        return CompositionProfileNormalizer.NormalizeRegionAccessRule(
            new CompositionProfileRegionAccessRuleDocument(
                "dp-code",
                access,
                "Owner-approved authoring access.",
                allowedSubregionIds));
    }
}
