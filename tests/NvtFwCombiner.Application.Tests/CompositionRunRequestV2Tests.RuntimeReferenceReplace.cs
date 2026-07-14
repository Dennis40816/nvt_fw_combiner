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
            "a5c13ad6486c7d50b67b20984d334b2334f1bb8cfb4328f566d2ee0b3349b86e",
            CreateRuntimeReferenceCandidate().CompilationFingerprint);
    }

    /// <summary>Verifies runtime-reference bindings retain their compiler-materialized identities.</summary>
    [Fact]
    public void RuntimeReferenceCandidateRejectsMismatchedBindingIdentity()
    {
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "runtime-reference-mismatched-binding",
            CreateRuntimeReferenceCandidate(),
            [
                new InputArtifactBinding(
                    "reference",
                    "other-reference",
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
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])));
    }

    private static CompiledComposition CreateRuntimeReferenceCandidate(
        bool useRuntimeContext = true,
        bool includeAuxiliarySource = true)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(
            "general-replace",
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
                ? new RuntimeReferenceReplaceV2CompilationContext(resolvedMap)
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
            CompiledInputArtifactClass.Auxiliary,
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
            []);
        var identity = new V2CompiledCompositionIdentity(
            "runtime-reference-profile",
            "1.0.0",
            ExperienceIds.GeneralReplace,
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
