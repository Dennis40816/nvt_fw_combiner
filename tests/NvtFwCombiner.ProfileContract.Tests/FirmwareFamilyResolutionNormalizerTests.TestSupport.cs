using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    private static FirmwareRegion NormalizeSingleRegion(
        string owner,
        string kind,
        string writeConstraint)
    {
        FirmwareFamilyDocument source = Document();
        FirmwareRegionSetDocument regionSet = Assert.Single(source.RegionSets);
        FirmwareMetadataSetDocument metadataSet = Assert.Single(source.MetadataSets);
        FirmwareMetadataStructureDocument structure = Assert.Single(metadataSet.Structures);
        FirmwareFamilyDocument singleRegion = source with
        {
            RegionSets =
            [
                regionSet with
                {
                    Regions =
                    [
                        new FirmwareRegionDocument(
                            "root",
                            owner,
                            kind,
                            Range(0, 16),
                            writeConstraint,
                            Number("1")),
                    ],
                },
            ],
            MetadataSets =
            [
                metadataSet with
                {
                    Structures =
                    [
                        structure with
                        {
                            Locator = new FirmwareMetadataLocatorDocument(
                                "absolute-range",
                                "root",
                                Range: AddressedRange(0, 4)),
                        },
                    ],
                },
            ],
        };

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(singleRegion, FamilyHash);
        return Assert.Single(Assert.Single(definition.ImageMaps).Regions);
    }

    private static FirmwareFamilyDocument WithPredicate(
        FirmwareFamilyDocument source,
        FirmwareMetadataPredicateDocument predicate)
    {
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        return source with
        {
            ImageMaps = [map with
            {
                Applicability = map.Applicability with { MetadataPredicates = [predicate] },
            }],
            Capabilities =
            [
                Assert.Single(source.Capabilities) with
                {
                    Applicability = Assert.Single(source.Capabilities).Applicability with
                    {
                        MetadataPredicates = [predicate],
                    },
                },
            ],
        };
    }

    private static FirmwareFamilyDocument Document(
        FirmwareMetadataLocatorDocument? locator = null,
        FirmwareTopologyRequirementDocument? topology = null,
        bool includePredicate = true)
    {
        FirmwareMetadataPredicateDocument[] predicates = includePredicate
            ?
            [
                new FirmwareMetadataPredicateDocument(
                    "config",
                    "chip-number",
                    "equals",
                    [Number("2")]),
            ]
            : [];
        return new FirmwareFamilyDocument(
            "1.1",
            "synthetic-family",
            "1.0.0",
            [new FirmwareFamilyMemberDocument("NT00001", "Synthetic IC")],
            [
                new FirmwareCapabilityFactDocument(
                    "ab-code-evidence",
                    "ab-code",
                    "NT00001",
                    "map",
                    new FirmwareAliasApplicabilityDocument(
                        ["standard"],
                        topology ?? new FirmwareTopologyRequirementDocument("none"),
                        Number("16.0"),
                        MetadataPredicates: predicates),
                    "confirmed-present",
                    "synthetic capability evidence",
                    ["capability-evidence"]),
            ],
            [RegionSet()],
            [MetadataSet(locator ?? AbsoluteLocator())],
            [Map(topology ?? new FirmwareTopologyRequirementDocument("none"), predicates)],
            [],
            ["family-evidence"]);
    }

    private static FirmwareRegionSetDocument RegionSet()
    {
        return new FirmwareRegionSetDocument(
            "physical",
            "flash",
            [
                new FirmwareRegionDocument(
                    "root",
                    "system",
                    "image",
                    Range(0, 16),
                    "forbidden",
                    Number("1")),
                new FirmwareRegionDocument(
                    "config-region",
                    "system",
                    "firmware-config",
                    Range(0, 8),
                    "forbidden",
                    Number("1"),
                    "root"),
                new FirmwareRegionDocument(
                    "other",
                    "system",
                    "data",
                    Range(8, 8),
                    "forbidden",
                    Number("1"),
                    "root"),
            ],
            ["region-evidence"]);
    }

    private static FirmwareMetadataSetDocument MetadataSet(FirmwareMetadataLocatorDocument locator)
    {
        return new FirmwareMetadataSetDocument(
            "metadata",
            [
                new FirmwareMetadataStructureDocument(
                    "config",
                    "tp-firmware",
                    Number("4"),
                    locator,
                    Fields(),
                    [
                        new FirmwareByteAssertionDocument(Number("0"), "aa"),
                        new FirmwareByteAssertionDocument(Number("2"), "02", "0f"),
                    ]),
            ],
            ["metadata-evidence"]);
    }

    private static FirmwareMetadataFieldDocument[] Fields()
    {
        return
        [
            new FirmwareMetadataFieldDocument("raw", Number("0"), Number("1"), "bytes"),
            new FirmwareMetadataFieldDocument("label", Number("1"), Number("1"), "printable-ascii"),
            new FirmwareMetadataFieldDocument(
                "chip-number",
                Number("2"),
                Number("1"),
                "unsigned-integer",
                "little",
                new FirmwareMetadataBitSliceDocument(Number("0"), Number("4"))),
            new FirmwareMetadataFieldDocument(
                "signed-offset",
                Number("3"),
                Number("1"),
                "signed-integer",
                "big"),
        ];
    }

    private static FirmwareImageMapDocument Map(
        FirmwareTopologyRequirementDocument topology,
        IReadOnlyList<FirmwareMetadataPredicateDocument> predicates)
    {
        return new FirmwareImageMapDocument(
            "map",
            "flash",
            new FirmwareMapApplicabilityDocument(
                ["NT00001"],
                ["standard"],
                topology,
                Number("16.0"),
                MetadataPredicates: predicates),
            "complete-with-explicit-gaps",
            ["physical"],
            ["metadata"],
            ["map-evidence"]);
    }

    private static FirmwareMetadataLocatorDocument AbsoluteLocator()
    {
        return new FirmwareMetadataLocatorDocument(
            "absolute-range",
            "config-region",
            Range: AddressedRange(0, 4));
    }

    private static FirmwareMetadataLocatorDocument MarkerLocator(
        FirmwareMarkerSelectionDocument selection)
    {
        return new FirmwareMetadataLocatorDocument(
            "marker-relative",
            "config-region",
            SearchRange: AddressedRange(8, 4),
            MarkerHex: "aa",
            Selection: selection,
            ResultOffset: Number("-8"));
    }

    private static FirmwareMapResolutionInputs Inputs(byte[] bytes)
    {
        return new FirmwareMapResolutionInputs(
            "NT00001",
            "standard",
            16,
            requestedTopology: null,
            [new FirmwareArtifactPayload("tp-firmware", bytes)]);
    }

    private static FirmwareByteRangeDocument Range(long start, long length)
    {
        return new FirmwareByteRangeDocument(
            Number(start.ToString(CultureInfo.InvariantCulture)),
            Number(length.ToString(CultureInfo.InvariantCulture)));
    }

    private static FirmwareAddressedRangeDocument AddressedRange(long start, long length)
    {
        return new FirmwareAddressedRangeDocument(
            "flash",
            Number(start.ToString(CultureInfo.InvariantCulture)),
            Number(length.ToString(CultureInfo.InvariantCulture)));
    }

    private static JsonElement Number(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static JsonElement Text(string value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }
}
