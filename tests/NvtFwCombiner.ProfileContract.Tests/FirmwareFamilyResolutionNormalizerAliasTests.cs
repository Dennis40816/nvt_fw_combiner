using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>Verifies region aliases preserve target identity while sharing one terminal region graph.</summary>
    [Fact]
    public void NormalizeResolvesMapBoundRegionAliasWithoutCloningFacts()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument document = source with
        {
            Capabilities = [],
            ImageMaps =
            [
                map with { MapId = "target-map", RegionSetIds = ["target-regions"] },
                map with { MapId = "source-map" },
            ],
            FactAliases =
            [
                new FirmwareRegionSetAliasDocument(
                    "target-regions-to-source",
                    "NT00001",
                    "target-map",
                    "target-regions",
                    "NT00001",
                    "source-map",
                    "physical",
                    AliasApplicability(),
                    "synthetic region graph inheritance",
                    ["region-alias-evidence"]),
            ],
        };

        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);
        FirmwareImageMap target = definition.ImageMaps.Single(map => map.MapId == "target-map");
        FirmwareImageMap directSource = definition.ImageMaps.Single(map => map.MapId == "source-map");
        FirmwareMapFactBinding<FirmwareRegionSet> targetBinding = Assert.Single(target.RegionSetBindings);
        FirmwareMapFactBinding<FirmwareRegionSet> sourceBinding = Assert.Single(directSource.RegionSetBindings);

        Assert.Equal("target-regions", targetBinding.EffectiveKey.FactId);
        Assert.Equal("source-map", targetBinding.DirectSourceKey.MapId);
        Assert.Equal("physical", targetBinding.DirectSourceKey.FactId);
        Assert.Same(sourceBinding.Value, targetBinding.Value);
        Assert.Equal("target-regions-to-source", Assert.Single(targetBinding.Provenance.AliasChain).AliasId);
    }

    /// <summary>Verifies metadata aliases retain target identity and ordered terminal provenance.</summary>
    [Fact]
    public void NormalizeResolvesMultiHopMetadataAliasWithoutCloningFacts()
    {
        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(
            PhysicalAliasDocument(),
            FamilyHash);

        FirmwareImageMap target = definition.ImageMaps.Single(map => map.MapId == "target-map");
        FirmwareImageMap source = definition.ImageMaps.Single(map => map.MapId == "source-map");
        FirmwareMapFactBinding<FirmwareMetadataSet> targetBinding = Assert.Single(target.MetadataSetBindings);
        FirmwareMapFactBinding<FirmwareMetadataSet> sourceBinding = Assert.Single(source.MetadataSetBindings);

        Assert.Equal("target-metadata", targetBinding.EffectiveKey.FactId);
        Assert.Equal("source-map", targetBinding.DirectSourceKey.MapId);
        Assert.Equal("metadata", targetBinding.DirectSourceKey.FactId);
        FirmwareFactAliasHop[] hops = [.. targetBinding.Provenance.AliasChain];
        Assert.Equal(["target-to-middle", "middle-to-source"], hops.Select(static hop => hop.AliasId));
        Assert.Equal(targetBinding.EffectiveKey, hops[0].TargetKey);
        Assert.Equal(new FirmwareMapFactKey(
            "NT00001",
            "middle-map",
            FirmwareFactKind.MetadataSet,
            "middle-metadata"), hops[0].SourceKey);
        Assert.Equal(hops[0].SourceKey, hops[1].TargetKey);
        Assert.Equal(targetBinding.DirectSourceKey, hops[1].SourceKey);
        Assert.Equal("synthetic transitive metadata alias", hops[0].Reason);
        Assert.Equal(["target-alias-evidence"], hops[0].EvidenceRefs);
        Assert.Equal("synthetic terminal metadata alias", hops[1].Reason);
        Assert.Equal(["middle-alias-evidence"], hops[1].EvidenceRefs);
        AssertSameApplicabilityShape(
            FirmwareFactApplicability.FromMap(target.Applicability),
            hops[0].Applicability);
        AssertSameApplicabilityShape(
            FirmwareFactApplicability.FromMap(source.Applicability),
            hops[1].Applicability);
        Assert.Equal(["metadata-evidence"], targetBinding.Provenance.DirectEvidenceRefs);
        Assert.Same(sourceBinding.Value, targetBinding.Value);
        Assert.Equal(sourceBinding.CanonicalFactId, targetBinding.CanonicalFactId);
    }

    private static void AssertSameApplicabilityShape(
        FirmwareFactApplicability expected,
        FirmwareFactApplicability actual)
    {
        Assert.Equal(expected.CapacityBytes, actual.CapacityBytes);
        Assert.Equal(expected.TopologyRequirement, actual.TopologyRequirement);
        Assert.Equal(expected.ModeIds, actual.ModeIds);
        Assert.Equal(expected.CommonFirmwareCategoryIds, actual.CommonFirmwareCategoryIds);
        Assert.Equal(expected.MetadataPredicates.Count, actual.MetadataPredicates.Count);

        List<FirmwareMetadataPredicate> unmatched = [.. actual.MetadataPredicates];
        foreach (FirmwareMetadataPredicate predicate in expected.MetadataPredicates)
        {
            int matchIndex = unmatched.FindIndex(candidate =>
                StringComparer.Ordinal.Equals(predicate.MetadataStructureId, candidate.MetadataStructureId) &&
                StringComparer.Ordinal.Equals(predicate.FieldId, candidate.FieldId) &&
                predicate.Comparison == candidate.Comparison &&
                predicate.ExpectedValues.Count == candidate.ExpectedValues.Count &&
                predicate.ExpectedValues.All(candidate.ExpectedValues.Contains));
            Assert.True(matchIndex >= 0, $"Missing applicability predicate for '{predicate.FieldId}'.");
            unmatched.RemoveAt(matchIndex);
        }
    }

    /// <summary>Verifies capability aliases remain evidence bindings and do not affect map selection.</summary>
    [Fact]
    public void NormalizeResolvesMapBoundCapabilityAlias()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareFamilyDocument document = source with
        {
            Capabilities =
            [
                new FirmwareCapabilityFactDocument(
                    "source-capability",
                    "ab-code",
                    "NT00001",
                    "source-map",
                    AliasApplicability(),
                    "confirmed-present",
                    "source capability",
                    ["source-capability-evidence"]),
            ],
            FactAliases =
            [
                .. source.FactAliases,
                new FirmwareCapabilityAliasDocument(
                    "capability-to-source",
                    "NT00001",
                    "target-map",
                    "target-capability",
                    "NT00001",
                    "source-map",
                    "source-capability",
                    AliasApplicability(),
                    "target capability inherits the source evidence",
                    ["target-capability-evidence"]),
            ],
        };

        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);
        FirmwareMapFactBinding<FirmwareCapabilityFact> target = Assert.Single(
            definition.CapabilityBindings,
            binding => binding.EffectiveKey.MapId == "target-map");

        Assert.Equal("target-capability", target.EffectiveKey.FactId);
        Assert.Equal("source-map", target.DirectSourceKey.MapId);
        Assert.Equal("source-capability", target.DirectSourceKey.FactId);
        Assert.Equal("ab-code", target.Value.CapabilityId);
        Assert.Equal("capability-to-source", Assert.Single(target.Provenance.AliasChain).AliasId);
        Assert.Equal(3, definition.ImageMaps.Count);
    }

    /// <summary>Verifies physical aliases accept semantically equivalent topology forms, not only matching JSON shapes.</summary>
    [Fact]
    public void NormalizeAcceptsEquivalentPhysicalAliasApplicability()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareFamilyDocument document = source with
        {
            ImageMaps =
            [
                .. source.ImageMaps.Select(map => map with
                {
                    Applicability = map.Applicability with
                    {
                        TopologyRequirement = new FirmwareTopologyRequirementDocument("single"),
                    },
                }),
            ],
            FactAliases =
            [
                .. source.FactAliases.Select(alias => alias with
                {
                    Applicability = new FirmwareAliasApplicabilityDocument(
                        ["standard"],
                        new FirmwareTopologyRequirementDocument("exact-count", ChipCount: Number("1")),
                        Number("16")),
                }),
            ],
        };

        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);
        FirmwareImageMap target = definition.ImageMaps.Single(map => map.MapId == "target-map");

        Assert.Equal(TopologyRequirementKind.SingleChip, target.Applicability.TopologyRequirement.Kind);
        Assert.Equal(
            TopologyRequirementKind.ExactCount,
            Assert.Single(target.MetadataSetBindings).Provenance.AliasChain[0]
                .Applicability.TopologyRequirement.Kind);
    }

    /// <summary>Verifies physical aliases cannot create conditional bindings inside one map shape.</summary>
    [Fact]
    public void NormalizeRejectsPhysicalAliasScopeNarrowerThanTargetMap()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareMetadataSetAliasDocument first = Assert.IsType<FirmwareMetadataSetAliasDocument>(
            source.FactAliases[0]);
        FirmwareFamilyDocument document = source with
        {
            FactAliases =
            [
                first with
                {
                    Applicability = new FirmwareAliasApplicabilityDocument(
                        ["standard"],
                        new FirmwareTopologyRequirementDocument("exact-count", ChipCount: Number("1")),
                        Number("16")),
                },
                source.FactAliases[1],
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("factAliases[0].applicability", exception.Path);
    }

    /// <summary>Verifies capability providers with overlapping scopes are rejected even when states match.</summary>
    [Fact]
    public void NormalizeRejectsOverlappingCapabilityEvidence()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareCapabilityFactDocument original = Assert.Single(source.Capabilities);
        FirmwareFamilyDocument document = source with
        {
            Capabilities =
            [
                original,
                original with { CapabilityFactId = "duplicate-capability-evidence" },
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("capabilities", exception.Path);
    }

    /// <summary>Verifies a capability fact id is unique only inside its member/map fact key.</summary>
    [Fact]
    public void NormalizeAllowsSameCapabilityFactIdForDifferentMaps()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareFamilyDocument document = source with
        {
            Capabilities =
            [
                new FirmwareCapabilityFactDocument(
                    "ab-code-evidence",
                    "ab-code",
                    "NT00001",
                    "target-map",
                    AliasApplicability(),
                    "confirmed-present",
                    "target evidence",
                    ["target-capability-evidence"]),
                new FirmwareCapabilityFactDocument(
                    "ab-code-evidence",
                    "ab-code",
                    "NT00001",
                    "source-map",
                    AliasApplicability(),
                    "confirmed-present",
                    "source evidence",
                    ["source-capability-evidence"]),
            ],
        };

        FirmwareFamilyResolutionDefinition definition = FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);

        Assert.Equal(2, definition.CapabilityBindings.Count);
        Assert.Equal(
            ["source-map", "target-map"],
            definition.CapabilityBindings.Select(static binding => binding.EffectiveKey.MapId));
    }

    /// <summary>Verifies every structural alias cycle fails before maps can materialize facts.</summary>
    [Fact]
    public void NormalizeRejectsStructuralAliasCycle()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument document = source with
        {
            Capabilities = [],
            ImageMaps =
            [
                map with { MapId = "target-map", MetadataSetIds = ["target-metadata"] },
                map with { MapId = "middle-map", MetadataSetIds = ["middle-metadata"] },
            ],
            FactAliases =
            [
                new FirmwareMetadataSetAliasDocument(
                    "target-to-middle",
                    "NT00001",
                    "target-map",
                    "target-metadata",
                    "NT00001",
                    "middle-map",
                    "middle-metadata",
                    AliasApplicability(),
                    "synthetic cycle",
                    ["target-cycle-evidence"]),
                new FirmwareMetadataSetAliasDocument(
                    "middle-to-target",
                    "NT00001",
                    "middle-map",
                    "middle-metadata",
                    "NT00001",
                    "target-map",
                    "target-metadata",
                    AliasApplicability(),
                    "synthetic cycle",
                    ["middle-cycle-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies an alias cannot replace a direct provider for the same map-bound target key.</summary>
    [Fact]
    public void NormalizeRejectsDirectAndAliasProviderConflict()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument document = source with
        {
            Capabilities = [],
            ImageMaps =
            [
                map,
                map with { MapId = "source-map" },
            ],
            FactAliases =
            [
                new FirmwareMetadataSetAliasDocument(
                    "conflicting-metadata-provider",
                    "NT00001",
                    "map",
                    "metadata",
                    "NT00001",
                    "source-map",
                    "metadata",
                    AliasApplicability(),
                    "synthetic direct conflict",
                    ["conflict-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("factAliases[0]", exception.Path);
    }

    /// <summary>Verifies alias applicability cannot depend on the metadata binding it is defining.</summary>
    [Fact]
    public void NormalizeRejectsPredicateBearingMetadataAliasDependencyCycle()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareMetadataPredicateDocument predicate = new(
            "config",
            "chip-number",
            "equals",
            [Number("2")]);
        FirmwareFamilyDocument document = source with
        {
            ImageMaps =
            [
                .. source.ImageMaps.Select(map => map with
                {
                    Applicability = map.Applicability with { MetadataPredicates = [predicate] },
                }),
            ],
            FactAliases =
            [
                .. source.FactAliases.Select(alias => alias with
                {
                    Applicability = AliasApplicability([predicate]),
                }),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("factAliases", exception.Path);
        Assert.Contains("dependency cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies predicate dependency traversal detects a cycle spanning two metadata aliases.</summary>
    [Fact]
    public void NormalizeRejectsMultiNodePredicateBearingMetadataAliasDependencyCycle()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareMetadataSetDocument metadataSet = Assert.Single(source.MetadataSets);
        FirmwareMetadataStructureDocument structure = Assert.Single(metadataSet.Structures);
        FirmwareMetadataPredicateDocument predicateA = new(
            "config-a",
            "chip-number",
            "equals",
            [Number("2")]);
        FirmwareMetadataPredicateDocument predicateB = new(
            "config-b",
            "chip-number",
            "equals",
            [Number("2")]);
        FirmwareMapApplicabilityDocument targetApplicability = map.Applicability with
        {
            MetadataPredicates = [predicateB, predicateA],
        };
        FirmwareFamilyDocument document = source with
        {
            Capabilities = [],
            MetadataSets =
            [
                metadataSet with
                {
                    MetadataSetId = "metadata-a",
                    Structures = [structure with { StructureId = "config-a" }],
                },
                metadataSet with
                {
                    MetadataSetId = "metadata-b",
                    Structures = [structure with { StructureId = "config-b" }],
                },
            ],
            ImageMaps =
            [
                map with
                {
                    MapId = "target-map",
                    Applicability = targetApplicability,
                    MetadataSetIds = ["target-a", "target-b"],
                },
                map with
                {
                    MapId = "source-map",
                    Applicability = targetApplicability,
                    MetadataSetIds = ["metadata-a", "metadata-b"],
                },
            ],
            FactAliases =
            [
                new FirmwareMetadataSetAliasDocument(
                    "target-a-to-source-a",
                    "NT00001",
                    "target-map",
                    "target-a",
                    "NT00001",
                    "source-map",
                    "metadata-a",
                    AliasApplicability([predicateB, predicateA]),
                    "synthetic two-node dependency cycle",
                    ["target-a-cycle-evidence"]),
                new FirmwareMetadataSetAliasDocument(
                    "target-b-to-source-b",
                    "NT00001",
                    "target-map",
                    "target-b",
                    "NT00001",
                    "source-map",
                    "metadata-b",
                    AliasApplicability([predicateA, predicateB]),
                    "synthetic two-node dependency cycle",
                    ["target-b-cycle-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("factAliases", exception.Path);
        Assert.Contains("dependency cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("target-a", exception.Message, StringComparison.Ordinal);
    }

    private static FirmwareFamilyDocument PhysicalAliasDocument()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        return source with
        {
            Capabilities = [],
            ImageMaps =
            [
                map with { MapId = "target-map", MetadataSetIds = ["target-metadata"] },
                map with { MapId = "middle-map", MetadataSetIds = ["middle-metadata"] },
                map with { MapId = "source-map" },
            ],
            FactAliases =
            [
                new FirmwareMetadataSetAliasDocument(
                    "target-to-middle",
                    "NT00001",
                    "target-map",
                    "target-metadata",
                    "NT00001",
                    "middle-map",
                    "middle-metadata",
                    AliasApplicability(),
                    "synthetic transitive metadata alias",
                    ["target-alias-evidence"]),
                new FirmwareMetadataSetAliasDocument(
                    "middle-to-source",
                    "NT00001",
                    "middle-map",
                    "middle-metadata",
                    "NT00001",
                    "source-map",
                    "metadata",
                    AliasApplicability(),
                    "synthetic terminal metadata alias",
                    ["middle-alias-evidence"]),
            ],
        };
    }

    private static FirmwareAliasApplicabilityDocument AliasApplicability(
        IReadOnlyList<FirmwareMetadataPredicateDocument>? predicates = null)
    {
        return new FirmwareAliasApplicabilityDocument(
            ["standard"],
            new FirmwareTopologyRequirementDocument("none"),
            Number("16"),
            MetadataPredicates: predicates);
    }
}
