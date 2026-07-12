using System.Security.Cryptography;
using System.Text;
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

    private static ProfileBundleEntry Entry(
        string entryId,
        ProfileBundleEntryKind kind,
        string path,
        byte[] bytes)
    {
        return new ProfileBundleEntry(entryId, kind, path, SchemaId, Hash(bytes));
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
}
