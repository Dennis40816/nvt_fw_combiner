using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Behavior of the local transport's admitted-extent stream, independent of file sharing.</summary>
public sealed class BoundedLocalReadStreamTests
{
    /// <summary>Every read entry point returns complete partial reads without exposing the growing tail.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PartialReadsNeverExposeBytesBeyondAdmittedExtent(int readMode)
    {
        using var source = new TestReadStream([1, 2, 3, 4, 99, 100], reportedLength: 4, partialBytes: 2);
        using var bounded = new BoundedLocalReadStream(source, 16, TestContext.Current.CancellationToken, 4);
        var exposed = new List<byte>();
        byte[] buffer = new byte[16];
        Func<byte[], int, int, CancellationToken, Task<int>> arrayReadAsync = bounded.ReadAsync;
        int read;
        while ((read = readMode switch
        {
            0 => bounded.Read(buffer, 0, buffer.Length),
            1 => bounded.Read(buffer.AsSpan()),
            2 => await arrayReadAsync(buffer, 0, buffer.Length, TestContext.Current.CancellationToken),
            _ => await bounded.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken),
        }) != 0)
        {
            exposed.AddRange(buffer.Take(read));
        }

        Assert.Equal([1, 2, 3, 4], exposed);
        Assert.Equal(4, bounded.Position);
        Assert.Equal(4, source.BytesRead);
        _ = await Assert.ThrowsAsync<LocalFileReadException>(() => bounded.CompleteAsync().AsTask());
        Assert.Equal(5, source.BytesRead);
        Assert.Equal([4, 2, 1], source.Requests);
        _ = await Assert.ThrowsAsync<LocalFileReadException>(() => bounded.CompleteAsync().AsTask());
        Assert.Equal(5, source.BytesRead);
    }

    /// <summary>An unknown-length stream ends normally below or exactly at the caller's ceiling.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task NonSeekableSourcesAcceptEmptyPartialAndExactLimit(int length)
    {
        byte[] expected = [.. Enumerable.Range(1, length).Select(value => (byte)value)];
        using var source = new TestReadStream(expected, seekable: false, partialBytes: 1);
        using var bounded = new BoundedLocalReadStream(source, 4, TestContext.Current.CancellationToken);
        using var destination = new MemoryStream();

        await bounded.CopyToAsync(destination, TestContext.Current.CancellationToken);
        await bounded.CompleteAsync();

        Assert.Equal(expected, destination.ToArray());
        Assert.Equal(length, bounded.Position);
        Assert.False(bounded.CanSeek);
        Assert.All(source.Requests, count => Assert.InRange(count, 1, 4));
        Assert.Equal(-1, bounded.ReadByte());
    }

    /// <summary>The limit probe is never returned even through synchronous reads.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonSeekableOverflowRefusesAfterExactlyOneExtraByte(bool asynchronous)
    {
        using var source = new TestReadStream([1, 2, 3, 4, 5, 6], seekable: false);
        using var bounded = new BoundedLocalReadStream(source, 4, TestContext.Current.CancellationToken);
        byte[] buffer = new byte[8];
        int read = asynchronous
            ? await bounded.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken)
            : bounded.Read(buffer);
        Assert.Equal(4, read);
        Assert.Equal([1, 2, 3, 4], buffer[..read]);

        _ = asynchronous
            ? await Assert.ThrowsAsync<LocalFileTooLargeException>(() =>
                bounded.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken).AsTask())
            : Assert.Throws<LocalFileTooLargeException>(() => bounded.ReadByte());
        _ = await Assert.ThrowsAsync<LocalFileTooLargeException>(() => bounded.CompleteAsync().AsTask());
        Assert.Equal(5, source.BytesRead);
        Assert.Equal([4, 1], source.Requests);
    }

    /// <summary>An empty read does not falsely establish EOF or reject a valid exact extent.</summary>
    [Fact]
    public async Task EmptyReadDoesNotConsumeOrValidateSource()
    {
        using var source = new TestReadStream([1]);
        using var bounded = new BoundedLocalReadStream(source, 1, TestContext.Current.CancellationToken, 1);

        Assert.Equal(0, bounded.Read([]));
        Assert.Equal(0, await bounded.ReadAsync(Memory<byte>.Empty, TestContext.Current.CancellationToken));
        Assert.Empty(source.Requests);
        Assert.Equal(1, bounded.ReadByte());
        Assert.Equal(-1, bounded.ReadByte());
        await bounded.CompleteAsync();
    }

    /// <summary>Premature EOF is a refusal that completion cannot erase after a projector catches it.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShrinkageFailureCannotBeSwallowedByProjector(bool asynchronous)
    {
        using var source = new TestReadStream([1, 2, 3], reportedLength: 4);
        using var bounded = new BoundedLocalReadStream(source, 4, TestContext.Current.CancellationToken, 4);
        byte[] buffer = new byte[4];
        _ = asynchronous
            ? await Assert.ThrowsAsync<LocalFileReadException>(() =>
                bounded.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken).AsTask())
            : Assert.Throws<LocalFileReadException>(() => bounded.ReadExactly(buffer));

        _ = await Assert.ThrowsAsync<LocalFileReadException>(() => bounded.CompleteAsync().AsTask());
        Assert.Equal(3, source.BytesRead);
    }

    /// <summary>Completion checks source length even when projection returns without reading the tail.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task CompletionRejectsChangedLengthAfterEarlyProjection(int changedLength)
    {
        using var source = new MemoryStream();
        source.Write([1, 2, 3, 4]);
        source.Position = 0;
        using var bounded = new BoundedLocalReadStream(source, 8, TestContext.Current.CancellationToken, 4);
        Assert.Equal(1, bounded.ReadByte());
        source.SetLength(changedLength);

        _ = await Assert.ThrowsAsync<LocalFileReadException>(() => bounded.CompleteAsync().AsTask());
    }

    /// <summary>A known-length source that grows over the caller ceiling retains the typed limit refusal.</summary>
    [Fact]
    public async Task CompletionKeepsTypedLimitFailureForGrowthPastCeiling()
    {
        using var source = new MemoryStream();
        source.Write([1, 2, 3, 4]);
        source.Position = 0;
        using var bounded = new BoundedLocalReadStream(source, 4, TestContext.Current.CancellationToken, 4);
        source.SetLength(5);

        _ = await Assert.ThrowsAsync<LocalFileTooLargeException>(() => bounded.CompleteAsync().AsTask());
    }

    /// <summary>Incomplete unknown-length projection is not mislabeled as exceeding the caller's ceiling.</summary>
    [Fact]
    public async Task CompletionRefusesIncompleteUnknownLengthProjection()
    {
        using var source = new TestReadStream([1, 2, 3], seekable: false);
        using var bounded = new BoundedLocalReadStream(source, 4, TestContext.Current.CancellationToken);
        Assert.Equal(1, bounded.ReadByte());

        _ = await Assert.ThrowsAsync<LocalFileReadException>(() => bounded.CompleteAsync().AsTask());
        Assert.Equal(1, source.BytesRead);
    }

    /// <summary>Seeks are relative to the admitted start and never reach a prefix or overflowing offset.</summary>
    [Fact]
    public async Task SeeksStayInsideRemainingExtentWithCheckedOffsets()
    {
        using var source = new MemoryStream([99, 98, 1, 2, 3, 4]);
        source.Position = 2;
        using var bounded = new BoundedLocalReadStream(source, 4, TestContext.Current.CancellationToken, 4);

        Assert.Equal(0, bounded.Position);
        Assert.Equal(4, bounded.Length);
        Assert.Equal(3, bounded.Seek(-1, SeekOrigin.End));
        Assert.Equal(4, bounded.ReadByte());
        Assert.Equal(0, bounded.Seek(-4, SeekOrigin.Current));
        Assert.Equal(1, bounded.ReadByte());
        _ = Assert.Throws<IOException>(() => bounded.Position = -1);
        _ = Assert.Throws<IOException>(() => bounded.Position = 5);
        _ = Assert.Throws<IOException>(() => bounded.Seek(long.MaxValue, SeekOrigin.End));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => bounded.Seek(0, (SeekOrigin)99));
        Assert.Equal(1, bounded.Position);
        Assert.Equal(4, bounded.Seek(0, SeekOrigin.End));
        Assert.Equal(-1, bounded.ReadByte());
        await bounded.CompleteAsync();
    }

    /// <summary>Long extents and offsets do not require materializing the entire source.</summary>
    [Fact]
    public async Task ReadsSmallSliceBeyondFourGiB()
    {
        const long extent = (1L << 32) + 17;
        using var source = new LongSourceStream(extent);
        using var bounded = new BoundedLocalReadStream(source, extent, TestContext.Current.CancellationToken, extent);
        Assert.Equal(extent - 2, bounded.Seek(-2, SeekOrigin.End));
        byte[] buffer = new byte[8];

        Assert.Equal(2, await bounded.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));
        Assert.Equal([15, 16], buffer[..2]);
        Assert.Equal(extent, bounded.Position);
        await bounded.CompleteAsync();
        Assert.Equal([2, 1], source.Requests);
    }

    /// <summary>A read-only view cannot write or resize its backing source.</summary>
    [Fact]
    public void WritesAndNonSeekableSeeksAreRefused()
    {
        using var source = new TestReadStream([1], seekable: false);
        using var bounded = new BoundedLocalReadStream(source, 1, TestContext.Current.CancellationToken);

        Assert.False(bounded.CanWrite);
        _ = Assert.Throws<NotSupportedException>(() => bounded.WriteByte(2));
        _ = Assert.Throws<NotSupportedException>(() => bounded.SetLength(2));
        _ = Assert.Throws<NotSupportedException>(() => bounded.Seek(0, SeekOrigin.Begin));
        Assert.Equal(1, bounded.ReadByte());
    }

    /// <summary>Disposing the view invalidates it while keeping adapter-owned source lifetime intact.</summary>
    [Fact]
    public async Task DisposedViewRefusesUseAndLeavesSourceOpen()
    {
        using var source = new MemoryStream([1]);
        var bounded = new BoundedLocalReadStream(source, 1, TestContext.Current.CancellationToken, 1);
        await bounded.DisposeAsync();

        Assert.False(bounded.CanRead);
        Assert.False(bounded.CanSeek);
        _ = Assert.Throws<ObjectDisposedException>(() => bounded.ReadByte());
        _ = Assert.Throws<ObjectDisposedException>(() => bounded.Seek(0, SeekOrigin.Begin));
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            bounded.ReadAsync(new byte[1].AsMemory(), TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => bounded.CompleteAsync().AsTask());
        Assert.Equal(1, source.ReadByte());
    }

    /// <summary>Either operation or per-read cancellation interrupts an in-flight async read.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EitherCancellationTokenStopsBlockedRead(bool cancelOperation)
    {
        using var source = new BlockingStream();
        using var operation = new CancellationTokenSource();
        using var perRead = new CancellationTokenSource();
        using var bounded = new BoundedLocalReadStream(source, 4, operation.Token);
        Task<int> pending = bounded.ReadAsync(new byte[4].AsMemory(), perRead.Token).AsTask();
        await source.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        (cancelOperation ? operation : perRead).Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    /// <summary>Cancellation is still a refusal when a source returns bytes instead of observing the token.</summary>
    [Fact]
    public async Task CancellationAfterUncooperativeReadReturnsNoValue()
    {
        using var cancellation = new CancellationTokenSource();
        using var source = new TestReadStream([1], onRead: cancellation.Cancel);
        using var bounded = new BoundedLocalReadStream(source, 1, cancellation.Token, 1);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            bounded.ReadAsync(new byte[1].AsMemory(), TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(1, source.BytesRead);
        _ = Assert.ThrowsAny<OperationCanceledException>(() => bounded.Position);
    }

    /// <summary>A cancelled per-read token before source access leaves the admitted stream usable.</summary>
    [Fact]
    public async Task PreCancelledReadLeavesStreamUsable()
    {
        using var perRead = new CancellationTokenSource();
        perRead.Cancel();
        using var source = new TestReadStream([1, 2, 3, 4]);
        using var bounded = new BoundedLocalReadStream(source, 4, TestContext.Current.CancellationToken, 4);
        byte[] buffer = new byte[4];

        OperationCanceledException cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            bounded.ReadAsync(buffer.AsMemory(), perRead.Token).AsTask());

        Assert.Equal(perRead.Token, cancelled.CancellationToken);
        Assert.Empty(source.Requests);
        Assert.Equal(0, bounded.Position);
        Assert.Equal(4, await bounded.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));
        Assert.Equal([1, 2, 3, 4], buffer);
        await bounded.CompleteAsync();
        Assert.Equal(4, source.BytesRead);
    }

    /// <summary>Cancellation after source consumption permanently refuses retry and completion without reading a growing tail.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PerReadCancellationAfterSourceConsumptionPreventsReuse(bool seekable, bool completeFirst)
    {
        using var perRead = new CancellationTokenSource();
        using var source = new TestReadStream(
            [1, 2, 3, 4, 5, 6], reportedLength: 4, seekable: seekable, onRead: perRead.Cancel);
        using var bounded = new BoundedLocalReadStream(
            source, 4, TestContext.Current.CancellationToken, seekable ? 4 : null);
        byte[] buffer = new byte[4];

        OperationCanceledException cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            bounded.ReadAsync(buffer.AsMemory(0, 2), perRead.Token).AsTask());

        Assert.Equal(perRead.Token, cancelled.CancellationToken);
        Assert.Equal(2, source.BytesRead);
        OperationCanceledException refused = completeFirst
            ? await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bounded.CompleteAsync().AsTask())
            : await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                bounded.ReadAsync(buffer, 0, buffer.Length, TestContext.Current.CancellationToken));
        Assert.Same(cancelled, refused);
        Assert.Same(cancelled, Assert.ThrowsAny<OperationCanceledException>(() => bounded.ReadByte()));
        Assert.Same(cancelled, Assert.ThrowsAny<OperationCanceledException>(() => bounded.Position));
        Assert.Same(cancelled, await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bounded.CompleteAsync().AsTask()));
        if (seekable)
        {
            Assert.Same(cancelled, Assert.ThrowsAny<OperationCanceledException>(() => bounded.Seek(0, SeekOrigin.Begin)));
        }
        Assert.Equal(2, source.BytesRead);
        Assert.Equal([2], source.Requests);
    }

    /// <summary>Invalid admission and unreadable sources are refused before reading.</summary>
    [Fact]
    public void InvalidAdmissionIsRefused()
    {
        using var source = new MemoryStream([1]);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedLocalReadStream(source, 0, CancellationToken.None));
        _ = Assert.Throws<LocalFileTooLargeException>(() => new BoundedLocalReadStream(source, 1, CancellationToken.None, 2));
        _ = Assert.Throws<IOException>(() => new BoundedLocalReadStream(source, 1, CancellationToken.None, -1));
        source.Dispose();
        _ = Assert.Throws<IOException>(() => new BoundedLocalReadStream(source, 1, CancellationToken.None));
    }

    private sealed class TestReadStream(
        byte[] bytes,
        long? reportedLength = null,
        bool seekable = true,
        int partialBytes = int.MaxValue,
        Action? onRead = null) : MemoryStream(bytes, writable: false)
    {
        internal List<int> Requests { get; } = [];
        internal int BytesRead { get; private set; }
        public override bool CanSeek => seekable;
        public override long Length => seekable ? reportedLength ?? base.Length : throw new NotSupportedException();
        public override long Position
        {
            get => seekable ? base.Position : throw new NotSupportedException();
            set => base.Position = value;
        }

        public override int Read(Span<byte> buffer)
        {
            Requests.Add(buffer.Length);
            int read = base.Read(buffer[..Math.Min(buffer.Length, partialBytes)]);
            BytesRead += read;
            onRead?.Invoke();
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return new(Read(buffer.Span));
        }
    }

    private sealed class LongSourceStream(long length) : Stream
    {
        internal List<int> Requests { get; } = [];
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get; set; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Requests.Add(count);
            int read = (int)Math.Min(count, length - Position);
            for (int index = 0; index < read; index++)
            {
                buffer[offset + index] = (byte)((Position + index) % 256);
            }
            Position += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            return Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
        }
        public override void Flush()
        {
            throw new NotSupportedException();
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingStream : Stream
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _ = Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override void Flush()
        {
            throw new NotSupportedException();
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
