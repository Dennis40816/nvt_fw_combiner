using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests content-authoritative host-file inspection and hashing.</summary>
public sealed class FileContentSnapshotInspectorTests
{
    /// <summary>Inspection computes accepted length and SHA-256 from one selected file.</summary>
    [Fact]
    public async Task InspectAsyncReturnsContentAuthoritativeStampAndNonAuthoritativeHints()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("input.bin", [1, 2, 3, 4]);
        var inspector = new FileContentSnapshotInspector([workspace.Root]);

        SelectedFileContentInspection result = await inspector.InspectAsync(
            path,
            maximumBytes: int.MaxValue,
            CancellationToken.None);

        Assert.Equal(FileStamp.FromBytes([1, 2, 3, 4]), result.FileStamp);
        Assert.Equal("input.bin", result.DisplayNameHint);
        Assert.Null(result.LastWriteTimeUtcHint);
    }

    /// <summary>Inspection rejects a file above the caller-resolved ceiling before hashing it.</summary>
    [Fact]
    public async Task InspectAsyncRejectsFileAboveResolvedMaximum()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("oversized.bin", [1, 2, 3, 4]);
        var inspector = new FileContentSnapshotInspector([workspace.Root]);

        SelectedFileSizeLimitExceededException exception =
            await Assert.ThrowsAsync<SelectedFileSizeLimitExceededException>(() =>
                inspector.InspectAsync(
                    path,
                    maximumBytes: 3,
                    TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(4, exception.ObservedBytes);
        Assert.Equal(3, exception.MaximumBytes);
    }

    /// <summary>Concurrent growth is rejected after reading only one byte beyond the admitted length.</summary>
    [Fact]
    public async Task HashExactLengthAsyncRejectsGrowthWithOneByteProbe()
    {
        await using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]);

        _ = await Assert.ThrowsAsync<IOException>(() =>
            FileContentSnapshotInspector.HashExactLengthAsync(
                stream,
                observedLength: 4,
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(5, stream.Position);
    }

    /// <summary>Same-size file mutation is visible even when host length does not change.</summary>
    [Fact]
    public async Task InspectAsyncDetectsSameSizeMutation()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("input.bin", [1, 2, 3, 4]);
        var inspector = new FileContentSnapshotInspector([workspace.Root]);
        SelectedFileContentInspection first = await inspector.InspectAsync(
            path,
            maximumBytes: int.MaxValue,
            CancellationToken.None);
        await File.WriteAllBytesAsync(
            path,
            [1, 2, 9, 4],
            TestContext.Current.CancellationToken);

        SelectedFileContentInspection second = await inspector.InspectAsync(
            path,
            maximumBytes: int.MaxValue,
            CancellationToken.None);

        Assert.Equal(first.FileStamp.AcceptedLength, second.FileStamp.AcceptedLength);
        Assert.NotEqual(first.FileStamp, second.FileStamp);
    }

    /// <summary>Inspection rejects a selected path outside its configured roots.</summary>
    [Fact]
    public async Task InspectAsyncRejectsPathOutsideAllowedRoot()
    {
        using var workspace = TempWorkspace.Create();
        string allowed = Path.Combine(workspace.Root, "allowed");
        string outside = Path.Combine(workspace.Root, "outside");
        _ = Directory.CreateDirectory(allowed);
        _ = Directory.CreateDirectory(outside);
        string path = Path.Combine(outside, "input.bin");
        await File.WriteAllBytesAsync(
            path,
            [1],
            TestContext.Current.CancellationToken);
        var inspector = new FileContentSnapshotInspector([allowed]);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            inspector.InspectAsync(
                path,
                maximumBytes: int.MaxValue,
                TestContext.Current.CancellationToken).AsTask());
    }
}
