using System.Security.Cryptography;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

public sealed partial class FileContentSnapshotInspectorTests
{
    /// <summary>Both modes identify every byte, including a partial final buffer, and only capture retains bytes.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(131089)]
    public async Task InspectAsyncHashesCompleteFileWithExplicitPayloadMode(int length)
    {
        using var workspace = TempWorkspace.Create();
        byte[] bytes = [.. Enumerable.Range(0, length).Select(static index => (byte)(index % 251))];
        string path = workspace.Write("boundary.bin", bytes);
        var inspector = new FileContentSnapshotInspector([workspace.Root]);
        var expected = new FileStamp(length, Convert.ToHexStringLower(SHA256.HashData(bytes)));

        SelectedFileContentInspection identity = await inspector.InspectAsync(
            path, long.MaxValue, TestContext.Current.CancellationToken,
            SelectedFileContentInspectionMode.IdentityOnly);
        SelectedFileContentInspection capture = await inspector.InspectAsync(
            path, long.MaxValue, TestContext.Current.CancellationToken,
            SelectedFileContentInspectionMode.CaptureBytes);

        Assert.Equal(expected, identity.FileStamp);
        Assert.Null(identity.AcceptedBytes);
        Assert.Equal(expected, capture.FileStamp);
        Assert.Equal(bytes, capture.AcceptedBytes!.Value.ToArray());
        Assert.Equal(identity.DisplayNameHint, capture.DisplayNameHint);
    }

    /// <summary>Partial positive reads continue until the entire admitted content has been hashed.</summary>
    [Fact]
    public async Task HashExactLengthAsyncAccumulatesPartialReadsAcrossBufferBoundary()
    {
        const int length = 65553;
        await using var stream = new GeneratedReadStream(length, maximumReadSize: 17);
        byte[] expectedBytes = new byte[length];
        Array.Fill(expectedBytes, (byte)0xA5);

        byte[] actual = await FileContentSnapshotInspector.HashExactLengthAsync(
            stream, length, TestContext.Current.CancellationToken);

        Assert.Equal(SHA256.HashData(expectedBytes), actual);
        Assert.Equal(length, stream.Position);
        Assert.Equal(length, stream.TotalBytesRead);
        Assert.True(stream.MaximumRequested <= 65536);
    }

    /// <summary>Identity inspection hashes beyond 2 and 4 GiB against Python-computed SHA-256 for 0xA5 bytes.</summary>
    [Theory]
    [InlineData(2147483665L, "f650a6f46439a9d3cf3d83254cff0321a139e94bc9dbdf280da93df545f0a196")]
    [InlineData(4294967313L, "f8378e7969a7ef05ad12276fed64adf6cbcf7e31ec74249a7b6f0b84d4caf32e")]
    public async Task ReadAndHashExactLengthAsyncUsesLongCountersWithoutPayload(long length, string expectedSha256)
    {
        await using var stream = new GeneratedReadStream(length);

        (byte[]? acceptedBytes, byte[] actual) = await FileContentSnapshotInspector.ReadAndHashExactLengthAsync(
            stream, length, SelectedFileContentInspectionMode.IdentityOnly,
            TestContext.Current.CancellationToken);

        Assert.Null(acceptedBytes);
        Assert.Equal(expectedSha256, Convert.ToHexStringLower(actual));
        Assert.Equal(length, stream.Position);
        Assert.Equal(length, stream.TotalBytesRead);
        Assert.Equal(((length + 65535) / 65536) + 1, stream.ReadCalls);
        Assert.True(stream.MaximumRequested <= 65536);
    }

    /// <summary>A capture request cannot overflow its byte-array index before any content read.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAndHashExactLengthAsyncRejectsUnrepresentableCaptureBeforeReading(bool useArrayBoundary)
    {
        long length = useArrayBoundary ? (long)Array.MaxLength + 1 : (long)int.MaxValue + 1;
        await using var stream = new GeneratedReadStream(length);

        SelectedFileSizeLimitExceededException exception =
            await Assert.ThrowsAsync<SelectedFileSizeLimitExceededException>(() =>
                FileContentSnapshotInspector.ReadAndHashExactLengthAsync(
                    stream, length, SelectedFileContentInspectionMode.CaptureBytes,
                    TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(length, exception.ObservedBytes);
        Assert.Equal(Array.MaxLength, exception.MaximumBytes);
        Assert.True(exception.IsCaptureStorageLimit);
        Assert.Equal(0, stream.ReadCalls);
    }

    /// <summary>Capture preserves and hashes every byte when the stream returns only 17 bytes per read.</summary>
    [Fact]
    public async Task ReadAndHashExactLengthAsyncCapturesPartialReads()
    {
        const int length = 53;
        await using var stream = new GeneratedReadStream(length, maximumReadSize: 17);
        byte[] expectedBytes = new byte[length];
        Array.Fill(expectedBytes, (byte)0xA5);

        (byte[]? acceptedBytes, byte[] actual) = await FileContentSnapshotInspector.ReadAndHashExactLengthAsync(
            stream, length, SelectedFileContentInspectionMode.CaptureBytes,
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedBytes, acceptedBytes);
        Assert.Equal(SHA256.HashData(expectedBytes), actual);
        Assert.Equal(length, stream.TotalBytesRead);
        Assert.Equal(5, stream.ReadCalls);
        Assert.Equal(length, stream.Position);
    }

    /// <summary>A changed position at EOF rejects both modes without publishing a stamp or captured bytes.</summary>
    [Theory]
    [InlineData(SelectedFileContentInspectionMode.IdentityOnly)]
    [InlineData(SelectedFileContentInspectionMode.CaptureBytes)]
    public async Task ReadAndHashExactLengthAsyncRejectsFinalPositionChange(SelectedFileContentInspectionMode mode)
    {
        const long length = 53;
        await using var stream = new GeneratedReadStream(length);
        stream.AfterRead = (source, read) =>
        {
            if (read == 0)
            {
                source.Position = length - 1;
            }
        };

        SelectedFileChangedDuringInspectionException exception =
            await Assert.ThrowsAsync<SelectedFileChangedDuringInspectionException>(() =>
                FileContentSnapshotInspector.ReadAndHashExactLengthAsync(
                    stream, length, mode, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(SelectedFileContentChangeKind.PositionChanged, exception.ChangeKind);
        Assert.Equal(length, stream.TotalBytesRead);
        Assert.Equal(2, stream.ReadCalls);
    }

    /// <summary>Non-seekable streams are identified without accessing unsupported length or position members.</summary>
    [Theory]
    [InlineData(SelectedFileContentInspectionMode.IdentityOnly)]
    [InlineData(SelectedFileContentInspectionMode.CaptureBytes)]
    public async Task ReadAndHashExactLengthAsyncSupportsNonSeekableStream(SelectedFileContentInspectionMode mode)
    {
        const int length = 53;
        await using var stream = new GeneratedReadStream(length, canSeek: false);
        byte[] expectedBytes = new byte[length];
        Array.Fill(expectedBytes, (byte)0xA5);

        (byte[]? acceptedBytes, byte[] actual) = await FileContentSnapshotInspector.ReadAndHashExactLengthAsync(
            stream, length, mode, TestContext.Current.CancellationToken);

        Assert.False(stream.CanSeek);
        Assert.Equal(SHA256.HashData(expectedBytes), actual);
        Assert.Equal(length, stream.TotalBytesRead);
        Assert.Equal(2, stream.ReadCalls);
        if (mode == SelectedFileContentInspectionMode.CaptureBytes)
        {
            Assert.Equal(expectedBytes, acceptedBytes);
        }
        else
        {
            Assert.Null(acceptedBytes);
        }
    }

    /// <summary>Growth after a buffer read rejects both modes with only a one-byte trailing probe.</summary>
    [Theory]
    [InlineData(SelectedFileContentInspectionMode.IdentityOnly)]
    [InlineData(SelectedFileContentInspectionMode.CaptureBytes)]
    public async Task ReadAndHashExactLengthAsyncRejectsGrowthDuringRead(SelectedFileContentInspectionMode mode)
    {
        const long length = 65553;
        await using var stream = new GeneratedReadStream(length);
        stream.AfterRead = (source, _) =>
        {
            source.ReadableLength = length + 3;
            source.ReportedLength = length + 3;
        };

        SelectedFileChangedDuringInspectionException exception =
            await Assert.ThrowsAsync<SelectedFileChangedDuringInspectionException>(() =>
                FileContentSnapshotInspector.ReadAndHashExactLengthAsync(
                    stream, length, mode, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(SelectedFileContentChangeKind.Growth, exception.ChangeKind);
        Assert.Equal(length + 1, stream.Position);
    }

    /// <summary>Truncation after the first buffer returns a typed short read and no successful result.</summary>
    [Theory]
    [InlineData(SelectedFileContentInspectionMode.IdentityOnly)]
    [InlineData(SelectedFileContentInspectionMode.CaptureBytes)]
    public async Task ReadAndHashExactLengthAsyncRejectsShortReadDuringRead(SelectedFileContentInspectionMode mode)
    {
        const long length = 65553;
        await using var stream = new GeneratedReadStream(length);
        stream.AfterRead = (source, _) =>
        {
            source.ReadableLength = 65536;
            source.ReportedLength = 65536;
        };

        SelectedFileChangedDuringInspectionException exception =
            await Assert.ThrowsAsync<SelectedFileChangedDuringInspectionException>(() =>
                FileContentSnapshotInspector.ReadAndHashExactLengthAsync(
                    stream, length, mode, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(SelectedFileContentChangeKind.ShortRead, exception.ChangeKind);
        Assert.Equal(65536, stream.Position);
    }

    /// <summary>Length changes at EOF remain observable even when the trailing probe finds no bytes.</summary>
    [Theory]
    [InlineData(-1, SelectedFileContentChangeKind.Shrinkage)]
    [InlineData(1, SelectedFileContentChangeKind.Growth)]
    public async Task HashExactLengthAsyncRejectsFinalLengthChange(int delta, SelectedFileContentChangeKind kind)
    {
        const long length = 65553;
        await using var stream = new GeneratedReadStream(length);
        stream.AfterRead = (source, read) =>
        {
            if (read == 0)
            {
                source.ReportedLength = length + delta;
            }
        };

        SelectedFileChangedDuringInspectionException exception =
            await Assert.ThrowsAsync<SelectedFileChangedDuringInspectionException>(() =>
                FileContentSnapshotInspector.HashExactLengthAsync(
                    stream, length, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(kind, exception.ChangeKind);
    }

    /// <summary>Cancellation during a successful read is propagated before further reads or publication.</summary>
    [Theory]
    [InlineData(SelectedFileContentInspectionMode.IdentityOnly)]
    [InlineData(SelectedFileContentInspectionMode.CaptureBytes)]
    public async Task ReadAndHashExactLengthAsyncPropagatesCancellationMidRead(SelectedFileContentInspectionMode mode)
    {
        using var cancellation = new CancellationTokenSource();
        await using var stream = new GeneratedReadStream(65553);
        stream.AfterRead = (_, _) => cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FileContentSnapshotInspector.ReadAndHashExactLengthAsync(
                stream, stream.Length, mode, cancellation.Token).AsTask());

        Assert.Equal(1, stream.ReadCalls);
        Assert.Equal(65536, stream.Position);
    }

    private sealed class GeneratedReadStream(
        long length,
        int maximumReadSize = int.MaxValue,
        bool canSeek = true) : Stream
    {
        private long _position;

        internal long ReadableLength { get; set; } = length;
        internal long ReportedLength { get; set; } = length;
        internal long TotalBytesRead { get; private set; }
        internal long ReadCalls { get; private set; }
        internal int MaximumRequested { get; private set; }
        internal Action<GeneratedReadStream, int>? AfterRead { get; set; }

        public override bool CanRead => true;
        public override bool CanSeek => canSeek;
        public override bool CanWrite => false;
        public override long Length => canSeek ? ReportedLength : throw new NotSupportedException();
        public override long Position
        {
            get => canSeek ? _position : throw new NotSupportedException();
            set
            {
                if (!canSeek)
                {
                    throw new NotSupportedException();
                }
                _position = value;
            }
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            MaximumRequested = Math.Max(MaximumRequested, buffer.Length);
            int read = (int)Math.Min(Math.Min(buffer.Length, maximumReadSize), ReadableLength - _position);
            buffer[..read].Fill(0xA5);
            _position = checked(_position + read);
            TotalBytesRead = checked(TotalBytesRead + read);
            AfterRead?.Invoke(this, read);
            return read;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
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
