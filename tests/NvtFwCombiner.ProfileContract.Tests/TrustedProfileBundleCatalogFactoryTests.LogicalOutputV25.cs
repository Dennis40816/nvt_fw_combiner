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
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileLogicalOutput(
            catalog,
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            LogicalRequest(outputCapacity: 6));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        _ = Assert.IsType<LogicalOutputV2CompilationContext>(composition.V2Details!.Provenance.Context);
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

    /// <summary>Verifies one rejected logical member returns only its request-scoped admission issue.</summary>
    [Fact]
    public void LogicalOutputLoweringRejectsUnadmittedMemberWithoutMapResolution()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileLogicalOutput(
            CreateLogicalOutputCatalog(),
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
            TrustedProfileBundleCatalogFactory.Create(Source(
                [Family("family-entry", familyHash, TrustedV2BundleTestDocuments.Family())],
                [Profile("logical-profile-entry", Hash(profileJson), profileDocument.RootElement.Clone())])));

        Assert.Equal("profile-bundle.catalog.logical-member-missing", exception.Code);
        Assert.Equal("logical-profile-entry", exception.EntryId);
    }

    /// <summary>Verifies logical request capacity and concrete mapping bounds remain compiler-checked before plan creation.</summary>
    [Fact]
    public void LogicalOutputLoweringRejectsInvalidCapacityAndSourceRange()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileLogicalOutput(
            CreateLogicalOutputCatalog(),
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                outputCapacity: 0,
                [new V2LogicalOutputInputBinding("source-a", "source", 4)],
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

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.logical.output-capacity-invalid");
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.logical.source-out-of-bounds");
    }

    /// <summary>Verifies malformed logical mappings are rejected only during the selected request's lowering.</summary>
    [Fact]
    public void LogicalOutputLoweringRejectsDuplicateAndOverlappingMappings()
    {
        TrustedProfileBundleCatalog catalog = CreateLogicalOutputCatalog();
        ExplicitMapping first = Mapping("copy-first", 10, new ByteRange(0, 2), new ByteRange(0, 2));
        V2CompositionPlanCompileResult duplicate = TrustedV2CompositionCompiler.CompileLogicalOutput(
            catalog,
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                6,
                [new V2LogicalOutputInputBinding("source-a", "source", 4)],
                [first, Mapping("copy-first", 11, new ByteRange(2, 2), new ByteRange(2, 2))]));
        V2CompositionPlanCompileResult overlapping = TrustedV2CompositionCompiler.CompileLogicalOutput(
            catalog,
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                6,
                [new V2LogicalOutputInputBinding("source-a", "source", 4)],
                [first, Mapping("copy-second", 11, new ByteRange(2, 2), new ByteRange(1, 2))]));

        Assert.Contains(duplicate.Issues, issue => issue.Code == "profile.v2.logical.mapping-invalid");
        Assert.Contains(overlapping.Issues, issue => issue.Code == "profile.v2.plan.operation-overlap");
    }

    /// <summary>Verifies a malformed input/output address-space collision stays within that logical compile request.</summary>
    [Fact]
    public void LogicalOutputLoweringRejectsOutputSpaceBindingCollisionWithoutAffectingLaterRequests()
    {
        TrustedProfileBundleCatalog catalog = CreateLogicalOutputCatalog();
        V2CompositionPlanCompileResult rejected = TrustedV2CompositionCompiler.CompileLogicalOutput(
            catalog,
            "logical-general-merge",
            "1.0.0",
            LogicalTestMemberId,
            new V2LogicalOutputCompileRequest(
                6,
                [new V2LogicalOutputInputBinding(CompositionAddressSpaceIds.OutputImage, "source", 4)],
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
        V2CompositionPlanCompileResult valid = TrustedV2CompositionCompiler.CompileLogicalOutput(
            catalog,
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
            TrustedV2CompositionCompiler.CompileLogicalOutput(
                catalog,
                "logical-general-merge",
                "1.0.0",
            LogicalTestMemberId,
                LogicalRequest(outputCapacity: 6)).CompiledComposition);
        CompiledComposition seven = Assert.IsType<CompiledComposition>(
            TrustedV2CompositionCompiler.CompileLogicalOutput(
                catalog,
                "logical-general-merge",
                "1.0.0",
            LogicalTestMemberId,
                LogicalRequest(outputCapacity: 7)).CompiledComposition);

        Assert.Equal(
            "e859723cdbca137445a9dc3e6113ac9806e9e63a2cde28982043e85e37dda763",
            six.CompilationFingerprint);
        Assert.NotEqual(six.CompilationFingerprint, seven.CompilationFingerprint);
    }

    private static TrustedProfileBundleCatalog CreateLogicalOutputCatalog()
    {
        string familyJson = TrustedV2BundleTestDocuments.FamilyJson();
        string familyHash = Hash(familyJson);
        string profileJson = LogicalOutputProfileJson(familyHash);
        using var profileDocument = JsonDocument.Parse(profileJson);
        return TrustedProfileBundleCatalogFactory.Create(Source(
            [Family("family-entry", familyHash, TrustedV2BundleTestDocuments.Family())],
            [Profile("logical-profile-entry", Hash(profileJson), profileDocument.RootElement.Clone())]));
    }

    private static V2LogicalOutputCompileRequest LogicalRequest(int outputCapacity)
    {
        return new V2LogicalOutputCompileRequest(
            outputCapacity,
            [new V2LogicalOutputInputBinding("source-a", "source", 4)],
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

    private static ExplicitMapping Mapping(string mappingId, int sequence, ByteRange source, ByteRange target)
    {
        return new ExplicitMapping(
            mappingId,
            sequence,
            ExplicitMappingOperationKind.CopyRange,
            "source-a",
            source,
            CompositionAddressSpaceIds.OutputImage,
            target,
            OverlapPolicy.Reject,
            alignment: 1,
            reason: "test logical mapping");
    }

    private static string LogicalOutputProfileJson(string familyHash, string memberId = LogicalTestMemberId)
    {
        var profile = new JsonObject
        {
            ["schemaVersion"] = "2.5",
            ["profileId"] = "logical-general-merge",
            ["profileVersion"] = "1.0.0",
            ["promotion"] = new JsonObject
            {
                ["stage"] = "compilable",
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
