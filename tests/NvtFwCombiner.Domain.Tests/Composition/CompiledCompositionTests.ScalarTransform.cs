using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Verifies the canonical V2 writer pins external-processor fingerprint bytes.</summary>
    [Fact]
    public void V2ExternalProcessorCompositionHasPinnedFingerprint()
    {
        CompiledComposition composition = CreateExternalProcessorComposition();

        Assert.Equal(
            "ab62d604a3e8e44d27a80f751055dcd384ab5f007f53e09c28a6a4a407457c88",
            composition.CompilationFingerprint);
    }

    /// <summary>Verifies every scalar-transform declaration field contributes to compilation identity.</summary>
    [Fact]
    public void CompilationFingerprintBindsScalarTransformDeclaration()
    {
        CompiledComposition baseline = CreateScalarTransformComposition(new ScalarTransform(
            ScalarTransformWidth.TwoBytes,
            ScalarTransformByteOrder.LittleEndian,
            1,
            expectedBefore: 0x1200,
            ScalarTransformOverflowPolicy.Reject));
        CompiledComposition[] variants =
        [
            CreateScalarTransformComposition(new ScalarTransform(
                ScalarTransformWidth.TwoBytes,
                ScalarTransformByteOrder.BigEndian,
                1,
                expectedBefore: 0x1200,
                ScalarTransformOverflowPolicy.Reject)),
            CreateScalarTransformComposition(new ScalarTransform(
                ScalarTransformWidth.TwoBytes,
                ScalarTransformByteOrder.LittleEndian,
                2,
                expectedBefore: 0x1200,
                ScalarTransformOverflowPolicy.Reject)),
            CreateScalarTransformComposition(new ScalarTransform(
                ScalarTransformWidth.TwoBytes,
                ScalarTransformByteOrder.LittleEndian,
                1,
                expectedBefore: 0x1201,
                ScalarTransformOverflowPolicy.Reject)),
            CreateScalarTransformComposition(new ScalarTransform(
                ScalarTransformWidth.OneByte,
                ScalarTransformByteOrder.LittleEndian,
                1,
                expectedBefore: 0x12,
                ScalarTransformOverflowPolicy.Reject)),
        ];

        Assert.All(
            variants,
            variant =>
            {
                Assert.NotEqual(
                    baseline.CompilationFingerprint,
                    variant.CompilationFingerprint);
            });
        CompiledComposition reasonOnly = CreateScalarTransformComposition(
            baseline.Plan.OrderedOperations.Single().ScalarTransform!,
            "wording-only change");
        Assert.NotEqual(
            baseline.CompilationFingerprint,
            reasonOnly.CompilationFingerprint);
    }

    /// <summary>Verifies a resolved instance delta cannot collide with an equal fixed addend.</summary>
    [Fact]
    public void CompilationFingerprintBindsScalarTransformAddendSource()
    {
        CompiledComposition fixedAddend = CreateScalarTransformComposition(new ScalarTransform(
            ScalarTransformWidth.FourBytes,
            ScalarTransformByteOrder.LittleEndian,
            0x40000,
            expectedBefore: null,
            ScalarTransformOverflowPolicy.Reject));
        CompiledComposition instanceDelta = CreateScalarTransformComposition(new ScalarTransform(
            ScalarTransformWidth.FourBytes,
            ScalarTransformByteOrder.LittleEndian,
            0x40000,
            expectedBefore: null,
            ScalarTransformOverflowPolicy.Reject,
            ScalarTransformAddendSource.RegionInstanceDelta("a-bank", "b-bank")));

        Assert.NotEqual(fixedAddend.CompilationFingerprint, instanceDelta.CompilationFingerprint);
    }

    private static CompiledComposition CreateScalarTransformComposition(
        ScalarTransform transform,
        string reason = "adjust scalar")
    {
        long width = transform.WidthBytes;
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", width, 0),
            [
                new AddressSpace("source", width, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", width, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.TransformScalar(
                "transform-scalar",
                10,
                "source",
                new ByteRange(0, width),
                "output-image",
                new ByteRange(0, width),
                transform,
                OverlapPolicy.Reject,
                reason)]);
        return CreateV2(
            outputTemplate: "output.bin",
            requiredOutputTokenIds: [],
            inputContract: CreateExactMapInputContract(
                "source",
                CompiledInputArtifactClass.ReferenceImage,
                width),
            resolvedMap: CreateResolvedMap(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                capacity: width),
            plan: plan,
            profileId: "profile-a",
            profileVersion: "1.0.0");
    }

    private static CompiledComposition CreateExternalProcessorComposition()
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [
                new AddressSpace("input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.RunExternalProcessor(
                "postbuild",
                10,
                "output-image",
                new ByteRange(0, 4),
                new ExternalProcessorInvocation(
                    "processor-a",
                    "tool-a",
                    [new ByteRange(0, 4)],
                    [new ByteRange(2, 1)]),
                OverlapPolicy.Reject,
                "refresh postbuild")]);
        return CreateV2(
            outputTemplate: "output.bin",
            requiredOutputTokenIds: [],
            inputContract: CreateExactMapInputContract(
                "input",
                CompiledInputArtifactClass.ReferenceImage),
            plan: plan,
            profileId: "profile-a",
            profileVersion: "1.0.0");
    }
}
