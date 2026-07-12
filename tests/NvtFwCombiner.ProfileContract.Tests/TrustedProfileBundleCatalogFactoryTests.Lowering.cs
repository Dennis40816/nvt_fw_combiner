using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies the admitted blank-copy subset lowers into one complete non-executable V2 plan artifact.</summary>
    [Fact]
    public void BlankCopyLoweringBuildsOneV2PlanWithTrustedProvenance()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy());
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);

        Assert.True(result.IsCompiled);
        Assert.Empty(result.Issues);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(composition.Authority);
        Assert.Equal("{original-name}_merged.bin", composition.DefaultOutputFileName);
        Assert.Equal("output", composition.Plan.OutputSpaceId);
        Assert.Equal(2, composition.Plan.AddressSpaces.Count);
        AddressSpace input = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "tp-source");
        Assert.Equal(AddressSpaceMutability.Immutable, input.Mutability);
        Assert.Equal(16, input.Length);
        Assert.Equal([16L], input.AllowedInputLengths);
        AddressSpace output = Assert.Single(composition.Plan.AddressSpaces, space => space.AddressSpaceId == "output");
        Assert.Equal(AddressSpaceMutability.Mutable, output.Mutability);
        Assert.Equal(16, output.Length);
        ImageInitialization initialization = Assert.Single(composition.Plan.Initializations);
        Assert.Equal(ImageInitializationKind.Blank, initialization.Kind);
        Assert.Equal("output", initialization.TargetSpaceId);
        Assert.Equal(0xFF, initialization.FillByte);
        CompositionOperation operation = Assert.Single(composition.Plan.OrderedOperations);
        Assert.Equal(CompositionOperationKind.CopyRange, operation.Kind);
        Assert.Equal("tp-source", operation.SourceSpaceId);
        Assert.Equal(new ByteRange(0, 16), operation.SourceRange);
        Assert.Equal("output", operation.TargetSpaceId);
        Assert.Equal(new ByteRange(0, 16), operation.TargetRange);
        Assert.Equal(OverlapPolicy.Reject, operation.OverlapPolicy);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal(BundleHash, details.Provenance.Bundle.ContentHash);
        Assert.Equal("profile-entry", details.Provenance.ProfileEntry.EntryId);
        Assert.Equal("map", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(CompiledProfilePromotionStage.Compilable, details.Provenance.Promotion.Stage);
        Assert.Equal(["synthetic-evidence"], details.Provenance.ProfileEvidenceRefs);
        CompiledInputContract inputContract = details.InputContract;
        CompiledInputSlotRequirement slot = Assert.Single(inputContract.Slots);
        Assert.Equal("tp-input", slot.SlotId);
        Assert.Equal("tp", slot.Role);
        Assert.Equal(CompiledInputArtifactClass.ReferenceImage, slot.ArtifactClass);
        Assert.True(slot.Required);
        Assert.Equal(CompiledInputSlotCardinality.ExactlyOne, slot.Cardinality);
        Assert.Equal([".bin"], slot.AcceptedExtensions);
        CompiledExactResolvedMapCapacityInputLengthRequirement length = Assert.IsType<CompiledExactResolvedMapCapacityInputLengthRequirement>(slot.LengthRequirement);
        Assert.Equal(16, length.Bytes);
        _ = Assert.IsType<CompiledNoInputNormalization>(slot.Normalization);
        CompiledInputSpaceBinding inputBinding = Assert.Single(inputContract.SpaceBindings);
        Assert.Equal("tp-source", inputBinding.AddressSpaceId);
        Assert.Equal("tp-input", inputBinding.SlotId);
        Assert.Equal(CompiledInputInstancePolicy.Singleton, inputBinding.InstancePolicy);
    }

    /// <summary>Verifies a region slice ending exactly at its half-open boundary lowers while one byte beyond fails closed.</summary>
    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void BlankCopyLoweringChecksMapRegionSliceBounds(int length, bool expectedSuccess)
    {
        V2CompositionPreparationResult preparation = PrepareSupportedBlankCopy(familyHash => SupportedProfileJson(familyHash)
            .Replace(
                "\"kind\": \"map-region\", \"regionId\": \"root\"",
                $"\"kind\": \"map-region-slice\", \"regionId\": \"root\", \"offset\": 0, \"length\": {length}",
                StringComparison.Ordinal));

        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(preparation);

        Assert.Equal(expectedSuccess, result.IsCompiled);
        if (expectedSuccess)
        {
            Assert.Empty(result.Issues);
            Assert.Equal(new ByteRange(0, 16), Assert.Single(result.CompiledComposition!.Plan.OrderedOperations).SourceRange);
            return;
        }

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.invalid-view", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies an otherwise valid Replace operation cannot silently enter the blank-copy lowering subset.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsUnsupportedReplaceOperationWithoutAnArtifact()
    {
        V2CompositionPreparationResult preparation = PrepareSupportedBlankCopy(familyHash => SupportedProfileJson(familyHash)
            .Replace("\"kind\": \"copy-range\"", "\"kind\": \"replace-range\"", StringComparison.Ordinal));

        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(preparation);

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies a declared overlap policy cannot bypass the first lowering subset.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsNonRejectOverlapPolicyWithoutAnArtifact()
    {
        V2CompositionPreparationResult preparation = PrepareSupportedBlankCopy(familyHash => SupportedProfileJson(familyHash)
            .Replace("\"overlapPolicy\": \"reject\"", "\"overlapPolicy\": \"allow-declared\"", StringComparison.Ordinal));

        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(preparation);

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies plan lowering rejects a profile below the Compilable evidence stage without attempting execution.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsPromotionBelowCompilable()
    {
        V2CompositionPreparationResult preparation = PrepareSupportedBlankCopy(familyHash => SupportedProfileJson(familyHash)
            .Replace("\"stage\": \"compilable\"", "\"stage\": \"authorable\"", StringComparison.Ordinal));

        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(preparation);

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies nonempty profile-owned region access cannot disappear from the first lowering subset.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsRegionAccessPolicyWithoutAnArtifact()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(
            PrepareSupportedBlankCopy(familyHash => SupportedProfileJson(familyHash, removeRegionAccess: false)));

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.unsupported-declaration", issue.Code);
        Assert.Contains("region access rules", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies direct confirmed capability evidence is retained as the exact admitted binding.</summary>
    [Fact]
    public void BlankCopyLoweringRetainsDirectCapabilityAdmission()
    {
        V2CompositionPreparationResult preparation = PrepareSupportedBlankCopy(
            familyHash => ProfileRequiringCapability(SupportedProfileJson(familyHash)),
            FamilyJsonWithDirectCapability());

        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(preparation);

        CompiledCapabilityAdmission capability = Assert.Single(
            Assert.IsType<V2CompiledCompositionDetails>(result.CompiledComposition!.V2Details)
                .Provenance.RequiredCapabilities);
        Assert.Equal("ab-code", capability.RequiredCapabilityId);
        Assert.Same(Assert.Single(preparation.Admission!.RequiredCapabilities).Binding, capability.Binding);
        Assert.Equal("NT00001", capability.Binding.EffectiveKey.MemberId);
        Assert.Equal("map", capability.Binding.EffectiveKey.MapId);
        Assert.Empty(capability.Binding.Provenance.AliasChain);
    }

    /// <summary>Verifies fact-scoped capability aliases retain their effective-to-direct chain through plan lowering.</summary>
    [Fact]
    public void BlankCopyLoweringRetainsAliasedCapabilityAdmission()
    {
        V2CompositionPreparationResult preparation = PrepareSupportedBlankCopy(
            familyHash => ProfileRequiringCapability(SupportedProfileJson(familyHash)),
            FamilyJsonWithAliasedCapability());

        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(preparation);

        CompiledCapabilityAdmission capability = Assert.Single(
            Assert.IsType<V2CompiledCompositionDetails>(result.CompiledComposition!.V2Details)
                .Provenance.RequiredCapabilities);
        Assert.Equal("NT00001", capability.Binding.EffectiveKey.MemberId);
        Assert.Equal("map", capability.Binding.EffectiveKey.MapId);
        Assert.Equal("NT00002", capability.Binding.DirectSourceKey.MemberId);
        Assert.Equal("source-map", capability.Binding.DirectSourceKey.MapId);
        Assert.Equal(
            ["target-capability-to-source"],
            capability.Binding.Provenance.AliasChain.Select(static alias => alias.AliasId));
        Assert.Same(Assert.Single(preparation.Admission!.RequiredCapabilities).Binding, capability.Binding);
    }

    private static V2CompositionPreparationResult PrepareSupportedBlankCopy(
        Func<string, string>? profileJsonFactory = null,
        string? familyJson = null)
    {
        familyJson ??= TrustedV2BundleTestDocuments.FamilyJson();
        string familyHash = Hash(familyJson);
        string profileJson = (profileJsonFactory ?? (hash => SupportedProfileJson(hash)))(familyHash);
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            catalog.SelectProfile("profile", "1.0.0").Selection);
        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            Request(selection));
        Assert.True(preparation.IsAdmitted);
        return preparation;
    }

    private static string SupportedProfileJson(string familyHash, bool removeRegionAccess = true)
    {
        string profile = TrustedV2BundleTestDocuments.ProfileJson(familyHash)
            .Replace("\"stage\": \"known\"", "\"stage\": \"compilable\"", StringComparison.Ordinal)
            .Replace("\"artifactClass\": \"tp-firmware\"", "\"artifactClass\": \"reference-image\"", StringComparison.Ordinal)
            .Replace(
                "\"lengthRule\": { \"kind\": \"tp-maximum-256k\", \"maximumBytes\": 262144 }",
                "\"lengthRule\": { \"kind\": \"exact-resolved-map-capacity\" }",
                StringComparison.Ordinal);
        return removeRegionAccess
            ? profile.Replace(
                """
                  "regionAccessRules": [
                    {
                      "regionId": "root",
                      "access": "read-only",
                      "reason": "Synthetic source is immutable."
                    }
                  ],
                """,
                """
                  "regionAccessRules": [],
                """,
                StringComparison.Ordinal)
            : profile;
    }

    private static string ProfileRequiringCapability(string profileJson)
    {
        return profileJson.Replace(
            "\"requiredCapabilityIds\": []",
            "\"requiredCapabilityIds\": [\"ab-code\"]",
            StringComparison.Ordinal);
    }

    private static string FamilyJsonWithDirectCapability()
    {
        JsonObject family = ParseFamily();
        Assert.IsType<JsonArray>(family["capabilities"]).Add(Capability(
            "target-capability",
            "ab-code",
            "NT00001",
            "map",
            "direct capability evidence"));
        return family.ToJsonString();
    }

    private static string FamilyJsonWithAliasedCapability()
    {
        JsonObject family = ParseFamily();
        Assert.IsType<JsonArray>(family["members"]).Add(new JsonObject
        {
            ["memberId"] = "NT00002",
            ["displayName"] = "Synthetic source IC",
        });
        JsonArray maps = Assert.IsType<JsonArray>(family["imageMaps"]);
        JsonObject sourceMap = Assert.IsType<JsonObject>(maps[0]?.DeepClone());
        sourceMap["mapId"] = "source-map";
        JsonObject applicability = Assert.IsType<JsonObject>(sourceMap["applicability"]);
        applicability["memberIds"] = new JsonArray("NT00002");
        maps.Add(sourceMap);
        Assert.IsType<JsonArray>(family["capabilities"]).Add(Capability(
            "source-capability",
            "ab-code",
            "NT00002",
            "source-map",
            "source capability evidence"));
        Assert.IsType<JsonArray>(family["factAliases"]).Add(new JsonObject
        {
            ["aliasId"] = "target-capability-to-source",
            ["factKind"] = "capability",
            ["targetMemberId"] = "NT00001",
            ["targetMapId"] = "map",
            ["targetCapabilityFactId"] = "target-capability",
            ["sourceMemberId"] = "NT00002",
            ["sourceMapId"] = "source-map",
            ["sourceCapabilityFactId"] = "source-capability",
            ["applicability"] = Applicability(),
            ["reason"] = "Target capability inherits source evidence.",
            ["evidenceRefs"] = new JsonArray("target-capability-evidence"),
        });
        return family.ToJsonString();
    }

    private static JsonObject ParseFamily()
    {
        return Assert.IsType<JsonObject>(JsonNode.Parse(TrustedV2BundleTestDocuments.FamilyJson()));
    }

    private static JsonObject Capability(
        string capabilityFactId,
        string capabilityId,
        string memberId,
        string mapId,
        string reason)
    {
        return new JsonObject
        {
            ["capabilityFactId"] = capabilityFactId,
            ["capabilityId"] = capabilityId,
            ["memberId"] = memberId,
            ["mapId"] = mapId,
            ["applicability"] = Applicability(),
            ["state"] = "confirmed-present",
            ["reason"] = reason,
            ["evidenceRefs"] = new JsonArray("source-capability-evidence"),
        };
    }

    private static JsonObject Applicability()
    {
        return new JsonObject
        {
            ["modeIds"] = new JsonArray("standard"),
            ["topologyRequirement"] = new JsonObject { ["kind"] = "none" },
            ["capacityBytes"] = 16,
        };
    }
}
