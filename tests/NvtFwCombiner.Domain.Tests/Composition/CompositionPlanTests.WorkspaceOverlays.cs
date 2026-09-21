using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionPlanTests
{
    /// <summary>Compiler-owned workspace overlays may explicitly replace a completely seeded range.</summary>
    [Theory]
    [InlineData(CompositionOperationKind.ReplaceRange)]
    [InlineData(CompositionOperationKind.PatchScalar)]
    [InlineData(CompositionOperationKind.TransformScalar)]
    public void ExplicitWorkspaceOverlayPreservesAllUnselectedBytes(CompositionOperationKind kind)
    {
        byte[] reference = [10, 11, 12, 13, 0, 15, 42, 0];
        CompositionPlan plan = WorkspaceOverlayPlan(kind, [new ByteRange(0, 8)], OverlapPolicy.ReplaceExisting);
        CompositionExecutionResult result = CompositionEngine.Execute(plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]> { ["reference"] = reference }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(new byte[] { 10, 11, 12, 42, 0, 15, 42, 0 }, result.OutputBytes.ToArray());
        Assert.Equal(new byte[] { 10, 11, 12, 13, 0, 15, 42, 0 }, reference);
    }

    /// <summary>Explicit replacement cannot claim coverage from an adjacent or absent seed.</summary>
    [Theory]
    [InlineData(CompositionOperationKind.ReplaceRange)]
    [InlineData(CompositionOperationKind.PatchScalar)]
    [InlineData(CompositionOperationKind.TransformScalar)]
    public void WorkspaceOverlayStillRequiresPriorCompleteCoverage(CompositionOperationKind kind)
    {
        _ = Assert.Throws<ArgumentException>(() => WorkspaceOverlayPlan(kind, [],
            OverlapPolicy.ReplaceExisting));
        _ = Assert.Throws<ArgumentException>(() => WorkspaceOverlayPlan(kind, [new ByteRange(0, 4)],
            OverlapPolicy.ReplaceExisting));
        _ = Assert.Throws<ArgumentException>(() => WorkspaceOverlayPlan(kind, [new ByteRange(3, 1), new ByteRange(4, 1)],
            OverlapPolicy.ReplaceExisting));
        _ = Assert.Throws<ArgumentException>(() => WorkspaceOverlayPlan(kind, [new ByteRange(0, 8)],
            OverlapPolicy.ReplaceExisting, crossSpace: true));
        _ = Assert.Throws<ArgumentException>(() => WorkspaceOverlayPlan(kind, [new ByteRange(0, 8)],
            OverlapPolicy.Reject));
    }

    private static CompositionPlan WorkspaceOverlayPlan(
        CompositionOperationKind kind, ByteRange[] seedRanges, OverlapPolicy overlap, bool crossSpace = false)
    {
        var field = new ByteRange(3, 2);
        CompositionOperation overlay = kind switch
        {
            CompositionOperationKind.ReplaceRange => CompositionOperation.ReplaceRange(
                "overlay", 100, "reference", new ByteRange(6, 2), "output", field, overlap, "replace"),
            CompositionOperationKind.PatchScalar => CompositionOperation.PatchScalar(
                "overlay", 100, "output", field, [42, 0], overlap, "patch"),
            CompositionOperationKind.TransformScalar => CompositionOperation.TransformScalar(
                "overlay", 100, "output", field, "output", field,
                new ScalarTransform(ScalarTransformWidth.TwoBytes, ScalarTransformByteOrder.LittleEndian,
                    29, 13, ScalarTransformOverflowPolicy.Reject), overlap, "transform"),
            CompositionOperationKind.CopyRange or CompositionOperationKind.FillRange or
                CompositionOperationKind.RunExternalProcessor => throw new ArgumentOutOfRangeException(nameof(kind)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        CompositionOperation[] operations =
        [
            .. seedRanges.Select((range, index) => CompositionOperation.CopyRange($"seed-{index}", index,
                "reference", range, crossSpace ? "other" : "output", range, OverlapPolicy.Reject, "seed workspace")),
            overlay,
        ];
        string? profileError = overlay.GetProfileOverlapError(operations[..^1]);
        _ = profileError is null ? true : throw new ArgumentException(profileError, nameof(overlap));
        return new CompositionPlan([ImageInitialization.Blank("output", 8, 0), ImageInitialization.Blank("other", 8, 0)], "output",
            [new AddressSpace("reference", 8, AddressSpaceMutability.Immutable),
             new AddressSpace("other", 8, AddressSpaceMutability.Mutable),
             new AddressSpace("output", 8, AddressSpaceMutability.Mutable)], operations);
    }
}
