using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies the admitted blank-copy subset lowers into one complete non-executable V2 plan artifact.</summary>
    [Fact]
    public void BlankCopyLoweringBuildsOneV2PlanWithTrustedProvenance()
    {
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy());
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);

        Assert.True(result.IsCompiled);
        Assert.Empty(result.Issues);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
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
        CompiledRegionAccessRequirement access = Assert.Single(details.RegionAccessContract.Requirements);
        Assert.Equal("root", access.RegionId);
        Assert.Equal(RegionAccessKind.Whole, access.Access);
        Assert.Equal(FirmwareWriteConstraint.WholeRegion, Assert.Single(access.GoverningRegionChain).WriteConstraint);
        FirmwareRegion canonicalRoot = details.Provenance.ResolvedMap.ImageMap.Regions.Single(
            static region => region.RegionId == "root");
        Assert.Same(canonicalRoot, Assert.Single(access.GoverningRegionChain));
        Assert.All(
            details.RegionAccessContract.ResolvedViews,
            view => Assert.Same(canonicalRoot, Assert.Single(view.GoverningRegionChain)));
        Assert.Equal(["output-code", "tp-code"], details.RegionAccessContract.ResolvedViews.Select(static view => view.ViewId));
    }

    /// <summary>Verifies only the fully lowered supported token-free declaration mints a V2 runtime artifact.</summary>
    [Fact]
    public void BlankCopyLoweringMintsRuntimeArtifactOnlyForSupportedTokenFreeProfile()
    {
        V2CompositionPlanCompileResult runtime = Compile(PrepareSupportedBlankCopy(
            familyHash => RuntimeSupportedProfileJson(familyHash)));
        V2CompositionPlanCompileResult candidate = Compile(PrepareSupportedBlankCopy(
            familyHash => RuntimeSupportedProfileJson(familyHash, "executable-candidate")));
        V2CompositionPlanCompileResult tokenized = Compile(PrepareSupportedBlankCopy(
            familyHash => RuntimeSupportedProfileJson(familyHash, "supported", tokenizedOutput: true)));
        V2CompositionPlanCompileResult overridable = Compile(PrepareSupportedBlankCopy(
            familyHash => RuntimeSupportedProfileJson(familyHash, "supported", allowOutputOverride: true)));
        V2CompositionPlanCompileResult replacementPolicy = Compile(PrepareSupportedBlankCopy(
            familyHash => RuntimeSupportedProfileJson(familyHash, "supported", invalidCharacterPolicy: "replace-underscore")));

        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, runtime.CompiledComposition?.Eligibility);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, candidate.CompiledComposition?.Eligibility);
        Assert.Null(tokenized.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(tokenized.Issues).Code);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, overridable.CompiledComposition?.Eligibility);
        Assert.Empty(overridable.Issues);
        Assert.Null(replacementPolicy.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(replacementPolicy.Issues).Code);
    }

    /// <summary>Verifies a region slice ending exactly at its half-open boundary lowers while one byte beyond fails closed.</summary>
    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void BlankCopyLoweringChecksMapRegionSliceBounds(int length, bool expectedSuccess)
    {
        PreparedProfile preparation = PrepareSupportedBlankCopy(
            familyHash => ProfileWithSourceSlice(SupportedProfileJson(familyHash), length));

        V2CompositionPlanCompileResult result = Compile(preparation);

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
        PreparedProfile preparation = PrepareSupportedBlankCopy(familyHash => SupportedProfileJson(familyHash)
            .Replace("\"kind\": \"copy-range\"", "\"kind\": \"replace-range\"", StringComparison.Ordinal));

        V2CompositionPlanCompileResult result = Compile(preparation);

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies a declared overlap policy cannot bypass the first lowering subset.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsNonRejectOverlapPolicyWithoutAnArtifact()
    {
        PreparedProfile preparation = PrepareSupportedBlankCopy(familyHash => SupportedProfileJson(familyHash)
            .Replace("\"overlapPolicy\": \"reject\"", "\"overlapPolicy\": \"allow-declared\"", StringComparison.Ordinal));

        V2CompositionPlanCompileResult result = Compile(preparation);

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies plan lowering rejects a profile below the Compilable evidence stage without attempting execution.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsPromotionBelowCompilable()
    {
        PreparedProfile preparation = PrepareSupportedBlankCopy(familyHash => SupportedProfileJson(familyHash)
            .Replace("\"stage\": \"compilable\"", "\"stage\": \"authorable\"", StringComparison.Ordinal));

        V2CompositionPlanCompileResult result = Compile(preparation);

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies a read-only profile rule cannot authorize an otherwise writable copy target.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsReadOnlyTargetWithoutAnArtifact()
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => SupportedProfileJson(familyHash, access: "read-only"),
                FamilyJsonWithRootWriteConstraint("whole-region")));

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.region-access-denied", issue.Code);
        Assert.Contains("ReadOnly", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a physical writable constraint cannot substitute for one missing profile access rule.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsTargetWithoutDeclaredAccess()
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => SupportedProfileJson(familyHash, access: null),
                FamilyJsonWithRootWriteConstraint("whole-region")));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.region-access-denied", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies physical forbidden is non-relaxable even when the profile declares whole access.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsWholeAccessOverForbiddenPhysicalRegion()
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => SupportedProfileJson(familyHash, access: "whole"),
                TrustedV2BundleTestDocuments.FamilyJson()));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.region-access-denied", issue.Code);
        Assert.Contains("Forbidden", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a restrictive parent profile rule narrows an otherwise authorizable direct child target.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsChildTargetWhenParentRuleIsReadOnly()
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => ProfileWithParentAndChildRules(SupportedProfileJson(familyHash), "read-only"),
                FamilyJsonWithSplitRoot("declared-subregions")));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.region-access-denied", issue.Code);
        Assert.Contains("ReadOnly", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a restrictive physical parent constraint narrows an otherwise allowed direct child target.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsChildTargetWhenParentConstraintIsForbidden()
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => ProfileWithTargetSlice(
                    SupportedProfileJson(familyHash, access: "parts"),
                    0,
                    8,
                    "parts",
                    ["left"]),
                FamilyJsonWithSplitRoot("forbidden")));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.region-access-denied", issue.Code);
        Assert.Contains("Forbidden", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies parts access permits only an explicitly named direct child while physical parent and child constraints intersect.</summary>
    [Fact]
    public void BlankCopyLoweringAcceptsDeclaredDirectPart()
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => ProfileWithTargetSlice(SupportedProfileJson(familyHash, access: "parts"), 0, 8, "parts", ["left"]),
                FamilyJsonWithSplitRoot("declared-subregions")));

        CompiledRegionAccessRequirement access = Assert.Single(
            result.CompiledComposition!.V2Details.RegionAccessContract.Requirements);
        Assert.Equal(RegionAccessKind.Parts, access.Access);
        Assert.Equal(["left"], access.AllowedSubregionIds);
        Assert.Equal(["root"], access.GoverningRegionChain.Select(static region => region.RegionId));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        FirmwareRegion root = composition.V2Details.Provenance.ResolvedMap.ImageMap.Regions.Single(
            static region => region.RegionId == "root");
        FirmwareRegion left = composition.V2Details.Provenance.ResolvedMap.ImageMap.Regions.Single(
            static region => region.RegionId == "left");
        Assert.Same(root, Assert.Single(access.GoverningRegionChain));
        IReadOnlyList<FirmwareRegion> viewChain = Assert.Single(
            result.CompiledComposition.V2Details.RegionAccessContract.ResolvedViews,
            static view => view.ViewId == "output-code").GoverningRegionChain;
        Assert.Equal(["root", "left"], viewChain.Select(static region => region.RegionId));
        Assert.Same(root, viewChain[0]);
        Assert.Same(left, viewChain[1]);
    }

    /// <summary>Verifies parts access cannot authorize a sibling or an unknown non-child target declaration.</summary>
    [Theory]
    [InlineData("right", "profile.v2.plan.region-access-denied")]
    [InlineData("unknown", "profile.v2.plan.invalid-region-access")]
    public void BlankCopyLoweringRejectsNonMatchingPartsDeclaration(string allowedSubregionId, string issueCode)
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => ProfileWithTargetSlice(
                    SupportedProfileJson(familyHash, access: "parts"),
                    0,
                    8,
                    "parts",
                    [allowedSubregionId]),
                FamilyJsonWithSplitRoot("declared-subregions")));

        Assert.Null(result.CompiledComposition);
        Assert.Equal(issueCode, Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies whole access requires exact half-open region equality rather than containment.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsPartialWholeRegionTarget()
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => ProfileWithTargetSlice(SupportedProfileJson(familyHash), 0, 15, "whole"),
                FamilyJsonWithRootWriteConstraint("whole-region")));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.region-access-denied", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies explicit-range access observes the canonical physical alignment rather than only containment.</summary>
    [Theory]
    [InlineData(4, 4, true)]
    [InlineData(1, 4, false)]
    public void BlankCopyLoweringChecksExplicitRangeAlignment(int start, int length, bool expectedSuccess)
    {
        V2CompositionPlanCompileResult result = Compile(
            PrepareSupportedBlankCopy(
                familyHash => ProfileWithTargetSlice(SupportedProfileJson(familyHash, access: "explicit-range"), start, length, "explicit-range"),
                FamilyJsonWithRootWriteConstraint("explicit-range", alignment: 4)));

        Assert.Equal(expectedSuccess, result.IsCompiled);
        if (!expectedSuccess)
        {
            Assert.Equal("profile.v2.plan.region-access-denied", Assert.Single(result.Issues).Code);
        }
    }

    /// <summary>Verifies source-only access policy remains fingerprint evidence when the byte plan is identical.</summary>
    [Fact]
    public void BlankCopyLoweringFingerprintsReadOnlySourceAccess()
    {
        V2CompositionPlanCompileResult readOnly = Compile(
            PrepareSupportedBlankCopy(
                familyHash => ProfileWithSplitSourceAndTarget(SupportedProfileJson(familyHash), "read-only"),
                FamilyJsonWithSplitRoot("explicit-range")));
        V2CompositionPlanCompileResult hidden = Compile(
            PrepareSupportedBlankCopy(
                familyHash => ProfileWithSplitSourceAndTarget(SupportedProfileJson(familyHash), "hidden"),
                FamilyJsonWithSplitRoot("explicit-range")));

        CompiledRegionAccessRequirement rule = Assert.Single(
            readOnly.CompiledComposition!.V2Details.RegionAccessContract.Requirements,
            static requirement => requirement.RegionId == "left");
        Assert.Equal(RegionAccessKind.ReadOnly, rule.Access);
        Assert.NotEqual(readOnly.CompiledComposition.CompilationFingerprint, hidden.CompiledComposition!.CompilationFingerprint);
    }

    /// <summary>Verifies direct confirmed capability evidence is retained as the exact admitted binding.</summary>
    [Fact]
    public void BlankCopyLoweringRetainsDirectCapabilityAdmission()
    {
        PreparedProfile preparation = PrepareSupportedBlankCopy(
            familyHash => ProfileRequiringCapability(SupportedProfileJson(familyHash)),
            FamilyJsonWithDirectCapability());

        V2CompositionPlanCompileResult result = Compile(preparation);

        CompiledCapabilityAdmission capability = Assert.Single(
            Assert.IsType<V2CompiledCompositionDetails>(result.CompiledComposition!.V2Details)
                .Provenance.RequiredCapabilities);
        Assert.Equal("ab-code", capability.RequiredCapabilityId);
        Assert.Same(Assert.Single(preparation.CapabilityAdmissions).Binding, capability.Binding);
        Assert.Equal("NT00001", capability.Binding.EffectiveKey.MemberId);
        Assert.Equal("map", capability.Binding.EffectiveKey.MapId);
        Assert.Empty(capability.Binding.Provenance.AliasChain);
    }

    /// <summary>Verifies fact-scoped capability aliases retain their effective-to-direct chain through plan lowering.</summary>
    [Fact]
    public void BlankCopyLoweringRetainsAliasedCapabilityAdmission()
    {
        PreparedProfile preparation = PrepareSupportedBlankCopy(
            familyHash => ProfileRequiringCapability(SupportedProfileJson(familyHash)),
            FamilyJsonWithAliasedCapability());

        V2CompositionPlanCompileResult result = Compile(preparation);

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
        Assert.Same(Assert.Single(preparation.CapabilityAdmissions).Binding, capability.Binding);
    }

    private static PreparedProfile PrepareSupportedBlankCopy(
        Func<string, string>? profileJsonFactory = null,
        string? familyJson = null,
        long capacityBytes = 16)
    {
        familyJson ??= FamilyJsonWithRootWriteConstraint("whole-region");
        string familyHash = Hash(familyJson);
        string profileJson = (profileJsonFactory ?? (hash => SupportedProfileJson(hash)))(familyHash);
        TrustedProfileBundleCatalog catalog = CreateCatalog(familyJson, profileJson);
        TrustedCompositionProfileCatalogEntry selection = Select(catalog);
        return PrepareAdmitted(catalog, selection, Inputs(capacityBytes));
    }

    private static string SupportedProfileJson(string familyHash, string? access = "whole")
    {
        string profile = TrustedV2BundleTestDocuments.ProfileJson(familyHash)
            .Replace("\"stage\": \"known\"", "\"stage\": \"compilable\"", StringComparison.Ordinal)
            .Replace("\"artifactClass\": \"tp-firmware\"", "\"artifactClass\": \"reference-image\"", StringComparison.Ordinal)
            .Replace(
                "\"lengthRule\": { \"kind\": \"tp-maximum-256k\", \"maximumBytes\": 262144 }",
                "\"lengthRule\": { \"kind\": \"exact-resolved-map-capacity\" }",
                StringComparison.Ordinal);
        JsonObject profileNode = Assert.IsType<JsonObject>(JsonNode.Parse(profile));
        JsonArray rules = Assert.IsType<JsonArray>(profileNode["regionAccessRules"]);
        if (access is null)
        {
            rules.Clear();
        }
        else
        {
            Assert.IsType<JsonObject>(rules[0])["access"] = access;
        }

        return profileNode.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string RuntimeSupportedProfileJson(
        string familyHash,
        string stage = "supported",
        bool tokenizedOutput = false,
        bool allowOutputOverride = false,
        string invalidCharacterPolicy = "reject")
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(SupportedProfileJson(familyHash)));
        JsonObject promotion = Assert.IsType<JsonObject>(profile["promotion"]);
        promotion["stage"] = stage;
        promotion["blockers"] = new JsonArray();
        JsonObject output = Assert.IsType<JsonObject>(profile["output"]);
        output["fileNameTemplate"] = tokenizedOutput ? "{original-name}.bin" : "v2-output.bin";
        output["allowOverride"] = allowOutputOverride;
        output["invalidCharacterPolicy"] = invalidCharacterPolicy;
        output["requiredTokenIds"] = tokenizedOutput
            ? new JsonArray("original-name")
            : [];
        return profile.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileRequiringCapability(string profileJson)
    {
        return profileJson.Replace(
            "\"requiredCapabilityIds\": []",
            "\"requiredCapabilityIds\": [\"ab-code\"]",
            StringComparison.Ordinal);
    }

    private static string ProfileWithSourceSlice(string profileJson, int length)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonObject source = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["views"])[0]);
        source["selector"] = new JsonObject
        {
            ["kind"] = "map-region-slice",
            ["regionId"] = "root",
            ["offset"] = 0,
            ["length"] = length,
        };
        return profile.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithTargetSlice(
        string profileJson,
        int start,
        int length,
        string access,
        IReadOnlyList<string>? allowedSubregionIds = null)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        foreach (int index in new[] { 0, 1 })
        {
            JsonObject view = Assert.IsType<JsonObject>(views[index]);
            view["selector"] = index == 0
                ? new JsonObject
                {
                    ["kind"] = "map-region-slice",
                    ["regionId"] = "root",
                    ["offset"] = start,
                    ["length"] = length,
                }
                : new JsonObject
                {
                    ["kind"] = "space-range",
                    ["range"] = new JsonObject
                    {
                        ["start"] = start,
                        ["length"] = length,
                    },
                };
        }

        JsonObject rule = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["regionAccessRules"])[0]);
        rule["access"] = access;
        if (allowedSubregionIds is null)
        {
            _ = rule.Remove("allowedSubregionIds");
        }
        else
        {
            rule["allowedSubregionIds"] = new JsonArray([.. allowedSubregionIds.Select(static value => (JsonNode?)value)]);
        }

        return profile.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithSplitSourceAndTarget(string profileJson, string sourceAccess)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray requiredRegionIds = Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(profile["mapBinding"])["requiredRegionIds"]);
        requiredRegionIds.Clear();
        requiredRegionIds.Add("left");
        requiredRegionIds.Add("right");
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        Assert.IsType<JsonObject>(views[0])["selector"] = new JsonObject
        {
            ["kind"] = "map-region",
            ["regionId"] = "left",
        };
        Assert.IsType<JsonObject>(views[1])["selector"] = new JsonObject
        {
            ["kind"] = "space-range",
            ["range"] = new JsonObject { ["start"] = 8, ["length"] = 8 },
        };
        JsonArray rules = Assert.IsType<JsonArray>(profile["regionAccessRules"]);
        JsonObject sourceRule = Assert.IsType<JsonObject>(rules[0]);
        sourceRule["regionId"] = "left";
        sourceRule["access"] = sourceAccess;
        rules.Add(new JsonObject
        {
            ["regionId"] = "right",
            ["access"] = "whole",
            ["reason"] = "Synthetic target is writable as one physical region.",
        });
        return profile.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithParentAndChildRules(string profileJson, string parentAccess)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            ProfileWithTargetSlice(profileJson, 0, 8, parentAccess)));
        Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(profile["mapBinding"])["requiredRegionIds"]).Add("left");
        Assert.IsType<JsonArray>(profile["regionAccessRules"]).Add(new JsonObject
        {
            ["regionId"] = "left",
            ["access"] = "whole",
            ["reason"] = "Synthetic child is authorable only as a whole region.",
        });
        return profile.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string FamilyJsonWithDirectCapability()
    {
        JsonObject family = ParseFamily(FamilyJsonWithRootWriteConstraint("whole-region"));
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
        JsonObject family = ParseFamily(FamilyJsonWithRootWriteConstraint("whole-region"));
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

    private static string FamilyJsonWithRootWriteConstraint(
        string writeConstraint,
        int alignment = 1,
        long capacity = 16)
    {
        JsonObject family = ParseFamily();
        JsonObject root = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["regionSets"])[0])["regions"])[0]);
        root["writeConstraint"] = writeConstraint;
        root["alignment"] = alignment;
        root["range"] = new JsonObject { ["start"] = 0, ["length"] = capacity };
        JsonObject map = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["imageMaps"])[0]);
        JsonObject applicability = Assert.IsType<JsonObject>(map["applicability"]);
        applicability["capacityBytes"] = capacity;
        return family.ToJsonString();
    }

    private static string FamilyJsonWithSplitRoot(string rootWriteConstraint)
    {
        JsonObject family = ParseFamily();
        JsonArray regions = Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(family["regionSets"])[0])["regions"]);
        JsonObject root = Assert.IsType<JsonObject>(regions[0]);
        root["writeConstraint"] = rootWriteConstraint;
        regions.Add(new JsonObject
        {
            ["regionId"] = "left",
            ["parentRegionId"] = "root",
            ["owner"] = "system",
            ["kind"] = "data",
            ["range"] = new JsonObject { ["start"] = 0, ["length"] = 8 },
            ["writeConstraint"] = "whole-region",
            ["alignment"] = 1,
        });
        regions.Add(new JsonObject
        {
            ["regionId"] = "right",
            ["parentRegionId"] = "root",
            ["owner"] = "system",
            ["kind"] = "data",
            ["range"] = new JsonObject { ["start"] = 8, ["length"] = 8 },
            ["writeConstraint"] = "whole-region",
            ["alignment"] = 1,
        });
        return family.ToJsonString();
    }

    private static JsonObject ParseFamily(string? familyJson = null)
    {
        return Assert.IsType<JsonObject>(JsonNode.Parse(familyJson ?? TrustedV2BundleTestDocuments.FamilyJson()));
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
