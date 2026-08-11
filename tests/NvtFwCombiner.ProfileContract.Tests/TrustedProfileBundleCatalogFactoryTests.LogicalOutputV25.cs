using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    private const string LogicalTestMemberId = "NT00001";

    /// <summary>Verifies logical General Merge lowers through the existing plan and engine without a physical map claim.</summary>
    [Fact]
    public void LogicalOutputLoweringCompilesAndExecutesThroughTheSharedEngine()
    {
        TrustedProfileBundleCatalog catalog = CreateLogicalOutputCatalog();
        V2CompositionPlanCompileResult result = catalog.CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            LogicalRequest(outputCapacity: 6));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        _ = Assert.IsType<LogicalOutputV2CompilationContext>(composition.V2Details.Provenance.Context);
        Assert.Empty(composition.V2Details.RegionAccessContract.Requirements);
        Assert.Empty(composition.V2Details.RegionAccessContract.ResolvedViews);

        CompositionExecutionResult execution = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["source-a"] = [0xAA, 0xBB, 0xCC, 0xDD],
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal([0, 0xAA, 0xBB, 0, 0, 0], execution.OutputBytes.ToArray());
    }

    /// <summary>Verifies the logical compiler cannot mint an artifact below the compilable promotion boundary.</summary>
    [Fact]
    public void LogicalOutputLoweringRequiresCompilablePromotion()
    {
        TrustedProfileBundleCatalog catalog = CreateLogicalOutputCatalog("authorable");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => catalog.CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            LogicalRequest(outputCapacity: 6)));

        Assert.StartsWith("Only compilable v2 profiles", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies one rejected logical member returns only its request-scoped admission issue.</summary>
    [Fact]
    public void LogicalOutputLoweringRejectsUnadmittedMemberWithoutMapResolution()
    {
        V2CompositionPlanCompileResult result = CreateLogicalOutputCatalog().CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            "NT00002",
            LogicalRequest(outputCapacity: 6));

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.logical.member-not-admitted", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies a logical profile cannot name a member outside its exact trusted family identity.</summary>
    [Fact]
    public void LogicalOutputCatalogRejectsMemberOutsideItsExactFamily()
    {
        string familyJson = TrustedV2BundleTestDocuments.FamilyJson();
        string familyHash = Hash(familyJson);
        string profileJson = LogicalOutputProfileJson(familyHash, "NT00002");
        using var profileDocument = JsonDocument.Parse(profileJson);

        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            CreateCatalogFromSources(
                [Family("family-entry", familyHash, TrustedV2BundleTestDocuments.Family())],
                [Profile("logical-profile-entry", Hash(profileJson), profileDocument.RootElement.Clone())]));

        Assert.Equal("profile-bundle.catalog.logical-member-missing", exception.Code);
        Assert.Equal("logical-profile-entry", exception.EntryId);
    }

    /// <summary>Verifies logical request capacity and concrete mapping bounds remain compiler-checked before plan creation.</summary>
    [Fact]
    public void LogicalOutputLoweringRejectsInvalidCapacityAndSourceRange()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeneralMergeOutputInitializer(0));

        V2CompositionPlanCompileResult result = CreateLogicalOutputCatalog().CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(6),
                [new V2ExplicitMappingInputBinding("source-a", "source", 4)],
                [new ExplicitMapping(
                    "copy-source",
                    10,
                    ExplicitMappingOperationKind.CopyRange,
                    "source-a",
                    new ByteRange(3, 2),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "test logical mapping")]));
        V2CompositionPlanCompileResult oversizedBinding = CreateLogicalOutputCatalog().CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(6),
                [new V2ExplicitMappingInputBinding("source-a", "source", (long)int.MaxValue + 1)],
                [Mapping("copy-source", 10, new ByteRange(0, 2), new ByteRange(0, 2))]));
        V2CompositionPlanCompileResult maximumBinding = CreateLogicalOutputCatalog().CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(6),
                [new V2ExplicitMappingInputBinding("source-a", "source", int.MaxValue)],
                [Mapping("copy-source", 10, new ByteRange(0, 2), new ByteRange(0, 2))]));

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.logical.source-out-of-bounds");
        Assert.False(oversizedBinding.IsCompiled);
        Assert.Null(oversizedBinding.CompiledComposition);
        Assert.Contains(oversizedBinding.Issues, issue => issue.Code == "profile.v2.logical.binding-invalid");
        Assert.True(maximumBinding.IsCompiled);
    }

    /// <summary>Verifies malformed logical mappings are rejected only during the selected request's lowering.</summary>
    [Fact]
    public void LogicalOutputLoweringRejectsDuplicateAndOverlappingMappings()
    {
        TrustedProfileBundleCatalog catalog = CreateLogicalOutputCatalog();
        ExplicitMapping first = Mapping("copy-first", 10, new ByteRange(0, 2), new ByteRange(0, 2));
        V2CompositionPlanCompileResult duplicate = catalog.CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(6),
                [new V2ExplicitMappingInputBinding("source-a", "source", 4)],
                [first, Mapping("copy-first", 11, new ByteRange(2, 2), new ByteRange(2, 2))]));
        V2CompositionPlanCompileResult overlapping = catalog.CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(6),
                [new V2ExplicitMappingInputBinding("source-a", "source", 4)],
                [first, Mapping("copy-second", 11, new ByteRange(2, 2), new ByteRange(1, 2))]));

        Assert.Contains(duplicate.Issues, issue => issue.Code == "profile.v2.logical.mapping-invalid");
        Assert.Contains(overlapping.Issues, issue => issue.Code == "profile.v2.plan.operation-overlap");
    }

    /// <summary>Locks every compiler-owned logical binding and mapping guard at the shared request seam.</summary>
    [Theory]
    [InlineData("duplicate-binding", "profile.v2.logical.binding-invalid")]
    [InlineData("duplicate-sequence", "profile.v2.logical.mapping-invalid")]
    [InlineData("unknown-binding", "profile.v2.logical.mapping-invalid")]
    [InlineData("wrong-kind", "profile.v2.logical.mapping-invalid")]
    [InlineData("wrong-overlap", "profile.v2.logical.mapping-invalid")]
    [InlineData("source-start-alignment", "profile.v2.logical.mapping-invalid")]
    [InlineData("source-length-alignment", "profile.v2.logical.mapping-invalid")]
    [InlineData("target-out-of-bounds", "profile.v2.logical.target-out-of-bounds")]
    public void LogicalOutputLoweringRejectsInvalidExplicitMappingRequest(
        string mutation,
        string expectedIssueCode)
    {
        V2ExplicitMappingInputBinding[] bindings =
            [new("source-a", "source", 4)];
        ExplicitMapping[] mappings =
            [Mapping("copy-source", 10, new ByteRange(0, 2), new ByteRange(0, 2))];
        switch (mutation)
        {
            case "duplicate-binding":
                bindings = [.. bindings, new("source-a", "source", 4)];
                break;
            case "duplicate-sequence":
                mappings =
                [
                    mappings[0],
                    Mapping("copy-second", 10, new ByteRange(2, 2), new ByteRange(2, 2)),
                ];
                break;
            case "unknown-binding":
                mappings =
                    [Mapping("copy-source", 10, new ByteRange(0, 2), new ByteRange(0, 2), "missing")];
                break;
            case "wrong-kind":
                mappings =
                [
                    Mapping(
                        "copy-source",
                        10,
                        new ByteRange(0, 2),
                        new ByteRange(0, 2),
                        operationKind: ExplicitMappingOperationKind.ReplaceRange),
                ];
                break;
            case "wrong-overlap":
                mappings =
                [
                    Mapping(
                        "copy-source",
                        10,
                        new ByteRange(0, 2),
                        new ByteRange(0, 2),
                        overlapPolicy: OverlapPolicy.ReplaceExisting),
                ];
                break;
            case "source-start-alignment":
                mappings =
                [Mapping("copy-source", 10, new ByteRange(1, 2), new ByteRange(0, 2), alignment: 2)];
                break;
            case "source-length-alignment":
                bindings = [new("source-a", "source", 6)];
                mappings =
                [Mapping("copy-source", 10, new ByteRange(0, 3), new ByteRange(0, 3), alignment: 2)];
                break;
            case "target-out-of-bounds":
                mappings =
                    [Mapping("copy-source", 10, new ByteRange(0, 2), new ByteRange(5, 2))];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown logical mutation.");
        }

        V2CompositionPlanCompileResult result = CreateLogicalOutputCatalog().CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(6),
                bindings,
                mappings));

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Contains(result.Issues, issue => issue.Code == expectedIssueCode);
    }

    /// <summary>Verifies a malformed input/output address-space collision stays within that logical compile request.</summary>
    [Fact]
    public void LogicalOutputLoweringRejectsOutputSpaceBindingCollisionWithoutAffectingLaterRequests()
    {
        TrustedProfileBundleCatalog catalog = CreateLogicalOutputCatalog();
        V2CompositionPlanCompileResult rejected = catalog.CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(6),
                [new V2ExplicitMappingInputBinding(CompositionAddressSpaceIds.OutputImage, "source", 4)],
                [new ExplicitMapping(
                    "copy-source",
                    10,
                    ExplicitMappingOperationKind.CopyRange,
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 2),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(1, 2),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "test logical mapping")]));
        V2CompositionPlanCompileResult valid = catalog.CompileLogicalOutput(
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            LogicalRequest(outputCapacity: 6));

        Assert.False(rejected.IsCompiled);
        Assert.Contains(rejected.Issues, issue => issue.Code == "profile.v2.logical.binding-invalid");
        Assert.True(valid.IsCompiled);
    }

    /// <summary>Verifies concrete logical request facts participate in the atomic compilation fingerprint.</summary>
    [Fact]
    public void LogicalOutputCompilationFingerprintIncludesRequestedCapacity()
    {
        TrustedProfileBundleCatalog catalog = CreateLogicalOutputCatalog();
        CompiledComposition six = Assert.IsType<CompiledComposition>(
catalog.CompileLogicalOutput(
                "logical-general-merge",
                "1.0.0",
            LogicalTestMemberId,
                LogicalRequest(outputCapacity: 6)).CompiledComposition);
        CompiledComposition seven = Assert.IsType<CompiledComposition>(
catalog.CompileLogicalOutput(
                "logical-general-merge",
                "1.0.0",
            LogicalTestMemberId,
                LogicalRequest(outputCapacity: 7)).CompiledComposition);

        Assert.Equal(
            "5a94482a7990095f25ca562dc4b5fa7f523deac261b35575524f469924a508e0",
            six.CompilationFingerprint);
        Assert.NotEqual(six.CompilationFingerprint, seven.CompilationFingerprint);
    }

    /// <summary>Verifies the exact typed initializer controls blank bytes and compiled identity.</summary>
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x5A)]
    [InlineData(0xFF)]
    public void LogicalOutputInitializerControlsFillAndFingerprint(int fillByte)
    {
        TrustedProfileBundleCatalog catalog = CreateLogicalOutputCatalog();
        CompiledComposition composition = Assert.IsType<CompiledComposition>(
catalog.CompileLogicalOutput(
                "logical-general-merge",
                "1.0.0",
                LogicalTestMemberId,
                LogicalRequest(
                    outputCapacity: 6,
                    fillByte: checked((byte)fillByte))).CompiledComposition);

        CompositionExecutionResult execution = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["source-a"] = [0xAA, 0xBB, 0xCC, 0xDD],
            }));

        Assert.Equal(
            [checked((byte)fillByte), 0xAA, 0xBB, checked((byte)fillByte), checked((byte)fillByte), checked((byte)fillByte)],
            execution.OutputBytes.ToArray());
        if (fillByte != 0)
        {
            CompiledComposition zero = Assert.IsType<CompiledComposition>(
catalog.CompileLogicalOutput(
                    "logical-general-merge",
                    "1.0.0",
                    LogicalTestMemberId,
                    LogicalRequest(outputCapacity: 6)).CompiledComposition);
            Assert.NotEqual(zero.CompilationFingerprint, composition.CompilationFingerprint);
        }
    }

    private static TrustedProfileBundleCatalog CreateLogicalOutputCatalog(
        string promotionStage = "compilable")
    {
        string familyJson = TrustedV2BundleTestDocuments.FamilyJson();
        string familyHash = Hash(familyJson);
        string profileJson = LogicalOutputProfileJson(familyHash, promotionStage: promotionStage);
        using var profileDocument = JsonDocument.Parse(profileJson);
        return CreateCatalogFromSources(
            [Family("family-entry", familyHash, TrustedV2BundleTestDocuments.Family())],
            [Profile("logical-profile-entry", Hash(profileJson), profileDocument.RootElement.Clone())]);
    }

    private static V2LogicalOutputCompileRequest LogicalRequest(
        int outputCapacity,
        byte fillByte = GeneralMergeOutputInitializer.DefaultFillByte)
    {
        return new V2LogicalOutputCompileRequest(
            new GeneralMergeOutputInitializer(outputCapacity, fillByte),
            [new V2ExplicitMappingInputBinding("source-a", "source", 4)],
            [new ExplicitMapping(
                "copy-source",
                10,
                ExplicitMappingOperationKind.CopyRange,
                "source-a",
                new ByteRange(0, 2),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(1, 2),
                OverlapPolicy.Reject,
                alignment: 1,
                reason: "test logical mapping")]);
    }

    private static ExplicitMapping Mapping(
        string mappingId,
        int sequence,
        ByteRange source,
        ByteRange target,
        string sourceBindingId = "source-a",
        ExplicitMappingOperationKind operationKind = ExplicitMappingOperationKind.CopyRange,
        OverlapPolicy overlapPolicy = OverlapPolicy.Reject,
        int alignment = 1)
    {
        return new ExplicitMapping(
            mappingId,
            sequence,
            operationKind,
            sourceBindingId,
            source,
            CompositionAddressSpaceIds.OutputImage,
            target,
            overlapPolicy,
            alignment,
            reason: "test logical mapping");
    }

    private static string LogicalOutputProfileJson(
        string familyHash,
        string memberId = LogicalTestMemberId,
        string promotionStage = "compilable")
    {
        var profile = new JsonObject
        {
            ["schemaVersion"] = "2.5",
            ["profileId"] = "logical-general-merge",
            ["profileVersion"] = "1.0.0",
            ["promotion"] = new JsonObject
            {
                ["stage"] = promotionStage,
                ["blockers"] = new JsonArray(),
            },
            ["compositionKind"] = "merge",
            ["experience"] = new JsonObject
            {
                ["experienceId"] = "general-merge",
                ["audience"] = "advanced",
                ["layoutPolicy"] = "user-defined",
                ["inputPolicy"] = "extensible",
                ["topologyAuthoring"] = "hidden",
                ["displayNameKey"] = "logical-general-merge",
            },
            ["compilationContext"] = new JsonObject { ["kind"] = "logical-output" },
            ["logicalOutputBinding"] = new JsonObject
            {
                ["familyId"] = "family",
                ["familyVersion"] = "1.0.0",
                ["familyContentHash"] = familyHash,
                ["memberIds"] = new JsonArray(memberId),
            },
            ["inputSlots"] = new JsonArray
            {
                new JsonObject
                {
                    ["slotId"] = "source",
                    ["role"] = "source",
                    ["artifactClass"] = "auxiliary",
                    ["required"] = true,
                    ["cardinality"] = "one-or-more",
                    ["acceptedExtensions"] = new JsonArray(".bin"),
                    ["acceptance"] = new JsonObject
                    {
                        ["lengthRule"] = new JsonObject
                        {
                            ["kind"] = "bounded",
                            ["minimumBytes"] = 1,
                            ["maximumBytes"] = int.MaxValue,
                        },
                        ["normalization"] = new JsonObject { ["kind"] = "none" },
                    },
                },
            },
            ["spaces"] = new JsonArray
            {
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
                        ["kind"] = "blank",
                        ["fillByte"] = 0,
                    },
                },
            },
            ["views"] = new JsonArray(),
            ["metadataBindings"] = new JsonArray(),
            ["regionAccessRules"] = new JsonArray(),
            ["operations"] = new JsonArray(),
            ["validations"] = new JsonArray(),
            ["processorStages"] = new JsonArray(),
            ["output"] = new JsonObject
            {
                ["fileNameTemplate"] = "member-general-merge.bin",
                ["allowOverride"] = true,
                ["invalidCharacterPolicy"] = "reject",
                ["requiredTokenIds"] = new JsonArray(),
            },
            ["evidenceRefs"] = new JsonArray("logical-output-contract"),
        };
        return profile.ToJsonString();
    }
}
