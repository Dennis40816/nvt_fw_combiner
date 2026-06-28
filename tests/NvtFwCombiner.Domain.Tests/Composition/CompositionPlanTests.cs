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
