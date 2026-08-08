using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Tests the Bootstrap-only bridge from trusted bundle JSON to the normalized V2 catalog.</summary>
public sealed class TrustedProfileBundleCatalogProjectionTests
{
    private const string FirmwareFamilySchemaId =
        "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";

    private const string CompositionProfileSchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    /// <summary>Static catalog failures retain their exact typed code at the production compiler boundary.</summary>
    [Fact]
    public void BuiltInCompilerPreservesStaticCatalogFailureCodeAndProvenance()
    {
        using var workspace = TempWorkspace.Create("nfc-bootstrap-invalid-catalog");
        byte[] familySchema = ReadSchema("firmware-family-v1.schema.json");
        byte[] profileSchema = ReadSchema("composition-profile-v2.schema.json");
        byte[] family = Encoding.UTF8.GetBytes(TrustedV2BundleTestDocuments.FamilyJson());
        string familyHash = Hash(family);
        byte[] profile = Encoding.UTF8.GetBytes(
            TrustedV2BundleTestDocuments.ProfileJson(familyHash).Replace(
                "\"requiredMetadataStructureIds\": []",
                "\"requiredMetadataStructureIds\": [\"missing-structure\"]",
                StringComparison.Ordinal));
        var entries = new List<ProfileBundleEntryDocument>
        {
            new("family-schema", "schema", "schemas/family.schema.json", FirmwareFamilySchemaId, Hash(familySchema)),
            new("profile-schema", "schema", "schemas/profile.schema.json", CompositionProfileSchemaId, Hash(profileSchema)),
            new("family-entry", "firmware-family", "families/family.json", FirmwareFamilySchemaId, familyHash),
            new("profile-entry", "composition-profile", "profiles/profile.json", CompositionProfileSchemaId, Hash(profile)),
        };
        string bundleContentHash = ProfileBundleEntryArrayHasher.CalculateContentHash(entries);
        _ = workspace.Write("schemas/family.schema.json", familySchema);
        _ = workspace.Write("schemas/profile.schema.json", profileSchema);
        _ = workspace.Write("families/family.json", family);
        _ = workspace.Write("profiles/profile.json", profile);
        _ = workspace.Write(
            "profile-bundle.json",
            Encoding.UTF8.GetBytes(Manifest(entries, bundleContentHash)));
        var bundle = new BuiltInV2Bundle(
            workspace.Root,
            "1.0.0",
            bundleContentHash,
            "release-manifest");

        V2CompositionPlanCompileResult result = bundle.Compile(
            "profile",
            "1.0.0",
            "NT00001",
            "display-merge",
            requestedMapCapacity: 16,
            resolutionArtifacts: []);

        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile-bundle.catalog.profile-required-metadata-missing", issue.Code);
        Assert.Contains("profile-entry", issue.Message, StringComparison.Ordinal);
        Assert.Contains("profiles/profile.json", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies the bridge preserves trusted entry identity while Profiles owns semantic normalization.</summary>
    [Fact]
    public async Task CreateProjectsTrustedBundleIntoRuntimeArtifactAndExistingEngine()
    {
        using var workspace = TempWorkspace.Create("nfc-bootstrap-trusted-catalog");
        byte[] familySchema = ReadSchema("firmware-family-v1.schema.json");
        byte[] profileSchema = ReadSchema("composition-profile-v2.schema.json");
        string familyJson = TrustedV2BundleTestDocuments.FamilyJson()
            .Replace("\"writeConstraint\": \"forbidden\"", "\"writeConstraint\": \"whole-region\"", StringComparison.Ordinal);
        byte[] family = Encoding.UTF8.GetBytes(familyJson);
        string familyHash = Hash(family);
        string profileJson = TrustedV2BundleTestDocuments.ProfileJson(familyHash)
            .Replace("\"experienceId\": \"display-merge\"", "\"experienceId\": \"standard\"", StringComparison.Ordinal)
            .Replace("\"artifactClass\": \"tp-firmware\"", "\"artifactClass\": \"reference-image\"", StringComparison.Ordinal)
            .Replace(
                "\"lengthRule\": { \"kind\": \"tp-maximum-256k\", \"maximumBytes\": 262144 }",
                "\"lengthRule\": { \"kind\": \"exact-resolved-map-capacity\" }",
                StringComparison.Ordinal)
            .Replace("\"access\": \"read-only\"", "\"access\": \"whole\"", StringComparison.Ordinal);
        JsonObject profileNode = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonObject promotion = Assert.IsType<JsonObject>(profileNode["promotion"]);
        promotion["stage"] = "supported";
        promotion["blockers"] = new JsonArray();
        JsonObject output = Assert.IsType<JsonObject>(profileNode["output"]);
        output["fileNameTemplate"] = "v2-output.bin";
        output["invalidCharacterPolicy"] = "reject";
        output["requiredTokenIds"] = new JsonArray();
        profileJson = profileNode.ToJsonString();
        byte[] profile = Encoding.UTF8.GetBytes(profileJson);
        var entries = new List<ProfileBundleEntryDocument>
        {
            new("family-schema", "schema", "schemas/family.schema.json", FirmwareFamilySchemaId, Hash(familySchema)),
            new("profile-schema", "schema", "schemas/profile.schema.json", CompositionProfileSchemaId, Hash(profileSchema)),
            new("family-entry", "firmware-family", "families/family.json", FirmwareFamilySchemaId, familyHash),
            new("profile-entry", "composition-profile", "profiles/profile.json", CompositionProfileSchemaId, Hash(profile)),
        };
        string bundleContentHash = ProfileBundleEntryArrayHasher.CalculateContentHash(entries);
        _ = workspace.Write("schemas/family.schema.json", familySchema);
        _ = workspace.Write("schemas/profile.schema.json", profileSchema);
        _ = workspace.Write("families/family.json", family);
        _ = workspace.Write("profiles/profile.json", profile);
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes(Manifest(entries, bundleContentHash)));

        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(bundleContentHash, "release-manifest"),
            new ProfileBundleLoadLimits(
                16384,
                32,
                new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
        TrustedProfileBundleCatalog catalog = TrustedProfileBundleCatalogProjection.Create(
            bundle.CreateDocumentProjection());

        Assert.Equal("bundle", catalog.BundleIdentity.BundleId);
        Assert.Equal(bundleContentHash, catalog.BundleIdentity.ContentHash);
        TrustedFirmwareFamilyCatalogEntry familyEntry = Assert.Single(catalog.Families);
        Assert.Equal("family-entry", familyEntry.Identity.EntryId);
        Assert.Equal(familyHash, familyEntry.Family.FamilyContentHash);
        TrustedCompositionProfileCatalogEntry profileEntry = Assert.Single(catalog.Profiles);
        Assert.Equal("profile-entry", profileEntry.Identity.EntryId);
        Assert.Same(familyEntry, profileEntry.Family);
        Assert.Equal("map", Assert.Single(profileEntry.Profile.MapBinding.MapIds));

        TrustedCompositionProfileCatalogEntry selection = Assert.IsType<TrustedCompositionProfileCatalogEntry>(
            catalog.SelectProfile("profile", "1.0.0", out IReadOnlyList<CompositionIssue> selectionIssues));
        Assert.Empty(selectionIssues);
        bool admitted = V2CompositionPreparationService.PreparedCompilation.TryCreate(
            catalog,
            selection,
            new FirmwareMapResolutionInputs("NT00001", "standard", 16, requestedTopology: null, []),
            out V2CompositionPreparationService.PreparedCompilation? preparation,
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompositionIssue> preparationIssues);

        Assert.True(admitted);
        Assert.Equal(FirmwareMapResolutionStatus.Unique, mapResolution?.Status);
        Assert.Equal("map", mapResolution?.ResolvedMap?.ImageMap.MapId);
        Assert.Empty(Assert.IsType<V2CompositionPreparationService.PreparedCompilation>(preparation)
            .CapabilityAdmissions);
        Assert.Empty(preparationIssues);
        V2CompositionPlanCompileResult compilation = catalog.Compile(
            "profile",
            "1.0.0",
            "NT00001",
            "standard",
            requestedMapCapacity: 16);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.True(compilation.IsCompiled);
        Assert.Empty(compilation.Issues);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        Assert.Equal("root", Assert.Single(artifact.V2Details.RegionAccessContract.Requirements).RegionId);

        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]> { ["tp-artifact"] = [.. Enumerable.Range(0, 16).Select(static value => (byte)value)] }),
            new FakeClock([
                new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 12, 0, 0, 1, TimeSpan.Zero),
            ]));
        var request = new CompositionRunRequest(
            "trusted-v2-run",
            artifact,
            [new InputArtifactBinding(
                "tp-source",
                "tp-source",
                "tp-artifact",
                "input.bin",
                CompiledInputArtifactClass.ReferenceImage)],
            "v2-output.bin");

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(Enumerable.Range(0, 16).Select(static value => (byte)value).ToArray(), result.OutputBytes.ToArray());
        Assert.Equal(artifact.CompilationFingerprint, result.Report.CompilationFingerprint);
    }

    private static byte[] ReadSchema(string fileName)
    {
        return Encoding.UTF8.GetBytes(File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("docs", "contracts", fileName)));
    }

    private static string Manifest(IEnumerable<ProfileBundleEntryDocument> entries, string contentHash)
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
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "{{contentHash}}",
              "trustAnchorBindingId": "release-manifest",
              "entries": [{{entryJson}}]
            }
            """;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
