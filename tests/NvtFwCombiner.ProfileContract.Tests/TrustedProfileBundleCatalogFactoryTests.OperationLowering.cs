using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies profile-owned fill and patch declarations lower through the same V2 plan and target access gate.</summary>
    [Fact]
    public void BlankOutputLoweringBuildsFillAndPatchOperations()
    {
        V2CompositionPlanCompileResult fillResult = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash), operation =>
            {
                operation["operationId"] = "fill-output";
                operation["kind"] = "fill-range";
                _ = operation.Remove("sourceViewId");
                operation["fillByte"] = 90;
            })));
        V2CompositionPlanCompileResult patchResult = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash), operation =>
            {
                operation["operationId"] = "patch-output";
                operation["kind"] = "patch-scalar";
                _ = operation.Remove("sourceViewId");
                operation["valueHex"] = "00112233445566778899aabbccddeeff";
            })));

        CompositionOperation fill = Assert.Single(fillResult.CompiledComposition!.Plan.OrderedOperations);
        Assert.Equal(CompositionOperationKind.FillRange, fill.Kind);
        Assert.Equal((byte)90, fill.FillByte);
        Assert.Equal(new ByteRange(0, 16), fill.TargetRange);
        CompositionOperation patch = Assert.Single(patchResult.CompiledComposition!.Plan.OrderedOperations);
        Assert.Equal(CompositionOperationKind.PatchScalar, patch.Kind);
        Assert.Equal("00112233445566778899aabbccddeeff", Convert.ToHexString(patch.PatchBytes.Span).ToLowerInvariant());
        Assert.Equal(new ByteRange(0, 16), patch.TargetRange);
    }

    /// <summary>Verifies every closed scalar width and byte order maps without widening its checked transform semantics.</summary>
    [Theory]
    [InlineData(1, "little", 1, 2)]
    [InlineData(2, "big", -1, 2)]
    [InlineData(4, "little", -1, null)]
    [InlineData(8, "big", 1, null)]
    public void BlankOutputLoweringMapsClosedTransformScalarValues(
        int widthBytes,
        string byteOrder,
        int addend,
        int? expectedBefore)
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTransformScalar(
                SupportedProfileJson(familyHash, access: "explicit-range"),
                widthBytes,
                addend,
                byteOrder,
                expectedBefore,
                viewLength: widthBytes),
            FamilyJsonWithRootWriteConstraint("explicit-range")));

        CompositionOperation operation = Assert.Single(result.CompiledComposition!.Plan.OrderedOperations);
        Assert.Equal(CompositionOperationKind.TransformScalar, operation.Kind);
        Assert.Equal("tp-source", operation.SourceSpaceId);
        Assert.Equal(new ByteRange(0, widthBytes), operation.SourceRange);
        Assert.Equal("output", operation.TargetSpaceId);
        Assert.Equal(new ByteRange(0, widthBytes), operation.TargetRange);
        ScalarTransform transform = Assert.IsType<ScalarTransform>(operation.ScalarTransform);
        Assert.Equal((ScalarTransformWidth)widthBytes, transform.Width);
        Assert.Equal(
            byteOrder == "little" ? ScalarTransformByteOrder.LittleEndian : ScalarTransformByteOrder.BigEndian,
            transform.ByteOrder);
        Assert.Equal((BigInteger)addend, transform.Addend);
        Assert.Equal(expectedBefore is { } expected ? (ulong?)expected : null, transform.ExpectedBefore);
        Assert.Equal(ScalarTransformOverflowPolicy.Reject, transform.OverflowPolicy);
    }

    /// <summary>Verifies patch length and scalar width mismatches fail before a V2 plan artifact is minted.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsPatchAndScalarWidthMismatches()
    {
        V2CompositionPlanCompileResult patch = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash), operation =>
            {
                operation["kind"] = "patch-scalar";
                _ = operation.Remove("sourceViewId");
                operation["valueHex"] = "aa";
            })));
        V2CompositionPlanCompileResult transform = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTransformScalar(SupportedProfileJson(familyHash, access: "explicit-range"), widthBytes: 2),
            FamilyJsonWithRootWriteConstraint("explicit-range")));

        Assert.Null(patch.CompiledComposition);
        Assert.Equal("profile.v2.plan.operation-length-mismatch", Assert.Single(patch.Issues).Code);
        Assert.Null(transform.CompiledComposition);
        Assert.Equal("profile.v2.plan.scalar-width-mismatch", Assert.Single(transform.Issues).Code);
    }

    /// <summary>Verifies a scalar value outside the declared transform width fails before an engine operation is minted.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsUnrepresentableScalarTransform()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTransformScalar(
                SupportedProfileJson(familyHash, access: "explicit-range"),
                widthBytes: 1,
                addend: 256),
            FamilyJsonWithRootWriteConstraint("explicit-range")));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.invalid-scalar-transform", Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies every newly admitted target-writing operation remains subject to the same read-only access denial.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsNewTargetOperationsWithoutWriteAccess()
    {
        V2CompositionPlanCompileResult fill = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash, access: "read-only"), operation =>
            {
                operation["kind"] = "fill-range";
                _ = operation.Remove("sourceViewId");
                operation["fillByte"] = 90;
            }),
            FamilyJsonWithRootWriteConstraint("whole-region")));
        V2CompositionPlanCompileResult patch = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash, access: "read-only"), operation =>
            {
                operation["kind"] = "patch-scalar";
                _ = operation.Remove("sourceViewId");
                operation["valueHex"] = "00112233445566778899aabbccddeeff";
            }),
            FamilyJsonWithRootWriteConstraint("whole-region")));
        V2CompositionPlanCompileResult transform = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTransformScalar(SupportedProfileJson(familyHash, access: "read-only"), widthBytes: 1),
            FamilyJsonWithRootWriteConstraint("whole-region")));

        Assert.All(
            [fill, patch, transform],
            result => Assert.Equal("profile.v2.plan.region-access-denied", Assert.Single(result.Issues).Code));
    }

    /// <summary>Verifies a profile overlap returns a deterministic compiler issue instead of leaking a plan-construction exception.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsOverlappingRejectWritesWithoutAnArtifact()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOverlappingOperations(SupportedProfileJson(familyHash))));

        Assert.Null(result.CompiledComposition);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("profile.v2.plan.operation-overlap", issue.Code);
        Assert.Equal("patch-output", issue.OperationId);
        Assert.Contains("fill-output", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies adjacent half-open target ranges remain non-overlapping and preserve canonical sequence and provenance.</summary>
    [Fact]
    public void BlankOutputLoweringAcceptsAdjacentWritesInDeterministicOrder()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithAdjacentOperations(SupportedProfileJson(familyHash, access: "explicit-range")),
            FamilyJsonWithRootWriteConstraint("explicit-range")));

        CompositionOperation[] operations = [.. result.CompiledComposition!.Plan.OrderedOperations];
        Assert.Equal(["patch-tail", "fill-head"], operations.Select(static operation => operation.OperationId));
        Assert.Equal([1, 10], operations.Select(static operation => operation.Sequence));
        Assert.Equal([new ByteRange(8, 8), new ByteRange(0, 8)], operations.Select(static operation => operation.TargetRange));
        Assert.All(operations, static operation => Assert.Equal("built-in-profile", operation.Provenance.Kind));
    }

    /// <summary>Verifies operation-specific profile values are bound into the compilation fingerprint.</summary>
    [Fact]
    public void BlankOutputLoweringFingerprintsNewOperationSemantics()
    {
        V2CompositionPlanCompileResult fill90 = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash), operation =>
            {
                operation["kind"] = "fill-range";
                _ = operation.Remove("sourceViewId");
                operation["fillByte"] = 90;
            })));
        V2CompositionPlanCompileResult fill91 = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash), operation =>
            {
                operation["kind"] = "fill-range";
                _ = operation.Remove("sourceViewId");
                operation["fillByte"] = 91;
            })));
        V2CompositionPlanCompileResult patchA = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash), operation =>
            {
                operation["kind"] = "patch-scalar";
                _ = operation.Remove("sourceViewId");
                operation["valueHex"] = "00112233445566778899aabbccddeeff";
            })));
        V2CompositionPlanCompileResult patchB = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithOperation(SupportedProfileJson(familyHash), operation =>
            {
                operation["kind"] = "patch-scalar";
                _ = operation.Remove("sourceViewId");
                operation["valueHex"] = "00112233445566778899aabbccddeefe";
            })));
        V2CompositionPlanCompileResult transformLittle = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTransformScalar(SupportedProfileJson(familyHash, access: "explicit-range"), 1),
            FamilyJsonWithRootWriteConstraint("explicit-range")));
        V2CompositionPlanCompileResult transformBig = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTransformScalar(SupportedProfileJson(familyHash, access: "explicit-range"), 1, byteOrder: "big"),
            FamilyJsonWithRootWriteConstraint("explicit-range")));
        V2CompositionPlanCompileResult transformAddend = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTransformScalar(SupportedProfileJson(familyHash, access: "explicit-range"), 1, addend: -1),
            FamilyJsonWithRootWriteConstraint("explicit-range")));
        V2CompositionPlanCompileResult transformExpected = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithTransformScalar(SupportedProfileJson(familyHash, access: "explicit-range"), 1, expectedBefore: 3),
            FamilyJsonWithRootWriteConstraint("explicit-range")));

        Assert.NotEqual(fill90.CompiledComposition!.CompilationFingerprint, fill91.CompiledComposition!.CompilationFingerprint);
        Assert.NotEqual(patchA.CompiledComposition!.CompilationFingerprint, patchB.CompiledComposition!.CompilationFingerprint);
        Assert.NotEqual(transformLittle.CompiledComposition!.CompilationFingerprint, transformBig.CompiledComposition!.CompilationFingerprint);
        Assert.NotEqual(transformLittle.CompiledComposition!.CompilationFingerprint, transformAddend.CompiledComposition!.CompilationFingerprint);
        Assert.NotEqual(transformLittle.CompiledComposition!.CompilationFingerprint, transformExpected.CompiledComposition!.CompilationFingerprint);
    }

    /// <summary>Verifies Merge clone initialization is rejected before compiler lowering can grant an executable shape.</summary>
    [Fact]
    public void BlankOutputPreparationRejectsCloneInitializer()
    {
        _ = Assert.Throws<TrustedProfileBundleCatalogException>(() => PrepareSupportedBlankCopy(
            familyHash => ProfileWithCloneOutputInitializer(SupportedProfileJson(familyHash))));
    }

    /// <summary>Verifies an additional mutable work buffer remains outside the non-executable blank-output subset.</summary>
    [Fact]
    public void BlankOutputLoweringRejectsWorkBufferWithoutAnArtifact()
    {
        V2CompositionPlanCompileResult result = V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
            familyHash => ProfileWithWorkBuffer(SupportedProfileJson(familyHash))));

        Assert.Null(result.CompiledComposition);
        Assert.Equal("profile.v2.plan.unsupported-declaration", Assert.Single(result.Issues).Code);
    }

    private static string ProfileWithOperation(string profileJson, Action<JsonObject> mutateOperation)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        mutateOperation(Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["operations"])[0]));
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithTransformScalar(
        string profileJson,
        int widthBytes,
        int addend = 1,
        string byteOrder = "little",
        int? expectedBefore = 2,
        int viewLength = 1)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        Assert.IsType<JsonObject>(views[0])["selector"] = new JsonObject
        {
            ["kind"] = "map-region-slice",
            ["regionId"] = "root",
            ["offset"] = 0,
            ["length"] = viewLength,
        };
        Assert.IsType<JsonObject>(views[1])["selector"] = new JsonObject
        {
            ["kind"] = "space-range",
            ["range"] = new JsonObject { ["start"] = 0, ["length"] = viewLength },
        };
        JsonObject operation = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["operations"])[0]);
        operation.Clear();
        operation["operationId"] = "transform-scalar";
        operation["sequence"] = 0;
        operation["overlapPolicy"] = "reject";
        operation["reason"] = "Transform one synthetic scalar.";
        operation["kind"] = "transform-scalar";
        operation["sourceViewId"] = "tp-code";
        operation["targetViewId"] = "output-code";
        operation["widthBytes"] = widthBytes;
        operation["byteOrder"] = byteOrder;
        operation["valueInterpretation"] = "unsigned";
        operation["addend"] = addend;
        if (expectedBefore is { } expected)
        {
            operation["expectedBefore"] = expected;
        }

        operation["overflowPolicy"] = "reject";
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithOverlappingOperations(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);
        JsonObject fill = Assert.IsType<JsonObject>(operations[0]);
        ConfigureFill(
            fill,
            operationId: "fill-output",
            sequence: 0,
            targetViewId: "output-code",
            fillByte: 90);
        operations.Add(new JsonObject
        {
            ["operationId"] = "patch-output",
            ["sequence"] = 1,
            ["overlapPolicy"] = "reject",
            ["reason"] = "Patch the same synthetic output range.",
            ["kind"] = "patch-scalar",
            ["targetViewId"] = "output-code",
            ["valueHex"] = "00112233445566778899aabbccddeeff",
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithAdjacentOperations(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray views = Assert.IsType<JsonArray>(profile["views"]);
        Assert.IsType<JsonObject>(views[1])["selector"] = new JsonObject
        {
            ["kind"] = "space-range",
            ["range"] = new JsonObject { ["start"] = 0, ["length"] = 8 },
        };
        views.Add(new JsonObject
        {
            ["viewId"] = "output-tail",
            ["spaceId"] = "output",
            ["selector"] = new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject { ["start"] = 8, ["length"] = 8 },
            },
        });
        JsonArray operations = Assert.IsType<JsonArray>(profile["operations"]);
        ConfigureFill(
            Assert.IsType<JsonObject>(operations[0]),
            operationId: "fill-head",
            sequence: 10,
            targetViewId: "output-code",
            fillByte: 90);
        operations.Add(new JsonObject
        {
            ["operationId"] = "patch-tail",
            ["sequence"] = 1,
            ["overlapPolicy"] = "reject",
            ["reason"] = "Patch the adjacent synthetic output range.",
            ["kind"] = "patch-scalar",
            ["targetViewId"] = "output-tail",
            ["valueHex"] = "0011223344556677",
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithCloneOutputInitializer(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray spaces = Assert.IsType<JsonArray>(profile["spaces"]);
        Assert.IsType<JsonObject>(spaces[1])["initializer"] = new JsonObject
        {
            ["kind"] = "clone",
            ["sourceSlotId"] = "tp-input",
        };
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ProfileWithWorkBuffer(string profileJson)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        JsonArray spaces = Assert.IsType<JsonArray>(profile["spaces"]);
        spaces.Add(new JsonObject
        {
            ["spaceId"] = "scratch",
            ["kind"] = "work-buffer",
            ["capacity"] = new JsonObject { ["kind"] = "resolved-map" },
            ["initializer"] = new JsonObject
            {
                ["kind"] = "blank",
                ["fillByte"] = 0,
            },
        });
        return profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void ConfigureFill(
        JsonObject operation,
        string operationId,
        int sequence,
        string targetViewId,
        int fillByte)
    {
        operation.Clear();
        operation["operationId"] = operationId;
        operation["sequence"] = sequence;
        operation["overlapPolicy"] = "reject";
        operation["reason"] = "Fill one synthetic output range.";
        operation["kind"] = "fill-range";
        operation["targetViewId"] = targetViewId;
        operation["fillByte"] = fillByte;
    }
}
