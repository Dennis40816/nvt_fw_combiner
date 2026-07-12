using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests offline Draft 2020-12 validation for closed bundle snapshots.</summary>
public sealed class ProfileBundleSchemaValidatorTests
{
    private const string SchemaId =
        "https://example.invalid/nfc/schemas/synthetic-profile.schema.json";

    /// <summary>Verifies a hash-verified schema validates a matching immutable content snapshot.</summary>
    [Fact]
    public void ValidateEntriesAcceptsMatchingClosedBundleContent()
    {
        ProfileBundleEntrySnapshotCollection collection = Capture(
            Schema("integer"),
            /*lang=json,strict*/ "{\"value\":1}");

        ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
    }

    /// <summary>Verifies instance validation rejects a value outside the declared schema.</summary>
    [Fact]
    public void ValidateEntriesRejectsContentOutsideDeclaredSchema()
    {
        ProfileBundleEntrySnapshotCollection collection = Capture(
            Schema("integer"),
            /*lang=json,strict*/ "{\"value\":\"wrong\"}");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));

        Assert.Contains("profiles/synthetic.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains(SchemaId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies format assertions are evaluated rather than treated as annotations.</summary>
    [Fact]
    public void ValidateEntriesRequiresValidFormats()
    {
        const string schema = /*lang=json,strict*/ """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.invalid/nfc/schemas/synthetic-profile.schema.json",
              "type": "object",
              "additionalProperties": false,
              "required": ["createdAt"],
              "properties": {
                "createdAt": { "type": "string", "format": "date-time" }
              }
            }
            """;
        ProfileBundleEntrySnapshotCollection collection = Capture(
            schema,
            /*lang=json,strict*/ "{\"createdAt\":\"not-a-date-time\"}");

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }

    /// <summary>Verifies schema identity and dialect cannot drift from the closed manifest declaration.</summary>
    [Theory]
    [InlineData("$id", "https://example.invalid/nfc/schemas/other.schema.json")]
    [InlineData("$schema", "https://json-schema.org/draft/2019-09/schema")]
    public void ValidateEntriesRejectsSchemaIdentityOrDialectDrift(string propertyName, string value)
    {
        string schema = Schema("integer").Replace(
            propertyName == "$id" ? SchemaId : "https://json-schema.org/draft/2020-12/schema",
            value,
            StringComparison.Ordinal);
        ProfileBundleEntrySnapshotCollection collection = Capture(schema, /*lang=json,strict*/ "{\"value\":1}");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));

        Assert.Contains("schemas/synthetic-profile.schema.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies the declared schema itself must satisfy the Draft 2020-12 meta-schema.</summary>
    [Fact]
    public void ValidateEntriesRejectsMalformedDraftSchema()
    {
        string schema = Schema("integer").Replace(
            "\"type\": \"object\"",
            "\"type\": 42",
            StringComparison.Ordinal);
        ProfileBundleEntrySnapshotCollection collection = Capture(schema, /*lang=json,strict*/ "{\"value\":1}");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));

        Assert.Contains("Draft 2020-12", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a schema cannot request external content through reference or nested resource identifiers.</summary>
    [Theory]
    [InlineData("$ref", "https://example.invalid/remote.schema.json")]
    [InlineData("$dynamicRef", "https://example.invalid/remote.schema.json#anchor")]
    [InlineData("$id", "https://example.invalid/nested.schema.json")]
    public void ValidateEntriesRejectsExternalSchemaResolution(string keyword, string value)
    {
        string schema = $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaId}}",
              "allOf": [
                { "{{keyword}}": "{{value}}" }
              ]
            }
            """;
        ProfileBundleEntrySnapshotCollection collection = Capture(schema, /*lang=json,strict*/ "{\"value\":1}");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));

        Assert.Contains(
            keyword == "$id" ? "nested $id" : "local fragment reference",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>Verifies the repository's v1.1 family schema remains a closed Draft 2020-12 schema.</summary>
    [Fact]
    public void ParseSchemaAcceptsRepositoryFirmwareFamilyV11Contract()
    {
        const string schemaId = "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";
        string path = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "firmware-family-v1.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        JsonSchema schema = ProfileBundleSchemaValidator.ParseSchema(path, schemaId, document.RootElement);

        Assert.NotNull(schema);
    }

    /// <summary>Verifies the repository family schema accepts one complete v1.1 alias shape and rejects drift.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("missing-target-fact")]
    [InlineData("wrong-discriminator")]
    [InlineData("unknown-property")]
    public void ValidateEntriesEnforcesRepositoryFirmwareFamilyV11AliasContract(string mutation)
    {
        string family = FirmwareFamilyV11AliasJson();
        family = mutation switch
        {
            "valid" => family,
            "missing-target-fact" => family.Replace(
                "\"targetCapabilityFactId\": \"target-capability\",",
                string.Empty,
                StringComparison.Ordinal),
            "wrong-discriminator" => family.Replace(
                "\"factKind\": \"capability\"",
                "\"factKind\": \"wrong\"",
                StringComparison.Ordinal),
            "unknown-property" => family.Replace(
                "\"reason\": \"synthetic alias\"",
                "\"unexpected\": true, \"reason\": \"synthetic alias\"",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown schema mutation."),
        };
        ProfileBundleEntrySnapshotCollection collection = CaptureFirmwareFamily(family);

        if (mutation == "valid")
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }

    /// <summary>Verifies the executable V2 schema rejects the TP-only maximum rule on another artifact class.</summary>
    [Fact]
    public void ValidateEntriesRejectsTpMaximumRuleForNonTpArtifact()
    {
        string profile = TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64)).Replace(
            "\"artifactClass\": \"tp-firmware\"",
            "\"artifactClass\": \"auxiliary\"",
            StringComparison.Ordinal);

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile),
            32));
    }

    /// <summary>Verifies the executable V2 schema bounds optional Normal-DP outer-container expectations.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("empty")]
    [InlineData("zero")]
    [InlineData("too-many")]
    public void ValidateEntriesEnforcesNormalDpExpectedContainerLengths(string mutation)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        slot["artifactClass"] = "dp-firmware";
        JsonObject acceptance = Assert.IsType<JsonObject>(slot["acceptance"]);
        var lengthRule = new JsonObject
        {
            ["kind"] = "normal-dp-extract-with-warning",
            ["issueCode"] = "DP_SIZE_WARNING",
        };
        var lengths = new JsonArray();
        switch (mutation)
        {
            case "valid":
                lengths.Add(0x80000);
                lengths.Add(0x200000);
                break;
            case "empty":
                break;
            case "zero":
                lengths.Add(0);
                break;
            case "too-many":
                for (int value = 1; value <= 9; value++)
                {
                    lengths.Add(value);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown schema mutation.");
        }

        lengthRule["expectedInputLengths"] = lengths;
        acceptance["lengthRule"] = lengthRule;

        ProfileBundleEntrySnapshotCollection collection = CaptureCompositionProfile(profile.ToJsonString());
        if (mutation == "valid")
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }

    private static ProfileBundleEntrySnapshotCollection Capture(string schema, string profile)
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-schema-validation");
        byte[] schemaBytes = Encoding.UTF8.GetBytes(schema);
        byte[] profileBytes = Encoding.UTF8.GetBytes(profile);
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write("schemas/synthetic-profile.schema.json", schemaBytes);
        _ = workspace.Write("profiles/synthetic.json", profileBytes);

        return ProfileBundleEntrySnapshotCollection.Capture(
            workspace.Root,
            "profile-bundle.json",
            new ProfileBundleManifest(
                "bundle",
                "1.0.0",
                new string('a', 64),
                "release-manifest",
                [
                    Entry(
                        "schema",
                        ProfileBundleEntryKind.Schema,
                        "schemas/synthetic-profile.schema.json",
                        schemaBytes),
                    Entry(
                        "profile",
                        ProfileBundleEntryKind.CompositionProfile,
                        "profiles/synthetic.json",
                        profileBytes),
                ]),
            new ProfileBundleEntrySnapshotLimits(8, 4096, 8192, 8));
    }

    private static ProfileBundleEntrySnapshotCollection CaptureFirmwareFamily(string family)
    {
        const string schemaId = "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";
        using var workspace = TempWorkspace.Create("nfc-firmware-family-v11-schema-validation");
        byte[] schemaBytes = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "firmware-family-v1.schema.json"));
        byte[] familyBytes = Encoding.UTF8.GetBytes(family);
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write("schemas/firmware-family-v1.schema.json", schemaBytes);
        _ = workspace.Write("families/family.json", familyBytes);

        return ProfileBundleEntrySnapshotCollection.Capture(
            workspace.Root,
            "profile-bundle.json",
            new ProfileBundleManifest(
                "bundle",
                "1.0.0",
                new string('a', 64),
                "release-manifest",
                [
                    Entry(
                        "schema",
                        ProfileBundleEntryKind.Schema,
                        "schemas/firmware-family-v1.schema.json",
                        schemaBytes,
                        schemaId),
                    Entry(
                        "family",
                        ProfileBundleEntryKind.FirmwareFamily,
                        "families/family.json",
                        familyBytes,
                        schemaId),
                ]),
            new ProfileBundleEntrySnapshotLimits(8, 65536, 131072, 32));
    }

    private static ProfileBundleEntrySnapshotCollection CaptureCompositionProfile(string profile)
    {
        const string schemaId = "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";
        using var workspace = TempWorkspace.Create("nfc-composition-profile-v2-schema-validation");
        byte[] schemaBytes = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "composition-profile-v2.schema.json"));
        byte[] profileBytes = Encoding.UTF8.GetBytes(profile);
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write("schemas/composition-profile-v2.schema.json", schemaBytes);
        _ = workspace.Write("profiles/profile.json", profileBytes);

        return ProfileBundleEntrySnapshotCollection.Capture(
            workspace.Root,
            "profile-bundle.json",
            new ProfileBundleManifest(
                "bundle",
                "1.0.0",
                new string('a', 64),
                "release-manifest",
                [
                    Entry(
                        "schema",
                        ProfileBundleEntryKind.Schema,
                        "schemas/composition-profile-v2.schema.json",
                        schemaBytes,
                        schemaId),
                    Entry(
                        "profile",
                        ProfileBundleEntryKind.CompositionProfile,
                        "profiles/profile.json",
                        profileBytes,
                        schemaId),
                ]),
            new ProfileBundleEntrySnapshotLimits(8, 65536, 131072, 32));
    }

    private static ProfileBundleEntry Entry(
        string entryId,
        ProfileBundleEntryKind kind,
        string path,
        byte[] bytes,
        string schemaId = SchemaId)
    {
        return new ProfileBundleEntry(entryId, kind, path, schemaId, Hash(bytes));
    }

    private static string Schema(string valueType)
    {
        return $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaId}}",
              "allOf": [
                { "$ref": "#/$defs/value" }
              ],
              "$defs": {
                "value": {
                  "type": "object",
                  "additionalProperties": false,
                  "required": ["value"],
                  "properties": {
                    "value": { "type": "{{valueType}}" }
                  }
                }
              }
            }
            """;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FirmwareFamilyV11AliasJson()
    {
        return /*lang=json,strict*/ """
            {
              "schemaVersion": "1.1",
              "familyId": "synthetic-family",
              "familyVersion": "1.0.0",
              "members": [
                { "memberId": "NT00001", "displayName": "Synthetic IC" }
              ],
              "capabilities": [],
              "regionSets": [
                {
                  "regionSetId": "physical",
                  "addressSpaceId": "flash",
                  "regions": [
                    {
                      "regionId": "root",
                      "owner": "system",
                      "kind": "image",
                      "range": { "start": 0, "length": 16 },
                      "writeConstraint": "forbidden",
                      "alignment": 1
                    }
                  ],
                  "evidenceRefs": ["region-evidence"]
                }
              ],
              "metadataSets": [],
              "imageMaps": [
                {
                  "mapId": "map",
                  "addressSpaceId": "flash",
                  "applicability": {
                    "memberIds": ["NT00001"],
                    "modeIds": ["standard"],
                    "topologyRequirement": { "kind": "none" },
                    "capacityBytes": 16
                  },
                  "coveragePolicy": "complete-with-explicit-gaps",
                  "regionSetIds": ["physical"],
                  "metadataSetIds": [],
                  "evidenceRefs": ["map-evidence"]
                }
              ],
              "factAliases": [
                {
                  "aliasId": "capability-alias",
                  "factKind": "capability",
                  "targetMemberId": "NT00001",
                  "targetMapId": "map",
                  "targetCapabilityFactId": "target-capability",
                  "sourceMemberId": "NT00001",
                  "sourceMapId": "map",
                  "sourceCapabilityFactId": "source-capability",
                  "applicability": {
                    "modeIds": ["standard"],
                    "topologyRequirement": { "kind": "none" },
                    "capacityBytes": 16
                  },
                  "reason": "synthetic alias",
                  "evidenceRefs": ["alias-evidence"]
                }
              ],
              "evidenceRefs": ["family-evidence"]
            }
            """;
    }
}
