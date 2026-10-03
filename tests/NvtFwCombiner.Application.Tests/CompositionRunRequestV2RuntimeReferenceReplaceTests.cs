using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests runtime-reference Replace admission and execution.</summary>
[Collection(nameof(CompositionRunRequestV2SerialGroup))]
public sealed partial class CompositionRunRequestV2RuntimeReferenceReplaceTests
{
    /// <summary>A typed CtrlRAM Replace choice cannot bind a differently shaped compiler result.</summary>
    [Fact]
    public void RuntimeReferenceCompilationRejectsRouteSelectorModeDrift()
    {
        CompiledComposition composition = CreateRuntimeReferenceCandidate();
        var choice = new CapabilityNumberChoice(
            IcNumberSelectionTokens.Cascade,
            "Cascade");

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            WorkflowIcNumberChoiceProjection.ValidateCompilation(choice, composition));

        Assert.Equal("composition", exception.ParamName);
        Assert.Contains("IC-number mode", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The final run-request boundary rejects a stale selector mode independently of UI reconciliation.</summary>
    [Fact]
    public void RuntimeReferenceCandidateRejectsIncompatibleIcNumberMode()
    {
        CompiledComposition composition = CreateRuntimeReferenceCandidate();

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new CompositionRunRequest(
                "runtime-reference-stale-selector",
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
                        CompiledInputArtifactClass.CtrlRamReplacement),
                ],
                "runtime-reference.bin",
                icNumberSelection: new IcNumberSelection(
                    IcNumberInputMode.CascadeSelector,
                    ["cascade"])));

        Assert.Equal("selection", exception.ParamName);
        Assert.Contains("mode must match", exception.Message, StringComparison.Ordinal);
    }

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
            $"postbuild-plan:{LegacyCombinerPostbuildPlanCompiler.CalculateIntegrityFingerprint(postbuildPlan, 4)}",
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
        CompiledComposition wrongTopology = CreateRuntimeReferenceCandidate(
            allowsConditionalProcessor: true,
            includeProcessorView: true,
            modeId: ExperienceIds.CtrlRamReplace,
            experienceId: ExperienceIds.CtrlRamReplace,
            sourceArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement,
            rootOwner: FirmwareRegionOwner.Tp,
            rootKind: FirmwareRegionKind.CtrlRam,
            processorId: "nfc.synthetic.postbuild",
            topologyRequirement: TopologyRequirement.RequireExactCount(2),
            requestedTopology: new TopologySelection(
                2,
                "2 IC",
                TopologySelectionSource.Requested,
                "test"));
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                wrongTopology,
                postbuildPlan));
        Assert.Empty(typeof(LegacyCombinerPostbuildCommandPlan).GetConstructors());
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                CreateRuntimeReferenceCandidate(),
                postbuildPlan));
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
        CompiledComposition broadenedToReadView = CreateRuntimeReferenceCandidate(
            allowsConditionalProcessor: true,
            includeProcessorView: true,
            modeId: ExperienceIds.CtrlRamReplace,
            experienceId: ExperienceIds.CtrlRamReplace,
            sourceArtifactClass: CompiledInputArtifactClass.CtrlRamReplacement,
            rootOwner: FirmwareRegionOwner.System,
            rootKind: FirmwareRegionKind.Image,
            processorId: "nfc.synthetic.postbuild",
            processorAdditionalWriteRanges: [new ByteRange(0, 4)],
            useNestedCtrlRamMap: true);
        _ = Assert.Throws<ArgumentException>(() =>
            RuntimeReferenceCompilationProof.CreateLegacyPostbuild(
                broadenedToReadView,
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

        var authoringCatalog =
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(resolved);
        Assert.Equal(
            ["reference-slot", "source-a"],
            authoringCatalog.Routes.Single().SlotDefinitions.Select(static slot => slot.DefinitionId));
        IReadOnlyDictionary<string, AuthoringInputSlotStatus> authoringStatuses =
            AuthoringInputSlotInspectionService.InspectBatch(
                resolved,
                new AuthoringRevision(1),
                new Dictionary<string, ReadOnlyMemory<byte>?>
                {
                    ["reference"] = new byte[] { 0, 1, 2, 3 },
                    ["source-a"] = new byte[] { 0xAA, 0xBB },
                });
        Assert.Equal("source-a", authoringStatuses["source-a"].SlotId);
        Assert.NotNull(resolved.RuntimeReferenceProof);

        ResolvedCapabilityRoute CreateRoute(IReadOnlyList<string> bindings)
        {
            var contract = new CanonicalCapabilityCompilationContract(
                composition.V2Details.ProfileId,
                composition.V2Details.ProfileVersion,
                composition.V2Details.Provenance.Bundle.ContentHash,
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
                    CompiledInputArtifactClass.CtrlRamReplacement),
            ],
            "runtime-reference.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        CompositionRunResult preview = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        Assert.Equal([0, 0xAA, 0xBB, 3], preview.OutputBytes.ToArray());
        Assert.False(writer.WasCalled);
        _ = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            composition.V2Details.Provenance.Context);
    }

    /// <summary>A supported runtime-reference artifact uses ordinary runtime admission without the candidate exception.</summary>
    [Fact]
    public void RuntimeReferenceSupportedArtifactUsesOrdinaryRuntimeAdmission()
    {
        CompiledComposition composition = CreateRuntimeReferenceCandidate(runtimeExecutable: true);

        var request = new CompositionRunRequest(
            "runtime-reference-supported",
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
                    CompiledInputArtifactClass.CtrlRamReplacement),
            ],
            "runtime-reference.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        Assert.Same(composition, request.CompiledComposition);
    }

    /// <summary>Verifies the runtime-reference context has an explicit compilation-fingerprint vector.</summary>
    [Fact]
    public void RuntimeReferenceCandidateHasStableCompilationFingerprint()
    {
        // Historical synthetic fingerprint vector; it does not prepare or execute a retired workflow.
        CompiledComposition candidate = CreateRuntimeReferenceCandidate(
            modeId: ExperienceIds.GeneralReplace,
            experienceId: ExperienceIds.GeneralReplace,
            sourceArtifactClass: CompiledInputArtifactClass.Auxiliary);
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
            baseline.V2Details.Provenance.Context).AllowsConditionalProcessor);
        Assert.True(Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            conditional.V2Details.Provenance.Context).AllowsConditionalProcessor);
        Assert.NotEqual(baseline.CompilationFingerprint, conditional.CompilationFingerprint);
    }

    /// <summary>Verifies runtime-reference bindings retain their compiler-materialized identities.</summary>
    [Theory]
    [InlineData("other-reference", "source-a")]
    [InlineData("reference", "other-source")]
    public void RuntimeReferenceCandidateRejectsMismatchedBindingIdentity(
        string referenceBindingId,
        string sourceBindingId)
    {
        ArgumentNullException.ThrowIfNull(referenceBindingId);
        ArgumentNullException.ThrowIfNull(sourceBindingId);
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
                    CompiledInputArtifactClass.CtrlRamReplacement),
            ],
            "runtime-reference.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));
    }

}
