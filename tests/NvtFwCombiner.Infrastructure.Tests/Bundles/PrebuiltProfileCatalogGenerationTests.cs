using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.PrebuiltProfileCatalogGeneration;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>The build tool must execute the same strict runtime admission and DTO gate.</summary>
public sealed class PrebuiltProfileCatalogGenerationTests
{
    /// <summary>Every failure leaves no newly generated or stale successful pack.</summary>
    [Theory]
    [InlineData("family-schema")]
    [InlineData("profile-schema")]
    [InlineData("schema-rejects-valid-dto")]
    [InlineData("invalid-schema")]
    [InlineData("dto-incompatible")]
    [InlineData("corrupt-family")]
    [InlineData("unknown-file")]
    [InlineData("anchor")]
    [InlineData("version")]
    [InlineData("aggregate")]
    [InlineData("index-whitespace")]
    [InlineData("manifest-whitespace")]
    [InlineData("bundle-id")]
    public void GeneratorRejectsInvalidAdmissionAndIdentityDrift(string mutation)
    {
        using TempWorkspace workspace = CreatePackage(mutation, out BuiltInProfileBuildAdmissionIdentity identity);
        string output = workspace.PathFor("output.pack");
        File.WriteAllText(output, "stale success");
        Exception? failure = Record.Exception(() => PrebuiltProfileCatalogGenerator.Generate(
            workspace.Root, workspace.PathFor("package-trust-index.json"), output, identity));
        Assert.NotNull(failure);
        string expectedMessage = mutation switch
        {
            "family-schema" or "profile-schema" or "schema-rejects-valid-dto" => "schema validation failed",
            "invalid-schema" => "schema",
            "dto-incompatible" => "cannot deserialize",
            "corrupt-family" => "content hash",
            "unknown-file" => "unlisted",
            "anchor" => "trust-anchor",
            "version" => "version",
            "aggregate" => "limit",
            _ => "compiled admission identity",
        };
        Assert.Contains(expectedMessage, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
        if (mutation is "index-whitespace" or "manifest-whitespace" or "bundle-id")
        {
            Assert.Contains("compiled admission identity", failure.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>The library re-admits and writes byte-identical output under a different culture.</summary>
    [Fact]
    public void GeneratorReproducesEveryRawDocumentAndManifestByte()
    {
        using TempWorkspace workspace = CreatePackage("valid", out BuiltInProfileBuildAdmissionIdentity identity);
        string first = workspace.PathFor("first.pack"), second = workspace.PathFor("second.pack");
        PrebuiltProfileCatalogGenerator.Generate(workspace.Root, workspace.PathFor("package-trust-index.json"), first, identity);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            PrebuiltProfileCatalogGenerator.Generate(workspace.Root, workspace.PathFor("package-trust-index.json"), second, identity);
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        PrebuiltProfileCatalogSnapshot snapshot = PrebuiltProfileCatalogCodec.Decode(File.ReadAllBytes(first));
        byte[] expected = [.. File.ReadAllBytes(workspace.PathFor("bundle/families/family.json")),
            .. File.ReadAllBytes(workspace.PathFor("bundle/profiles/profile.json")),
            .. File.ReadAllBytes(workspace.PathFor("bundle/profile-bundle.json"))];
        Assert.Equal(expected, snapshot.Body.ToArray());
        Assert.Equal(2, snapshot.Header.GetProperty("bundles")[0].GetProperty("documents").GetArrayLength());
    }

    /// <summary>The actual executable entry converts invalid admission into a stable failing exit.</summary>
    [Fact]
    public void GeneratorEntryReturnsFailureForInvalidRuntimeAdmission()
    {
        using TempWorkspace workspace = CreatePackage("valid", out _);
        File.WriteAllText(workspace.PathFor("package-trust-index.json"), "{}");
        string output = workspace.PathFor("entry.pack");
        File.WriteAllText(output, "stale");
        object? result = typeof(PrebuiltProfileCatalogGenerator).Assembly.EntryPoint!.Invoke(null,
            [new[] { workspace.Root, workspace.PathFor("package-trust-index.json"), output }]);
        Assert.Equal(1, Assert.IsType<int>(result));
        Assert.False(File.Exists(output));
    }

    private static TempWorkspace CreatePackage(string mutation, out BuiltInProfileBuildAdmissionIdentity identity)
    {
        var workspace = TempWorkspace.Create("prebuilt-generator");
        const string familyId = TrustedProfileBundleDocumentProjection.FirmwareFamilySchemaId;
        const string profileId = TrustedProfileBundleDocumentProjection.CompositionProfileSchemaId;
        string family = TrustedV2BundleTestDocuments.FamilyJson("family");
        string profile = TrustedV2BundleTestDocuments.ProfileJson(Hash(Encoding.UTF8.GetBytes(family)));
        string familySchema = File.ReadAllText(RepositoryPaths.FromRepositoryRoot("docs", "contracts", "firmware-family-v1.schema.json"));
        string profileSchema = File.ReadAllText(RepositoryPaths.FromRepositoryRoot("docs", "contracts", "composition-profile-v2.schema.json"));
        if (mutation == "family-schema") { family = "{}"; }
        if (mutation == "profile-schema") { profile = "{}"; }
        if (mutation == "schema-rejects-valid-dto")
        { familySchema = $$$"""{"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"{{{familyId}}}","not":{}}"""; }
        if (mutation == "invalid-schema") { familySchema = "{}"; }
        if (mutation == "dto-incompatible")
        {
            familySchema = $$$"""{"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"{{{familyId}}}","type":"object"}""";
            family = "{\"familyId\":42}";
        }
        if (mutation == "aggregate")
        {
            family += new string(' ', 100_000);
            profile += new string(' ', 100_000);
            familySchema += new string(' ', 70_000);
        }
        var entries = new List<ProfileBundleEntryDocument>();
        Add("family-schema", "schema", "schemas/family.schema.json", familyId, familySchema);
        Add("profile-schema", "schema", "schemas/profile.schema.json", profileId, profileSchema);
        Add("family", "firmware-family", "families/family.json", familyId, family);
        Add("profile", "composition-profile", "profiles/profile.json", profileId, profile);
        if (mutation == "aggregate")
        {
            long[] lengths = [.. entries.Select(e => new FileInfo(workspace.PathFor("bundle/" + e.Path)).Length)];
            Assert.All(lengths, length => Assert.InRange(length, 1, 131072));
            Assert.True(lengths.Sum() > 262144);
        }
        string contentHash = ProfileBundleEntryArrayHasher.CalculateContentHash(entries);
        var entryNodes = new JsonArray();
        foreach (ProfileBundleEntryDocument entry in entries)
        {
            entryNodes.Add(new JsonObject
            {
                ["entryId"] = entry.EntryId,
                ["kind"] = entry.Kind,
                ["path"] = entry.Path,
                ["schemaId"] = entry.SchemaId,
                ["contentHash"] = entry.ContentHash
            });
        }
        var manifest = new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["bundleId"] = "bundle",
            ["bundleVersion"] = "1.0.0",
            ["hashAlgorithm"] = "sha256-rfc8785-entry-array-v1",
            ["contentHash"] = contentHash,
            ["trustAnchorBindingId"] = mutation == "anchor" ? "wrong-anchor" : "test-anchor",
            ["entries"] = entryNodes
        };
        _ = workspace.Write("bundle/profile-bundle.json", Encoding.UTF8.GetBytes(manifest.ToJsonString()));
        string index = $$"""
            {"schemaVersion":"1.5","trustIndexId":"test-index","trustIndexVersion":"1.0.0","trustAnchorBindingId":"test-anchor",
             "bundles":[{"bundleDirectory":"bundle","bundleSchemaVersion":"1.0","bundleVersion":"{{(mutation == "version" ? "2.0.0" : "1.0.0")}}",
             "contentHash":"{{contentHash}}","materialization":{"compositionProfileSchemaFile":"composition-profile-v2.schema.json",
             "firmwareFamilySchemaFile":"firmware-family-v1.schema.json"},"runtimeRegistrations":[]}]}
            """;
        _ = workspace.Write("package-trust-index.json", Encoding.UTF8.GetBytes(index));
        identity = new(Hash(File.ReadAllBytes(workspace.PathFor("package-trust-index.json"))),
            BuiltInProfileBuildAdmissionIdentity.CalculateManifestSet([("bundle", Hash(File.ReadAllBytes(workspace.PathFor("bundle/profile-bundle.json"))))]));
        if (mutation == "unknown-file") { _ = workspace.Write("bundle/unknown.json", "{}"u8.ToArray()); }
        if (mutation == "corrupt-family") { File.AppendAllText(workspace.PathFor("bundle/families/family.json"), " "); }
        if (mutation == "index-whitespace") { File.AppendAllText(workspace.PathFor("package-trust-index.json"), " "); }
        if (mutation == "manifest-whitespace") { File.AppendAllText(workspace.PathFor("bundle/profile-bundle.json"), " "); }
        if (mutation == "bundle-id")
        {
            manifest["bundleId"] = "changed-bundle";
            File.WriteAllText(workspace.PathFor("bundle/profile-bundle.json"), manifest.ToJsonString());
        }
        return workspace;

        void Add(string id, string kind, string path, string schema, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            _ = workspace.Write("bundle/" + path, bytes);
            entries.Add(new(id, kind, path, schema, Hash(bytes)));
        }
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
