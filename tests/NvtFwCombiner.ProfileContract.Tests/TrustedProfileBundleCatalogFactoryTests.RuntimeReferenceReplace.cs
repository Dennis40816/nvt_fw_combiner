using System.Text.Json;
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
        V2CompositionPlanCompileResult result = catalog.CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest());

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        _ = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(composition.V2Details.Provenance.Context);
        Assert.Equal(ImageInitializationKind.Reference, composition.Plan.OutputInitialization.Kind);
        Assert.Equal("base", composition.Plan.OutputInitialization.ReferenceSpaceId);
        Assert.Equal("output-image", composition.Plan.OutputSpaceId);
        Assert.Equal(CompositionOperationKind.ReplaceRange, Assert.Single(composition.Plan.OrderedOperations).Kind);

        byte[] reference = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        byte[] source = [0xAA, 0xBB, 0xCC, 0xDD];
        byte[] originalReference = [.. reference];
        byte[] originalSource = [.. source];
        CompositionExecutionResult execution = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["base"] = reference,
                ["source-a"] = source,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 0xCC, 0xDD, 10, 11, 12, 13, 14, 15], execution.OutputBytes.ToArray());
        Assert.Equal(originalReference, reference);
        Assert.Equal(originalSource, source);
    }

    /// <summary>Verifies a denied map-bound target rejects only the selected runtime-reference-replace compilation request.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsForbiddenPhysicalTarget()
    {
        V2CompositionPlanCompileResult result = CreateRuntimeReferenceReplaceCatalog(FirmwareWriteConstraint.Forbidden).CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest());

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.plan.region-access-denied");
    }

    /// <summary>Verifies only the exact reference-image binding can select a runtime reference-replace map capacity.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsMissingReferenceLengthBeforeMapSelection()
    {
        V2CompositionPlanCompileResult result = CreateRuntimeReferenceReplaceCatalog().CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            new V2RuntimeReferenceReplaceCompileRequest(
                [new V2ExplicitMappingInputBinding("source-a", "source", 16)],
                [RuntimeReferenceReplaceMapping("replace-source", 10, new ByteRange(2, 2), new ByteRange(8, 2))]));

        Assert.False(result.IsCompiled);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.v2.runtime-reference-replace.reference-length-invalid");
    }

    /// <summary>Verifies only the exact singleton reference length selects a canonical map; auxiliary length has no selection authority.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringSelectsMapFromReferenceLengthOnly()
    {
        TrustedProfileBundleCatalog catalog = CreateRuntimeReferenceReplaceCatalog(
            mapDefinitions:
            [
                new RuntimeReferenceReplaceMapDocument("map-16", 16),
                new RuntimeReferenceReplaceMapDocument("map-32", 32),
            ]);

        V2CompositionPlanCompileResult result = catalog.CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(referenceLength: 16, sourceLength: 32));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal("map-16", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(16, composition.Plan.OutputInitialization.Capacity);
    }

    /// <summary>Verifies absent, ambiguous, or duplicate reference bindings reject only their selected request.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsInvalidReferenceMapSelectionWithoutStateLeakage()
    {
        TrustedProfileBundleCatalog catalog = CreateRuntimeReferenceReplaceCatalog(
            mapDefinitions:
            [
                new RuntimeReferenceReplaceMapDocument("map-16", 16),
                new RuntimeReferenceReplaceMapDocument("map-32", 32),
            ]);
        V2CompositionPlanCompileResult unavailable = catalog.CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(referenceLength: 24));
        V2CompositionPlanCompileResult duplicateReference = catalog.CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2ExplicitMappingInputBinding("base", "reference", 16),
                    new V2ExplicitMappingInputBinding("base-duplicate", "reference", 16),
                    new V2ExplicitMappingInputBinding("source-a", "source", 4),
                ],
                [RuntimeReferenceReplaceMapping("replace-source", 10, new ByteRange(2, 2), new ByteRange(8, 2))]));
        V2CompositionPlanCompileResult valid = catalog.CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest());

        Assert.False(unavailable.IsCompiled);
        Assert.Contains(unavailable.Issues, issue => issue.Code == "profile.v2.compile.map-selection-invalid");
        Assert.False(duplicateReference.IsCompiled);
        Assert.Contains(
            duplicateReference.Issues,
            issue => issue.Code == "profile.v2.runtime-reference-replace.reference-length-invalid");
        Assert.True(valid.IsCompiled);
    }

    /// <summary>Verifies duplicate canonical maps with the same reference capacity are never selected arbitrarily.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsAmbiguousReferenceCapacity()
    {
        V2CompositionPlanCompileResult result = CreateRuntimeReferenceReplaceCatalog(
                mapDefinitions:
                [
                    new RuntimeReferenceReplaceMapDocument("map-a", 16),
                    new RuntimeReferenceReplaceMapDocument("map-b", 16),
                ]).CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest());

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.compile.map-selection-invalid");
    }

    /// <summary>Verifies a source range that escapes its concrete binding rejects only that request.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsOutOfBoundsSourceWithoutAffectingLaterRequest()
    {
        TrustedProfileBundleCatalog catalog = CreateRuntimeReferenceReplaceCatalog();
        V2CompositionPlanCompileResult rejected = catalog.CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping(
                    "out-of-bounds-source",
                    10,
                    new ByteRange(3, 2),
                    new ByteRange(8, 2))]));
        V2CompositionPlanCompileResult valid = catalog.CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest());

        Assert.False(rejected.IsCompiled);
        Assert.Contains(rejected.Issues, issue => issue.Code == "profile.v2.runtime-reference-replace.source-out-of-bounds");
        Assert.True(valid.IsCompiled);
    }

    /// <summary>Verifies a target range that escapes the selected reference capacity rejects with its stable candidate issue.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsOutOfBoundsTarget()
    {
        V2CompositionPlanCompileResult result = CreateRuntimeReferenceReplaceCatalog().CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping(
                    "out-of-bounds-target",
                    10,
                    new ByteRange(0, 2),
                    new ByteRange(15, 2))]));

        Assert.False(result.IsCompiled);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.v2.runtime-reference-replace.target-out-of-bounds");
    }

    /// <summary>Verifies reject-overlap remains enforced for typed runtime mappings.</summary>
    [Fact]
    public void RuntimeReferenceReplaceLoweringRejectsOverlappingTargets()
    {
        V2CompositionPlanCompileResult result = CreateRuntimeReferenceReplaceCatalog().CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(
                mappings:
                [
                    RuntimeReferenceReplaceMapping("first", 10, new ByteRange(0, 2), new ByteRange(7, 2)),
                    RuntimeReferenceReplaceMapping("second", 11, new ByteRange(2, 2), new ByteRange(8, 2)),
                ]));

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.plan.operation-overlap");
    }

    /// <summary>Locks shared runtime-reference binding and mapping guards without duplicating workflow tests.</summary>
    [Theory]
    [InlineData("duplicate-binding", "profile.v2.runtime-reference-replace.binding-invalid")]
    [InlineData("duplicate-mapping-id", "profile.v2.runtime-reference-replace.mapping-invalid")]
    [InlineData("duplicate-sequence", "profile.v2.runtime-reference-replace.mapping-invalid")]
    [InlineData("unknown-binding", "profile.v2.runtime-reference-replace.mapping-invalid")]
    [InlineData("wrong-kind", "profile.v2.runtime-reference-replace.mapping-invalid")]
    [InlineData("wrong-overlap", "profile.v2.runtime-reference-replace.mapping-invalid")]
    [InlineData("wrong-alignment", "profile.v2.runtime-reference-replace.mapping-invalid")]
    public void RuntimeReferenceReplaceLoweringRejectsInvalidExplicitMappingRequest(
        string mutation,
        string expectedIssueCode)
    {
        V2ExplicitMappingInputBinding[] bindings =
        [
            new("base", "reference", 16),
            new("source-a", "source", 4),
        ];
        ExplicitMapping[] mappings =
            [RuntimeReferenceReplaceMapping("replace-source", 10, new ByteRange(0, 2), new ByteRange(8, 2))];
        switch (mutation)
        {
            case "duplicate-binding":
                bindings = [.. bindings, new("source-a", "source", 4)];
                break;
            case "duplicate-mapping-id":
                mappings =
                [
                    mappings[0],
                    RuntimeReferenceReplaceMapping(
                        "replace-source",
                        11,
                        new ByteRange(2, 2),
                        new ByteRange(10, 2)),
                ];
                break;
            case "duplicate-sequence":
                mappings =
                [
                    mappings[0],
                    RuntimeReferenceReplaceMapping(
                        "replace-second",
                        10,
                        new ByteRange(2, 2),
                        new ByteRange(10, 2)),
                ];
                break;
            case "unknown-binding":
                mappings =
                [
                    RuntimeReferenceReplaceMapping(
                        "replace-source",
                        10,
                        new ByteRange(0, 2),
                        new ByteRange(8, 2),
                        "missing"),
                ];
                break;
            case "wrong-kind":
                mappings =
                [
                    RuntimeReferenceReplaceMapping(
                        "replace-source",
                        10,
                        new ByteRange(0, 2),
                        new ByteRange(8, 2),
                        operationKind: ExplicitMappingOperationKind.CopyRange),
                ];
                break;
            case "wrong-overlap":
                mappings =
                [
                    RuntimeReferenceReplaceMapping(
                        "replace-source",
                        10,
                        new ByteRange(0, 2),
                        new ByteRange(8, 2),
                        overlapPolicy: OverlapPolicy.ReplaceExisting),
                ];
                break;
            case "wrong-alignment":
                mappings =
                [
                    RuntimeReferenceReplaceMapping(
                        "replace-source",
                        10,
                        new ByteRange(0, 2),
                        new ByteRange(8, 2),
                        alignment: 2),
                ];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown runtime mutation.");
        }

        V2CompositionPlanCompileResult result = CreateRuntimeReferenceReplaceCatalog()
            .CompileRuntimeReferenceReplace(
                "runtime-general-replace",
                "1.0.0",
                LogicalTestMemberId,
                new V2RuntimeReferenceReplaceCompileRequest(bindings, mappings));

        Assert.False(result.IsCompiled);
        Assert.Null(result.CompiledComposition);
        Assert.Contains(result.Issues, issue => issue.Code == expectedIssueCode);
    }

    private static TrustedProfileBundleCatalog CreateRuntimeReferenceReplaceCatalog(
        FirmwareWriteConstraint writeConstraint = FirmwareWriteConstraint.ExplicitRange,
        string promotionStage = "compilable",
        IReadOnlyList<RuntimeReferenceReplaceMapDocument>? mapDefinitions = null)
    {
        RuntimeReferenceReplaceMapDocument[] maps = mapDefinitions is { Count: > 0 }
            ? [.. mapDefinitions]
            : [new RuntimeReferenceReplaceMapDocument("map", 16)];
        string writeConstraintToken = writeConstraint switch
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
        string familyJson = RuntimeReferenceReplaceTestDocuments.FamilyJson(maps, writeConstraintToken);
        string familyHash = Hash(familyJson);
        string profileJson = RuntimeReferenceReplaceTestDocuments.ProfileJson(
            familyHash,
            promotionStage,
            maps.Select(static map => map.MapId));
        using var familyDocument = JsonDocument.Parse(familyJson);
        using var profileDocument = JsonDocument.Parse(profileJson);
        return CreateCatalogFromSources(
            [Family("family-entry", familyHash, familyDocument.RootElement.Clone())],
            [Profile("runtime-reference-replace-profile", Hash(profileJson), profileDocument.RootElement.Clone())]);
    }

    private static V2RuntimeReferenceReplaceCompileRequest RuntimeReferenceReplaceRequest(
        long referenceLength = 16,
        long sourceLength = 4,
        params ExplicitMapping[] mappings)
    {
        return new V2RuntimeReferenceReplaceCompileRequest(
            [
                new V2ExplicitMappingInputBinding("base", "reference", referenceLength),
                new V2ExplicitMappingInputBinding("source-a", "source", sourceLength),
            ],
            mappings.Length == 0
                ? [RuntimeReferenceReplaceMapping("replace-source", 10, new ByteRange(2, 2), new ByteRange(8, 2))]
                : mappings);
    }

    private static ExplicitMapping RuntimeReferenceReplaceMapping(
        string mappingId,
        int sequence,
        ByteRange sourceRange,
        ByteRange targetRange,
        string sourceBindingId = "source-a",
        ExplicitMappingOperationKind operationKind = ExplicitMappingOperationKind.ReplaceRange,
        OverlapPolicy overlapPolicy = OverlapPolicy.Reject,
        int alignment = 1)
    {
        return new ExplicitMapping(
            mappingId,
            sequence,
            operationKind,
            sourceBindingId,
            sourceRange,
            "output-image",
            targetRange,
            overlapPolicy,
            alignment,
            reason: "Synthetic runtime General Replace mapping");
    }

}
