using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Verifies adding a transform primitive preserves existing external-processor fingerprint bytes.</summary>
    [Fact]
    public void ExistingExternalProcessorCompositionKeepsPinnedFingerprint()
    {
        CompiledComposition composition = CreateExternalProcessorComposition();

        Assert.Equal(
            "73c0d059c0385d1f9c25e65e09889f7afc14d605c998e4ff5665f007534627e4",
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

        Assert.All(variants, variant => Assert.NotEqual(baseline.CompilationFingerprint, variant.CompilationFingerprint));
    }

    private static CompiledComposition CreateScalarTransformComposition(ScalarTransform transform)
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
                "adjust scalar")]);
        return CompiledComposition.CreateLegacy(
            plan,
            CreateMergeIdentity(),
            "output.bin",
            CompiledIcNumberPolicy.NotApplicable,
            []);
    }

    private static CompiledComposition CreateExternalProcessorComposition()
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable)],
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
        return CompiledComposition.CreateLegacy(
            plan,
            CreateMergeIdentity(),
            "output.bin",
            CompiledIcNumberPolicy.NotApplicable,
            []);
    }
}
