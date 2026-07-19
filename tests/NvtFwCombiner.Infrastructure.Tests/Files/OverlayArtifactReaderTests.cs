using System.Runtime.InteropServices;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests immutable in-memory artifact overlays.</summary>
public sealed class OverlayArtifactReaderTests
{
    /// <summary>Verifies repeated reads borrow one private constructor snapshot without copying it again.</summary>
    [Fact]
    public async Task RepeatedReadsReusePrivateConstructorSnapshot()
    {
        byte[] callerBytes = [0x10, 0x20, 0x30];
        var reader = new OverlayArtifactReader(
            fallback: null,
            artifacts: new Dictionary<string, byte[]> { ["virtual-artifact"] = callerBytes });
        callerBytes.AsSpan().Fill(0xFF);

        ReadOnlyMemory<byte> first = await reader.ReadAsync("virtual-artifact", CancellationToken.None);
        ReadOnlyMemory<byte> second = await reader.ReadAsync("virtual-artifact", CancellationToken.None);

        Assert.Equal([0x10, 0x20, 0x30], first.ToArray());
        Assert.True(MemoryMarshal.TryGetArray(first, out ArraySegment<byte> firstBacking));
        Assert.True(MemoryMarshal.TryGetArray(second, out ArraySegment<byte> secondBacking));
        Assert.NotSame(callerBytes, firstBacking.Array);
        Assert.Same(firstBacking.Array, secondBacking.Array);
    }
}
