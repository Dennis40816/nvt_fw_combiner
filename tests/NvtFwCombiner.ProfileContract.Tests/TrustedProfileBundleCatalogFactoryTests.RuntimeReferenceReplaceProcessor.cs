using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Verifies General Replace cannot attach private map-resolution artifacts to its length-only request.</summary>
    [Fact]
    public void RuntimeReferenceGeneralReplaceRejectsResolutionArtifact()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(includeProcessor: false),
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            ExperienceIds.GeneralReplace,
            requestedTopology: null,
            [new FirmwareArtifactPayload("base", new byte[16])],
            RuntimeReferenceReplaceRequest());

        Assert.False(result.IsCompiled);
        Assert.Equal(
            "profile.v2.runtime-reference-replace.resolution-artifact-invalid",
            Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies CtrlRAM accepts only one immutable artifact matching the reference binding identity and length.</summary>
    [Fact]
    public void RuntimeReferenceCtrlRamValidatesResolutionArtifactIdentity()
    {
        TrustedProfileBundleCatalog catalog = CreateConditionalRuntimeReferenceReplaceCatalog(
            includeProcessor: true,
            experienceId: ExperienceIds.CtrlRamReplace);
        V2RuntimeReferenceReplaceCompileRequest request = RuntimeReferenceReplaceRequest(
            mappings: [RuntimeReferenceReplaceMapping(
                "replace-tp",
                10,
                new ByteRange(0, 2),
                new ByteRange(8, 2))]);

        V2CompositionPlanCompileResult valid = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            catalog,
            "runtime-ctrlram-replace",
            "1.0.0",
            LogicalTestMemberId,
            ExperienceIds.CtrlRamReplace,
            requestedTopology: null,
            [new FirmwareArtifactPayload("base", new byte[16])],
            request);
        V2CompositionPlanCompileResult rejected = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            catalog,
            "runtime-ctrlram-replace",
            "1.0.0",
            LogicalTestMemberId,
            ExperienceIds.CtrlRamReplace,
            requestedTopology: null,
            [new FirmwareArtifactPayload("wrong-base", new byte[16])],
            request);

        Assert.True(valid.IsCompiled, string.Join(Environment.NewLine, valid.Issues));
        Assert.False(rejected.IsCompiled);
        Assert.Equal(
            "profile.v2.runtime-reference-replace.resolution-artifact-invalid",
            Assert.Single(rejected.Issues).Code);
    }

    /// <summary>Verifies CtrlRAM runtime mappings select topology, consume only supplied bytes, and append one processor.</summary>
    [Fact]
    public void RuntimeReferenceCtrlRamReplaceCompilesShortSourceForSelectedTopology()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(
                includeProcessor: true,
                experienceId: ExperienceIds.CtrlRamReplace,
                mapDefinitions:
                [
                    new RuntimeReferenceReplaceMapDocument("single-map", 16, "single"),
                    new RuntimeReferenceReplaceMapDocument("cascade-map", 16, "cascade"),
                ]),
            "runtime-ctrlram-replace",
            "1.0.0",
            LogicalTestMemberId,
            ExperienceIds.CtrlRamReplace,
            new TopologySelection(3, "cascade", TopologySelectionSource.Requested, "ic-number"),
            RuntimeReferenceReplaceRequest(
                sourceLength: 2,
                mappings:
                [RuntimeReferenceReplaceMapping("replace-tp-prefix", 10, new ByteRange(0, 2), new ByteRange(8, 2))]));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        Assert.Equal(ExperienceIds.CtrlRamReplace, composition.ExperienceId);
        Assert.Equal("cascade-map", composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        CompiledInputSlotRequirement sourceSlot = Assert.Single(
            composition.V2Details.InputContract.Slots,
            slot => slot.ArtifactClass == CompiledInputArtifactClass.CtrlRamReplacement);
        Assert.Equal(CompiledInputSlotCardinality.OneOrMore, sourceSlot.Cardinality);
        Assert.Collection(
            composition.Plan.OrderedOperations,
            mapping =>
            {
                Assert.Equal(CompositionOperationKind.ReplaceRange, mapping.Kind);
                Assert.Equal(new ByteRange(8, 2), mapping.TargetRange);
            },
            processor =>
            {
                Assert.Equal(CompositionOperationKind.RunExternalProcessor, processor.Kind);
                Assert.Equal(
                    [new ByteRange(8, 2), new ByteRange(12, 4)],
                    processor.ExternalProcessorInvocation!.AllowedWriteRanges);
            });
    }

    /// <summary>CtrlRAM lowering is selected by its closed semantic shape, not by workflow identity.</summary>
    [Fact]
    public void RuntimeReferenceCtrlRamSemanticPlanIsInvariantAcrossWorkflowIdentity()
    {
        RuntimeReferenceReplaceMapDocument[] maps =
        [
            new("single-map", 16, "single"),
            new("cascade-map", 16, "cascade"),
        ];
        var topology = new TopologySelection(
            3,
            "cascade",
            TopologySelectionSource.Requested,
            "ic-number");
        V2RuntimeReferenceReplaceCompileRequest request = RuntimeReferenceReplaceRequest(
            sourceLength: 2,
            mappings:
            [RuntimeReferenceReplaceMapping("replace-tp-prefix", 10, new ByteRange(0, 2), new ByteRange(8, 2))]);
        V2CompositionPlanCompileResult ctrlRamResult = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(
                includeProcessor: true,
                experienceId: ExperienceIds.CtrlRamReplace,
                mapDefinitions: maps),
            "runtime-ctrlram-replace",
            "1.0.0",
            LogicalTestMemberId,
            ExperienceIds.CtrlRamReplace,
            topology,
            [new FirmwareArtifactPayload("base", new byte[16])],
            request);
        V2CompositionPlanCompileResult alternateResult = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(
                includeProcessor: true,
                experienceId: ExperienceIds.GeneralReplace,
                mapDefinitions: maps,
                usesCtrlRamSemantics: true),
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            ExperienceIds.GeneralReplace,
            topology,
            [new FirmwareArtifactPayload("base", new byte[16])],
            request);

        Assert.True(
            ctrlRamResult.IsCompiled,
            string.Join(Environment.NewLine, ctrlRamResult.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        Assert.True(
            alternateResult.IsCompiled,
            string.Join(Environment.NewLine, alternateResult.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        CompiledComposition ctrlRam = Assert.IsType<CompiledComposition>(ctrlRamResult.CompiledComposition);
        CompiledComposition alternate = Assert.IsType<CompiledComposition>(alternateResult.CompiledComposition);
        Assert.Equal(ExperienceIds.CtrlRamReplace, ctrlRam.ExperienceId);
        Assert.Equal(ExperienceIds.GeneralReplace, alternate.ExperienceId);
        AssertEquivalentRuntimeExecutionSemantics(ctrlRam, alternate);
    }

    /// <summary>
    /// Reviewed postbuild section identities survive compiler lowering and
    /// therefore distinguish the exact compiled processor plan.
    /// </summary>
    [Fact]
    public void RuntimeReferenceCtrlRamWriteSectionsChangeCompilationFingerprint()
    {
        TrustedProfileBundleCatalog catalog = CreateConditionalRuntimeReferenceReplaceCatalog(
            includeProcessor: true,
            experienceId: ExperienceIds.CtrlRamReplace);
        var topology = new TopologySelection(
            1,
            "single",
            TopologySelectionSource.Requested,
            "ic-number");
        V2RuntimeReferenceReplaceInputBinding[] bindings =
        [
            new("base", "reference", 16),
            new("source-a", "source", 4),
        ];
        ExplicitMapping[] mappings =
        [
            RuntimeReferenceReplaceMapping(
                "replace-tp",
                10,
                new ByteRange(0, 2),
                new ByteRange(8, 2)),
        ];
        V2CompositionPlanCompileResult baseline =
            TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
                catalog,
                "runtime-ctrlram-replace",
                "1.0.0",
                LogicalTestMemberId,
                ExperienceIds.CtrlRamReplace,
                topology,
                new V2RuntimeReferenceReplaceCompileRequest(bindings, mappings));
        V2CompositionPlanCompileResult classified =
            TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
                catalog,
                "runtime-ctrlram-replace",
                "1.0.0",
                LogicalTestMemberId,
                ExperienceIds.CtrlRamReplace,
                topology,
                new V2RuntimeReferenceReplaceCompileRequest(
                    bindings,
                    mappings,
                    postbuildWriteRangeSections:
                    [
                        new ExternalProcessorWriteRangeSection(
                            "flash-header-crc",
                            new ByteRange(12, 4),
                            new ByteRange(0, 4)),
                    ]));

        CompiledComposition baselineComposition =
            Assert.IsType<CompiledComposition>(baseline.CompiledComposition);
        CompiledComposition classifiedComposition =
            Assert.IsType<CompiledComposition>(classified.CompiledComposition);
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            classifiedComposition.Plan.OrderedOperations[^1].ExternalProcessorInvocation);
        ExternalProcessorWriteRangeSection section =
            Assert.Single(invocation.AllowedWriteRangeSections);
        Assert.Equal("flash-header-crc", section.SectionId);
        Assert.Equal(new ByteRange(12, 4), section.Range);
        Assert.Equal(new ByteRange(0, 4), section.SourceRange);
        Assert.NotEqual(
            baselineComposition.CompilationFingerprint,
            classifiedComposition.CompilationFingerprint);
    }

    /// <summary>Verifies CtrlRAM Replace cannot borrow an explicitly writable DP region.</summary>
    [Fact]
    public void RuntimeReferenceCtrlRamReplaceRejectsDpTarget()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(
                includeProcessor: true,
                experienceId: ExperienceIds.CtrlRamReplace),
            "runtime-ctrlram-replace",
            "1.0.0",
            LogicalTestMemberId,
            ExperienceIds.CtrlRamReplace,
            new TopologySelection(1, "single", TopologySelectionSource.Requested, "ic-number"),
            RuntimeReferenceReplaceRequest(
                sourceLength: 2,
                mappings:
                [RuntimeReferenceReplaceMapping("replace-dp", 10, new ByteRange(0, 2), new ByteRange(2, 2))]));

        Assert.False(result.IsCompiled);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.v2.runtime-reference-replace.ctrlram-target-invalid");
    }

    /// <summary>Verifies CtrlRAM runtime-reference profiles cannot omit their final refresh processor.</summary>
    [Fact]
    public void RuntimeReferenceCtrlRamReplaceProfileRequiresProcessor()
    {
        TrustedProfileBundleCatalogException exception = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            CreateConditionalRuntimeReferenceReplaceCatalog(
                includeProcessor: false,
                experienceId: ExperienceIds.CtrlRamReplace));

        Assert.Contains("closed user-defined source or fixed processor mapping shape", exception.Message);
    }

    /// <summary>Verifies General Replace cannot use the CtrlRAM-only topology disambiguation input.</summary>
    [Fact]
    public void RuntimeReferenceGeneralReplaceRejectsExplicitTopology()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(includeProcessor: true),
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            ExperienceIds.GeneralReplace,
            new TopologySelection(1, "single", TopologySelectionSource.Requested, "ic-number"),
            RuntimeReferenceReplaceRequest());

        Assert.False(result.IsCompiled);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.v2.runtime-reference-replace.topology-not-admitted");
    }

    /// <summary>Verifies a processor-capable profile does not run its Legacy Combiner stage for DP-only mappings.</summary>
    [Fact]
    public void RuntimeReferenceReplaceSkipsDeclaredProcessorForDpOnlyMappings()
    {
        V2CompositionPlanCompileResult result = CompileConditionalRuntimeReferenceReplace(
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping("replace-dp", 10, new ByteRange(0, 2), new ByteRange(2, 2))]));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Equal(CompositionOperationKind.ReplaceRange, Assert.Single(composition.Plan.OrderedOperations).Kind);
    }

    /// <summary>Verifies one TP mapping appends exactly one profile-owned Legacy Combiner operation after all mappings.</summary>
    [Fact]
    public void RuntimeReferenceReplaceAppendsOneProcessorForTpMapping()
    {
        V2CompositionPlanCompileResult result = CompileConditionalRuntimeReferenceReplace(
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping("replace-tp", 10, new ByteRange(0, 2), new ByteRange(8, 2))]));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Collection(
            composition.Plan.OrderedOperations,
            mapping => Assert.Equal(CompositionOperationKind.ReplaceRange, mapping.Kind),
            processor =>
            {
                Assert.Equal(CompositionOperationKind.RunExternalProcessor, processor.Kind);
                Assert.Equal(int.MaxValue, processor.Sequence);
                Assert.Equal("nfc.synthetic.general-replace", processor.ExternalProcessorInvocation!.ProcessorId);
                Assert.Equal("legacy-combiner-1.13.0", processor.ExternalProcessorInvocation.ToolBindingId);
                Assert.Empty(processor.ExternalProcessorInvocation.StagedSourceBindings);
                Assert.Empty(processor.ExternalProcessorInvocation.StagedArtifactBindings);
                Assert.Equal([new ByteRange(8, 4), new ByteRange(12, 4)], processor.ExternalProcessorInvocation.AllowedWriteRanges);
            });
    }

    /// <summary>Verifies mixed DP/TP and repeated TP mappings still lower one final processor stage.</summary>
    [Fact]
    public void RuntimeReferenceReplaceAppendsOnlyOneProcessorForManyTpMappings()
    {
        V2CompositionPlanCompileResult result = CompileConditionalRuntimeReferenceReplace(
            RuntimeReferenceReplaceRequest(
                mappings:
                [
                    RuntimeReferenceReplaceMapping("replace-dp", 10, new ByteRange(0, 1), new ByteRange(2, 1)),
                    RuntimeReferenceReplaceMapping("replace-tp-a", 11, new ByteRange(1, 1), new ByteRange(8, 1)),
                    RuntimeReferenceReplaceMapping("replace-tp-b", 12, new ByteRange(2, 1), new ByteRange(10, 1)),
                ]));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Equal(3, composition.Plan.OrderedOperations.Count(operation =>
            operation.Kind == CompositionOperationKind.ReplaceRange));
        _ = Assert.Single(composition.Plan.OrderedOperations, operation =>
            operation.Kind == CompositionOperationKind.RunExternalProcessor);
        Assert.Equal(CompositionOperationKind.RunExternalProcessor, composition.Plan.OrderedOperations[^1].Kind);
    }

    /// <summary>Verifies TP authoring fails closed when the selected profile has no reviewed refresh stage.</summary>
    [Fact]
    public void RuntimeReferenceReplaceRejectsTpMappingWithoutProcessorStage()
    {
        V2CompositionPlanCompileResult result = CompileConditionalRuntimeReferenceReplace(
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping("replace-tp", 10, new ByteRange(0, 2), new ByteRange(8, 2))]),
            includeProcessor: false);

        Assert.False(result.IsCompiled);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.v2.runtime-reference-replace.processor-required");
    }

    /// <summary>Verifies caller ordering cannot place a mapping at or after the final profile-owned processor.</summary>
    [Fact]
    public void RuntimeReferenceReplaceRejectsMappingAtProcessorSequence()
    {
        V2CompositionPlanCompileResult result = CompileConditionalRuntimeReferenceReplace(
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping("replace-tp", int.MaxValue, new ByteRange(0, 2), new ByteRange(8, 2))]));

        Assert.False(result.IsCompiled);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.v2.runtime-reference-replace.processor-order-invalid");
    }

    /// <summary>Verifies processor-only header authority never becomes General Replace authoring authority.</summary>
    [Fact]
    public void RuntimeReferenceReplaceRejectsDirectHeaderMapping()
    {
        V2CompositionPlanCompileResult result = CompileConditionalRuntimeReferenceReplace(
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping("replace-header", 10, new ByteRange(0, 1), new ByteRange(12, 1))]));

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.plan.region-access-denied");
    }

    /// <summary>Verifies processor authority cannot override a forbidden canonical physical range.</summary>
    [Fact]
    public void RuntimeReferenceReplaceRejectsProcessorWriteOutsidePhysicalAuthority()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(
                includeProcessor: true,
                headerWriteConstraint: "forbidden"),
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping("replace-tp", 10, new ByteRange(0, 2), new ByteRange(8, 2))]));

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.plan.region-access-denied");
    }

    /// <summary>Verifies processor write views stay hidden from user mappings even for DP-only requests.</summary>
    [Fact]
    public void RuntimeReferenceReplaceRejectsProcessorWritesExposedToAuthoring()
    {
        V2CompositionPlanCompileResult result = TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(
                includeProcessor: true,
                headerAccess: "explicit-range"),
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            RuntimeReferenceReplaceRequest(
                mappings:
                [RuntimeReferenceReplaceMapping("replace-dp", 10, new ByteRange(0, 2), new ByteRange(2, 2))]));

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.plan.region-access-denied");
    }

    private static V2CompositionPlanCompileResult CompileConditionalRuntimeReferenceReplace(
        V2RuntimeReferenceReplaceCompileRequest request,
        bool includeProcessor = true)
    {
        return TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
            CreateConditionalRuntimeReferenceReplaceCatalog(includeProcessor),
            "runtime-general-replace",
            "1.0.0",
            LogicalTestMemberId,
            request);
    }

    private static TrustedProfileBundleCatalog CreateConditionalRuntimeReferenceReplaceCatalog(
        bool includeProcessor,
        string headerWriteConstraint = "explicit-range",
        string headerAccess = "hidden",
        string experienceId = ExperienceIds.GeneralReplace,
        IReadOnlyList<RuntimeReferenceReplaceMapDocument>? mapDefinitions = null,
        bool usesCtrlRamSemantics = false)
    {
        string familyJson = ConditionalRuntimeReferenceReplaceFamilyJson(
            headerWriteConstraint,
            experienceId,
            mapDefinitions);
        string familyHash = Hash(familyJson);
        string profileJson = ConditionalRuntimeReferenceReplaceProfileJson(
            familyHash,
            includeProcessor,
            headerAccess,
            experienceId,
            mapDefinitions?.Select(static map => map.MapId),
            usesCtrlRamSemantics);
        using var familyDocument = JsonDocument.Parse(familyJson);
        using var profileDocument = JsonDocument.Parse(profileJson);
        return TrustedProfileBundleCatalogFactory.Create(Source(
            [Family("family-entry", familyHash, familyDocument.RootElement.Clone())],
            [Profile("runtime-reference-replace-profile", Hash(profileJson), profileDocument.RootElement.Clone())]));
    }

    private static string ConditionalRuntimeReferenceReplaceFamilyJson(
        string headerWriteConstraint,
        string experienceId,
        IReadOnlyList<RuntimeReferenceReplaceMapDocument>? mapDefinitions)
    {
        JsonObject family = Assert.IsType<JsonObject>(JsonNode.Parse(
            RuntimeReferenceReplaceTestDocuments.FamilyJson(
                mapDefinitions ?? [new RuntimeReferenceReplaceMapDocument("map", 16)],
                "explicit-range",
                experienceId)));
        foreach (JsonNode? regionSetNode in Assert.IsType<JsonArray>(family["regionSets"]))
        {
            JsonArray regions = Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(regionSetNode)["regions"]);
            regions.Add(Region("dp", "root", "dp", "code", 0, 8, "explicit-range"));
            regions.Add(Region("tp", "root", "tp", "ctrlram", 8, 4, "explicit-range"));
            regions.Add(Region("header", "root", "system", "header", 12, 4, headerWriteConstraint));
        }

        return family.ToJsonString();
    }

    private static string ConditionalRuntimeReferenceReplaceProfileJson(
        string familyHash,
        bool includeProcessor,
        string headerAccess,
        string experienceId,
        IEnumerable<string>? mapIds,
        bool usesCtrlRamSemantics)
    {
        string shapeExperienceId = usesCtrlRamSemantics
            ? ExperienceIds.CtrlRamReplace
            : experienceId;
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            RuntimeReferenceReplaceTestDocuments.ProfileJson(
                familyHash,
                "compilable",
                mapIds ?? ["map"],
                shapeExperienceId)));
        profile["profileId"] = $"runtime-{experienceId}";
        JsonObject experience = Assert.IsType<JsonObject>(profile["experience"]);
        experience["experienceId"] = experienceId;
        experience["displayNameKey"] = $"runtime-{experienceId}";
        profile["schemaVersion"] = "2.9";
        if (StringComparer.Ordinal.Equals(shapeExperienceId, ExperienceIds.CtrlRamReplace))
        {
            JsonObject sourceSlot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[1]);
            Assert.IsType<JsonObject>(sourceSlot["acceptance"])["normalization"] = new JsonObject
            {
                ["kind"] = "truncate-ctrlram",
                ["warningIssueCode"] = "CTRLRAM_SIZE_WARNING",
                ["evidenceRef"] = "synthetic-tp-refresh",
            };
        }

        JsonObject mapBinding = Assert.IsType<JsonObject>(profile["mapBinding"]);
        mapBinding["requiredRegionIds"] = new JsonArray("root", "dp", "tp", "header");
        profile["regionAccessRules"] = new JsonArray
        {
            Access("dp", "explicit-range"),
            Access("tp", "explicit-range"),
            Access("header", headerAccess),
        };
        if (!includeProcessor)
        {
            return profile.ToJsonString();
        }

        profile["views"] = new JsonArray
        {
            View("processor-image", new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject { ["start"] = 0, ["length"] = 16 },
            }),
            View("processor-tp-write", new JsonObject { ["kind"] = "map-region", ["regionId"] = "tp" }),
            View("processor-header-write", new JsonObject { ["kind"] = "map-region", ["regionId"] = "header" }),
        };
        profile["operations"] = new JsonArray
        {
            new JsonObject
            {
                ["operationId"] = "refresh-tp-header",
                ["sequence"] = int.MaxValue,
                ["overlapPolicy"] = "replace-existing",
                ["reason"] = "Refresh TP header and integrity after all runtime mappings.",
                ["kind"] = "run-processor",
                ["processorStageId"] = "tp-refresh",
            },
        };
        profile["processorStages"] = new JsonArray
        {
            new JsonObject
            {
                ["processorStageId"] = "tp-refresh",
                ["kind"] = "legacy-combiner-v1",
                ["toolBindingId"] = "legacy-combiner-1.13.0",
                ["invocationProfileId"] = $"nfc.synthetic.{shapeExperienceId}",
                ["targetSpaceId"] = "output-image",
                ["targetViewId"] = "processor-image",
                ["authority"] = "transform",
                ["purpose"] = "header-and-integrity",
                ["integrityDisposition"] = "recalculate-and-write",
                ["allowedReadViewIds"] = new JsonArray("processor-image"),
                ["allowedWriteViewIds"] = new JsonArray("processor-tp-write", "processor-header-write"),
                ["stagedSourceBindings"] = new JsonArray(),
                ["stagedArtifactBindings"] = new JsonArray(),
                ["evidenceRef"] = "synthetic-tp-refresh",
                ["failurePolicy"] = "fail-closed",
            },
        };
        return profile.ToJsonString();
    }

    private static JsonObject Region(
        string id,
        string parentId,
        string owner,
        string kind,
        int start,
        int length,
        string writeConstraint)
    {
        return new JsonObject
        {
            ["regionId"] = id,
            ["parentRegionId"] = parentId,
            ["owner"] = owner,
            ["kind"] = kind,
            ["range"] = new JsonObject { ["start"] = start, ["length"] = length },
            ["writeConstraint"] = writeConstraint,
            ["alignment"] = 1,
        };
    }

    private static JsonObject Access(string regionId, string access)
    {
        return new JsonObject
        {
            ["regionId"] = regionId,
            ["access"] = access,
            ["reason"] = "Synthetic General Replace policy.",
        };
    }

    private static JsonObject View(string viewId, JsonObject selector)
    {
        return new JsonObject
        {
            ["viewId"] = viewId,
            ["spaceId"] = "output-image",
            ["selector"] = selector,
        };
    }
}
