using System.Runtime.InteropServices;
using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Application.Tests;

/// <summary>External-processor result ownership and full-buffer allocation tests.</summary>
public sealed class ExternalProcessorResultTests
{
    /// <summary>A successful adapter result isolates caller bytes with exactly one full-size allocation.</summary>
    [Fact]
    public void SuccessCopiesCallerBytesExactlyOnce()
    {
        const int byteCount = 1024 * 1024;
        byte[] callerBytes = new byte[byteCount];
        callerBytes[0] = 0x10;
        _ = ExternalProcessorResult.Success(new byte[] { 0x01 }, [], []);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var result = ExternalProcessorResult.Success(callerBytes, [], []);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.True(MemoryMarshal.TryGetArray(result.OutputBytes, out ArraySegment<byte> resultBytes));
        Assert.NotSame(callerBytes, resultBytes.Array);
        Assert.InRange(allocated, byteCount, byteCount + 32_768);
        callerBytes[0] = 0xFF;
        Assert.Equal(0x10, result.OutputBytes.Span[0]);
    }
}
