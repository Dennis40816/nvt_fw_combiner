using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies one map-bound General Replace candidate lowers and executes through the shared reference-clone engine.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringCompilesAndExecutesThroughTheSharedEngine()
    {
        TrustedProfileBundleCatalog catalog = CreateRuntimeReferenceReplaceCatalog();
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            catalog,
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest());

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        _ = Assert.IsType<ResolvedMapV2CompilationContext>(composition.V2Details!.Provenance.Context);
        Assert.Equal(ImageInitializationKind.Reference, composition.Plan.OutputInitialization.Kind);
        Assert.Equal("base", composition.Plan.OutputInitialization.ReferenceSpaceId);
        Assert.Equal("output-image", composition.Plan.OutputSpaceId);
        Assert.Equal(CompositionOperationKind.ReplaceRange, Assert.Single(composition.Plan.OrderedOperations).Kind);

        CompositionExecutionResult execution = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["base"] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
                ["source-a"] = [0xAA, 0xBB, 0xCC, 0xDD],
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 0xCC, 0xDD, 10, 11, 12, 13, 14, 15], execution.OutputBytes.ToArray());
    }

    /// <summary>Verifies a denied map-bound target rejects only the selected runtime-reference-replace compilation request.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsForbiddenPhysicalTarget()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateRuntimeReferenceReplaceCatalog(FirmwareWriteConstraint.Forbidden),
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest());

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.plan.region-access-denied");
    }

    /// <summary>Verifies a source range that escapes its concrete binding rejects only that request.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsOutOfBoundsSourceWithoutAffectingLaterRequest()
    {
        TrustedProfileBundleCatalog catalog = CreateRuntimeReferenceReplaceCatalog();
        V2CompositionPlanCompileResult rejected = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            catalog,
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(RuntimeReferenceReplaceMapping(
                "out-of-bounds-source",
                10,
                new ByteRange(3, 2),
                new ByteRange(8, 2))));
        V2CompositionPlanCompileResult valid = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            catalog,
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest());

        Assert.False(rejected.IsCompiled);
        Assert.Contains(rejected.Issues, issue => issue.Code == "profile.v2.runtime-reference-replace.source-out-of-bounds");
        Assert.True(valid.IsCompiled);
    }

    /// <summary>Verifies reject-overlap remains enforced for typed runtime mappings.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsOverlappingTargets()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateRuntimeReferenceReplaceCatalog(),
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(
                RuntimeReferenceReplaceMapping("first", 10, new ByteRange(0, 2), new ByteRange(7, 2)),
                RuntimeReferenceReplaceMapping("second", 11, new ByteRange(2, 2), new ByteRange(8, 2))));

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.plan.operation-overlap");
    }

    private static TrustedProfileBundleCatalog CreateRuntimeReferenceReplaceCatalog(
        FirmwareWriteConstraint writeConstraint = FirmwareWriteConstraint.ExplicitRange)
    {
        string familyJson = RuntimeReferenceReplaceFamilyJson(writeConstraint);
        string familyHash = Hash(familyJson);
        string profileJson = RuntimeReferenceReplaceProfileJson(familyHash);
        using var familyDocument = JsonDocument.Parse(familyJson);
        using var profileDocument = JsonDocument.Parse(profileJson);
        return TrustedProfileBundleCatalogFactory.Create(Source(
            [Family("family-entry", familyHash, familyDocument.RootElement.Clone())],
            [Profile("runtime-reference-replace-profile", Hash(profileJson), profileDocument.RootElement.Clone())]));
    }

    private static V2RuntimeReferenceReplaceCompileRequest RuntimeReferenceReplaceRequest(
        params ExplicitMapping[] mappings)
    {
        return new V2RuntimeReferenceReplaceCompileRequest(
            16,
            [
                new V2RuntimeReferenceReplaceInputBinding("base", "reference", 16),
                new V2RuntimeReferenceReplaceInputBinding("source-a", "source", 4),
            ],
            mappings.Length == 0
                ? [RuntimeReferenceReplaceMapping("replace-source", 10, new ByteRange(2, 2), new ByteRange(8, 2))]
                : mappings);
    }

    private static ExplicitMapping RuntimeReferenceReplaceMapping(
        string mappingId,
        int sequence,
        ByteRange sourceRange,
        ByteRange targetRange)
    {
        return new ExplicitMapping(
            mappingId,
            sequence,
            ExplicitMappingOperationKind.ReplaceRange,
            "source-a",
            sourceRange,
            "output-image",
            targetRange,
            OverlapPolicy.Reject,
            alignment: 1,
            reason: "Synthetic runtime General Replace mapping");
    }

    private static string RuntimeReferenceReplaceFamilyJson(FirmwareWriteConstraint writeConstraint)
    {
        JsonObject family = JsonNode.Parse(TrustedV2BundleTestDocuments.FamilyJson())!.AsObject();
        JsonObject map = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["imageMaps"])[0]);
        Assert.IsType<JsonObject>(map["applicability"])["modeIds"] = new JsonArray("general-replace");
        JsonObject root = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["regionSets"])[0])["regions"])[0]);
        root["writeConstraint"] = writeConstraint switch
        {
            FirmwareWriteConstraint.ExplicitRange => "explicit-range",
            FirmwareWriteConstraint.Forbidden => "forbidden",
            FirmwareWriteConstraint.WholeRegion => throw new ArgumentOutOfRangeException(
                nameof(writeConstraint),
                writeConstraint,
                "Synthetic fixture does not model whole-region write authority."),
            FirmwareWriteConstraint.DeclaredSubregions => throw new ArgumentOutOfRangeException(
                nameof(writeConstraint),
                writeConstraint,
                "Synthetic fixture does not model declared-subregion write authority."),
            _ => throw new ArgumentOutOfRangeException(nameof(writeConstraint), writeConstraint, "Synthetic fixture supports only explicit or forbidden write authority."),
        };
        return family.ToJsonString();
    }

    private static string RuntimeReferenceReplaceProfileJson(string familyHash)
    {
        var profile = new JsonObject
        {
            ["schemaVersion"] = "2.6",
            ["profileId"] = "runtime-general-replace",
            ["profileVersion"] = "1.0.0",
            ["promotion"] = new JsonObject
            {
                ["stage"] = "compilable",
                ["blockers"] = new JsonArray(),
            },
            ["compositionKind"] = "replace",
            ["icNumberInputMode"] = "single-selector",
            ["experience"] = new JsonObject
            {
                ["experienceId"] = "general-replace",
                ["audience"] = "advanced",
                ["layoutPolicy"] = "user-defined",
                ["inputPolicy"] = "extensible",
                ["topologyAuthoring"] = "hidden",
                ["displayNameKey"] = "runtime-general-replace",
            },
            ["compilationContext"] = new JsonObject { ["kind"] = "runtime-reference-replace" },
            ["mapBinding"] = new JsonObject
            {
                ["familyId"] = "family",
                ["familyVersion"] = "1.0.0",
                ["familyContentHash"] = familyHash,
                ["mapIds"] = new JsonArray("map"),
                ["requiredRegionIds"] = new JsonArray("root"),
                ["requiredMetadataStructureIds"] = new JsonArray(),
                ["requiredCapabilityIds"] = new JsonArray(),
            },
            ["inputSlots"] = new JsonArray
            {
                RuntimeReferenceReplaceSlot("reference", "reference-image", "exactly-one", new JsonObject
                {
                    ["kind"] = "exact-resolved-map-capacity",
                }),
                RuntimeReferenceReplaceSlot("source", "auxiliary", "one-or-more", new JsonObject
                {
                    ["kind"] = "bounded",
                    ["minimumBytes"] = 1,
                    ["maximumBytes"] = int.MaxValue,
                }),
            },
            ["spaces"] = new JsonArray
            {
                new JsonObject
                {
                    ["spaceId"] = "reference-image",
                    ["kind"] = "input-artifact",
                    ["slotId"] = "reference",
                    ["instancePolicy"] = "singleton",
                },
                new JsonObject
                {
                    ["spaceId"] = "source-template",
                    ["kind"] = "input-artifact",
                    ["slotId"] = "source",
                    ["instancePolicy"] = "per-binding",
                },
                new JsonObject
                {
                    ["spaceId"] = "output-image",
                    ["kind"] = "output-image",
                    ["capacity"] = new JsonObject { ["kind"] = "runtime-request" },
                    ["initializer"] = new JsonObject
                    {
                        ["kind"] = "clone",
                        ["sourceSlotId"] = "reference",
                    },
                },
            },
            ["views"] = new JsonArray(),
            ["metadataBindings"] = new JsonArray(),
            ["regionAccessRules"] = new JsonArray
            {
                new JsonObject
                {
                    ["regionId"] = "root",
                    ["access"] = "explicit-range",
                    ["reason"] = "Synthetic map-bound General Replace target.",
                },
            },
            ["operations"] = new JsonArray(),
            ["validations"] = new JsonArray(),
            ["processorStages"] = new JsonArray(),
            ["output"] = new JsonObject
            {
                ["fileNameTemplate"] = "runtime-general-replace.bin",
                ["allowOverride"] = true,
                ["invalidCharacterPolicy"] = "reject",
                ["requiredTokenIds"] = new JsonArray(),
            },
            ["evidenceRefs"] = new JsonArray("runtime-reference-replace-contract"),
        };
        return profile.ToJsonString();
    }

    private static JsonObject RuntimeReferenceReplaceSlot(
        string slotId,
        string artifactClass,
        string cardinality,
        JsonObject lengthRule)
    {
        return new JsonObject
        {
            ["slotId"] = slotId,
            ["role"] = slotId,
            ["artifactClass"] = artifactClass,
            ["required"] = true,
            ["cardinality"] = cardinality,
            ["acceptedExtensions"] = new JsonArray(".bin"),
            ["acceptance"] = new JsonObject
            {
                ["lengthRule"] = lengthRule,
                ["normalization"] = new JsonObject { ["kind"] = "none" },
            },
        };
    }
}
