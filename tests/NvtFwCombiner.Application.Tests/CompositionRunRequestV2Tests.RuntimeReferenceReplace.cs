using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunRequestV2Tests
{
    /// <summary>Runtime-reference publication requires independently derived processor, metadata, and typed plan proof.</summary>
    [Fact]
    public void DynamicRuntimeReferenceCapabilityRequiresTypedOwnerBindings()
    {
        CompiledComposition composition = CreateRuntimeReferenceCandidate(
            allowsConditionalProcessor: true,
            includeProcessorView: true,
            modeId: ExperienceIds.CtrlRamReplace,
            experienceId: ExperienceIds.CtrlRamReplace,
            sourceArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement,
            rootOwner: FirmwareRegionOwner.Tp,
            rootKind: FirmwareRegionKind.CtrlRam,
            processorId: "nfc.synthetic.postbuild");
        LegacyCombinerPostbuildCommandPlan postbuildPlan =
            CreateSyntheticPostbuildPlan();
        var proof =
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                composition,
                postbuildPlan);
        var identity = new CapabilityRouteIdentity(
            "NT-SYNTHETIC",
            ExperienceIds.CtrlRamReplace,
            "1-ic",
            "map");
        string[] reviewedBindings =
        [
            "postbuild-processor:nfc.synthetic.postbuild",
            "postbuild-selector:single",
            $"postbuild-plan:{LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(postbuildPlan, 4)}",
        ];
        ResolvedCapabilityRoute route = CreateRoute(reviewedBindings);

        _ = Assert.Throws<ArgumentException>(() =>
            route.BindCompilation(
                composition,
                MetadataPlanDefinition.Empty));
        CompiledComposition wrongProcessor = CreateRuntimeReferenceCandidate(
            allowsConditionalProcessor: true,
            includeProcessorView: true,
            modeId: ExperienceIds.CtrlRamReplace,
            experienceId: ExperienceIds.CtrlRamReplace,
            sourceArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement,
            rootOwner: FirmwareRegionOwner.Tp,
            rootKind: FirmwareRegionKind.CtrlRam,
            processorId: "nfc.synthetic.other-postbuild");
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                wrongProcessor,
                postbuildPlan));
        CompiledComposition wrongTool = CreateRuntimeReferenceCandidate(
            allowsConditionalProcessor: true,
            includeProcessorView: true,
            modeId: ExperienceIds.CtrlRamReplace,
            experienceId: ExperienceIds.CtrlRamReplace,
            sourceArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement,
            rootOwner: FirmwareRegionOwner.Tp,
            rootKind: FirmwareRegionKind.CtrlRam,
            processorId: "nfc.synthetic.postbuild",
            processorToolBindingId: "synthetic-other-tool");
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                wrongTool,
                postbuildPlan));
        var fabricatedCommand = new LegacyCombinerPostbuildCommand(
            "fabricated-command",
            LegacyCombinerCommandFamily.NormalMode,
            "CRC_Disable",
            crcArgument: null,
            [new LegacyCombinerBlockArgument(
                "fabricated-block",
                LegacyCombinerBlockSourceKind.StagedFile,
                "source.bin",
                0,
                new ByteRange(1, 1))]);
        var fabricatedPlan = new LegacyCombinerPostbuildCommandPlan(
            postbuildPlan.Profile,
            postbuildPlan.Selector,
            [fabricatedCommand]);
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                composition,
                fabricatedPlan));
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                CreateRuntimeReferenceCandidate(),
                postbuildPlan));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateRuntimeReferenceCandidate(
                allowsConditionalProcessor: true,
                includeProcessorView: true,
                modeId: ExperienceIds.CtrlRamReplace,
                experienceId: ExperienceIds.CtrlRamReplace,
                sourceArtifactClass:
                    CompiledInputArtifactClass.CtrlRamReplacement,
                rootOwner: FirmwareRegionOwner.Tp,
                rootKind: FirmwareRegionKind.CtrlRam));
        CompiledComposition driftedWrites = CreateRuntimeReferenceCandidate(
            allowsConditionalProcessor: true,
            includeProcessorView: true,
            modeId: ExperienceIds.CtrlRamReplace,
            experienceId: ExperienceIds.CtrlRamReplace,
            sourceArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement,
            rootOwner: FirmwareRegionOwner.Tp,
            rootKind: FirmwareRegionKind.CtrlRam,
            processorId: "nfc.synthetic.postbuild",
            processorWriteRange: new ByteRange(1, 1));
        _ = Assert.Throws<ArgumentException>(() =>
            route.BindCompilation(
                driftedWrites,
                MetadataPlanDefinition.Empty,
                proof));
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                driftedWrites,
                postbuildPlan));
        CompiledComposition driftedSections = CreateRuntimeReferenceCandidate(
            allowsConditionalProcessor: true,
            includeProcessorView: true,
            modeId: ExperienceIds.CtrlRamReplace,
            experienceId: ExperienceIds.CtrlRamReplace,
            sourceArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement,
            rootOwner: FirmwareRegionOwner.Tp,
            rootKind: FirmwareRegionKind.CtrlRam,
            processorId: "nfc.synthetic.postbuild",
            processorWriteSectionId: "fabricated-section");
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                driftedSections,
                postbuildPlan));

        ResolvedCapabilityRoute reportRoute = CreateRoute(
        [
            .. reviewedBindings,
            "report-metadata-profile:synthetic-report@1.0.0",
            $"report-metadata-bundle:{new string('e', 64)}",
            "report-metadata-slot:tp-input<-reference",
        ]);
        _ = Assert.Throws<ArgumentException>(() =>
            reportRoute.BindCompilation(
                composition,
                MetadataPlanDefinition.Empty,
                proof));
        var sourceOnlyReportPlan = new MetadataPlanDefinition(
            [],
            new MetadataPlanSourceIdentity(
                "synthetic-report",
                "1.0.0",
                new string('e', 64)));
        _ = Assert.Throws<ArgumentException>(() =>
            reportRoute.BindCompilation(
                composition,
                sourceOnlyReportPlan,
                proof));
        ResolvedCapability resolved = route.BindCompilation(
            composition,
            MetadataPlanDefinition.Empty,
            proof);

        Assert.NotNull(resolved.RuntimeReferenceProof);

        ResolvedCapabilityRoute CreateRoute(IReadOnlyList<string> bindings)
        {
            var contract = new CanonicalCapabilityCompilationContract(
                composition.ProfileId,
                composition.ProfileVersion,
                composition.V2Details!.Provenance.Bundle.ContentHash,
                ["map"],
                CapabilityDefinitionFingerprint.RuntimeReferenceReplaceCompilerSemanticId,
                bindings);
            string fingerprint = CapabilityDefinitionFingerprint.Compute(
                identity,
                contract.ProfileId,
                contract.ProfileVersion,
                contract.TrustedDefinitionSha256,
                contract.AllowedMapVariantIds,
                contract.CompilerSemanticId,
                contract.SemanticBindingIds);
            var definition = new CanonicalDynamicCapabilityDefinition(
                identity,
                fingerprint,
                contract,
                Decision("authoring", CapabilityAuthoringAvailability.Available),
                Decision("publication", CapabilityPublicationStatus.Candidate),
                Decision("evidence", CapabilityEvidenceStatus.ContractOnly));
            return new ResolvedCapabilityRoute(
                definition,
                new ResolutionToken("synthetic-runtime-reference"));

            PinnedCapabilityDecision<TValue> Decision<TValue>(
                string decisionId,
                TValue value)
                where TValue : struct, Enum
            {
                return new PinnedCapabilityDecision<TValue>(
                    decisionId,
                    identity.RouteId,
                    fingerprint,
                    value,
                    "synthetic-runtime-reference");
            }
        }
    }

    /// <summary>Verifies the explicit runtime-reference candidate is admitted without broadening other map-bound V2 plans.</summary>
    [Fact]
    public async Task RuntimeReferenceCandidateRunsThroughTheSharedApplicationEngine()
    {
        CompiledComposition composition = CreateRuntimeReferenceCandidate();
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0, 1, 2, 3],
                ["source-artifact"] = [0xAA, 0xBB],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            writer);
        var request = new CompositionRunRequest(
            "runtime-reference",
            composition,
            [
                new InputArtifactBinding(
                    "reference",
                    "reference",
                    "reference-artifact",
                    "base.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "source-a",
                    "source-a",
                    "source-artifact",
                    "patch.bin",
                    CompiledInputArtifactClass.Auxiliary),
            ],
            "runtime-reference.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        CompositionRunResult preview = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        Assert.Equal([0, 0xAA, 0xBB, 3], preview.OutputBytes.ToArray());
        Assert.False(writer.WasCalled);
        _ = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            composition.V2Details!.Provenance.Context);
    }

    /// <summary>Verifies a generic resolved-map context cannot mint the runtime-reference candidate shape.</summary>
    [Fact]
    public void GenericResolvedMapContextCannotMintRuntimeReferenceCandidate()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateRuntimeReferenceCandidate(useRuntimeContext: false));
    }

    /// <summary>Verifies the runtime-reference context cannot mint a singleton-only map-bound artifact.</summary>
    [Fact]
    public void RuntimeReferenceContextCannotMintSingletonOnlyCandidate()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateRuntimeReferenceCandidate(includeAuxiliarySource: false));
    }

    /// <summary>Verifies the runtime-reference context has an explicit compilation-fingerprint vector.</summary>
    [Fact]
    public void RuntimeReferenceCandidateHasStableCompilationFingerprint()
    {
        CompiledComposition candidate = CreateRuntimeReferenceCandidate();
        Assert.Equal(
            "2033ebd60caa4c4c058088533be283b044da14f463b2012ea3a998b3a10dbe3b",
            candidate.CompilationFingerprint);
    }

    /// <summary>Verifies the conditional processor capability is explicit fingerprint-bound authority.</summary>
    [Fact]
    public void RuntimeReferenceConditionalProcessorCapabilityChangesCompilationFingerprint()
    {
        CompiledComposition baseline = CreateRuntimeReferenceCandidate();
        CompiledComposition conditional = CreateRuntimeReferenceCandidate(allowsConditionalProcessor: true);

        Assert.False(Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            baseline.V2Details!.Provenance.Context).AllowsConditionalProcessor);
        Assert.True(Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            conditional.V2Details!.Provenance.Context).AllowsConditionalProcessor);
        Assert.NotEqual(baseline.CompilationFingerprint, conditional.CompilationFingerprint);
    }

    /// <summary>Verifies legacy runtime-reference contracts cannot retain processor-only physical views.</summary>
    [Fact]
    public void RuntimeReferenceContextRejectsProcessorViewsWithoutCapability()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateRuntimeReferenceCandidate(
            includeProcessorView: true));
    }

    /// <summary>Verifies runtime-reference bindings retain their compiler-materialized identities.</summary>
    [Theory]
    [InlineData("other-reference", "source-a")]
    [InlineData("reference", "other-source")]
    public void RuntimeReferenceCandidateRejectsMismatchedBindingIdentity(
        string referenceBindingId,
        string sourceBindingId)
    {
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "runtime-reference-mismatched-binding",
            CreateRuntimeReferenceCandidate(),
            [
                new InputArtifactBinding(
                    "reference",
                    referenceBindingId,
                    "reference-artifact",
                    "base.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "source-a",
                    sourceBindingId,
                    "source-artifact",
                    "patch.bin",
                    CompiledInputArtifactClass.Auxiliary),
            ],
            "runtime-reference.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));
    }

    /// <summary>Verifies only the closed General Replace mode and experience can mint this candidate context.</summary>
    [Theory]
    [InlineData("other-replace", ExperienceIds.GeneralReplace)]
    [InlineData(ExperienceIds.GeneralReplace, "other-replace")]
    public void RuntimeReferenceContextCannotMintWrongModeOrExperienceCandidate(
        string modeId,
        string experienceId)
    {
        _ = Assert.Throws<ArgumentException>(() => CreateRuntimeReferenceCandidate(
            modeId: modeId,
            experienceId: experienceId));
    }

    /// <summary>Verifies a hand-minted CtrlRAM context cannot target a non-CtrlRAM physical map region.</summary>
    [Fact]
    public void RuntimeReferenceContextCannotMintCtrlRamMappingOverSystemRegion()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateRuntimeReferenceCandidate(
            modeId: ExperienceIds.CtrlRamReplace,
            experienceId: ExperienceIds.CtrlRamReplace,
            sourceArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement));
    }

    private static CompiledComposition CreateRuntimeReferenceCandidate(
        bool useRuntimeContext = true,
        bool includeAuxiliarySource = true,
        bool allowsConditionalProcessor = false,
        bool includeProcessorView = false,
        string modeId = ExperienceIds.GeneralReplace,
        string experienceId = ExperienceIds.GeneralReplace,
        CompiledInputArtifactClass sourceArtifactClass = CompiledInputArtifactClass.Auxiliary,
        FirmwareRegionOwner rootOwner = FirmwareRegionOwner.System,
        FirmwareRegionKind rootKind = FirmwareRegionKind.Image,
        string? processorId = null,
        string processorToolBindingId = "synthetic-postbuild-tool",
        ByteRange? processorWriteRange = null,
        string? processorWriteSectionId = null)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(
            modeId,
            FirmwareWriteConstraint.ExplicitRange,
            rootOwner: rootOwner,
            rootKind: rootKind);
        CompiledPhysicalRegionConstraint[] chain =
        [
            new CompiledPhysicalRegionConstraint(
                "root",
                FirmwareWriteConstraint.ExplicitRange,
                alignment: 1),
        ];
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "bundle-v2",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "release-binding"),
            new ProfileBundleEntryIdentity(
                "runtime-reference-profile",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            useRuntimeContext
                ? new RuntimeReferenceReplaceV2CompilationContext(
                    resolvedMap,
                    allowsConditionalProcessor || processorId is not null)
                : new ResolvedMapV2CompilationContext(resolvedMap),
            new CompiledProfilePromotion(CompiledProfilePromotionStage.ExecutableCandidate, []),
            ["runtime-reference-evidence"],
            [],
            []);
        var referenceSlot = new CompiledInputSlotRequirement(
            "reference-slot",
            "reference",
            CompiledInputArtifactClass.ReferenceImage,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
            new CompiledNoInputNormalization());
        var sourceSlot = new CompiledInputSlotRequirement(
            "source-slot",
            "source",
            sourceArtifactClass,
            required: true,
            CompiledInputSlotCardinality.OneOrMore,
            [".bin"],
            new CompiledBoundedInputLengthRequirement(1, int.MaxValue),
            sourceArtifactClass == CompiledInputArtifactClass.CtrlRamReplacement
                ? new CompiledTruncateCtrlRamInputNormalization(
                    "synthetic.ctrlram-truncated",
                    "synthetic-ctrlram-normalization")
                : new CompiledNoInputNormalization());
        var referenceBinding = new CompiledInputSpaceBinding(
            "reference",
            "reference-slot",
            CompiledInputInstancePolicy.Singleton);
        var sourceBinding = new CompiledInputSpaceBinding(
            "source-a",
            "source-slot",
            CompiledInputInstancePolicy.PerBinding);
        var inputContract = new CompiledInputContract(
            includeAuxiliarySource ? [referenceSlot, sourceSlot] : [referenceSlot],
            includeAuxiliarySource ? [referenceBinding, sourceBinding] : [referenceBinding]);
        var regionAccess = new CompiledRegionAccessContract(
            [
                new CompiledRegionAccessRequirement(
                    "root",
                    CompiledRegionAccessKind.ExplicitRange,
                    "Synthetic runtime reference-replace target.",
                    [],
                    chain),
            ],
            includeProcessorView || processorId is not null
                ? [new CompiledResolvedPhysicalView(
                    "processor-output",
                    "output-image",
                    new ByteRange(0, 4),
                    chain)]
                : []);
        var identity = new V2CompiledCompositionIdentity(
            "runtime-reference-profile",
            "1.0.0",
            experienceId,
            CompositionKind.Replace,
            new V2CompiledCompositionDetails(
                provenance,
                inputContract,
                regionAccess,
                new CompiledOutputNamingRequirement(
                    "runtime-reference.bin",
                    allowOverride: false,
                    CompiledOutputInvalidCharacterPolicy.Reject,
                    [])));
        AddressSpace[] spaces = includeAuxiliarySource
            ?
            [
                new AddressSpace("reference", 4, AddressSpaceMutability.Immutable),
                new AddressSpace(
                    "source-a",
                    2,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy:
                        sourceArtifactClass == CompiledInputArtifactClass.CtrlRamReplacement
                            ? InputOversizePolicy.TruncateWithWarning
                            : InputOversizePolicy.Reject),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ]
            :
            [
                new AddressSpace("reference", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ];
        ByteRange effectiveMappingRange =
            processorWriteRange ?? new ByteRange(1, 2);
        CompositionOperation[] mappingOperations = includeAuxiliarySource
            ?
            [CompositionOperation.ReplaceRange(
                "replace-source",
                10,
                "source-a",
                new ByteRange(0, effectiveMappingRange.Length),
                "output-image",
                effectiveMappingRange,
                OverlapPolicy.Reject,
                "Replace synthetic runtime source.")]
            : [];
        CompositionOperation[] processorOperations = processorId is null
            ? []
            :
            [
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    int.MaxValue,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        processorId,
                        processorToolBindingId,
                        [new ByteRange(0, 4)],
                        [processorWriteRange ?? new ByteRange(1, 2)],
                        allowedWriteRangeSections:
                            CreateSyntheticPostbuildWriteSections(
                                processorWriteRange ?? new ByteRange(1, 2),
                                processorWriteSectionId)),
                    OverlapPolicy.ReplaceExisting,
                    "Run the synthetic postbuild processor."),
            ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference", 4),
            spaces,
            [.. mappingOperations, .. processorOperations]);
        return CompiledComposition.CreateV2(
            plan,
            identity,
            CompiledIcNumberPolicy.SingleSelector);

        static IReadOnlyList<ExternalProcessorWriteRangeSection>
            CreateSyntheticPostbuildWriteSections(
                ByteRange allowedWriteRange,
                string? sectionId)
        {
            LegacyCombinerPostbuildCommandPlan plan =
                CreateSyntheticPostbuildPlan();
            ByteRange[] stagedTargetRanges =
            [
                .. LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan)
                    .Select(static block => block.FirmwareRange),
            ];
            return
            [
                .. LegacyCombinerPostbuildPlanner
                    .GetAllowedWriteRangeSectionsForStagedSources(
                        plan,
                        4,
                        stagedTargetRanges,
                        stagedTargetRanges)
                    .Where(section =>
                        allowedWriteRange.Contains(section.Range))
                    .Select(section => sectionId is null
                        ? section
                        : new ExternalProcessorWriteRangeSection(
                            sectionId,
                            section.Range,
                            section.SourceRange)),
            ];
        }
    }

    private static LegacyCombinerPostbuildCommandPlan
        CreateSyntheticPostbuildPlan()
    {
        var block = new LegacyCombinerBlockArgument(
            "synthetic-block",
            LegacyCombinerBlockSourceKind.StagedFile,
            "source.bin",
            0,
            new ByteRange(1, 2));
        var command = new LegacyCombinerPostbuildCommand(
            "synthetic-command",
            LegacyCombinerCommandFamily.NormalMode,
            "CRC_Disable",
            crcArgument: null,
            [block]);
        var profile = new LegacyCombinerPostbuildProfile(
            "nfc.synthetic.postbuild",
            "NT-SYNTHETIC",
            "synthetic-postbuild-tool",
            "firmware.bin",
            [command],
            [command],
            "synthetic-evidence");
        LegacyCombinerPostbuildPlanSelector selector =
            profile.PlanSelectors.Single(static candidate =>
                candidate.Kind ==
                    LegacyCombinerPostbuildPlanSelectorKind.SingleChip);
        return LegacyCombinerPostbuildPlanner.CreatePlan(profile, selector);
    }
}
