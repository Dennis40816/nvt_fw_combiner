using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests composition plan validation invariants.</summary>
public sealed class CompositionPlanTests
{
    /// <summary>Verifies overlapping writes are rejected by default.</summary>
    [Fact]
    public void OverlapRejectPolicyFailsPlanValidation()
    {
        _ = Assert.Throws<ArgumentException>(() => CreatePlan(
            CompositionOperation.FillRange(
                "fill-a",
                10,
                "output-image",
                new ByteRange(0, 4),
                0x11,
                OverlapPolicy.Reject,
                "fill range"),
            CompositionOperation.FillRange(
                "fill-b",
                20,
                "output-image",
                new ByteRange(2, 2),
                0x22,
                OverlapPolicy.Reject,
                "accidental overlap")));
    }

    /// <summary>Verifies declared replace-existing overlap is allowed when operation order is deterministic.</summary>
    [Fact]
    public void ReplaceExistingPolicyAllowsOrderedOverlap()
    {
        CompositionPlan plan = CreatePlan(
            CompositionOperation.FillRange(
                "fill-a",
                10,
                "output-image",
                new ByteRange(0, 4),
                0x11,
                OverlapPolicy.Reject,
                "fill range"),
            CompositionOperation.FillRange(
                "fill-b",
                20,
                "output-image",
                new ByteRange(2, 2),
                0x22,
                OverlapPolicy.ReplaceExisting,
                "declared overwrite"));

        CompositionExecutionResult result = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>()));

        Assert.Equal([0x11, 0x11, 0x22, 0x22], result.OutputBytes.ToArray());
    }

    /// <summary>Verifies overlapping writes with the same sequence are rejected even with declared overlap policy.</summary>
    [Fact]
    public void SameSequenceOverlapFailsPlanValidation()
    {
        _ = Assert.Throws<ArgumentException>(() => CreatePlan(
            CompositionOperation.FillRange(
                "fill-a",
                10,
                "output-image",
                new ByteRange(0, 3),
                0x11,
                OverlapPolicy.Reject,
                "fill range"),
            CompositionOperation.FillRange(
                "fill-b",
                10,
                "output-image",
                new ByteRange(1, 2),
                0x22,
                OverlapPolicy.ReplaceExisting,
                "same sequence overlap")));
    }

    /// <summary>Verifies target ranges outside their address space fail before execution.</summary>
    [Fact]
    public void TargetRangeOutsideAddressSpaceFailsPlanValidation()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CreatePlan(
            CompositionOperation.FillRange(
                "fill-outside",
                10,
                "output-image",
                new ByteRange(3, 2),
                0x11,
                OverlapPolicy.Reject,
                "out of bounds")));
    }

    /// <summary>Verifies same-sequence mutable read/write dependencies are rejected before operation-id tie breaking.</summary>
    [Fact]
    public void SameSequenceMutableReadWriteDependencyFailsPlanValidation()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "fill-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 1),
                    0x11,
                    OverlapPolicy.Reject,
                    "write scratch"),
                CompositionOperation.CopyRange(
                    "copy-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 1),
                    "output-image",
                    new ByteRange(3, 1),
                    OverlapPolicy.Reject,
                    "read scratch"),
            ]));
    }

    /// <summary>Verifies allow-declared overlap is rejected until validation evidence is modeled.</summary>
    [Fact]
    public void AllowDeclaredOverlapWithoutValidationEvidenceFailsPlanValidation()
    {
        _ = Assert.Throws<ArgumentException>(() => CreatePlan(
            CompositionOperation.FillRange(
                "fill-a",
                10,
                "output-image",
                new ByteRange(0, 4),
                0x11,
                OverlapPolicy.Reject,
                "fill range"),
            CompositionOperation.FillRange(
                "fill-b",
                20,
                "output-image",
                new ByteRange(2, 2),
                0x22,
                OverlapPolicy.AllowDeclared,
                "declared overlay")));
    }

    /// <summary>Verifies required seeded mutable spaces are exposed to application services.</summary>
    [Fact]
    public void RequiredSeededMutableAddressSpacesListsWorkBuffers()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "fill-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 1),
                    0x11,
                    OverlapPolicy.Reject,
                    "write scratch"),
            ]);

        string addressSpaceId = Assert.Single(plan.RequiredSeededMutableAddressSpaceIds);
        Assert.Equal("scratch", addressSpaceId);
    }

    /// <summary>Verifies replace initialization cannot fabricate missing base image bytes via padding.</summary>
    [Fact]
    public void ReferenceInitializationRejectsPaddedReferenceSpace()
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            []));
    }

    /// <summary>Verifies mutable work buffers cannot fabricate seed bytes via padding.</summary>
    [Fact]
    public void MutableAddressSpaceRejectsInputPadding()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable, inputPaddingByte: 0xFF),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            []));
    }

    /// <summary>Verifies processor plans cannot receive fabricated padded bytes.</summary>
    [Fact]
    public void ExternalProcessorRejectsInputPadding()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("source", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
        ];

        _ = Assert.Throws<ArgumentException>(() => new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-crc",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "crc-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(3, 1)]),
                    OverlapPolicy.Reject,
                    "run crc"),
            ]));
    }

    private static CompositionPlan CreatePlan(params CompositionOperation[] operations)
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        return new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            operations);
    }
}
