using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionPlanTests
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

    /// <summary>Rejects processor input that would otherwise depend on same-sequence operation-id ordering.</summary>
    [Fact]
    public void SameSequenceExternalProcessorReadWriteDependencyFailsPlanValidation()
    {
        _ = Assert.Throws<ArgumentException>(() => CreatePlan(
            CompositionOperation.FillRange(
                "write-processor-input",
                10,
                "output-image",
                new ByteRange(0, 1),
                0x11,
                OverlapPolicy.Reject,
                "write processor read range"),
            CompositionOperation.RunExternalProcessor(
                "run-postbuild",
                10,
                "output-image",
                new ByteRange(0, 4),
                new ExternalProcessorInvocation(
                    "processor-v1",
                    "tool-v1",
                    [new ByteRange(0, 1)],
                    [new ByteRange(3, 1)]),
                OverlapPolicy.ReplaceExisting,
                "run postbuild")));
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
}
