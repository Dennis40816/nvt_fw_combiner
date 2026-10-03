using System.Text;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests the one bounded report and report-history filesystem adapter.</summary>
public sealed class LocalFileStoreTests
{
    /// <summary>Empty and exact-limit inputs retain their complete text.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("test")]
    public async Task ReadTextAsyncAcceptsEmptyAndExactLimit(string text)
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("report.json");
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
        var store = new LocalFileStore();
        var progress = new List<LocalFileReadProgress>();

        string result = await store.ReadTextAsync(
            path,
            Math.Max(1, bytes.Length),
            TestContext.Current.CancellationToken,
            progress.Add);

        Assert.Equal(text, result);
        Assert.NotEmpty(progress);
        Assert.Equal(new LocalFileReadProgress(0, bytes.Length), progress[0]);
        Assert.Equal(new LocalFileReadProgress(bytes.Length, bytes.Length), progress[^1]);
        Assert.Equal(progress.Count, progress.Distinct().Count());
        Assert.All(progress, update => Assert.InRange(update.BytesRead, 0, update.TotalBytes));
        Assert.All(progress.Zip(progress.Skip(1)), pair => Assert.True(pair.First.BytesRead < pair.Second.BytesRead));
        var observerFailure = new IOException("observer failed");
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadTextAsync(
            path,
            Math.Max(1, bytes.Length),
            TestContext.Current.CancellationToken,
            _ => throw observerFailure).AsTask());
        Assert.Same(observerFailure, thrown.InnerException);
        Assert.IsNotType<LocalFileReadException>(thrown);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadTextAsync(
            path,
            Math.Max(1, bytes.Length),
            cancelled.Token,
            _ => throw new OperationCanceledException(cancelled.Token)).AsTask());
    }

    /// <summary>The standalone report ceiling accepts exactly ten MiB.</summary>
    [Fact]
    public async Task ReadTextAsyncAcceptsTenMiBExactly()
    {
        const int maximumBytes = 10 * 1024 * 1024;
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("maximum-report.json");
        await File.WriteAllBytesAsync(
            path,
            new byte[maximumBytes],
            TestContext.Current.CancellationToken);

        string result = await new LocalFileStore().ReadTextAsync(
            path,
            maximumBytes,
            TestContext.Current.CancellationToken);

        Assert.Equal(maximumBytes, result.Length);
    }

    /// <summary>A one-byte-over input is rejected before text projection with the typed limit failure.</summary>
    [Fact]
    public async Task ReadTextAsyncRejectsOneByteOverLimit()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("report.json", [1, 2, 3, 4, 5]);
        var store = new LocalFileStore();

        LocalFileTooLargeException exception = await Assert.ThrowsAsync<LocalFileTooLargeException>(() =>
            store.ReadTextAsync(path, 4, TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("4-byte limit", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A missing path is reported through the stable typed category.</summary>
    [Fact]
    public async Task ReadTextAsyncReportsMissingPath()
    {
        using var workspace = TempWorkspace.Create();

        _ = await Assert.ThrowsAsync<LocalFileNotFoundException>(() =>
            new LocalFileStore().ReadTextAsync(
                workspace.PathFor("missing.json"),
                1,
                TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>An unreadable admitted path is reported without leaking a partial value.</summary>
    [Fact]
    public async Task ReadTextAsyncReportsUnreadablePath()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("locked.json", [1]);
        await using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        _ = await Assert.ThrowsAsync<LocalFileReadException>(() =>
            new LocalFileStore().ReadTextAsync(path, 1, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>A storage-provider stream outside application roots uses the same bounded read.</summary>
    [Fact]
    public async Task ReadTextAsyncAcceptsArbitraryStorageProviderStream()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"source\":\"picker\"}");
        var store = new LocalFileStore();

        string result = await store.ReadTextAsync(
            _ => new ValueTask<Stream>(new NonSeekableStream(bytes)),
            bytes.Length,
            TestContext.Current.CancellationToken);

        Assert.Equal(/*lang=json,strict*/ "{\"source\":\"picker\"}", result);
    }

    /// <summary>A seekable storage-provider stream cannot grow or truncate during its bounded copy.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task ReadTextAsyncRejectsStorageProviderLengthMutation(int replacementLength)
    {
        byte[] bytes = [.. Enumerable.Repeat((byte)'A', replacementLength)];

        _ = await Assert.ThrowsAsync<LocalFileReadException>(() =>
            new LocalFileStore().ReadTextAsync(
                _ => new ValueTask<Stream>(new LengthChangingStream(bytes, admittedLength: 4)),
                8,
                TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Invalid UTF-8 is a typed text failure and cannot be projected.</summary>
    [Fact]
    public async Task ReadTextAsyncRejectsInvalidText()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("report.json", [0xC3, 0x28]);
        var store = new LocalFileStore();

        _ = await Assert.ThrowsAsync<LocalFileReadException>(() =>
            store.ReadTextAsync(path, 2, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Cancellation while the stream is blocked stops the read without returning a partial value.</summary>
    [Fact]
    public async Task ReadTextAsyncCooperativelyCancelsDuringRead()
    {
        await using var stream = new BlockingReadStream();
        var store = new LocalFileStore();
        using var cancellation = new CancellationTokenSource();
        Task<string> read = store.ReadTextAsync(
            _ => new ValueTask<Stream>(stream),
            1024,
            cancellation.Token).AsTask();
        await stream.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }

    /// <summary>Growth is detected with one probe byte rather than copying the growing tail.</summary>
    [Fact]
    public async Task ReadTextAsyncBoundsGrowingProviderToAdmittedExtentAndOneProbe()
    {
        using var source = new MeasuredReadStream("AAAABBBBBBBBBBBBBBBB"u8.ToArray(), admittedLength: 4);

        _ = await Assert.ThrowsAsync<LocalFileReadException>(() => new LocalFileStore().ReadTextAsync(
            _ => new ValueTask<Stream>(source), 1024, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(5, source.BytesRead);
        Assert.InRange(source.LargestRequest, 1, 4);
        Assert.False(source.CanRead);
    }

    /// <summary>An unknown-length provider can expose only the caller ceiling plus one refusal probe.</summary>
    [Fact]
    public async Task ReadTextAsyncBoundsNonSeekableProviderRequests()
    {
        using var source = new MeasuredReadStream("AAAABBBBBBBBBBBBBBBB"u8.ToArray(), seekable: false);

        _ = await Assert.ThrowsAsync<LocalFileTooLargeException>(() => new LocalFileStore().ReadTextAsync(
            _ => new ValueTask<Stream>(source), 4, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(5, source.BytesRead);
        Assert.InRange(source.LargestRequest, 1, 4);
        Assert.False(source.CanRead);
    }

    /// <summary>Known-length provider growth beyond the caller ceiling keeps the typed oversized category.</summary>
    [Fact]
    public async Task ReadTextAsyncKeepsTypedCeilingRefusalForGrowingSeekableProvider()
    {
        using var source = new MeasuredReadStream("AAAABB"u8.ToArray(), admittedLength: 4);

        _ = await Assert.ThrowsAsync<LocalFileTooLargeException>(() => new LocalFileStore().ReadTextAsync(
            _ => new ValueTask<Stream>(source), 4, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(5, source.BytesRead);
    }

    /// <summary>A provider already beyond the ceiling is disposed without reading it.</summary>
    [Fact]
    public async Task ReadTextAsyncRejectsKnownProviderOverLimitBeforeReading()
    {
        using var source = new MeasuredReadStream("AAAAA"u8.ToArray());

        _ = await Assert.ThrowsAsync<LocalFileTooLargeException>(() => new LocalFileStore().ReadTextAsync(
            _ => new ValueTask<Stream>(source), 4, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(0, source.BytesRead);
        Assert.False(source.CanRead);
    }

    /// <summary>Pre-cancellation prevents storage-provider opening and preserves cancellation as its own category.</summary>
    [Fact]
    public async Task ReadTextAsyncDoesNotOpenAlreadyCancelledProvider()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool opened = false;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LocalFileStore().ReadTextAsync(
            _ =>
            {
                opened = true;
                return new ValueTask<Stream>(new MemoryStream([1]));
            }, 4, cancellation.Token).AsTask());

        Assert.False(opened);
    }

    /// <summary>Partial reads and a BOM retain the encoding and the provider's admitted remaining extent.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadTextAsyncPreservesBomEncodingAcrossPartialProviderReads(bool seekable)
    {
        const string expected = "本機檔案\U0001F600";
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(expected)];
        using var source = new MeasuredReadStream(bytes, seekable: seekable, partialReadBytes: 1);

        string actual = await new LocalFileStore().ReadTextAsync(
            _ => new ValueTask<Stream>(source), bytes.Length, TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
        Assert.Equal(bytes.Length, source.BytesRead);
    }

    /// <summary>Seekable providers admit only the bytes after their initial position.</summary>
    [Fact]
    public async Task ReadTextAsyncAdmitsProviderRemainingExtent()
    {
        using var source = new MemoryStream("skiptext"u8.ToArray());
        source.Position = 4;

        Assert.Equal("text", await new LocalFileStore().ReadTextAsync(
            _ => new ValueTask<Stream>(source), 4, TestContext.Current.CancellationToken));
    }

    /// <summary>Cancellation is checked before invoking an arbitrary projector.</summary>
    [Fact]
    public async Task ReadAsyncDoesNotInvokeProjectorWhenAlreadyCancelled()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("cancelled.json", [1, 2, 3, 4]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool invoked = false;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LocalFileStore().ReadAsync(
            path, 4, (stream, token) =>
            {
                invoked = true;
                return new ValueTask<int>(stream.ReadByte());
            }, cancellation.Token).AsTask());

        Assert.False(invoked);
    }

    /// <summary>Stream projection inherits cancellation even for synchronous reads without a token.</summary>
    [Fact]
    public async Task ReadAsyncCancelsSynchronousProjectorReads()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("cancelled.json", [1, 2, 3, 4]);
        using var cancellation = new CancellationTokenSource();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LocalFileStore().ReadAsync(
            path, 4, (stream, token) =>
            {
                cancellation.Cancel();
                _ = Assert.ThrowsAny<OperationCanceledException>(() => stream.ReadByte());
                return new ValueTask<int>(0);
            }, cancellation.Token).AsTask());
    }

    /// <summary>Projectors keep read-only seek support within the admitted extent.</summary>
    [Fact]
    public async Task ReadAsyncRestrictsProjectorSeekingToAdmittedExtent()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("bounded.json", [1, 2, 3, 4]);

        int value = await new LocalFileStore().ReadAsync(path, 8, (stream, token) =>
        {
            Assert.Equal(4, stream.Length);
            Assert.True(stream.CanSeek);
            Assert.False(stream.CanWrite);
            _ = Assert.Throws<IOException>(() => stream.Seek(5, SeekOrigin.Begin));
            Assert.Equal(3, stream.Seek(-1, SeekOrigin.End));
            return new ValueTask<int>(stream.ReadByte());
        }, TestContext.Current.CancellationToken);

        Assert.Equal(4, value);
    }

    /// <summary>An admitted path cannot grow or truncate while its stable read handle is active.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task ReadAsyncDeniesInPlaceLengthMutation(int replacementLength)
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("stable.json", "AAAA"u8.ToArray());
        TaskCompletionSource opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string> read = new LocalFileStore().ReadAsync(
            path,
            8,
            async (stream, token) =>
            {
                _ = opened.TrySetResult();
                await release.Task.WaitAsync(token);
                using var reader = new StreamReader(stream, leaveOpen: true);
                return await reader.ReadToEndAsync(token);
            },
            TestContext.Current.CancellationToken).AsTask();
        await opened.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            _ = Assert.Throws<IOException>(() =>
            {
                using var writer = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
                writer.SetLength(replacementLength);
            });
        }
        finally
        {
            _ = release.TrySetResult();
        }

        Assert.Equal("AAAA", await read);
    }

    /// <summary>Atomic path replacement cannot alter the already admitted snapshot.</summary>
    [Fact]
    public async Task ReadAsyncKeepsSnapshotAcrossPathReplacement()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("stable.json", "AAAA"u8.ToArray());
        string replacement = workspace.Write("replacement.json", "BBBB"u8.ToArray());
        TaskCompletionSource opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string> read = new LocalFileStore().ReadAsync(
            path,
            4,
            async (stream, token) =>
            {
                _ = opened.TrySetResult();
                await release.Task.WaitAsync(token);
                using var reader = new StreamReader(stream, leaveOpen: true);
                return await reader.ReadToEndAsync(token);
            },
            TestContext.Current.CancellationToken).AsTask();
        await opened.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            File.Replace(replacement, path, destinationBackupFileName: null);
        }
        finally
        {
            _ = release.TrySetResult();
        }

        Assert.Equal("AAAA", await read);
        Assert.Equal("BBBB", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    /// <summary>A writer opened before admission prevents a stable snapshot from being admitted.</summary>
    [Fact]
    public async Task ReadAsyncRejectsMutationFromPreexistingWriter()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("stable.json", "AAAA"u8.ToArray());
        await using var writer = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);

        _ = await Assert.ThrowsAsync<LocalFileReadException>(() =>
            new LocalFileStore().ReadTextAsync(
                path,
                4,
                TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>A cancelled atomic replacement leaves the previous complete file unchanged.</summary>
    [Fact]
    public async Task WriteAtomicallyAsyncPreservesPreviousFileWhenCancelled()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("report-history.json", [1, 2, 3]);
        var store = new LocalFileStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.WriteAsync(path, new byte[] { 4, 5, 6 }, cancellation.Token).AsTask());

        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override bool CanSeek => false;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin loc)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class MeasuredReadStream(
        byte[] bytes,
        long? admittedLength = null,
        bool seekable = true,
        int partialReadBytes = int.MaxValue) : MemoryStream(bytes, writable: false)
    {
        internal int BytesRead { get; private set; }
        internal int LargestRequest { get; private set; }
        public override bool CanSeek => seekable;
        public override long Length => seekable ? admittedLength ?? base.Length : throw new NotSupportedException();
        public override long Position
        {
            get => seekable ? base.Position : throw new NotSupportedException();
            set => base.Position = value;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            LargestRequest = Math.Max(LargestRequest, buffer.Length);
            int read = await base.ReadAsync(buffer[..Math.Min(buffer.Length, partialReadBytes)], cancellationToken);
            BytesRead += read;
            return read;
        }
    }

    private sealed class LengthChangingStream(byte[] bytes, long admittedLength)
        : MemoryStream(bytes, writable: false)
    {
        private bool _changed;

        public override long Length => _changed ? base.Length : admittedLength;

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int read = await base.ReadAsync(buffer, cancellationToken);
            _changed = true;
            return read;
        }
    }

    private sealed class BlockingReadStream : Stream
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
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

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _ = Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

}
