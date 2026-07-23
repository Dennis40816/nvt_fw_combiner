using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunRequestV2Tests
{
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
        Assert.Equal(
            "4abd207b9d82473e7b9224387bf8ed32e84fbaccd89acd7a97ad94640f298625",
            CreateRuntimeReferenceCandidate().CompilationFingerprint);
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
        CompiledInputArtifactClass sourceArtifactClass = CompiledInputArtifactClass.Auxiliary)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(
            modeId,
            FirmwareWriteConstraint.ExplicitRange);
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
                    allowsConditionalProcessor)
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
            new CompiledNoInputNormalization());
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
            includeProcessorView
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
                new AddressSpace("source-a", 2, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ]
            :
            [
                new AddressSpace("reference", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ];
        CompositionOperation[] operations = includeAuxiliarySource
            ?
            [CompositionOperation.ReplaceRange(
                "replace-source",
                10,
                "source-a",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(1, 2),
                OverlapPolicy.Reject,
                "Replace synthetic runtime source.")]
            : [];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference", 4),
            spaces,
            operations);
        return CompiledComposition.CreateV2(
            plan,
            identity,
            CompiledIcNumberPolicy.SingleSelector);
    }
}
