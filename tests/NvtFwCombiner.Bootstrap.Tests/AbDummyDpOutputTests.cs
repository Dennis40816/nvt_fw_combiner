using System.Buffers.Binary;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Independent accepted-map expected bytes; not certified product Golden evidence.</summary>
public sealed class AbDummyDpOutputTests
{
    /// <summary>Complete 512-KiB output preserves TP overlays/relocations and fills every other byte with FF.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void DummyOutputMatchesIndependentTwoBankMap(string icId)
    {
        ArgumentNullException.ThrowIfNull(icId);
        var adapter = new BuiltInV2DynamicCompilationAdapter();
        adapter.Compile(new CapabilityRouteIdentity(icId, ExperienceIds.AbMerge,
            "selector-free", $"{icId.ToLowerInvariant()}-ab-merge-512k"), null, [],
            out CompiledComposition? composition, out _, out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        Assert.NotNull(composition);

        // Constants come from the accepted map, not candidate operations or output.
        byte[] tpA = [.. Enumerable.Range(0, 0x40000).Select(index => (byte)(((index * 17) + (index >> 8) + 0x23) & 0xFF))];
        byte[] tpB = [.. Enumerable.Range(0, 0x40000).Select(index => (byte)(((index * 29) + (index >> 9) + 0x61) & 0xFF))];
        int[] relocatedFields = [0x7164, 0x7168, 0x716C];
        foreach (int offset in relocatedFields)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(offset, 4), (uint)(offset + 0x100));
        }
        byte[] originalTpB = [.. tpB];
        byte[] expected = new byte[0x80000];
        Array.Fill(expected, (byte)0xFF);
        tpA.AsSpan(0x7000, 0x39000).CopyTo(expected.AsSpan(0x7000));
        tpB.AsSpan(0x7000, 0x39000).CopyTo(expected.AsSpan(0x47000));
        foreach (int offset in relocatedFields)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(offset + 0x40000, 4),
                (uint)(offset + 0x100 + 0x40000));
        }

        CompositionExecutionResult result = CompositionEngine.Execute(composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["tp-a-input"] = tpA,
                ["tp-b-input"] = tpB,
            }));
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal(originalTpB, tpB);
        Assert.Equal(
            [new ByteRange(0x7164, 4), new ByteRange(0x7168, 4), new ByteRange(0x716C, 4)],
            result.Mutations.Where(mutation => mutation.OperationKind == CompositionOperationKind.TransformScalar)
                .Select(mutation => mutation.TargetRange));
    }
}
