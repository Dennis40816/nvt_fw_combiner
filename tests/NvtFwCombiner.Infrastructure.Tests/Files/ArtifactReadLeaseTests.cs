using System.Diagnostics;
using System.Security.Cryptography;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests immutable artifact lifetime, bounded reads and staging cleanup.</summary>
public sealed class ArtifactReadLeaseTests
{
    /// <summary>Partial source reads are copied once and random source-local ranges retain exact bytes.</summary>
    [Fact]
    public async Task PartialCopyAndRandomRangesPreserveCompleteIdentity()
    {
        using var workspace = TempWorkspace.Create();
        byte[] expected = Pattern(0, 4099);
        await using var source = new PartialSource(expected);
        using ArtifactReadLease lease = await FileArtifactReadLease.CreateAsync(
            source, workspace.Root, TestContext.Current.CancellationToken);
        ICompositionByteSource bytes = Assert.IsType<ICompositionByteSource>(lease, exactMatch: false);
        var random = new Random(305);

        for (int index = 0; index < 100; index++)
        {
            int offset = random.Next(expected.Length + 1);
            int length = random.Next(Math.Min(257, expected.Length - offset) + 1);
            byte[] actual = new byte[length];
            await bytes.ReadExactlyAsync(offset, actual, TestContext.Current.CancellationToken);
            Assert.Equal(expected.AsSpan(offset, length).ToArray(), actual);
        }
        Assert.Equal(expected.Length, source.BytesRead);
        Assert.Equal(FileStamp.FromBytes(expected), lease.FileStamp);
        Assert.True(source.CanRead);
    }

    /// <summary>Half-open ranges admit empty EOF reads and reject negatives, overflow and crossing EOF.</summary>
    [Fact]
    public async Task RangeBoundariesRejectBeforeChangingDestination()
    {
        using var workspace = TempWorkspace.Create();
        var reader = new OverlayArtifactReader(null, new Dictionary<string, byte[]> { ["input"] = [1, 2, 3] });
        using ArtifactReadLease lease = await reader.OpenReadLeaseAsync(
            "input", workspace.Root, TestContext.Current.CancellationToken);
        byte[] destination = [0xFF, 0xFF];

        await lease.ReadExactlyAsync(0, Memory<byte>.Empty, TestContext.Current.CancellationToken);
        await lease.ReadExactlyAsync(3, Memory<byte>.Empty, TestContext.Current.CancellationToken);
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            lease.ReadExactlyAsync(-1, destination, TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            lease.ReadExactlyAsync(2, destination, TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            lease.ReadExactlyAsync(3, new byte[1], TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            lease.ReadExactlyAsync(4, Memory<byte>.Empty, TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<OverflowException>(() =>
            lease.ReadExactlyAsync(long.MaxValue, destination, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal([0xFF, 0xFF], destination);
        await lease.ReadExactlyAsync(1, destination, TestContext.Current.CancellationToken);
        Assert.Equal([2, 3], destination);
    }

    /// <summary>The original is unlocked after sealing and mutation, rename and deletion cannot alter the lease.</summary>
    [Fact]
    public async Task SealedLeaseSurvivesOriginalReplacementAndDeletesCopyOnce()
    {
        using var workspace = TempWorkspace.Create();
        byte[] expected = Pattern(0, 71);
        string path = workspace.Write("input.bin", expected);
        string staging = Directory.CreateDirectory(workspace.PathFor("staging")).FullName;
        var reader = new FileArtifactReader([workspace.Root]);
        using ArtifactReadLease lease = await reader.OpenReadLeaseAsync(
            path, staging, TestContext.Current.CancellationToken);
        string stagedPath = Assert.Single(Directory.GetFiles(staging));

        await File.WriteAllBytesAsync(path, [9, 8], TestContext.Current.CancellationToken);
        string moved = workspace.PathFor("moved.bin");
        File.Move(path, moved);
        File.Delete(moved);
        byte[] actual = new byte[expected.Length];
        await lease.ReadExactlyAsync(0, actual, TestContext.Current.CancellationToken);
        Assert.Equal(expected, actual);
        Assert.Equal(FileStamp.FromBytes(expected), lease.FileStamp);
        _ = Assert.Throws<IOException>(() =>
        {
            using FileStream writer = File.OpenWrite(stagedPath);
        });
        lease.Dispose();
        lease.Dispose();
        Assert.Empty(Directory.GetFileSystemEntries(staging));
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            lease.ReadExactlyAsync(0, actual, TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            lease.ReadExactlyAsync(0, Memory<byte>.Empty, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Cancelling a read preserves the sealed source for a subsequent independent read.</summary>
    [Fact]
    public async Task CancelledRangeReadDoesNotDisposeLeaseOrWriteDestination()
    {
        using var workspace = TempWorkspace.Create();
        var reader = new OverlayArtifactReader(null, new Dictionary<string, byte[]> { ["input"] = [1, 2] });
        using ArtifactReadLease lease = await reader.OpenReadLeaseAsync(
            "input", workspace.Root, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        byte[] destination = [0xFF, 0xFF];

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            lease.ReadExactlyAsync(0, destination, cancellation.Token).AsTask());
        Assert.Equal([0xFF, 0xFF], destination);
        await lease.ReadExactlyAsync(0, destination, TestContext.Current.CancellationToken);
        Assert.Equal([1, 2], destination);
    }

    /// <summary>Cancellation after a partial copy removes the owned file and preserves caller-owned siblings.</summary>
    [Fact]
    public async Task CancelledPartialCopyLeavesNoOwnedResidue()
    {
        using var workspace = TempWorkspace.Create();
        string sibling = workspace.Write("keep.bin", [9]);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var source = new PartialSource(Pattern(0, 71), read =>
        {
            if (read > 17)
            {
                Assert.Equal(2, Directory.GetFiles(workspace.Root).Length);
                cancellation.Cancel();
            }
        });

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FileArtifactReadLease.CreateAsync(source, workspace.Root, cancellation.Token).AsTask());

        Assert.Equal(sibling, Assert.Single(Directory.GetFiles(workspace.Root)));
        Assert.Equal([9], await File.ReadAllBytesAsync(sibling, TestContext.Current.CancellationToken));
        Assert.True(source.CanRead);
    }

    /// <summary>Read failures and source length changes reject incomplete staging and remove every owned byte.</summary>
    [Theory]
    [InlineData("read-failure")]
    [InlineData("short-read")]
    [InlineData("growth")]
    public async Task SourceFailureCleansPartialStaging(string failure)
    {
        using var workspace = TempWorkspace.Create();
        byte[] expected = Pattern(0, 71);
        await using var source = new PartialSource(expected, read =>
        {
            if (failure == "read-failure" && read > 17)
            {
                throw new IOException("Injected source read failure.");
            }
        }, failure == "short-read" ? 72 : failure == "growth" ? 70 : null);

        Exception exception = await Assert.ThrowsAnyAsync<IOException>(() =>
            FileArtifactReadLease.CreateAsync(source, workspace.Root, TestContext.Current.CancellationToken).AsTask());

        if (failure != "read-failure")
        {
            _ = Assert.IsType<SelectedFileChangedDuringInspectionException>(exception);
        }
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
        Assert.True(source.CanRead);
    }

    /// <summary>A storage write failure after writing bytes is propagated and the partial staging file is deleted.</summary>
    [Fact]
    public async Task StagingWriteFailureLeavesNoResidue()
    {
        using var workspace = TempWorkspace.Create();
        await using var source = new PartialSource(Pattern(0, 71));

        IOException exception = await Assert.ThrowsAsync<IOException>(() =>
            FileArtifactReadLease.CreateAsync(source, workspace.Root, TestContext.Current.CancellationToken,
                path => new FailingStagingFile(path)).AsTask());

        Assert.Equal("Injected staging write failure.", exception.Message);
        Assert.True(source.BytesRead > 17);
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
        Assert.True(source.CanRead);
    }

    /// <summary>Verification covers the final sealed handle, including mutation between writable close and seal.</summary>
    [Fact]
    public async Task ChangedCopyBeforeSealIsRejectedAndCleaned()
    {
        using var workspace = TempWorkspace.Create();
        await using var source = new PartialSource(Pattern(0, 71));

        IOException exception = await Assert.ThrowsAsync<IOException>(() =>
            FileArtifactReadLease.CreateAsync(source, workspace.Root, TestContext.Current.CancellationToken,
                path => new CallbackStagingFile(path, stagedPath =>
                {
                    using FileStream tamper = File.OpenWrite(stagedPath);
                    tamper.WriteByte(0xFF);
                })).AsTask());

        Assert.Equal("Artifact staging verification failed.", exception.Message);
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>File sources are closed before staging verification and changing the original leaves the copy intact.</summary>
    [Fact]
    public async Task SourceCanChangeImmediatelyAfterCopyBeforeVerification()
    {
        using var workspace = TempWorkspace.Create();
        byte[] expected = Pattern(0, 71);
        string original = workspace.Write("input.bin", expected);
        string staging = Directory.CreateDirectory(workspace.PathFor("staging")).FullName;
        await using var source = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ArtifactReadLease lease = await FileArtifactReadLease.CreateAsync(
            source, staging, TestContext.Current.CancellationToken,
            path => new CallbackStagingFile(path, _ => File.WriteAllBytes(original, [9])),
            closeSourceAfterCopy: true);
        byte[] actual = new byte[expected.Length];

        await lease.ReadExactlyAsync(0, actual, TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
        Assert.Equal(FileStamp.FromBytes(expected), lease.FileStamp);
        Assert.False(source.CanRead);
        Assert.Equal([9], await File.ReadAllBytesAsync(original, TestContext.Current.CancellationToken));
    }

    /// <summary>Cancellation at sealing after a complete copy still removes the owned staging file.</summary>
    [Fact]
    public async Task CancellationBetweenCopyAndSealLeavesNoResidue()
    {
        using var workspace = TempWorkspace.Create();
        await using var source = new PartialSource(Pattern(0, 71));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FileArtifactReadLease.CreateAsync(source, workspace.Root, cancellation.Token,
                path => new CallbackStagingFile(path, _ => cancellation.Cancel())).AsTask());

        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>A real streamed 2.2 GiB file is completely staged and read across the signed-int boundary.</summary>
    [Fact]
    public async Task RealFileAboveTwoGiBHasExactIdentityAndBoundaryRanges()
    {
        using var workspace = TempWorkspace.Create();
        const long length = 2_362_232_013;
        const long boundary = 1L << 31;
        string path = workspace.PathFor("large.bin");
        string staging = Directory.CreateDirectory(workspace.PathFor("staging")).FullName;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(60));
        var elapsed = Stopwatch.StartNew();
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] block = Pattern(0, 251 * 4096);
            await using (var file = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 1,
            }))
            {
                for (long offset = 0; offset < length;)
                {
                    int count = (int)Math.Min(block.Length, length - offset);
                    hash.AppendData(block.AsSpan(0, count));
                    await file.WriteAsync(block.AsMemory(0, count), cancellation.Token);
                    offset = checked(offset + count);
                }
            }
            string expectedHash = Convert.ToHexStringLower(hash.GetHashAndReset());
            var reader = new FileArtifactReader([workspace.Root]);
            using (ArtifactReadLease lease = await reader.OpenReadLeaseAsync(path, staging, cancellation.Token))
            {
                Assert.Equal(length, lease.Length);
                Assert.Equal(expectedHash, lease.FileStamp.Sha256);
                foreach (long offset in new[] { boundary - 17, boundary - 1, boundary, boundary + 1, length - 33 })
                {
                    byte[] actual = new byte[33];
                    await lease.ReadExactlyAsync(offset, actual, cancellation.Token);
                    Assert.Equal(Pattern(offset, actual.Length), actual);
                }
                await lease.ReadExactlyAsync(length, Memory<byte>.Empty, cancellation.Token);
                Assert.Equal(length, new FileInfo(Assert.Single(Directory.GetFiles(staging))).Length);
            }
            Assert.Empty(Directory.GetFileSystemEntries(staging));
            TestContext.Current.TestOutputHelper?.WriteLine($"Generated, staged, verified and read {length} bytes in {elapsed.Elapsed.TotalSeconds:F2}s.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] Pattern(long offset, int length)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < length; index++)
        {
            bytes[index] = (byte)((offset + index) % 251);
        }
        return bytes;
    }

    private sealed class PartialSource(byte[] bytes, Action<int>? afterRead = null, long? reportedLength = null)
        : MemoryStream(bytes, writable: false)
    {
        internal int BytesRead { get; private set; }

        public override long Length => reportedLength ?? base.Length;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await base.ReadAsync(buffer[..Math.Min(buffer.Length, 17)], cancellationToken);
            BytesRead += read;
            afterRead?.Invoke(BytesRead);
            return read;
        }
    }

    private sealed class FailingStagingFile(string path)
        : FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous)
    {
        private int _writes;

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await base.WriteAsync(buffer, cancellationToken);
            if (++_writes == 2)
            {
                throw new IOException("Injected staging write failure.");
            }
        }
    }

    private sealed class CallbackStagingFile(string path, Action<string> afterClose)
        : FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous)
    {
        public override async ValueTask DisposeAsync()
        {
            string stagingPath = Name;
            await base.DisposeAsync();
            afterClose(stagingPath);
            GC.SuppressFinalize(this);
        }
    }
}
