using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>Valid declarations are immutable and legacy family contracts remain policy-free.</summary>
    [Fact]
    public void NormalizeAbFormatPolicySnapshotsValidFactsAndLeavesLegacyFamiliesNull()
    {
        FirmwareFamilyResolutionDefinition legacy =
            FirmwareFamilyResolutionNormalizer.Normalize(Document(), FamilyHash);
        Assert.Null(legacy.AbFormatPolicy);

        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareFamilyResolutionDefinition family =
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash, resolver);

        FirmwareAbFormatPolicy policy = Assert.IsType<FirmwareAbFormatPolicy>(family.AbFormatPolicy);
        FirmwareAbFormatPolicyDocument sourcePolicy = Assert.IsType<FirmwareAbFormatPolicyDocument>(
            document.AbFormatPolicy);
        IList<int> sourceRecognitionValues = (IList<int>)sourcePolicy.Formats[0].DefaultRecognitionValues;
        sourceRecognitionValues[0] = 0x42;
        FirmwareAbFormatDefinition format = Assert.Single(policy.Formats);
        FirmwareAbFormatVariant variant = Assert.Single(policy.Variants, variant =>
            StringComparer.Ordinal.Equals(variant.FormatId, "format-a"));
        FirmwareAbFormatVariant common = Assert.Single(policy.Variants, variant =>
            StringComparer.Ordinal.Equals(variant.FormatId, "common"));
        Assert.Equal("format-scope", policy.ScopeId);
        Assert.Equal("common", policy.CommonFormatId);
        Assert.Equal("format-a", format.UniqueId);
        Assert.Equal<byte>([0x41], format.DefaultRecognitionValues);
        Assert.Equal(("NT00001", "format-a", "map"), (variant.MemberId, variant.FormatId, variant.MapId));
        Assert.Equal(("NT00001", "common", "map"), (common.MemberId, common.FormatId, common.MapId));

        IList<byte> recognitionValues = (IList<byte>)format.DefaultRecognitionValues;
        _ = Assert.Throws<NotSupportedException>(recognitionValues.Clear);
        Assert.Equal<byte>([0x41], format.DefaultRecognitionValues);
    }

    /// <summary>Direct normalization must enforce the schema's nonempty variant requirement.</summary>
    [Fact]
    public void NormalizeRejectsEmptyAbFormatVariants()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) = AbPolicyDocument();
        FirmwareAbFormatPolicyDocument policy = Assert.IsType<FirmwareAbFormatPolicyDocument>(document.AbFormatPolicy);
        _ = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document with { AbFormatPolicy = policy with { Variants = [] } },
                FamilyHash, resolver));
    }

    /// <summary>Recognition values cannot ambiguously identify two configurable formats.</summary>
    [Fact]
    public void NormalizeRejectsDuplicateRecognitionValuesAcrossFormats()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareAbFormatPolicyDocument policy = Assert.IsType<FirmwareAbFormatPolicyDocument>(document.AbFormatPolicy);
        FirmwareFamilyDocument duplicate = document with
        {
            AbFormatPolicy = policy with
            {
                Formats =
                [
                    policy.Formats[0],
                    new FirmwareAbFormatDefinitionDocument("format-b", "Format B", [0x41]),
                ],
            },
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(duplicate, FamilyHash, resolver));

        Assert.Equal("abFormatPolicy", exception.Path);
    }

    /// <summary>Format identifiers and member-format-map declarations are each unambiguous.</summary>
    [Fact]
    public void NormalizeRejectsDuplicateAbFormatIdentifiersAndVariantTriples()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareAbFormatPolicyDocument policy = Assert.IsType<FirmwareAbFormatPolicyDocument>(document.AbFormatPolicy);
        FirmwareAbFormatVariantDocument format = Assert.Single(policy.Variants, variant =>
            StringComparer.Ordinal.Equals(variant.FormatId, "format-a"));
        FirmwareFamilyDocument duplicateId = document with
        {
            AbFormatPolicy = policy with { Formats = [policy.Formats[0], policy.Formats[0]] },
        };
        FirmwareFamilyDocument duplicateVariant = document with
        {
            AbFormatPolicy = policy with { Variants = [format, format] },
        };

        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(duplicateId, FamilyHash, resolver));
        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(duplicateVariant, FamilyHash, resolver));
    }

    /// <summary>Every A/B format declaration must close over real family facts.</summary>
    [Theory]
    [InlineData("member")]
    [InlineData("map")]
    [InlineData("field")]
    [InlineData("relation")]
    public void NormalizeRejectsUnknownAbFormatReferences(string mutation)
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareAbFormatPolicyDocument policy = Assert.IsType<FirmwareAbFormatPolicyDocument>(document.AbFormatPolicy);
        FirmwareAbFormatVariantDocument variant = Assert.Single(policy.Variants, variant =>
            StringComparer.Ordinal.Equals(variant.FormatId, "format-a"));
        FirmwareFamilyDocument invalid = mutation switch
        {
            "member" => document with
            {
                AbFormatPolicy = policy with
                {
                    Variants = [variant with { MemberId = "NT99999" }],
                },
            },
            "map" => document with
            {
                AbFormatPolicy = policy with
                {
                    Variants = [variant with { MapId = "unknown-map" }],
                },
            },
            "field" => document with
            {
                AbFormatPolicy = policy with
                {
                    PrimaryBindings = policy.PrimaryBindings with { FieldId = "unknown-field" },
                },
            },
            "relation" => document with
            {
                AbFormatPolicy = policy with
                {
                    PrimaryBindings = policy.PrimaryBindings with { RelationId = "unknown-relation" },
                },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(invalid, FamilyHash, resolver));
    }

    /// <summary>A/B target maps remain selector-free to retain one format selection authority.</summary>
    [Fact]
    public void NormalizeRejectsMetadataPredicateOnAbFormatTargetMap()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareImageMapDocument map = Assert.Single(document.ImageMaps);
        FirmwareFamilyDocument invalid = document with
        {
            ImageMaps =
            [
                map with
                {
                    Applicability = map.Applicability with
                    {
                        MetadataPredicates =
                        [new FirmwareMetadataPredicateDocument("primary-a", "firmware-version", "equals", [Number("1")])],
                    },
                },
            ],
        };

        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(invalid, FamilyHash, resolver));
    }

    /// <summary>Target maps must declare A/B mode; policy scope is not a substitute for that fact.</summary>
    [Fact]
    public void NormalizeRejectsTargetMapWithoutAbMergeMode()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareImageMapDocument map = Assert.Single(document.ImageMaps);
        FirmwareFamilyDocument invalid = document with
        {
            ImageMaps =
            [
                map with
                {
                    Applicability = map.Applicability with { ModeIds = ["standard"] },
                },
            ],
        };

        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(invalid, FamilyHash, resolver));
    }

    /// <summary>One member-format group cannot mix its topology-independent and Single baselines.</summary>
    [Fact]
    public void NormalizeRejectsConflictingAbFormatBaselineKinds()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareAbFormatPolicyDocument policy = Assert.IsType<FirmwareAbFormatPolicyDocument>(document.AbFormatPolicy);
        FirmwareImageMapDocument map = Assert.Single(document.ImageMaps);
        FirmwareImageMapDocument single = map with
        {
            MapId = "single-map",
            Applicability = map.Applicability with
            {
                TopologyRequirement = new FirmwareTopologyRequirementDocument("single"),
            },
        };
        FirmwareFamilyDocument invalid = document with
        {
            ImageMaps = [map, single],
            AbFormatPolicy = policy with
            {
                Variants =
                [
                    new FirmwareAbFormatVariantDocument("NT00001", "format-a", "map"),
                    new FirmwareAbFormatVariantDocument("NT00001", "format-a", "single-map"),
                ],
            },
        };

        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(invalid, FamilyHash, resolver));
    }

    /// <summary>An exact-count override cannot exist without its declared baseline.</summary>
    [Fact]
    public void NormalizeRejectsExactCountOverrideWithoutCompatibleBaseline()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareAbFormatPolicyDocument policy = Assert.IsType<FirmwareAbFormatPolicyDocument>(document.AbFormatPolicy);
        FirmwareImageMapDocument map = Assert.Single(document.ImageMaps);
        FirmwareImageMapDocument exactMap = map with
        {
            MapId = "exact-map",
            Applicability = map.Applicability with
            {
                TopologyRequirement = new FirmwareTopologyRequirementDocument(
                    "exact-count",
                    ChipCount: Number("2")),
            },
        };
        FirmwareFamilyDocument invalid = document with
        {
            ImageMaps = [exactMap],
            AbFormatPolicy = policy with
            {
                Variants =
                [
                    new FirmwareAbFormatVariantDocument("NT00001", "format-a", "exact-map"),
                ],
            },
        };

        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(invalid, FamilyHash, resolver));
    }

    /// <summary>Cascade baselines are generic and therefore always start at two without a maximum.</summary>
    [Fact]
    public void NormalizeRejectsNonGenericCascadeBaseline()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            AbPolicyDocument();
        FirmwareImageMapDocument map = Assert.Single(document.ImageMaps);
        FirmwareFamilyDocument invalid = document with
        {
            ImageMaps =
            [
                map with
                {
                    Applicability = map.Applicability with
                    {
                        TopologyRequirement = new FirmwareTopologyRequirementDocument(
                            "cascade",
                            MinimumChipCount: Number("3")),
                    },
                },
            ],
        };

        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(invalid, FamilyHash, resolver));
    }

    /// <summary>Only a canonical bitwise-complement relation can satisfy A/B primary bindings.</summary>
    [Fact]
    public void NormalizeRejectsUnknownAbPrimaryRelationKind()
    {
        FirmwareFamilyDocument direct = AbPolicyDirectDocument();
        FirmwareMetadataSetDocument set = Assert.Single(direct.MetadataSets);
        FirmwareMetadataStructureDocument structure = Assert.Single(set.Structures);
        FirmwareMetadataFieldRelationDocument relation = Assert.Single(structure.Relations!);
        FirmwareFamilyDocument invalid = direct with
        {
            MetadataSets =
            [
                set with
                {
                    Structures =
                    [
                        structure with
                        {
                            Relations = [relation with { Kind = "not-a-relation" }],
                        },
                    ],
                },
            ],
        };

        _ = Assert.Throws<FirmwareFamilyNormalizationException>(
            () => FirmwareFamilyResolutionNormalizer.Normalize(invalid, FamilyHash));
    }

    private static (FirmwareFamilyDocument Document, IFirmwareMetadataStructureDefinitionResolver Resolver)
        AbPolicyDocument()
    {
        FirmwareFamilyDocument direct = AbPolicyDirectDocument();
        FirmwareImageMapDocument sourceMap = Assert.Single(direct.ImageMaps);
        FirmwareCapabilityFactDocument sourceCapability = Assert.Single(direct.Capabilities);
        direct = direct with
        {
            ImageMaps =
            [
                sourceMap with
                {
                    Applicability = sourceMap.Applicability with { ModeIds = ["ab-merge"] },
                },
            ],
            Capabilities =
            [
                sourceCapability with
                {
                    Applicability = sourceCapability.Applicability with { ModeIds = ["ab-merge"] },
                },
            ],
        };
        FirmwareMetadataStructureDefinition definition = Assert.Single(
            FirmwareFamilyResolutionNormalizer.Normalize(direct, FamilyHash)
                .GetStructuresForMap("map")).Definition;
        var reference = new FirmwareMetadataStructureDefinitionReferenceDocument(
            ProviderFamilyId,
            ProviderFamilyVersion,
            ProviderFamilyHash,
            definition.DefinitionId);
        FirmwareMetadataSetDocument set = Assert.Single(direct.MetadataSets);
        FirmwareMetadataStructureDocument source = Assert.Single(set.Structures);
        FirmwareMetadataStructureDocument primaryA = source with
        {
            StructureId = "primary-a",
            ArtifactBindingId = "tp-a",
            Length = default,
            Fields = null!,
            Assertions = null!,
            Relations = null,
            DefinitionReference = reference,
        };
        FirmwareMetadataStructureDocument primaryB = primaryA with
        {
            StructureId = "primary-b",
            ArtifactBindingId = "tp-b",
        };
        var recognitionValues = new List<int> { 0x41 };
        FirmwareFamilyDocument document = direct with
        {
            MetadataSets = [set with { Structures = [primaryA, primaryB] }],
            AbFormatPolicy = new FirmwareAbFormatPolicyDocument(
                "format-scope",
                "common",
                "Common",
                [new FirmwareAbFormatDefinitionDocument("format-a", "Format A", recognitionValues)],
                new FirmwareAbPrimaryBindingsDocument(
                    "primary-a",
                    "primary-b",
                    "event-buffer-format-version",
                    "firmware-version-complement"),
                [
                    new FirmwareAbFormatVariantDocument("NT00001", "common", "map"),
                    new FirmwareAbFormatVariantDocument("NT00001", "format-a", "map"),
                ]),
        };
        return (document, new ExactDefinitionResolver(reference, definition));
    }

    private static FirmwareFamilyDocument AbPolicyDirectDocument()
    {
        FirmwareFamilyDocument direct = Document(includePredicate: false, includeRelations: true);
        FirmwareMetadataSetDocument set = Assert.Single(direct.MetadataSets);
        FirmwareMetadataStructureDocument structure = Assert.Single(set.Structures);
        FirmwareMetadataFieldDocument[] fields =
        [
            .. structure.Fields,
            new FirmwareMetadataFieldDocument(
                "event-buffer-format-version",
                Number("12"),
                Number("1"),
                "unsigned-integer",
                "little"),
        ];
        return direct with
        {
            MetadataSets =
            [
                set with
                {
                    Structures =
                    [
                        structure with
                        {
                            Length = Number("13"),
                            Locator = new FirmwareMetadataLocatorDocument(
                                "absolute-range",
                                "root",
                                Range: AddressedRange(0, 13)),
                            Fields = fields,
                        },
                    ],
                },
            ],
        };
    }
}
