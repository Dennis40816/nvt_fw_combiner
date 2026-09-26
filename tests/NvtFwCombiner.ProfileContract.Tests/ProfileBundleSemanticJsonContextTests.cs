using System.Text.Json;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests the one source-generated strict binding for canonical family and profile document roots.</summary>
public sealed class ProfileBundleSemanticJsonContextTests
{
    /// <summary>Verifies family and profile roots are included without reflection fallback.</summary>
    [Fact]
    public void ContextIncludesCanonicalContentRoots()
    {
        Assert.Equal(
            "NvtFwCombiner.Contracts.Firmware.FirmwareFamilyDocument",
            ProfileBundleSemanticJsonContext.Default.FirmwareFamilyDocument.Type.FullName);
        Assert.Equal(
            "NvtFwCombiner.Contracts.Profiles.CompositionProfileDocument",
            ProfileBundleSemanticJsonContext.Default.CompositionProfileDocument.Type.FullName);
    }

    /// <summary>Verifies generated firmware-family metadata accepts a non-leading v1.1 alias discriminator.</summary>
    [Fact]
    public void ContextDeserializesOutOfOrderMapBoundAlias()
    {
        const string json = """
            {
              "schemaVersion": "1.1",
              "familyId": "family",
              "familyVersion": "1.0.0",
              "members": [],
              "capabilities": [],
              "regionSets": [],
              "metadataSets": [],
              "imageMaps": [],
              "factAliases": [
                {
                  "aliasId": "alias",
                  "targetMemberId": "NT00001",
                  "factKind": "capability",
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
                  "reason": "synthetic alias",
                  "evidenceRefs": ["evidence"]
                }
              ],
              "evidenceRefs": []
            }
            """;

        FirmwareFamilyDocument family = Assert.IsType<FirmwareFamilyDocument>(
            JsonSerializer.Deserialize(json, ProfileBundleSemanticJsonContext.Default.FirmwareFamilyDocument));

        _ = Assert.IsType<FirmwareCapabilityAliasDocument>(Assert.Single(family.FactAliases));
    }

    /// <summary>Generated metadata accepts the one typed partial relationship form.</summary>
    [Fact]
    public void ContextDeserializesSharedFactRelationship()
    {
        const string json = """
            {
              "schemaVersion": "1.1",
              "familyId": "family",
              "familyVersion": "1.0.0",
              "members": [],
              "capabilities": [],
              "regionSets": [],
              "metadataSets": [],
              "imageMaps": [],
              "factAliases": [],
              "familyRelationships": [
                {
                  "relationshipId": "shared",
                  "memberIds": ["NT00001", "NT00002"],
                  "role": "tp-shared",
                  "applicability": { "mapIds": ["map-a", "map-b"] },
                  "sharedFactReferences": [
                    { "factKind": "region", "factId": "tp-code" },
                    {
                      "factKind": "metadata-definition",
                      "factId": "firmware-config-general-parameters"
                    }
                  ],
                  "reason": "synthetic exact sharing",
                  "evidenceRefs": ["evidence"],
                  "relationshipKind": "shared-fact-relationship"
                }
              ],
              "evidenceRefs": []
            }
            """;

        FirmwareFamilyDocument family = Assert.IsType<FirmwareFamilyDocument>(
            JsonSerializer.Deserialize(json, ProfileBundleSemanticJsonContext.Default.FirmwareFamilyDocument));
        FirmwareSharedFactRelationshipDocument relationship =
            Assert.IsType<FirmwareSharedFactRelationshipDocument>(
                Assert.Single(family.FamilyRelationships ?? []));

        Assert.Equal("tp-shared", relationship.Role);
        Assert.Equal(["map-a", "map-b"], relationship.Applicability.MapIds);
        Assert.Equal(
            [("region", "tp-code"), ("metadata-definition", "firmware-config-general-parameters")],
            relationship.SharedFactReferences.Select(static reference =>
                (reference.FactKind, reference.FactId)));
    }

    /// <summary>Dedicated legacy partial discriminators are no longer admitted.</summary>
    [Theory]
    [InlineData("initial-code-shared-family")]
    [InlineData("tp-shared-family")]
    public void ContextRejectsLegacyPartialRelationshipDiscriminators(string relationshipKind)
    {
        string json = $$"""
            {
              "schemaVersion": "1.1",
              "familyId": "family",
              "familyVersion": "1.0.0",
              "members": [],
              "capabilities": [],
              "regionSets": [],
              "metadataSets": [],
              "imageMaps": [],
              "factAliases": [],
              "familyRelationships": [
                {
                  "relationshipKind": "{{relationshipKind}}",
                  "relationshipId": "legacy",
                  "memberIds": ["NT00001", "NT00002"],
                  "sharedRegionIds": ["tp-code"],
                  "metadataDefinitionIds": [],
                  "reason": "legacy",
                  "evidenceRefs": ["evidence"]
                }
              ],
              "evidenceRefs": []
            }
            """;

        _ = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(json, ProfileBundleSemanticJsonContext.Default.FirmwareFamilyDocument));
    }
}
