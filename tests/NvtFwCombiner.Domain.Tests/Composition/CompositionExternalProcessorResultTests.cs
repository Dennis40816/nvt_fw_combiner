using System.Runtime.InteropServices;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Domain external-processor import ownership and full-buffer allocation tests.</summary>
public sealed class CompositionExternalProcessorResultTests
{
    /// <summary>A successful imported result isolates hook bytes with exactly one full-size allocation.</summary>
    [Fact]
    public void SuccessCopiesHookBytesExactlyOnce()
    {
        const int byteCount = 1024 * 1024;
        byte[] hookBytes = new byte[byteCount];
        hookBytes[0] = 0x20;
        _ = CompositionExternalProcessorResult.Success(new byte[] { 0x02 });

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var result = CompositionExternalProcessorResult.Success(hookBytes);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.True(MemoryMarshal.TryGetArray(result.OutputBytes, out ArraySegment<byte> resultBytes));
        Assert.NotSame(hookBytes, resultBytes.Array);
        Assert.InRange(allocated, byteCount, byteCount + 32_768);
        hookBytes[0] = 0xFF;
        Assert.Equal(0x20, result.OutputBytes.Span[0]);
    }
}
