using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies only exact id/version lookup creates a catalog-owned selection and admitted map context.</summary>
    [Fact]
    public void PreparationSelectsAnExactTrustedProfileAndAdmitsItsUniqueMap()
    {
        TrustedProfileBundleCatalog catalog = CreateCatalog();

        TrustedProfileBundleCatalog.ProfileSelectionResult selectionResult = catalog.SelectProfile("profile", "1.0.0");
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            selectionResult.Selection);
        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            Request(selection));

        Assert.True(selectionResult.IsSelected);
        Assert.Empty(selectionResult.Issues);
        Assert.Equal(BundleHash, selection.BundleIdentity.ContentHash);
        Assert.Equal("profile-entry", selection.ProfileEntryIdentity.EntryId);
        Assert.Equal("profile", selection.ProfileId);
        Assert.Equal("1.0.0", selection.ProfileVersion);
        Assert.True(preparation.IsAdmitted);
        Assert.Equal(V2CompositionPreparationStatus.Admitted, preparation.Status);
        Assert.Same(selection, preparation.Selection);
        Assert.Equal(FirmwareMapResolutionStatus.Unique, preparation.MapResolution?.Status);
        Assert.Equal("map", preparation.Admission?.ResolvedMap.ImageMap.MapId);
        Assert.Empty(preparation.Issues);
    }

    /// <summary>Verifies profile version lookup cannot fall back to a different declaration or latest version.</summary>
    [Fact]
    public void SelectionRejectsUnknownProfileVersionWithoutFallback()
    {
        TrustedProfileBundleCatalog.ProfileSelectionResult selection = CreateCatalog().SelectProfile("profile", "2.0.0");

        Assert.False(selection.IsSelected);
        Assert.Null(selection.Selection);
        Assert.Equal("profile.v2.selection.not-found", Assert.Single(selection.Issues).Code);
    }

    /// <summary>Verifies friend assemblies cannot forge a catalog selection without its private minting token.</summary>
    [Fact]
    public void SelectionRejectsAnUntrustedMintingToken()
    {
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            CreateCatalog().SelectProfile("profile", "1.0.0").Selection);

        _ = Assert.Throws<ArgumentException>(() => new TrustedProfileBundleCatalog.ProfileSelection(
            new object(),
            selection.BundleIdentity,
            selection.ProfileEntryIdentity,
            selection.ProfileId,
            selection.ProfileVersion));
    }

    /// <summary>Verifies a catalog-minted token cannot be reused against a different trusted bundle identity.</summary>
    [Fact]
    public void PreparationRejectsAStaleBundleSelectionBeforeMapResolution()
    {
        TrustedProfileBundleCatalog source = CreateCatalog();
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            source.SelectProfile("profile", "1.0.0").Selection);
        TrustedProfileBundleCatalog current = CreateCatalog(bundleContentHash: new('c', 64));

        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            current,
            Request(selection));

        Assert.Equal(V2CompositionPreparationStatus.SelectionRejected, preparation.Status);
        Assert.Null(preparation.Selection);
        Assert.Null(preparation.MapResolution);
        Assert.Null(preparation.Admission);
        Assert.Equal("profile.v2.selection.stale", Assert.Single(preparation.Issues).Code);
    }

    /// <summary>Verifies a topology prerequisite remains the Domain resolver's typed pending outcome.</summary>
    [Fact]
    public void PreparationPreservesPendingMapTopologyRequirement()
    {
        string familyJson = TrustedV2BundleTestDocuments.FamilyJson().Replace(
            "\"topologyRequirement\": { \"kind\": \"none\" }",
            "\"topologyRequirement\": { \"kind\": \"single\" }",
            StringComparison.Ordinal);
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson: familyJson);
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            catalog.SelectProfile("profile", "1.0.0").Selection);

        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            Request(selection));

        Assert.Equal(V2CompositionPreparationStatus.MapPending, preparation.Status);
        FirmwareMapResolutionResult resolution = Assert.IsType<FirmwareMapResolutionResult>(preparation.MapResolution);
        Assert.Equal(FirmwareMapResolutionStatus.Pending, resolution.Status);
        Assert.Equal(
            FirmwareMapResolutionPendingKind.RequestedTopologyMissing,
            Assert.Single(resolution.PendingRequirements).Kind);
        Assert.Null(preparation.Admission);
        Assert.Empty(preparation.Issues);
    }

    /// <summary>Verifies a required metadata artifact remains a typed Domain pending requirement.</summary>
    [Fact]
    public void PreparationPreservesPendingMissingArtifactRequirement()
    {
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson: FamilyJsonRequiringArtifact());
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            catalog.SelectProfile("profile", "1.0.0").Selection);

        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            Request(selection));

        Assert.Equal(V2CompositionPreparationStatus.MapPending, preparation.Status);
        FirmwareMapResolutionPendingRequirement requirement = Assert.Single(
            Assert.IsType<FirmwareMapResolutionResult>(preparation.MapResolution).PendingRequirements);
        Assert.Equal(FirmwareMapResolutionPendingKind.ArtifactMissing, requirement.Kind);
        Assert.Equal("tp-firmware", requirement.ArtifactBindingId);
        Assert.Null(preparation.Admission);
    }

    /// <summary>Verifies no matching map remains distinct from selection and admission failures.</summary>
    [Fact]
    public void PreparationPreservesNoMatchingMapRejection()
    {
        TrustedProfileBundleCatalog catalog = CreateCatalog();
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            catalog.SelectProfile("profile", "1.0.0").Selection);

        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            Request(selection, capacityBytes: 17));

        Assert.Equal(V2CompositionPreparationStatus.MapRejected, preparation.Status);
        FirmwareMapResolutionResult resolution = Assert.IsType<FirmwareMapResolutionResult>(preparation.MapResolution);
        Assert.Equal(FirmwareMapResolutionStatus.Rejected, resolution.Status);
        Assert.Equal(FirmwareMapResolutionRejectionKind.NoMatchingMap, resolution.RejectionKind);
        Assert.Null(preparation.Admission);
        Assert.Empty(preparation.Issues);
    }

    /// <summary>Verifies multiple fully matching maps remain an ambiguous resolver rejection before admission.</summary>
    [Fact]
    public void PreparationPreservesAmbiguousMapRejection()
    {
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson: FamilyJsonWithAmbiguousMap());
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            catalog.SelectProfile("profile", "1.0.0").Selection);

        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            Request(selection));

        Assert.Equal(V2CompositionPreparationStatus.MapRejected, preparation.Status);
        FirmwareMapResolutionResult resolution = Assert.IsType<FirmwareMapResolutionResult>(preparation.MapResolution);
        Assert.Equal(FirmwareMapResolutionRejectionKind.AmbiguousMaps, resolution.RejectionKind);
        Assert.Null(preparation.Admission);
        Assert.Empty(preparation.Issues);
    }

    /// <summary>Verifies profile metadata requirements are admitted only after the selected map is unique.</summary>
    [Fact]
    public void PreparationReturnsExistingAdmissionIssuesWithoutCreatingAPlan()
    {
        string familyJson = TrustedV2BundleTestDocuments.FamilyJson();
        string familyHash = Hash(familyJson);
        string profileJson = TrustedV2BundleTestDocuments.ProfileJson(familyHash).Replace(
            "\"requiredMetadataStructureIds\": []",
            "\"requiredMetadataStructureIds\": [\"missing-b\", \"missing-a\"]",
            StringComparison.Ordinal);
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            catalog.SelectProfile("profile", "1.0.0").Selection);

        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            Request(selection));

        Assert.Equal(V2CompositionPreparationStatus.AdmissionRejected, preparation.Status);
        Assert.Equal(FirmwareMapResolutionStatus.Unique, preparation.MapResolution?.Status);
        Assert.Null(preparation.Admission);
        Assert.Equal(
            ["profile.v2.map.required-metadata-structure-missing", "profile.v2.map.required-metadata-structure-missing"],
            preparation.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies the Profiles-owned compiler facade resolves the exact canonical map and rejects a mismatched experience.</summary>
    [Fact]
    public void TrustedCompilerUsesSelectedProfileExperienceAndCanonicalMap()
    {
        string familyJson = FamilyJsonWithRootWriteConstraint("whole-region");
        string profileJson = RuntimeSupportedProfileJson(Hash(familyJson)).Replace(
            "\"experienceId\": \"display-merge\"",
            "\"experienceId\": \"standard\"",
            StringComparison.Ordinal);
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);

        V2CompositionPlanCompileResult admitted = TrustedV2CompositionCompiler.Compile(
            catalog,
            "profile",
            "1.0.0",
            "NT00001",
            "standard");
        V2CompositionPlanCompileResult rejected = TrustedV2CompositionCompiler.Compile(
            catalog,
            "profile",
            "1.0.0",
            "NT00001",
            "replace");

        Assert.NotNull(admitted.CompiledComposition);
        Assert.Empty(admitted.Issues);
        Assert.Null(rejected.CompiledComposition);
        Assert.Equal("profile.v2.compile.profile-experience-mismatch", Assert.Single(rejected.Issues).Code);
    }

    /// <summary>Verifies a profile with capacity variants never selects a default map and lowers only the explicitly requested map.</summary>
    [Fact]
    public void TrustedCompilerRequiresExactCapacityForMultipleCanonicalMaps()
    {
        string familyJson = FamilyJsonWithCapacityVariants();
        string profileJson = RuntimeProfileForCapacityVariants(Hash(familyJson));
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);

        V2CompositionPlanCompileResult missing = TrustedV2CompositionCompiler.Compile(
            catalog,
            "profile",
            "1.0.0",
            "NT00001",
            "standard");
        V2CompositionPlanCompileResult sixteen = TrustedV2CompositionCompiler.Compile(
            catalog,
            "profile",
            "1.0.0",
            "NT00001",
            "standard",
            requestedMapCapacity: 16);
        V2CompositionPlanCompileResult thirtyTwo = TrustedV2CompositionCompiler.Compile(
            catalog,
            "profile",
            "1.0.0",
            "NT00001",
            "standard",
            requestedMapCapacity: 32);
        V2CompositionPlanCompileResult unavailable = TrustedV2CompositionCompiler.Compile(
            catalog,
            "profile",
            "1.0.0",
            "NT00001",
            "standard",
            requestedMapCapacity: 64);

        Assert.Null(missing.CompiledComposition);
        Assert.Equal("profile.v2.compile.map-capacity-required", Assert.Single(missing.Issues).Code);
        Assert.Equal("map", sixteen.CompiledComposition?.V2Details?.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(16, sixteen.CompiledComposition?.Plan.OutputInitialization.Capacity);
        Assert.Equal("map-32", thirtyTwo.CompiledComposition?.V2Details?.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(32, thirtyTwo.CompiledComposition?.Plan.OutputInitialization.Capacity);
        Assert.Null(unavailable.CompiledComposition);
        Assert.Equal("profile.v2.compile.map-capacity-unavailable", Assert.Single(unavailable.Issues).Code);
    }

    private static TrustedProfileBundleCatalog CreateCatalog(
        string? familyJson = null,
        string? profileJson = null,
        string bundleContentHash = BundleHash)
    {
        familyJson ??= TrustedV2BundleTestDocuments.FamilyJson();
        string familyHash = Hash(familyJson);
        profileJson ??= TrustedV2BundleTestDocuments.ProfileJson(familyHash);
        return TrustedProfileBundleCatalogFactory.Create(Source(
            [Family("family-entry", familyHash, Parse(familyJson))],
            [Profile("profile-entry", Hash(profileJson), Parse(profileJson))],
            bundleContentHash));
    }

    private static V2CompositionPreparationRequest Request(
        TrustedProfileBundleCatalog.ProfileSelection selection,
        long capacityBytes = 16,
        string modeId = "standard")
    {
        return new V2CompositionPreparationRequest(
            selection,
            new FirmwareMapResolutionInputs(
                "NT00001",
                modeId,
                capacityBytes,
                requestedTopology: null,
                []));
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string FamilyJsonRequiringArtifact()
    {
        return TrustedV2BundleTestDocuments.FamilyJson()
            .Replace(
                "\"metadataSets\": [],",
                """
                "metadataSets": [
                  {
                    "metadataSetId": "metadata",
                    "structures": [
                      {
                        "structureId": "firmware-config",
                        "artifactBindingId": "tp-firmware",
                        "length": 1,
                        "locator": {
                          "kind": "absolute-range",
                          "range": { "addressSpaceId": "flash", "start": 0, "length": 1 },
                          "allowedResultRegionId": "root"
                        },
                        "fields": [
                          { "fieldId": "pid", "offset": 0, "widthBytes": 1, "encoding": "bytes" }
                        ],
                        "assertions": []
                      }
                    ],
                    "evidenceRefs": ["metadata-evidence"]
                  }
                ],
                """,
                StringComparison.Ordinal)
            .Replace(
                "\"metadataSetIds\": [],",
                "\"metadataSetIds\": [\"metadata\"],",
                StringComparison.Ordinal);
    }

    private static string FamilyJsonWithAmbiguousMap()
    {
        JsonObject family = Assert.IsType<JsonObject>(JsonNode.Parse(TrustedV2BundleTestDocuments.FamilyJson()));
        JsonArray maps = Assert.IsType<JsonArray>(family["imageMaps"]);
        JsonObject alternate = Assert.IsType<JsonObject>(maps[0]?.DeepClone());
        alternate["mapId"] = "alternate-map";
        maps.Add(alternate);
        return family.ToJsonString();
    }

    private static string FamilyJsonWithCapacityVariants()
    {
        JsonObject family = Assert.IsType<JsonObject>(JsonNode.Parse(FamilyJsonWithRootWriteConstraint("explicit-range")));
        JsonArray regionSets = Assert.IsType<JsonArray>(family["regionSets"]);
        JsonObject secondRegionSet = Assert.IsType<JsonObject>(regionSets[0]?.DeepClone());
        secondRegionSet["regionSetId"] = "physical-32";
        JsonObject root = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(secondRegionSet["regions"])[0]);
        root["range"] = new JsonObject { ["start"] = 0, ["length"] = 32 };
        regionSets.Add(secondRegionSet);
        JsonArray maps = Assert.IsType<JsonArray>(family["imageMaps"]);
        JsonObject secondMap = Assert.IsType<JsonObject>(maps[0]?.DeepClone());
        secondMap["mapId"] = "map-32";
        Assert.IsType<JsonObject>(secondMap["applicability"])["capacityBytes"] = 32;
        secondMap["regionSetIds"] = new JsonArray("physical-32");
        maps.Add(secondMap);
        return family.ToJsonString();
    }

    private static string RuntimeProfileForCapacityVariants(string familyHash)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(RuntimeSupportedProfileJson(familyHash)));
        Assert.IsType<JsonObject>(profile["experience"])["experienceId"] = "standard";
        Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(profile["mapBinding"])["mapIds"]).Add("map-32");
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["regionAccessRules"])[0])["access"] = "explicit-range";
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["views"])[1])["selector"] = new JsonObject
        {
            ["kind"] = "map-region",
            ["regionId"] = "root",
        };
        return profile.ToJsonString();
    }
}
