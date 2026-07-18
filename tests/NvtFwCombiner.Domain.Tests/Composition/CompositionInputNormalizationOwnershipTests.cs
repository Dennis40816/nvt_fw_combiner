using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Immutable input normalization ownership and allocation tests.</summary>
public sealed class CompositionInputNormalizationOwnershipTests
{
    private const int InputByteCount = 4 * 1024 * 1024;

    /// <summary>Exact normalization reuses the Domain-owned immutable input without another full copy.</summary>
    [Fact]
    public void ExactInputNormalizationUsesOneImmutableSnapshot()
    {
        _ = CompositionEngine.Execute(CreatePlan(inputByteCount: 1), CreateInput(inputByteCount: 1));
        CompositionPlan plan = CreatePlan(InputByteCount);
        CompositionExecutionInput input = CreateInput(InputByteCount);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"INPUT_NORMALIZATION_OWNERSHIP inputBytes={InputByteCount} allocated={allocated}");
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x5A], result.OutputBytes.ToArray());
        Assert.InRange(allocated, 0, 32_768);
    }

    private static CompositionPlan CreatePlan(int inputByteCount)
    {
        return new CompositionPlan(
            ImageInitialization.Blank("output-image", 1, 0),
            [
                new AddressSpace("input", inputByteCount, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 1, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-first-byte",
                    10,
                    "input",
                    new ByteRange(0, 1),
                    "output-image",
                    new ByteRange(0, 1),
                    OverlapPolicy.Reject,
                    "Read one byte from an exact immutable input."),
            ]);
    }

    private static CompositionExecutionInput CreateInput(int inputByteCount)
    {
        byte[] bytes = new byte[inputByteCount];
        bytes[0] = 0x5A;
        return new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["input"] = bytes,
        });
    }
}
