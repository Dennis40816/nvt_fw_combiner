using System.Text.Json;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests source-generated strict binding for canonical bundle DTO roots.</summary>
public sealed class ProfileBundleJsonContextTests
{
    /// <summary>Verifies one bundle manifest binds through generated metadata.</summary>
    [Fact]
    public void ContextDeserializesBundleManifest()
    {
        const string json = """
            {
              "schemaVersion": "1.0",
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "trustAnchorBindingId": "release-manifest",
              "entries": []
            }
            """;

        ProfileBundleDocument bundle = Assert.IsType<ProfileBundleDocument>(
            JsonSerializer.Deserialize(json, ProfileBundleJsonContext.Default.ProfileBundleDocument));

        Assert.Equal("bundle", bundle.BundleId);
        Assert.Empty(bundle.Entries);
    }

    /// <summary>Verifies generated metadata rejects unknown and case-mismatched members.</summary>
    [Theory]
    [InlineData("unexpected")]
    [InlineData("BundleId")]
    public void ContextRejectsUnmappedMembers(string propertyName)
    {
        string json = $$"""
            {
              "schemaVersion": "1.0",
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "trustAnchorBindingId": "release-manifest",
              "entries": [],
              "{{propertyName}}": true
            }
            """;

        _ = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(json, ProfileBundleJsonContext.Default.ProfileBundleDocument));
    }

    /// <summary>Verifies family and profile roots are included without reflection fallback.</summary>
    [Fact]
    public void ContextIncludesCanonicalContentRoots()
    {
        Assert.Equal(
            "NvtFwCombiner.Contracts.Firmware.FirmwareFamilyDocument",
            ProfileBundleJsonContext.Default.FirmwareFamilyDocument.Type.FullName);
        Assert.Equal(
            "NvtFwCombiner.Contracts.Profiles.CompositionProfileDocument",
            ProfileBundleJsonContext.Default.CompositionProfileDocument.Type.FullName);
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
            JsonSerializer.Deserialize(json, ProfileBundleJsonContext.Default.FirmwareFamilyDocument));

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
            JsonSerializer.Deserialize(json, ProfileBundleJsonContext.Default.FirmwareFamilyDocument));
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
            JsonSerializer.Deserialize(json, ProfileBundleJsonContext.Default.FirmwareFamilyDocument));
    }
}
