using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests that parsing each manifest and bundle document once keeps every load and projection verdict.</summary>
public sealed class ProfileBundleSingleParseTests
{
    private const int MaximumJsonDepth = 32;

    private const string SyntheticSchemaId =
        "https://example.invalid/nfc/schemas/synthetic-profile.schema.json";

    /// <summary>Verifies every projection of an accepted bundle carries the trees of a fresh strict parse.</summary>
    [Fact]
    public void AcceptedBundleProjectsTheTreesOfAFreshStrictParse()
    {
        byte[] family = Encoding.UTF8.GetBytes(TrustedV2BundleTestDocuments.FamilyJson("family"));
        byte[] profile = Encoding.UTF8.GetBytes(TrustedV2BundleTestDocuments.ProfileJson(Hash(family)));
        using TempWorkspace workspace = WriteBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            [
                new BundleFile(
                    "family-schema",
                    "schema",
                    "schemas/family.schema.json",
                    TrustedProfileBundleDocumentProjection.FirmwareFamilySchemaId,
                    ContractSchema("firmware-family-v1.schema.json")),
                new BundleFile(
                    "profile-schema",
                    "schema",
                    "schemas/profile.schema.json",
                    TrustedProfileBundleDocumentProjection.CompositionProfileSchemaId,
                    ContractSchema("composition-profile-v2.schema.json")),
                new BundleFile(
                    "family-entry",
                    "firmware-family",
                    "families/family.json",
                    TrustedProfileBundleDocumentProjection.FirmwareFamilySchemaId,
                    family),
                new BundleFile(
                    "profile-entry",
                    "composition-profile",
                    "profiles/profile.json",
                    TrustedProfileBundleDocumentProjection.CompositionProfileSchemaId,
                    profile),
            ]);
        TrustedProfileBundle bundle = Load(workspace, trustAnchor, MaximumJsonDepth);

        TrustedProfileBundleDocumentProjection first = bundle.CreateDocumentProjection();
        TrustedProfileBundleDocumentProjection second = bundle.CreateDocumentProjection();

        Assert.Equal(StrictRawText(family), Assert.Single(first.Families).Document.GetRawText());
        Assert.Equal(StrictRawText(profile), Assert.Single(first.Profiles).Document.GetRawText());
        Assert.Equal(StrictRawText(family), Assert.Single(second.Families).Document.GetRawText());
        Assert.Equal(StrictRawText(profile), Assert.Single(second.Profiles).Document.GetRawText());
    }

    /// <summary>Verifies a duplicate manifest key is still rejected with the strict reader's error.</summary>
    [Fact]
    public void LoadRejectsDuplicateManifestKeyWithTheStrictReaderError()
    {
        using TempWorkspace workspace = WriteSyntheticBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}"),
            manifestExtraProperty: "\"bundleId\": \"bundle\",");

        JsonException exception = Assert.Throws<JsonException>(() => Load(workspace, trustAnchor, MaximumJsonDepth));

        Assert.Equal("Duplicate JSON property 'bundleId'.", exception.Message);
    }

    /// <summary>Verifies a manifest deeper than the load limit is still rejected with the strict reader's error.</summary>
    [Fact]
    public void LoadRejectsManifestBeyondTheDepthLimitWithTheStrictReaderError()
    {
        using TempWorkspace workspace = WriteSyntheticBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}"));
        JsonException expected = StrictParseFailure(
            File.ReadAllBytes(workspace.PathFor("profile-bundle.json")),
            2);

        JsonException actual = Assert.ThrowsAny<JsonException>(() => Load(workspace, trustAnchor, 2));

        AssertSameFailure(expected, actual);
    }

    /// <summary>Verifies a duplicate entry key is still rejected by the load with the strict reader's error.</summary>
    [Fact]
    public void LoadRejectsDuplicateEntryKeyWithTheStrictReaderError()
    {
        JsonException exception = AssertLoadRejectsEntryLikeAStrictParse(
            Encoding.UTF8.GetBytes("{\"value\":1,\"value\":2}"));

        Assert.Equal("Duplicate JSON property 'value'.", exception.Message);
    }

    /// <summary>Verifies an entry deeper than the load limit is still rejected by the load with the strict reader's error.</summary>
    [Fact]
    public void LoadRejectsEntryBeyondTheDepthLimitWithTheStrictReaderError()
    {
        string nested = new string('[', MaximumJsonDepth + 8) + "1" + new string(']', MaximumJsonDepth + 8);

        _ = AssertLoadRejectsEntryLikeAStrictParse(Encoding.UTF8.GetBytes($"{{\"value\":{nested}}}"));
    }

    /// <summary>Verifies an entry outside its listed schema is still rejected with the same schema error.</summary>
    [Fact]
    public void LoadRejectsEntryOutsideItsSchemaWithTheSameSchemaError()
    {
        using TempWorkspace workspace = WriteSyntheticBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":\"wrong\"}"));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => Load(workspace, trustAnchor, MaximumJsonDepth));

        Assert.Equal(
            "Bundle schema validation failed for 'profiles/synthetic.json': " +
            $"Bundle document does not satisfy schema '{SyntheticSchemaId}'.",
            exception.Message);
    }

    /// <summary>
    /// Verifies a schema-valid canonical document that does not bind to its DTO is accepted by the load and
    /// rejected by every projection with the same error.
    /// </summary>
    [Theory]
    [InlineData("firmware-family", "families/family.json", /*lang=json,strict*/ "{\"unmappedMember\":true}", true)]
    [InlineData("composition-profile", "profiles/profile.json", /*lang=json,strict*/ "{\"unmappedMember\":true}", true)]
    [InlineData("firmware-family", "families/family.json", "null", false)]
    [InlineData("composition-profile", "profiles/profile.json", "null", false)]
    public void ProjectionRejectsSchemaValidDocumentThatDoesNotBindToItsDto(
        string kind,
        string path,
        string json,
        bool hasJsonCause)
    {
        string schemaId = StringComparer.Ordinal.Equals(kind, "firmware-family")
            ? TrustedProfileBundleDocumentProjection.FirmwareFamilySchemaId
            : TrustedProfileBundleDocumentProjection.CompositionProfileSchemaId;
        using TempWorkspace workspace = WriteBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            [
                new BundleFile(
                    "schema",
                    "schema",
                    "schemas/canonical.schema.json",
                    schemaId,
                    Encoding.UTF8.GetBytes(AnyInstanceSchema(schemaId))),
                new BundleFile("document", kind, path, schemaId, Encoding.UTF8.GetBytes(json)),
            ]);
        TrustedProfileBundle bundle = Load(workspace, trustAnchor, MaximumJsonDepth);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(bundle.CreateDocumentProjection);

            Assert.Equal(
                $"Bundle entry '{path}' cannot deserialize to its canonical document type.",
                exception.Message);
            if (hasJsonCause)
            {
                _ = Assert.IsType<JsonException>(exception.InnerException, exactMatch: false);
            }
            else
            {
                Assert.Null(exception.InnerException);
            }
        }
    }

    private static JsonException AssertLoadRejectsEntryLikeAStrictParse(byte[] document)
    {
        using TempWorkspace workspace = WriteSyntheticBundle(out ProfileBundleTrustAnchor trustAnchor, document);
        JsonException expected = StrictParseFailure(document, MaximumJsonDepth);

        JsonException actual = Assert.ThrowsAny<JsonException>(() => Load(workspace, trustAnchor, MaximumJsonDepth));

        AssertSameFailure(expected, actual);
        return actual;
    }

    private static void AssertSameFailure(JsonException expected, JsonException actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
    }

    private static JsonException StrictParseFailure(byte[] utf8Json, int maximumDepth)
    {
        return Assert.ThrowsAny<JsonException>(
            () => StrictJsonDocumentReader.Parse(utf8Json, utf8Json.Length, maximumDepth));
    }

    private static string StrictRawText(byte[] utf8Json)
    {
        using JsonDocument document = StrictJsonDocumentReader.Parse(utf8Json, utf8Json.Length, MaximumJsonDepth);
        return document.RootElement.GetRawText();
    }

    private static TrustedProfileBundle Load(
        TempWorkspace workspace,
        ProfileBundleTrustAnchor trustAnchor,
        int maximumJsonDepth)
    {
        return ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            trustAnchor,
            new ProfileBundleLoadLimits(
                16384,
                maximumJsonDepth,
                new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
    }

    private static TempWorkspace WriteSyntheticBundle(
        out ProfileBundleTrustAnchor trustAnchor,
        byte[] document,
        string manifestExtraProperty = "")
    {
        return WriteBundle(
            out trustAnchor,
            [
                new BundleFile(
                    "schema",
                    "schema",
                    "schemas/synthetic-profile.schema.json",
                    SyntheticSchemaId,
                    Encoding.UTF8.GetBytes(SyntheticSchema())),
                new BundleFile(
                    "profile",
                    "composition-profile",
                    "profiles/synthetic.json",
                    SyntheticSchemaId,
                    document),
            ],
            manifestExtraProperty);
    }

    private static TempWorkspace WriteBundle(
        out ProfileBundleTrustAnchor trustAnchor,
        BundleFile[] files,
        string manifestExtraProperty = "")
    {
        ProfileBundleEntryDocument[] entries = [.. files.Select(static file => new ProfileBundleEntryDocument(
            file.EntryId,
            file.Kind,
            file.Path,
            file.SchemaId,
            Hash(file.Content)))];
        string contentHash = ProfileBundleEntryArrayHasher.CalculateContentHash(entries);
        trustAnchor = new ProfileBundleTrustAnchor(contentHash, "release-manifest");

        var workspace = TempWorkspace.Create("nfc-profile-bundle-single-parse");
        foreach (BundleFile file in files)
        {
            _ = workspace.Write(file.Path, file.Content);
        }

        _ = workspace.Write(
            "profile-bundle.json",
            Encoding.UTF8.GetBytes(Manifest(entries, contentHash, manifestExtraProperty)));
        return workspace;
    }

    private static string Manifest(
        IEnumerable<ProfileBundleEntryDocument> entries,
        string contentHash,
        string extraProperty)
    {
        string entryJson = string.Join(',', entries.Select(static entry => $$"""
            {
              "entryId": "{{entry.EntryId}}",
              "kind": "{{entry.Kind}}",
              "path": "{{entry.Path}}",
              "schemaId": "{{entry.SchemaId}}",
              "contentHash": "{{entry.ContentHash}}"
            }
            """));
        return $$"""
            {
              "schemaVersion": "1.0",
              {{extraProperty}}
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "{{contentHash}}",
              "trustAnchorBindingId": "release-manifest",
              "entries": [{{entryJson}}]
            }
            """;
    }

    private static byte[] ContractSchema(string fileName)
    {
        return Encoding.UTF8.GetBytes(
            File.ReadAllText(RepositoryPaths.FromRepositoryRoot("docs", "contracts", fileName)));
    }

    private static string SyntheticSchema()
    {
        return /*lang=json,strict*/ $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SyntheticSchemaId}}",
              "type": "object",
              "additionalProperties": false,
              "required": ["value"],
              "properties": {
                "value": { "type": "integer" }
              }
            }
            """;
    }

    private static string AnyInstanceSchema(string schemaId)
    {
        return /*lang=json,strict*/ $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{schemaId}}"
            }
            """;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record BundleFile(string EntryId, string Kind, string Path, string SchemaId, byte[] Content);
}
