using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
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
            [
                "version",
                "display",
                "output-naming",
                "validation",
                "map-resolution",
                "inspection",
                "formatting",
                "copy",
                "relocation",
                "integrity",
                "processor",
                "memory-projection",
                "report-classification",
            ]);

        CompositionProfileMetadataBinding binding = CompositionProfileNormalizer.NormalizeMetadataBinding(document);

        Assert.Equal(["chip-number", "pid"], binding.FieldIds);
        Assert.Equal(Enum.GetValues<CompositionProfileMetadataPurpose>(), binding.Purposes);
    }

    /// <summary>
    /// New bindings retain exact typed targets and evidence while legacy
    /// fieldIds normalize into the same internal field-reference shape.
    /// </summary>
    [Fact]
    public void MetadataBindingNormalizesTypedReferenceTargetsWithoutGeometry()
    {
        var document = new CompositionProfileMetadataBindingDocument(
            "tp-header",
            "tp-input",
            "type-ab-tp-flash-header",
            FieldIds: null,
            Purposes:
            [
                "inspection",
                "formatting",
                "copy",
                "relocation",
                "memory-projection",
                "report-classification",
            ],
            TargetReferences:
            [
                new CompositionProfileMetadataTargetReferenceDocument(
                    "span",
                    "complete-header"),
                new CompositionProfileMetadataTargetReferenceDocument(
                    "series",
                    "dlm-crc-series"),
                new CompositionProfileMetadataTargetReferenceDocument(
                    "group",
                    "tp-bank-relative-start-addresses"),
            ],
            EvidenceRefs: ["owner-type-ab-header-table"]);

        CompositionProfileMetadataBinding binding =
            CompositionProfileNormalizer.NormalizeMetadataBinding(document);

        Assert.Equal(
            [
                (FirmwareMetadataReferenceTargetKind.Span, "complete-header"),
                (FirmwareMetadataReferenceTargetKind.Series, "dlm-crc-series"),
                (FirmwareMetadataReferenceTargetKind.Group, "tp-bank-relative-start-addresses"),
            ],
            binding.TargetReferences.Select(static target =>
                (target.Kind, target.TargetId)));
        Assert.Empty(binding.FieldIds);
        Assert.Equal(["owner-type-ab-header-table"], binding.EvidenceRefs);
        Assert.DoesNotContain(
            binding.GetType().GetProperties(),
            static property => property.Name.Contains("Range", StringComparison.Ordinal) ||
                               property.Name.Contains("Write", StringComparison.Ordinal) ||
                               property.Name.Contains("Processor", StringComparison.Ordinal));
    }

    /// <summary>Legacy fieldIds become typed field references without a second internal owner.</summary>
    [Fact]
    public void MetadataBindingConvertsLegacyFieldIdsToTypedTargets()
    {
        var document = new CompositionProfileMetadataBindingDocument(
            "fwconfig",
            "source",
            "firmware-config",
            ["pid", "chip-number"],
            ["display"]);

        CompositionProfileMetadataBinding binding =
            CompositionProfileNormalizer.NormalizeMetadataBinding(document);

        Assert.Equal(
            ["chip-number", "pid"],
            binding.TargetReferences.Select(static target => target.TargetId));
        Assert.All(
            binding.TargetReferences,
            static target => Assert.Equal(
                FirmwareMetadataReferenceTargetKind.Field,
                target.Kind));
        Assert.Equal(
            binding.TargetReferences.Select(static target => target.TargetId),
            binding.FieldIds);
    }

    /// <summary>Verifies an unknown purpose token retains its source path.</summary>
    [Fact]
    public void MetadataBindingRejectsUnknownPurposeWithPath()
    {
        CompositionProfileNormalizationException purpose = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeMetadataBinding(
                MetadataBinding(["future"], ["pid"])));
        Assert.Equal("metadataBindings[0].purposes[0]", purpose.Path);
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
