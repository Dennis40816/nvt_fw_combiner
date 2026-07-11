using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests alias-free Profiles normalization of schema-shaped firmware family DTOs.</summary>
public sealed class FirmwareFamilyResolutionNormalizerTests
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    /// <summary>Verifies direct family facts and typed predicates normalize into one Domain definition.</summary>
    [Fact]
    public void NormalizeAliasFreeCreatesCandidateScopedDomainFacts()
    {
        FirmwareFamilyDocument document = Document();

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(document, FamilyHash);

        Assert.Equal("synthetic-family", definition.FamilyId);
        Assert.Equal("1.0.0", definition.FamilyVersion);
        Assert.Equal(FamilyHash, definition.FamilyContentHash);
        FirmwareImageMap map = Assert.Single(definition.ImageMaps);
        Assert.Equal("map", map.MapId);
        Assert.Equal(16, map.CapacityBytes);
        Assert.Equal(["physical"], map.RegionSets.Select(static set => set.RegionSetId));
        Assert.Equal(["metadata"], map.MetadataSetIds);
        Assert.Equal(["tp-firmware"], definition.RequiredArtifactBindingIds);
        FirmwareMetadataPredicate predicate = Assert.Single(map.Applicability.MetadataPredicates);
        Assert.Equal(FirmwareMetadataValue.FromUnsignedInteger(2), Assert.Single(predicate.ExpectedValues));

        byte[] bytes = new byte[16];
        bytes[0] = 0xAA;
        bytes[1] = (byte)'A';
        bytes[2] = 0xF2;
        bytes[3] = 0xFE;
        FirmwareMetadataStructureResolution resolution = definition.ResolveMetadataStructure(
            "map",
            "config",
            Inputs(bytes));
        FirmwareDecodedMetadataStructure decoded = Assert.IsType<FirmwareDecodedMetadataStructure>(
            resolution.Resolved?.DecodedStructure);
        var facts = decoded.Facts.ToDictionary(
            static fact => fact.FieldId,
            static fact => fact.Value,
            StringComparer.Ordinal);

        Assert.Equal("aa", facts["raw"].BytesValue?.Hex);
        Assert.Equal("A", facts["label"].TextValue);
        Assert.Equal(2UL, facts["chip-number"].UnsignedIntegerValue);
        Assert.Equal(-2, facts["signed-offset"].SignedIntegerValue);
        Assert.Equal(FirmwarePredicateResult.Match, predicate.Evaluate(facts));
    }

    /// <summary>Verifies all locator and topology tokens map to their closed Domain forms.</summary>
    [Fact]
    public void NormalizeAliasFreeMapsClosedLocatorAndTopologyShapes()
    {
        (FirmwareMetadataLocatorDocument Locator, FirmwareTopologyRequirementDocument Topology,
            FirmwareMetadataLocatorKind LocatorKind, TopologyRequirementKind TopologyKind)[] cases =
        [
            (AbsoluteLocator(), new FirmwareTopologyRequirementDocument("none"),
                FirmwareMetadataLocatorKind.AbsoluteRange, TopologyRequirementKind.None),
            (new FirmwareMetadataLocatorDocument(
                    "region-relative",
                    "config-region",
                    RegionId: "config-region",
                    Offset: Number("0")),
                new FirmwareTopologyRequirementDocument("single"),
                FirmwareMetadataLocatorKind.RegionRelative,
                TopologyRequirementKind.SingleChip),
            (MarkerLocator(new FirmwareMarkerSelectionDocument("unique")),
                new FirmwareTopologyRequirementDocument(
                    "cascade",
                    MinimumChipCount: Number("2"),
                    MaximumChipCount: Number("3")),
                FirmwareMetadataLocatorKind.MarkerRelative,
                TopologyRequirementKind.Cascade),
            (MarkerLocator(new FirmwareMarkerSelectionDocument(
                    "terminal-match",
                    "highest-address",
                    Number("1"))),
                new FirmwareTopologyRequirementDocument("exact-count", ChipCount: Number("3")),
                FirmwareMetadataLocatorKind.MarkerRelative,
                TopologyRequirementKind.ExactCount),
            (MarkerLocator(new FirmwareMarkerSelectionDocument(
                    "terminal-match",
                    "lowest-address",
                    Number("1"))),
                new FirmwareTopologyRequirementDocument("none"),
                FirmwareMetadataLocatorKind.MarkerRelative,
                TopologyRequirementKind.None),
        ];

        foreach ((FirmwareMetadataLocatorDocument locator, FirmwareTopologyRequirementDocument topology,
                     FirmwareMetadataLocatorKind locatorKind, TopologyRequirementKind topologyKind) in cases)
        {
            FirmwareFamilyResolutionDefinition definition =
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(
                    Document(locator: locator, topology: topology, includePredicate: false),
                    FamilyHash);
            FirmwareImageMap map = Assert.Single(definition.ImageMaps);
            FirmwareMetadataStructure structure = Assert.Single(definition.GetStructuresForMap("map"));

            Assert.Equal(locatorKind, structure.Locator.Kind);
            Assert.Equal("config-region", structure.Locator.AllowedResultRegionId);
            Assert.Equal(topologyKind, map.Applicability.TopologyRequirement.Kind);
            switch (structure.Locator)
            {
                case FirmwareAbsoluteRangeLocator absolute:
                    Assert.Equal("flash", absolute.Range.AddressSpaceId);
                    Assert.Equal(new ByteRange(0, 4), absolute.Range.Range);
                    break;
                case FirmwareRegionRelativeLocator relative:
                    Assert.Equal("config-region", relative.RegionId);
                    Assert.Equal(0, relative.Offset);
                    break;
                case FirmwareMarkerRelativeLocator marker:
                    Assert.Equal("flash", marker.SearchRange.AddressSpaceId);
                    Assert.Equal(new ByteRange(8, 4), marker.SearchRange.Range);
                    Assert.Equal("aa", marker.MarkerBytes.Hex);
                    Assert.Equal(-8, marker.ResultOffset);
                    if (locator.Selection?.Kind == "unique")
                    {
                        _ = Assert.IsType<FirmwareUniqueMarkerSelection>(marker.Selection);
                    }

                    if (marker.Selection is FirmwareTerminalMarkerSelection terminal)
                    {
                        Assert.Equal(1, terminal.ExpectedMatchCount);
                        Assert.Equal(
                            locator.Selection?.Terminal == "lowest-address"
                                ? FirmwareMarkerTerminal.LowestAddress
                                : FirmwareMarkerTerminal.HighestAddress,
                            terminal.Terminal);
                    }

                    break;
                default:
                    break;
            }

            if (topologyKind == TopologyRequirementKind.Cascade)
            {
                Assert.Equal(2, map.Applicability.TopologyRequirement.MinimumChipCount);
                Assert.Equal(3, map.Applicability.TopologyRequirement.MaximumChipCount);
            }

            if (topologyKind == TopologyRequirementKind.ExactCount)
            {
                Assert.Equal(3, map.Applicability.TopologyRequirement.ExactChipCount);
            }
        }
    }

    /// <summary>Verifies every closed physical enum token maps without fallback or alias inference.</summary>
    [Fact]
    public void NormalizeAliasFreeMapsEveryPhysicalEnumToken()
    {
        (string Token, FirmwareRegionOwner Value)[] owners =
        [
            ("system", FirmwareRegionOwner.System),
            ("dp", FirmwareRegionOwner.Dp),
            ("tp", FirmwareRegionOwner.Tp),
            ("ldc", FirmwareRegionOwner.Ldc),
            ("register", FirmwareRegionOwner.Register),
            ("customer", FirmwareRegionOwner.Customer),
            ("shared", FirmwareRegionOwner.Shared),
            ("reserved", FirmwareRegionOwner.Reserved),
            ("unknown", FirmwareRegionOwner.Unknown),
        ];
        (string Token, string Owner, FirmwareRegionKind Value)[] kinds =
        [
            ("image", "system", FirmwareRegionKind.Image),
            ("code", "system", FirmwareRegionKind.Code),
            ("header", "system", FirmwareRegionKind.Header),
            ("data", "system", FirmwareRegionKind.Data),
            ("command", "system", FirmwareRegionKind.Command),
            ("firmware-config", "system", FirmwareRegionKind.FirmwareConfig),
            ("ctrlram", "tp", FirmwareRegionKind.CtrlRam),
            ("customer-information", "customer", FirmwareRegionKind.CustomerInformation),
            ("checksum", "system", FirmwareRegionKind.Checksum),
            ("padding", "system", FirmwareRegionKind.Padding),
            ("reserved", "reserved", FirmwareRegionKind.Reserved),
            ("unmapped", "unknown", FirmwareRegionKind.Unmapped),
        ];
        (string Token, FirmwareWriteConstraint Value)[] constraints =
        [
            ("forbidden", FirmwareWriteConstraint.Forbidden),
            ("whole-region", FirmwareWriteConstraint.WholeRegion),
            ("declared-subregions", FirmwareWriteConstraint.DeclaredSubregions),
            ("explicit-range", FirmwareWriteConstraint.ExplicitRange),
        ];

        foreach ((string token, FirmwareRegionOwner expected) in owners)
        {
            FirmwareRegion region = NormalizeSingleRegion(token, "data", "forbidden");
            Assert.Equal(expected, region.Owner);
        }

        foreach ((string token, string owner, FirmwareRegionKind expected) in kinds)
        {
            FirmwareRegion region = NormalizeSingleRegion(owner, token, "forbidden");
            Assert.Equal(expected, region.Kind);
        }

        foreach ((string token, FirmwareWriteConstraint expected) in constraints)
        {
            FirmwareRegion region = NormalizeSingleRegion("system", "data", token);
            Assert.Equal(expected, region.WriteConstraint);
        }
    }

    /// <summary>Verifies all predicate operators and typed scalar kinds use exact field context.</summary>
    [Fact]
    public void NormalizeAliasFreeMapsEveryPredicateOperatorAndValueKind()
    {
        (FirmwareMetadataPredicateDocument Predicate, FirmwareMetadataPredicateOperator Comparison,
            FirmwareMetadataValue[] ExpectedValues)[] cases =
        [
            (new FirmwareMetadataPredicateDocument("config", "raw", "equals", [Text("aa")]),
                FirmwareMetadataPredicateOperator.Equal, [FirmwareMetadataValue.FromBytes([0xAA])]),
            (new FirmwareMetadataPredicateDocument("config", "label", "not-equals", [Text("B")]),
                FirmwareMetadataPredicateOperator.NotEqual, [FirmwareMetadataValue.FromText("B")]),
            (new FirmwareMetadataPredicateDocument(
                    "config",
                    "signed-offset",
                    "one-of",
                    [Number("-2"), Number("-1")]),
                FirmwareMetadataPredicateOperator.OneOf,
                [
                    FirmwareMetadataValue.FromSignedInteger(-2),
                    FirmwareMetadataValue.FromSignedInteger(-1),
                ]),
            (new FirmwareMetadataPredicateDocument("config", "chip-number", "equals", [Number("2")]),
                FirmwareMetadataPredicateOperator.Equal, [FirmwareMetadataValue.FromUnsignedInteger(2)]),
        ];

        foreach ((FirmwareMetadataPredicateDocument sourcePredicate,
                     FirmwareMetadataPredicateOperator comparison,
                     FirmwareMetadataValue[] expectedValues) in cases)
        {
            FirmwareFamilyResolutionDefinition definition =
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(
                    WithPredicate(Document(), sourcePredicate),
                    FamilyHash);
            FirmwareMetadataPredicate predicate =
                Assert.Single(Assert.Single(definition.ImageMaps).Applicability.MetadataPredicates);

            Assert.Equal(comparison, predicate.Comparison);
            Assert.Equal(expectedValues, predicate.ExpectedValues);
        }
    }

    /// <summary>Verifies predicates cannot use a globally known but unselected structure or unknown field.</summary>
    [Fact]
    public void NormalizeAliasFreeKeepsPredicateLookupCandidateScoped()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareMetadataSetDocument primarySet = Assert.Single(source.MetadataSets);
        FirmwareMetadataStructureDocument primaryStructure = Assert.Single(primarySet.Structures);
        FirmwareMetadataSetDocument otherSet = primarySet with
        {
            MetadataSetId = "other-metadata",
            Structures = [primaryStructure with { StructureId = "other-config" }],
        };
        FirmwareImageMapDocument primaryMap = Assert.Single(source.ImageMaps);
        FirmwareImageMapDocument otherMap = primaryMap with
        {
            MapId = "other-map",
            MetadataSetIds = ["other-metadata"],
            Applicability = primaryMap.Applicability with { MetadataPredicates = [] },
        };
        FirmwareFamilyDocument unselected = source with
        {
            MetadataSets = [primarySet, otherSet],
            ImageMaps =
            [
                primaryMap with
                {
                    Applicability = primaryMap.Applicability with
                    {
                        MetadataPredicates =
                        [
                            new FirmwareMetadataPredicateDocument(
                                "other-config",
                                "chip-number",
                                "equals",
                                [Number("2")]),
                        ],
                    },
                },
                otherMap,
            ],
        };
        FirmwareFamilyDocument unknownField = WithPredicate(
            source,
            new FirmwareMetadataPredicateDocument(
                "config",
                "missing-field",
                "equals",
                [Number("2")]));

        Assert.Equal(
            "imageMaps[0].applicability.metadataPredicates[0].metadataStructureId",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(unselected, FamilyHash)).Path);
        Assert.Equal(
            "imageMaps[0].applicability.metadataPredicates[0].fieldId",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(unknownField, FamilyHash)).Path);
    }

    /// <summary>Verifies unresolved aliases are rejected instead of losing source/provenance facts.</summary>
    [Fact]
    public void NormalizeAliasFreeRejectsAliasDeclarations()
    {
        FirmwareFamilyDocument document = Document() with
        {
            FactAliases =
            [
                new FirmwareFactAliasDocument(
                    "alias",
                    "metadata-set",
                    "NT00001",
                    "target-metadata",
                    "NT00001",
                    "metadata",
                    new FirmwareAliasApplicabilityDocument(
                        ["standard"],
                        new FirmwareTopologyRequirementDocument("none"),
                        Number("16")),
                    "synthetic alias",
                    ["alias-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(document, FamilyHash));

        Assert.Equal("factAliases", exception.Path);
    }

    /// <summary>Verifies map and capability member references must resolve to declared family members.</summary>
    [Fact]
    public void NormalizeAliasFreeRejectsUnknownMemberReferences()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareCapabilityDocument capability = Assert.Single(source.Capabilities);
        FirmwareFamilyDocument badMap = source with
        {
            ImageMaps = [map with
            {
                Applicability = map.Applicability with { MemberIds = ["NT99999"] },
            }],
        };
        FirmwareFamilyDocument badCapability = source with
        {
            Capabilities = [capability with { MemberIds = ["NT99999"] }],
        };

        FirmwareFamilyNormalizationException mapException = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(badMap, FamilyHash));
        FirmwareFamilyNormalizationException capabilityException =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(badCapability, FamilyHash));

        Assert.Equal("imageMaps[0].applicability.memberIds", mapException.Path);
        Assert.Equal("capabilities[0].memberIds", capabilityException.Path);
    }

    /// <summary>Verifies map references and normalized region facts cannot remain missing or orphaned.</summary>
    [Fact]
    public void NormalizeAliasFreeRejectsMissingAndOrphanFactReferences()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareRegionSetDocument regionSet = Assert.Single(source.RegionSets);
        FirmwareFamilyDocument missingRegion = source with
        {
            ImageMaps = [map with { RegionSetIds = ["missing"] }],
        };
        FirmwareFamilyDocument missingMetadata = source with
        {
            ImageMaps = [map with { MetadataSetIds = ["missing"] }],
        };
        FirmwareFamilyDocument orphanRegion = source with
        {
            RegionSets =
            [
                regionSet,
                regionSet with { RegionSetId = "orphan" },
            ],
        };

        Assert.Equal(
            "imageMaps[0].regionSetIds[0]",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(missingRegion, FamilyHash)).Path);
        Assert.Equal(
            "imageMaps[0].metadataSetIds[0]",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(missingMetadata, FamilyHash)).Path);
        Assert.Equal(
            "regionSets",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(orphanRegion, FamilyHash)).Path);
    }

    /// <summary>Verifies metadata orphans reject and shared facts retain one normalized Domain identity.</summary>
    [Fact]
    public void NormalizeAliasFreeClosesNormalizedFactGraph()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareMetadataSetDocument metadataSet = Assert.Single(source.MetadataSets);
        FirmwareMetadataStructureDocument structure = Assert.Single(metadataSet.Structures);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument orphanMetadata = source with
        {
            MetadataSets =
            [
                metadataSet,
                metadataSet with
                {
                    MetadataSetId = "orphan-metadata",
                    Structures = [structure with { StructureId = "orphan-config" }],
                },
            ],
        };
        FirmwareFamilyDocument sharedFacts = source with
        {
            ImageMaps =
            [
                map,
                map with { MapId = "map-b" },
            ],
        };

        Assert.Equal(
            "$",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(orphanMetadata, FamilyHash)).Path);

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(sharedFacts, FamilyHash);
        FirmwareImageMap[] maps = [.. definition.ImageMaps];
        Assert.Same(maps[0].RegionSets[0], maps[1].RegionSets[0]);
        Assert.Same(
            Assert.Single(definition.GetStructuresForMap(maps[0].MapId)),
            Assert.Single(definition.GetStructuresForMap(maps[1].MapId)));
    }

    /// <summary>Verifies capability evidence state never changes resolution-map eligibility.</summary>
    [Fact]
    public void NormalizeAliasFreeDoesNotUseCapabilitiesAsResolutionPolicy()
    {
        string[] states = ["confirmed-present", "confirmed-absent", "unknown"];
        foreach (string state in states)
        {
            FirmwareFamilyDocument source = Document();
            FirmwareCapabilityDocument capability = Assert.Single(source.Capabilities);
            FirmwareFamilyDocument document = source with
            {
                Capabilities = [capability with { State = state }],
            };

            FirmwareFamilyResolutionDefinition definition =
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(document, FamilyHash);

            Assert.Equal("map", Assert.Single(definition.ImageMaps).MapId);
            Assert.Equal(["tp-firmware"], definition.RequiredArtifactBindingIds);
        }
    }

    /// <summary>Verifies JSON integer conversion accepts integral forms and rejects fractions or Domain overflow.</summary>
    [Fact]
    public void NormalizeAliasFreeChecksNumericDomainBoundaries()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument fractional = source with
        {
            ImageMaps = [map with
            {
                Applicability = map.Applicability with { CapacityBytes = Number("16.5") },
            }],
        };
        FirmwareFamilyDocument overflow = source with
        {
            ImageMaps = [map with
            {
                Applicability = map.Applicability with
                {
                    CapacityBytes = Number("9223372036854775808"),
                },
            }],
        };
        FirmwareFamilyDocument roundedFraction = source with
        {
            ImageMaps = [map with
            {
                Applicability = map.Applicability with
                {
                    CapacityBytes = Number("16.0000000000000000000000000001"),
                },
            }],
        };
        FirmwareFamilyDocument scientificInteger = source with
        {
            ImageMaps = [map with
            {
                Applicability = map.Applicability with { CapacityBytes = Number("16e0") },
            }],
        };

        Assert.Equal(
            "imageMaps[0].applicability.capacityBytes",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(fractional, FamilyHash)).Path);
        Assert.Equal(
            "imageMaps[0].applicability.capacityBytes",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(overflow, FamilyHash)).Path);
        Assert.Equal(
            "imageMaps[0].applicability.capacityBytes",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(roundedFraction, FamilyHash)).Path);
        Assert.Equal(
            16,
            Assert.Single(FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(
                scientificInteger,
                FamilyHash).ImageMaps).CapacityBytes);
    }

    /// <summary>Verifies checked range overflow is path-wrapped with the original exception.</summary>
    [Fact]
    public void NormalizeAliasFreeWrapsCheckedRangeOverflow()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareRegionSetDocument regionSet = Assert.Single(source.RegionSets);
        FirmwareRegionDocument root = regionSet.Regions[0];
        FirmwareFamilyDocument overflow = source with
        {
            RegionSets = [regionSet with
            {
                Regions =
                [
                    root with { Range = new FirmwareByteRangeDocument(Number(long.MaxValue.ToString(CultureInfo.InvariantCulture)), Number("1")) },
                    .. regionSet.Regions.Skip(1),
                ],
            }],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(overflow, FamilyHash));

        Assert.Equal("regionSets[physical].regions[0]", exception.Path);
        _ = Assert.IsType<OverflowException>(exception.InnerException);
    }

    /// <summary>Verifies predicate JSON values are converted only in their exact field context.</summary>
    [Fact]
    public void NormalizeAliasFreeRejectsWrongPredicateKindAndRange()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareMetadataPredicateDocument predicate = Assert.Single(map.Applicability.MetadataPredicates!);
        FirmwareFamilyDocument wrongKind = WithPredicate(
            source,
            predicate with { ExpectedValues = [Text("2")] });
        FirmwareFamilyDocument outOfRange = WithPredicate(
            source,
            predicate with { ExpectedValues = [Number("16")] });

        Assert.Equal(
            "imageMaps[0].applicability.metadataPredicates[0].expectedValues[0]",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(wrongKind, FamilyHash)).Path);
        Assert.Equal(
            "imageMaps[0].applicability.metadataPredicates[0].expectedValues[0]",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(outOfRange, FamilyHash)).Path);
    }

    /// <summary>Verifies wrapped Domain invariant failures preserve path and inner exception.</summary>
    [Fact]
    public void NormalizeAliasFreePreservesDomainInvariantFailure()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument tooSmall = source with
        {
            ImageMaps = [map with
            {
                Applicability = map.Applicability with { CapacityBytes = Number("15") },
            }],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(tooSmall, FamilyHash));

        Assert.Equal("imageMaps[0]", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

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
            FirmwareFamilyResolutionNormalizer.NormalizeAliasFree(singleRegion, FamilyHash);
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
            "1.0",
            "synthetic-family",
            "1.0.0",
            [new FirmwareFamilyMemberDocument("NT00001", "Synthetic IC")],
            [
                new FirmwareCapabilityDocument(
                    "ab-code",
                    "confirmed-present",
                    ["NT00001"],
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
