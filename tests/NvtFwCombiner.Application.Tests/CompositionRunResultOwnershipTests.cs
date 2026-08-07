using System.Runtime.InteropServices;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Application result publication ownership and full-output allocation tests.</summary>
public sealed class CompositionRunResultOwnershipTests
{
    private const int OutputByteCount = 4 * 1024 * 1024;
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Application publication retains the Domain-owned immutable output without a second full copy.</summary>
    [Fact]
    public async Task PreviewPublishesOneImmutableLargeOutput()
    {
        _ = await PreviewAsync(outputByteCount: 1);
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["ownership-input-artifact"] = [0],
            }),
            new FakeClock([StartedAtUtc, StartedAtUtc.AddSeconds(1)]));
        CompositionRunRequest request = CreateRequest(OutputByteCount);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        CompositionRunResult result = await service.PreviewAsync(
            request,
            TestContext.Current.CancellationToken);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"RUN_RESULT_OWNERSHIP outputBytes={OutputByteCount} allocated={allocated}");
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(OutputByteCount, result.OutputBytes.Length);
        Assert.Equal(0x5A, result.OutputBytes.Span[0]);
        Assert.Equal(0xFF, result.OutputBytes.Span[^1]);
        Assert.InRange(allocated, 0, ((long)OutputByteCount * 2) + 32_768);
    }

    /// <summary>The public result constructor still isolates bytes supplied by an arbitrary caller.</summary>
    [Fact]
    public async Task PublicConstructorCopiesCallerOutputBytes()
    {
        CompositionRunResult template = await PreviewAsync(outputByteCount: 1);
        byte[] callerBytes = [0x10, 0x20, 0x30, 0x40];

        var result = new CompositionRunResult(
            CompositionExecutionStatus.Succeeded,
            callerBytes,
            template.Report,
            committedOutputId: null);

        Assert.True(MemoryMarshal.TryGetArray(result.OutputBytes, out ArraySegment<byte> resultBytes));
        Assert.NotSame(callerBytes, resultBytes.Array);
        callerBytes[0] = 0xFF;
        Assert.Equal(0x10, result.OutputBytes.Span[0]);
    }

    private static ValueTask<CompositionRunResult> PreviewAsync(int outputByteCount)
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["ownership-input-artifact"] = [0],
            }),
            new FakeClock([StartedAtUtc, StartedAtUtc.AddSeconds(1)]));
        return service.PreviewAsync(CreateRequest(outputByteCount), CancellationToken.None);
    }

    private static CompositionRunRequest CreateRequest(int outputByteCount)
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", outputByteCount, 0xFF),
            [
                new AddressSpace("ownership-input", 1, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", outputByteCount, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.FillRange(
                    "fill-first-byte",
                    10,
                    "output-image",
                    new ByteRange(0, 1),
                    0x5A,
                    OverlapPolicy.Reject,
                    "Create a deterministic non-empty mutation."),
            ]);
        CompiledComposition compiled = CompiledCompositionTestFactory.Create(
            plan,
            new TestCompiledCompositionIdentity(
                "run-result-ownership",
                "1.0.0",
                "NT-SYNTHETIC",
                "general-merge",
                "general-merge",
                CompositionKind.Merge),
            "run-result-ownership.bin",
            null);
        return new CompositionRunRequest(
            "run-result-ownership",
            compiled,
            [new InputArtifactBinding(
                "ownership-input",
                "ownership-input",
                "ownership-input-artifact",
                "ownership-input.bin",
                CompiledInputArtifactClass.TpFirmware)],
            "run-result-ownership.bin");
    }
}
