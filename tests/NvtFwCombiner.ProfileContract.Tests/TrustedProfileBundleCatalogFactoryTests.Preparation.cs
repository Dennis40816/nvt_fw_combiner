using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
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

        TrustedCompositionProfileCatalogEntry selection = Select(catalog);
        bool admitted = V2CompositionPreparationService.TryPrepare(
            catalog,
            selection,
            Inputs(),
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.Equal("profile-entry", selection.EntryIdentity.EntryId);
        Assert.Equal("profile", selection.Profile.ProfileId);
        Assert.Equal("1.0.0", selection.Profile.ProfileVersion);
        Assert.True(admitted);
        Assert.Equal(FirmwareMapResolutionStatus.Unique, mapResolution?.Status);
        Assert.Equal("map", mapResolution?.ResolvedMap?.ImageMap.MapId);
        Assert.Empty(capabilityAdmissions);
        Assert.Empty(issues);
    }

    /// <summary>Verifies profile version lookup cannot fall back to a different declaration or latest version.</summary>
    [Fact]
    public void SelectionRejectsUnknownProfileVersionWithoutFallback()
    {
        TrustedCompositionProfileCatalogEntry? selection = CreateCatalog().SelectProfile(
            "profile",
            "2.0.0",
            out IReadOnlyList<CompositionIssue> issues);

        Assert.Null(selection);
        Assert.Equal("profile.v2.selection.not-found", Assert.Single(issues).Code);
    }

    /// <summary>Verifies an equivalent entry constructed by a friend assembly is not owned by the catalog.</summary>
    [Fact]
    public void SelectionRejectsAnUnownedEntryReference()
    {
        TrustedProfileBundleCatalog catalog = CreateCatalog();
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);
        var unowned = new TrustedCompositionProfileCatalogEntry(
            selection.Identity,
            selection.Profile,
            selection.Family);

        bool admitted = V2CompositionPreparationService.TryPrepare(
            catalog,
            unowned,
            Inputs(),
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(admitted);
        Assert.Null(mapResolution);
        Assert.Empty(capabilityAdmissions);
        Assert.Equal("profile.v2.selection.stale", Assert.Single(issues).Code);

        bool compiled = V2CompositionPlanCompiler.TryCompileAdmitted(
            catalog,
            unowned,
            Inputs(),
            selectedInputSlotIds: null,
            out V2CompositionPlanCompileResult? compilation,
            out IReadOnlyList<CompositionIssue> compilationIssues);
        Assert.False(compiled);
        Assert.Null(compilation);
        Assert.Equal("profile.v2.selection.stale", Assert.Single(compilationIssues).Code);
    }

    /// <summary>Verifies an entry from one trusted catalog cannot be reused against another catalog.</summary>
    [Fact]
    public void PreparationRejectsAStaleBundleSelectionBeforeMapResolution()
    {
        TrustedProfileBundleCatalog source = CreateCatalog();
        TrustedCompositionProfileCatalogEntry selection = Select(source);
        TrustedProfileBundleCatalog current = CreateCatalog(bundleContentHash: new('c', 64));

        bool admitted = V2CompositionPreparationService.TryPrepare(
            current,
            selection,
            Inputs(),
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(admitted);
        Assert.Null(mapResolution);
        Assert.Empty(capabilityAdmissions);
        Assert.Equal("profile.v2.selection.stale", Assert.Single(issues).Code);
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
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);

        bool admitted = V2CompositionPreparationService.TryPrepare(
            catalog,
            selection,
            Inputs(),
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(admitted);
        FirmwareMapResolutionResult resolution = Assert.IsType<FirmwareMapResolutionResult>(mapResolution);
        Assert.Equal(FirmwareMapResolutionStatus.Pending, resolution.Status);
        Assert.Equal(
            FirmwareMapResolutionPendingKind.RequestedTopologyMissing,
            Assert.Single(resolution.PendingRequirements).Kind);
        Assert.Empty(capabilityAdmissions);
        Assert.Empty(issues);
    }

    /// <summary>Verifies inspection-only metadata does not block composition preparation.</summary>
    [Fact]
    public void PreparationDefersInspectionOnlyMetadataArtifactRequirement()
    {
        string familyJson = FamilyJsonRequiringArtifact();
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(Hash(familyJson))));
        Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(profile["mapBinding"])["requiredMetadataStructureIds"])
            .Add("firmware-config");
        Assert.IsType<JsonArray>(profile["metadataBindings"]).Add(new JsonObject
        {
            ["bindingId"] = "firmware-config-inspection",
            ["spaceId"] = "tp-source",
            ["structureId"] = "firmware-config",
            ["fieldIds"] = new JsonArray("pid"),
            ["purposes"] = new JsonArray("display"),
        });
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profile.ToJsonString());
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);

        bool admitted = V2CompositionPreparationService.TryPrepare(
            catalog,
            selection,
            Inputs(),
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(admitted);
        Assert.Equal(FirmwareMapResolutionStatus.Unique, mapResolution?.Status);
        Assert.Empty(capabilityAdmissions);
        Assert.Empty(issues);
    }

    /// <summary>Verifies map-selection metadata still blocks when its required artifact is missing.</summary>
    [Fact]
    public void PreparationPreservesPendingMissingMapSelectionArtifactRequirement()
    {
        string familyJson = FamilyJsonRequiringArtifact();
        string profileJson = TrustedV2BundleTestDocuments.ProfileJson(Hash(familyJson)).Replace(
            "\"requiredMetadataStructureIds\": []",
            "\"requiredMetadataStructureIds\": [\"firmware-config\"]",
            StringComparison.Ordinal);
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);

        bool admitted = V2CompositionPreparationService.TryPrepare(
            catalog,
            selection,
            Inputs(),
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(admitted);
        FirmwareMapResolutionPendingRequirement requirement = Assert.Single(
            Assert.IsType<FirmwareMapResolutionResult>(mapResolution).PendingRequirements);
        Assert.Equal(FirmwareMapResolutionPendingKind.ArtifactMissing, requirement.Kind);
        Assert.Equal("tp-firmware", requirement.ArtifactBindingId);
        Assert.Empty(capabilityAdmissions);
        Assert.Empty(issues);
    }

    /// <summary>Verifies no matching map remains distinct from selection and admission failures.</summary>
    [Fact]
    public void PreparationPreservesNoMatchingMapRejection()
    {
        TrustedProfileBundleCatalog catalog = CreateCatalog();
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);

        bool admitted = V2CompositionPreparationService.TryPrepare(
            catalog,
            selection,
            Inputs(capacityBytes: 17),
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(admitted);
        FirmwareMapResolutionResult resolution = Assert.IsType<FirmwareMapResolutionResult>(mapResolution);
        Assert.Equal(FirmwareMapResolutionStatus.Rejected, resolution.Status);
        Assert.Equal(FirmwareMapResolutionRejectionKind.NoMatchingMap, resolution.RejectionKind);
        Assert.Empty(capabilityAdmissions);
        Assert.Empty(issues);
    }

    /// <summary>Verifies multiple fully matching maps inside the selected profile remain ambiguous before admission.</summary>
    [Fact]
    public void PreparationPreservesAmbiguousMapRejection()
    {
        string familyJson = FamilyJsonWithAmbiguousMap();
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(Hash(familyJson))));
        Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(profile["mapBinding"])["mapIds"]).Add("alternate-map");
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profile.ToJsonString());
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);

        bool admitted = V2CompositionPreparationService.TryPrepare(
            catalog,
            selection,
            Inputs(),
            out FirmwareMapResolutionResult? mapResolution,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(admitted);
        FirmwareMapResolutionResult resolution = Assert.IsType<FirmwareMapResolutionResult>(mapResolution);
        Assert.Equal(FirmwareMapResolutionRejectionKind.AmbiguousMaps, resolution.RejectionKind);
        Assert.Empty(capabilityAdmissions);
        Assert.Empty(issues);
    }

    /// <summary>Verifies static profile metadata requirements are rejected while the trusted catalog is built.</summary>
    [Fact]
    public void CatalogRejectsMissingStaticMetadataRequirementsBeforePreparation()
    {
        string familyJson = TrustedV2BundleTestDocuments.FamilyJson();
        string familyHash = Hash(familyJson);
        string profileJson = TrustedV2BundleTestDocuments.ProfileJson(familyHash).Replace(
            "\"requiredMetadataStructureIds\": []",
            "\"requiredMetadataStructureIds\": [\"missing-b\", \"missing-a\"]",
            StringComparison.Ordinal);
        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            CreateCatalog(familyJson, profileJson));

        Assert.Equal("profile-bundle.catalog.profile-required-metadata-missing", exception.Code);
        Assert.Contains("missing-a", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a missing required capability cannot enter raw plan lowering with an empty admission set.</summary>
    [Fact]
    public void AtomicCompilationRejectsIncompleteCapabilityAdmission()
    {
        string familyJson = FamilyJsonWithRootWriteConstraint("whole-region");
        string profileJson = ProfileRequiringCapability(SupportedProfileJson(Hash(familyJson)));
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);

        bool compiled = V2CompositionPlanCompiler.TryCompileAdmitted(
            catalog,
            selection,
            Inputs(),
            selectedInputSlotIds: null,
            out V2CompositionPlanCompileResult? compilation,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(compiled);
        Assert.Null(compilation);
        Assert.Equal("profile.v2.map.required-capability-missing", Assert.Single(issues).Code);
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

    private static TrustedCompositionProfileCatalogEntry Select(TrustedProfileBundleCatalog catalog)
    {
        TrustedCompositionProfileCatalogEntry? selection = catalog.SelectProfile(
            "profile",
            "1.0.0",
            out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        return Assert.IsType<TrustedCompositionProfileCatalogEntry>(selection);
    }

    private static PreparedProfile PrepareAdmitted(
        TrustedProfileBundleCatalog catalog,
        TrustedCompositionProfileCatalogEntry selection,
        FirmwareMapResolutionInputs inputs)
    {
        bool admitted = V2CompositionPreparationService.TryPrepare(
            catalog,
            selection,
            inputs,
            out _,
            out IReadOnlyList<CompiledCapabilityAdmission> capabilityAdmissions,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(
            admitted,
            string.Join(
                Environment.NewLine,
                issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        return new PreparedProfile(
            catalog,
            selection,
            inputs,
            capabilityAdmissions);
    }

    private static V2CompositionPlanCompileResult Compile(
        PreparedProfile preparation,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        bool admitted = V2CompositionPlanCompiler.TryCompileAdmitted(
            preparation.Catalog,
            preparation.ProfileEntry,
            preparation.Inputs,
            selectedInputSlotIds,
            out V2CompositionPlanCompileResult? compilation,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(
            admitted,
            string.Join(
                Environment.NewLine,
                issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        return Assert.IsType<V2CompositionPlanCompileResult>(compilation);
    }

    private sealed record PreparedProfile(
        TrustedProfileBundleCatalog Catalog,
        TrustedCompositionProfileCatalogEntry ProfileEntry,
        FirmwareMapResolutionInputs Inputs,
        IReadOnlyList<CompiledCapabilityAdmission> CapabilityAdmissions);

    private static FirmwareMapResolutionInputs Inputs(
        long capacityBytes = 16,
        string modeId = "standard")
    {
        return new FirmwareMapResolutionInputs(
            "NT00001",
            modeId,
            capacityBytes,
            requestedTopology: null,
            []);
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
