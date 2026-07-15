using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Contracts.Firmware;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests strict JSON transport mapping for the firmware-family contract DTOs.</summary>
public sealed class FirmwareFamilyDocumentTests
{
    /// <summary>Verifies a complete schema-shaped family maps without scalar or union coercion.</summary>
    [Fact]
    public void CompleteFamilyJsonMapsToTransportDocument()
    {
        const string json = """
            {
              "schemaVersion": "1.1",
              "familyId": "synthetic-family",
              "familyVersion": "1.2.3",
              "members": [
                { "memberId": "NT00001", "displayName": "Synthetic IC" }
              ],
              "capabilities": [
                {
                  "capabilityFactId": "ab-code-evidence",
                  "capabilityId": "ab-code",
                  "memberId": "NT00001",
                  "mapId": "cascade-map",
                  "applicability": {
                    "modeIds": ["standard"],
                    "topologyRequirement": { "kind": "exact-count", "chipCount": 2 },
                    "capacityBytes": 32
                  },
                  "state": "confirmed-present",
                  "reason": "synthetic evidence",
                  "evidenceRefs": ["capability-evidence"]
                }
              ],
              "regionSets": [
                {
                  "regionSetId": "physical",
                  "addressSpaceId": "flash",
                  "regions": [
                    {
                      "regionId": "root",
                      "owner": "system",
                      "kind": "image",
                      "range": { "start": 0, "length": 32 },
                      "writeConstraint": "forbidden",
                      "alignment": 1
                    },
                    {
                      "regionId": "metadata",
                      "parentRegionId": "root",
                      "owner": "system",
                      "kind": "firmware-config",
                      "range": { "start": 0, "length": 16 },
                      "writeConstraint": "forbidden",
                      "alignment": 1
                    }
                  ],
                  "evidenceRefs": ["region-evidence"]
                }
              ],
              "metadataSets": [
                {
                  "metadataSetId": "metadata",
                  "structures": [
                    {
                      "structureId": "firmware-config",
                      "artifactBindingId": "tp-firmware",
                      "length": 8,
                      "locator": {
                        "kind": "marker-relative",
                        "searchRange": { "addressSpaceId": "flash", "start": 8, "length": 8 },
                        "markerHex": "004e5654",
                        "selection": {
                          "kind": "terminal-match",
                          "terminal": "highest-address",
                          "expectedMatchCount": 2
                        },
                        "resultOffset": -4,
                        "allowedResultRegionId": "metadata"
                      },
                      "fields": [
                        { "fieldId": "pid", "offset": 0, "widthBytes": 2, "encoding": "bytes" },
                        { "fieldId": "label", "offset": 2, "widthBytes": 2, "encoding": "printable-ascii" },
                        {
                          "fieldId": "chip-number",
                          "offset": 4,
                          "widthBytes": 1,
                          "encoding": "unsigned-integer",
                          "byteOrder": "little",
                          "bitSlice": { "leastSignificantBit": 0, "bitCount": 4 }
                        },
                        {
                          "fieldId": "signed-offset",
                          "offset": 5,
                          "widthBytes": 1,
                          "encoding": "signed-integer",
                          "byteOrder": "big"
                        }
                      ],
                      "assertions": [
                        { "offset": 0, "expectedHex": "aa" },
                        { "offset": 1, "expectedHex": "20", "maskHex": "f0" }
                      ]
                    }
                  ],
                  "evidenceRefs": ["metadata-evidence"]
                }
              ],
              "imageMaps": [
                {
                  "mapId": "cascade-map",
                  "addressSpaceId": "flash",
                  "applicability": {
                    "memberIds": ["NT00001"],
                    "modeIds": ["standard"],
                    "topologyRequirement": {
                      "kind": "cascade",
                      "minimumChipCount": 2,
                      "maximumChipCount": 3
                    },
                    "capacityBytes": 32,
                    "commonFirmwareCategoryIds": ["common-v2"],
                    "metadataPredicates": [
                      {
                        "metadataStructureId": "firmware-config",
                        "fieldId": "chip-number",
                        "operator": "equals",
                        "expectedValues": [2]
                      }
                    ]
                  },
                  "coveragePolicy": "complete-with-explicit-gaps",
                  "regionSetIds": ["physical"],
                  "metadataSetIds": ["metadata"],
                  "evidenceRefs": ["map-evidence"]
                }
              ],
              "factAliases": [
                {
                  "aliasId": "metadata-alias",
                  "factKind": "metadata-set",
                  "targetMemberId": "NT00001",
                  "targetMapId": "cascade-map",
                  "targetMetadataSetId": "target-metadata",
                  "sourceMemberId": "NT00001",
                  "sourceMapId": "cascade-map",
                  "sourceMetadataSetId": "metadata",
                  "applicability": {
                    "modeIds": ["standard"],
                    "topologyRequirement": { "kind": "exact-count", "chipCount": 2 },
                    "capacityBytes": 32,
                    "metadataPredicates": [
                      {
                        "metadataStructureId": "firmware-config",
                        "fieldId": "label",
                        "operator": "one-of",
                        "expectedValues": ["A ", "B "]
                      }
                    ]
                  },
                  "reason": "synthetic alias",
                  "evidenceRefs": ["alias-evidence"]
                }
              ],
              "evidenceRefs": ["family-evidence"]
            }
            """;

        FirmwareFamilyDocument document = Assert.IsType<FirmwareFamilyDocument>(
            JsonSerializer.Deserialize<FirmwareFamilyDocument>(json, StrictOptions()));

        Assert.Equal("1.1", document.SchemaVersion);
        Assert.Equal("synthetic-family", document.FamilyId);
        Assert.Equal("NT00001", Assert.Single(document.Members).MemberId);
        Assert.Equal("synthetic evidence", Assert.Single(document.Capabilities).Reason);
        FirmwareRegionDocument child = document.RegionSets[0].Regions[1];
        Assert.Equal("root", child.ParentRegionId);
        Assert.Equal(0, child.Range.Start.GetInt32());
        Assert.Equal(16, child.Range.Length.GetInt32());

        FirmwareMetadataStructureDocument structure = Assert.Single(document.MetadataSets[0].Structures);
        Assert.Equal("marker-relative", structure.Locator.Kind);
        Assert.Equal(-4, structure.Locator.ResultOffset?.GetInt32());
        Assert.Equal("004e5654", structure.Locator.MarkerHex);
        Assert.Equal("highest-address", structure.Locator.Selection?.Terminal);
        Assert.Equal(2, structure.Locator.Selection?.ExpectedMatchCount?.GetInt32());
        Assert.Equal("little", structure.Fields[2].ByteOrder);
        Assert.Equal(0, structure.Fields[2].BitSlice?.LeastSignificantBit.GetInt32());
        Assert.Equal(4, structure.Fields[2].BitSlice?.BitCount.GetInt32());
        Assert.Null(structure.Fields[0].ByteOrder);
        Assert.Equal("f0", structure.Assertions[1].MaskHex);

        FirmwareImageMapDocument map = Assert.Single(document.ImageMaps);
        Assert.Equal("cascade", map.Applicability.TopologyRequirement.Kind);
        Assert.Equal(2, map.Applicability.TopologyRequirement.MinimumChipCount?.GetInt32());
        JsonElement numericExpected = Assert.Single(
            Assert.Single(map.Applicability.MetadataPredicates!).ExpectedValues);
        Assert.Equal(JsonValueKind.Number, numericExpected.ValueKind);
        Assert.Equal(2, numericExpected.GetInt32());

        FirmwareCapabilityFactDocument capability = Assert.Single(document.Capabilities);
        Assert.Equal("ab-code-evidence", capability.CapabilityFactId);
        Assert.Equal("cascade-map", capability.MapId);
        Assert.Equal("exact-count", capability.Applicability.TopologyRequirement.Kind);

        FirmwareMetadataSetAliasDocument alias = Assert.IsType<FirmwareMetadataSetAliasDocument>(
            Assert.Single(document.FactAliases));
        Assert.Equal("cascade-map", alias.TargetMapId);
        Assert.Equal("target-metadata", alias.TargetMetadataSetId);
        Assert.Equal("metadata", alias.SourceMetadataSetId);
        Assert.Equal("exact-count", alias.Applicability.TopologyRequirement.Kind);
        Assert.Equal(2, alias.Applicability.TopologyRequirement.ChipCount?.GetInt32());
        JsonElement textExpected = alias.Applicability.MetadataPredicates![0].ExpectedValues[0];
        Assert.Equal(JsonValueKind.String, textExpected.ValueKind);
        Assert.Equal("A ", textExpected.GetString());
    }

    /// <summary>Verifies all locator and topology union shapes preserve only declared transport fields.</summary>
    [Fact]
    public void UnionShapesMapWithoutInventingFields()
    {
        const string locatorsJson = """
            [
              {
                "kind": "absolute-range",
                "range": { "addressSpaceId": "flash", "start": 4, "length": 2 },
                "allowedResultRegionId": "metadata"
              },
              {
                "kind": "region-relative",
                "regionId": "metadata",
                "offset": 2,
                "allowedResultRegionId": "metadata"
              },
              {
                "kind": "marker-relative",
                "searchRange": { "addressSpaceId": "flash", "start": 0, "length": 8 },
                "markerHex": "aa",
                "selection": { "kind": "unique" },
                "resultOffset": 0,
                "allowedResultRegionId": "metadata"
              }
            ]
            """;
        const string topologyJson = """
            [
              { "kind": "none" },
              { "kind": "single" },
              { "kind": "cascade", "minimumChipCount": 2 },
              { "kind": "exact-count", "chipCount": 3 }
            ]
            """;
        const string fieldsJson = """
            [
              { "fieldId": "raw", "offset": 0, "widthBytes": 2, "encoding": "bytes" },
              { "fieldId": "text", "offset": 2, "widthBytes": 2, "encoding": "printable-ascii" },
              {
                "fieldId": "unsigned",
                "offset": 4,
                "widthBytes": 1,
                "encoding": "unsigned-integer",
                "byteOrder": "little",
                "bitSlice": { "leastSignificantBit": 0, "bitCount": 4 }
              },
              {
                "fieldId": "signed",
                "offset": 5,
                "widthBytes": 1,
                "encoding": "signed-integer",
                "byteOrder": "big"
              }
            ]
            """;
        const string assertionsJson = """
            [
              { "offset": 0, "expectedHex": "aa" },
              { "offset": 1, "expectedHex": "20", "maskHex": "f0" }
            ]
            """;
        const string selectionsJson = """
            [
              { "kind": "unique" },
              { "kind": "terminal-match", "terminal": "highest-address", "expectedMatchCount": 2 }
            ]
            """;

        FirmwareMetadataLocatorDocument[] locators = Assert.IsType<FirmwareMetadataLocatorDocument[]>(
            JsonSerializer.Deserialize<FirmwareMetadataLocatorDocument[]>(locatorsJson, StrictOptions()));
        FirmwareTopologyRequirementDocument[] topologies =
            Assert.IsType<FirmwareTopologyRequirementDocument[]>(
                JsonSerializer.Deserialize<FirmwareTopologyRequirementDocument[]>(topologyJson, StrictOptions()));
        FirmwareMetadataFieldDocument[] fields = Assert.IsType<FirmwareMetadataFieldDocument[]>(
            JsonSerializer.Deserialize<FirmwareMetadataFieldDocument[]>(fieldsJson, StrictOptions()));
        FirmwareByteAssertionDocument[] assertions = Assert.IsType<FirmwareByteAssertionDocument[]>(
            JsonSerializer.Deserialize<FirmwareByteAssertionDocument[]>(assertionsJson, StrictOptions()));
        FirmwareMarkerSelectionDocument[] selections = Assert.IsType<FirmwareMarkerSelectionDocument[]>(
            JsonSerializer.Deserialize<FirmwareMarkerSelectionDocument[]>(selectionsJson, StrictOptions()));

        Assert.NotNull(locators[0].Range);
        Assert.Null(locators[0].RegionId);
        Assert.Equal(2, locators[1].Offset?.GetInt32());
        Assert.Null(locators[1].Range);
        Assert.Equal("unique", locators[2].Selection?.Kind);
        Assert.Null(locators[2].Selection?.Terminal);
        Assert.Equal(["none", "single", "cascade", "exact-count"],
            topologies.Select(static topology => topology.Kind));
        Assert.Null(topologies[0].ChipCount);
        Assert.Equal(3, topologies[3].ChipCount?.GetInt32());

        AssertUnionPropertyShapes(locators, topologies, fields, assertions, selections);
    }

    /// <summary>Verifies schema-valid integers beyond Int64 remain lossless transport values.</summary>
    [Fact]
    public void NumericTransportPreservesIntegerBeyondInt64()
    {
        const string json = """
            { "start": 9223372036854775808, "length": 18446744073709551616 }
            """;

        FirmwareByteRangeDocument range = Assert.IsType<FirmwareByteRangeDocument>(
            JsonSerializer.Deserialize<FirmwareByteRangeDocument>(json, StrictOptions()));
        string roundTrip = JsonSerializer.Serialize(range, StrictOptions());
        using var roundTripDocument = JsonDocument.Parse(roundTrip);

        Assert.Equal("9223372036854775808", range.Start.GetRawText());
        Assert.Equal("18446744073709551616", range.Length.GetRawText());
        Assert.Equal(
            "9223372036854775808",
            roundTripDocument.RootElement.GetProperty("start").GetRawText());
        Assert.Equal(
            "18446744073709551616",
            roundTripDocument.RootElement.GetProperty("length").GetRawText());
    }

    /// <summary>Verifies the transport mapping rejects unknown JSON members rather than ignoring them.</summary>
    [Fact]
    public void StrictTransportOptionsRejectUnknownMembers()
    {
        const string json = """
            {
              "memberId": "NT00001",
              "displayName": "Synthetic IC",
              "unexpected": true
            }
            """;

        _ = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<FirmwareFamilyMemberDocument>(json, StrictOptions()));
    }

    /// <summary>Verifies the three closed map-bound alias shapes deserialize without declaration-order inference.</summary>
    [Fact]
    public void FactAliasShapesMapToClosedTransportTypes()
    {
        const string json = """
            [
              {
                "aliasId": "region-alias",
                "targetMemberId": "NT00001",
                "factKind": "region-set",
                "targetMapId": "target-map",
                "targetRegionSetId": "target-regions",
                "sourceMemberId": "NT00002",
                "sourceMapId": "source-map",
                "sourceRegionSetId": "source-regions",
                "applicability": {
                  "modeIds": ["standard"],
                  "topologyRequirement": { "kind": "none" },
                  "capacityBytes": 16
                },
                "reason": "synthetic region alias",
                "evidenceRefs": ["region-evidence"]
              },
              {
                "factKind": "metadata-set",
                "aliasId": "metadata-alias",
                "targetMemberId": "NT00001",
                "targetMapId": "target-map",
                "targetMetadataSetId": "target-metadata",
                "sourceMemberId": "NT00002",
                "sourceMapId": "source-map",
                "sourceMetadataSetId": "source-metadata",
                "applicability": {
                  "modeIds": ["standard"],
                  "topologyRequirement": { "kind": "none" },
                  "capacityBytes": 16
                },
                "reason": "synthetic metadata alias",
                "evidenceRefs": ["metadata-evidence"]
              },
              {
                "factKind": "capability",
                "aliasId": "capability-alias",
                "targetMemberId": "NT00001",
                "targetMapId": "target-map",
                "targetCapabilityFactId": "target-capability",
                "sourceMemberId": "NT00002",
                "sourceMapId": "source-map",
                "sourceCapabilityFactId": "source-capability",
                "applicability": {
                  "modeIds": ["standard"],
                  "topologyRequirement": { "kind": "none" },
                  "capacityBytes": 16
                },
                "reason": "synthetic capability alias",
                "evidenceRefs": ["capability-evidence"]
              }
            ]
            """;

        FirmwareFactAliasDocument[] aliases = Assert.IsType<FirmwareFactAliasDocument[]>(
            JsonSerializer.Deserialize<FirmwareFactAliasDocument[]>(json, StrictOptions()));

        _ = Assert.IsType<FirmwareRegionSetAliasDocument>(aliases[0]);
        _ = Assert.IsType<FirmwareMetadataSetAliasDocument>(aliases[1]);
        _ = Assert.IsType<FirmwareCapabilityAliasDocument>(aliases[2]);
    }

    private static JsonSerializerOptions StrictOptions()
    {
        return new JsonSerializerOptions
        {
            AllowOutOfOrderMetadataProperties = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
    }

    private static void AssertUnionPropertyShapes(
        FirmwareMetadataLocatorDocument[] locators,
        FirmwareTopologyRequirementDocument[] topologies,
        FirmwareMetadataFieldDocument[] fields,
        FirmwareByteAssertionDocument[] assertions,
        FirmwareMarkerSelectionDocument[] selections)
    {
        using JsonDocument locatorJson = SerializeToDocument(locators);
        using JsonDocument topologyJson = SerializeToDocument(topologies);
        using JsonDocument fieldJson = SerializeToDocument(fields);
        using JsonDocument assertionJson = SerializeToDocument(assertions);
        using JsonDocument selectionJson = SerializeToDocument(selections);

        AssertPropertyNames(locatorJson.RootElement[0], "allowedResultRegionId", "kind", "range");
        AssertPropertyNames(
            locatorJson.RootElement[1],
            "allowedResultRegionId",
            "kind",
            "offset",
            "regionId");
        AssertPropertyNames(
            locatorJson.RootElement[2],
            "allowedResultRegionId",
            "kind",
            "markerHex",
            "resultOffset",
            "searchRange",
            "selection");
        AssertPropertyNames(topologyJson.RootElement[0], "kind");
        AssertPropertyNames(topologyJson.RootElement[1], "kind");
        AssertPropertyNames(topologyJson.RootElement[2], "kind", "minimumChipCount");
        AssertPropertyNames(topologyJson.RootElement[3], "chipCount", "kind");
        AssertPropertyNames(fieldJson.RootElement[0], "encoding", "fieldId", "offset", "widthBytes");
        AssertPropertyNames(fieldJson.RootElement[1], "encoding", "fieldId", "offset", "widthBytes");
        AssertPropertyNames(
            fieldJson.RootElement[2],
            "bitSlice",
            "byteOrder",
            "encoding",
            "fieldId",
            "offset",
            "widthBytes");
        AssertPropertyNames(
            fieldJson.RootElement[3],
            "byteOrder",
            "encoding",
            "fieldId",
            "offset",
            "widthBytes");
        AssertPropertyNames(assertionJson.RootElement[0], "expectedHex", "offset");
        AssertPropertyNames(assertionJson.RootElement[1], "expectedHex", "maskHex", "offset");
        AssertPropertyNames(selectionJson.RootElement[0], "kind");
        AssertPropertyNames(selectionJson.RootElement[1], "expectedMatchCount", "kind", "terminal");
    }

    private static JsonDocument SerializeToDocument<T>(T value)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(value, StrictOptions()));
    }

    private static void AssertPropertyNames(JsonElement element, params string[] expectedNames)
    {
        Assert.Equal(
            expectedNames.Order(StringComparer.Ordinal),
            element.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal));
    }
}
