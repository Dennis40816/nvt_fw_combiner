using System.Runtime.InteropServices;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests immutable in-memory artifact overlays.</summary>
public sealed class OverlayArtifactReaderTests
{
    /// <summary>Virtual leases seal the private overlay snapshot and retain the materialized read behavior.</summary>
    [Fact]
    public async Task VirtualLeaseUsesSnapshotBeforeFallbackAndCleansOnDispose()
    {
        using var workspace = TempWorkspace.Create();
        byte[] callerBytes = [0x10, 0x20, 0x30];
        var reader = new OverlayArtifactReader(new MaterializationOnlyReader(),
            new Dictionary<string, byte[]> { ["virtual"] = callerBytes });
        callerBytes.AsSpan().Fill(0xFF);
        using (ArtifactReadLease lease = await reader.OpenReadLeaseAsync(
            "virtual", workspace.Root, TestContext.Current.CancellationToken))
        {
            byte[] actual = new byte[2];
            await lease.ReadExactlyAsync(1, actual, TestContext.Current.CancellationToken);
            Assert.Equal([0x20, 0x30], actual);
            Assert.Equal(FileStamp.FromBytes([0x10, 0x20, 0x30]), lease.FileStamp);
            Assert.Equal([0x10, 0x20, 0x30],
                (await reader.ReadAsync("virtual", TestContext.Current.CancellationToken)).ToArray());
            _ = Assert.Single(Directory.GetFiles(workspace.Root));
        }
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>Unknown overlay artifacts use the same fallback source and caller-provided staging root.</summary>
    [Fact]
    public async Task UnknownArtifactDelegatesBothCapabilitiesToFileReader()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("input.bin", [4, 5, 6]);
        string staging = Directory.CreateDirectory(workspace.PathFor("staging")).FullName;
        var reader = new OverlayArtifactReader(new FileArtifactReader([workspace.Root]), []);
        using ArtifactReadLease lease = await reader.OpenReadLeaseAsync(
            path, staging, TestContext.Current.CancellationToken);
        byte[] actual = new byte[3];

        await lease.ReadExactlyAsync(0, actual, TestContext.Current.CancellationToken);

        Assert.Equal([4, 5, 6], actual);
        Assert.Equal(actual, (await reader.ReadAsync(path, TestContext.Current.CancellationToken)).ToArray());
        _ = Assert.Single(Directory.GetFiles(staging));
    }

    /// <summary>Missing virtual artifacts fail consistently without staging or materialization.</summary>
    [Fact]
    public async Task MissingArtifactWithoutFallbackRefusesBothCapabilities()
    {
        using var workspace = TempWorkspace.Create();
        var reader = new OverlayArtifactReader(null, []);

        _ = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            reader.OpenReadLeaseAsync("missing", workspace.Root, TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            reader.ReadAsync("missing", TestContext.Current.CancellationToken).AsTask());

        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>Existing materialization-only implementations remain usable and do not silently materialize leases.</summary>
    [Fact]
    public async Task MaterializationOnlyReaderRetainsSignatureCompatibility()
    {
        using var workspace = TempWorkspace.Create();
        IArtifactReader reader = new MaterializationOnlyReader();
        var overlay = new OverlayArtifactReader(reader, []);

        Assert.Equal([9], (await reader.ReadAsync("legacy", TestContext.Current.CancellationToken)).ToArray());
        Assert.Equal([9], (await overlay.ReadAsync("legacy", TestContext.Current.CancellationToken)).ToArray());
        _ = await Assert.ThrowsAsync<NotSupportedException>(() =>
            reader.OpenReadLeaseAsync("legacy", workspace.Root, TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<NotSupportedException>(() =>
            overlay.OpenReadLeaseAsync("legacy", workspace.Root, TestContext.Current.CancellationToken).AsTask());
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>Cancellation refuses virtual staging and fallback dispatch before any file is created.</summary>
    [Theory]
    [InlineData("virtual")]
    [InlineData("missing")]
    public async Task CancelledLeaseCreatesNoCopy(string artifactId)
    {
        using var workspace = TempWorkspace.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reader = new OverlayArtifactReader(new MaterializationOnlyReader(),
            new Dictionary<string, byte[]> { ["virtual"] = [1] });

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.OpenReadLeaseAsync(artifactId, workspace.Root, cancellation.Token).AsTask());

        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
    }

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

    private sealed class MaterializationOnlyReader : IArtifactReader
    {
        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string artifactId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReadOnlyMemory<byte>>("\t"u8.ToArray());
        }
    }
}
