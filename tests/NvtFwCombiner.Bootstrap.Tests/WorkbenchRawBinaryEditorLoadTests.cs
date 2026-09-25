using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Out-of-order file reads cannot replace the latest requested Hex document.</summary>
public sealed class WorkbenchRawBinaryEditorLoadTests
{
    /// <summary>A delayed earlier read cannot overwrite a newer accepted document.</summary>
    [Fact]
    public async Task EarlierReadCompletingLastPreservesLatestDocument()
    {
        using var workspace = TempWorkspace.Create("hex-load-order");
        string firstPath = workspace.Write("first.bin", [1]);
        string secondPath = workspace.Write("second.bin", [2, 3]);
        var delayed = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var editor = new RawBinaryEditorSession();
        var files = new RawBinaryEditorFileSession(editor, (bytes, state, text, start, token) => RawBinaryEditorSearch.Find(bytes, state, text, start, token),
            (path, _) => path == firstPath ? new(delayed.Task) : ValueTask.FromResult<ReadOnlyMemory<byte>>(new byte[] { 2, 3 }));
        Task<RawBinaryEditorFileResult> first = files.LoadAsync(firstPath, TestContext.Current.CancellationToken);
        RawBinaryEditorFileResult second = await files.LoadAsync(secondPath, TestContext.Current.CancellationToken);
        delayed.SetResult(new byte[] { 1 });
        RawBinaryEditorFileResult obsolete = await first;

        Assert.True(second.Succeeded);
        Assert.False(obsolete.Succeeded);
        Assert.Equal(secondPath, files.SourcePath);
        Assert.True(editor.TryCopyWorkingBytes(out byte[]? bytes));
        Assert.Equal(new byte[] { 2, 3 }, bytes);
        Assert.Equal(second.State, editor.State);
    }

    /// <summary>A canceled read must not commit bytes even if its reader completes successfully.</summary>
    [Fact]
    public async Task CancellationBeforeReadCompletionPreservesAcceptedDocument()
    {
        using var workspace = TempWorkspace.Create("hex-load-cancel");
        string original = workspace.Write("original.bin", [7]);
        string pending = workspace.Write("pending.bin", [8]);
        var delayed = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var editor = new RawBinaryEditorSession();
        var files = new RawBinaryEditorFileSession(editor, (bytes, state, text, start, token) => RawBinaryEditorSearch.Find(bytes, state, text, start, token),
            (path, _) => path == pending ? new(delayed.Task) : ValueTask.FromResult<ReadOnlyMemory<byte>>(new byte[] { 7 }));
        RawBinaryEditorFileResult accepted = await files.LoadAsync(original, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        Task<RawBinaryEditorFileResult> load = files.LoadAsync(pending, cancellation.Token);
        await cancellation.CancelAsync();
        delayed.SetResult(new byte[] { 8 });
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);

        Assert.Equal(original, files.SourcePath);
        Assert.Equal(accepted.State, editor.State);
        Assert.True(editor.TryCopyWorkingBytes(out byte[]? bytes));
        Assert.Equal(new byte[] { 7 }, bytes);
    }

    /// <summary>A failed latest selection also retires previous pending reads without losing accepted bytes.</summary>
    [Fact]
    public async Task FailedLatestSelectionKeepsAcceptedDocumentAndRejectsOlderRead()
    {
        using var workspace = TempWorkspace.Create("hex-load-failure");
        string original = workspace.Write("original.bin", [7]);
        string pending = workspace.Write("pending.bin", [8]);
        var delayed = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var editor = new RawBinaryEditorSession();
        var files = new RawBinaryEditorFileSession(editor,
            (bytes, state, text, start, token) => RawBinaryEditorSearch.Find(bytes, state, text, start, token),
            (path, _) => path == pending ? new(delayed.Task) : ValueTask.FromResult<ReadOnlyMemory<byte>>(new byte[] { 7 }));
        RawBinaryEditorFileResult accepted = await files.LoadAsync(original, TestContext.Current.CancellationToken);
        Task<RawBinaryEditorFileResult> earlier = files.LoadAsync(pending, TestContext.Current.CancellationToken);
        RawBinaryEditorFileResult failed = await files.LoadAsync(string.Empty, TestContext.Current.CancellationToken);
        delayed.SetResult(new byte[] { 8 });
        RawBinaryEditorFileResult obsolete = await earlier;

        Assert.False(failed.Succeeded);
        Assert.False(obsolete.Succeeded);
        Assert.Equal(original, files.SourcePath);
        Assert.Equal(accepted.State, editor.State);
        Assert.True(editor.TryCopyWorkingBytes(out byte[]? bytes));
        Assert.Equal(new byte[] { 7 }, bytes);
    }
}
