using System.Text;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Performance and ownership contracts for background raw-BIN search.</summary>
public sealed class WorkbenchRawBinaryEditorSearchPerformanceTests
{
    private const int LargeDocumentLength = 1024 * 1024;

    /// <summary>Verifies repeated background search does not recopy an unchanged full document.</summary>
    [Fact]
    public async Task RepeatedSearchReusesOneImmutableDocumentSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-snapshot");
        string sourcePath = workspace.Write("source.bin", [.. Enumerable.Repeat((byte)'A', LargeDocumentLength)]);
        int searchCallCount = 0;
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor, (snapshot, state, text, startOffset, cancellationToken) =>
        {
            _ = Interlocked.Increment(ref searchCallCount);
            return RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken);
        });
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);

        long firstAllocationStart = GC.GetAllocatedBytesForCurrentThread();
        Task<RawBinaryEditorSearchResult> firstTask = session.FindAsciiAsync(
            "Z",
            0,
            TestContext.Current.CancellationToken);
        long firstInvocationAllocation = GC.GetAllocatedBytesForCurrentThread() - firstAllocationStart;
        RawBinaryEditorSearchResult first = await firstTask;

        long repeatedAllocationStart = GC.GetAllocatedBytesForCurrentThread();
        Task<RawBinaryEditorSearchResult> repeatedTask = session.FindAsciiAsync(
            "Z",
            0,
            TestContext.Current.CancellationToken);
        long repeatedInvocationAllocation = GC.GetAllocatedBytesForCurrentThread() - repeatedAllocationStart;
        RawBinaryEditorSearchResult repeated = await repeatedTask;

        Assert.Equal(RawBinaryEditorIssueCode.AsciiTextNotFound, first.Issue?.Code);
        Assert.Equal(RawBinaryEditorIssueCode.AsciiTextNotFound, repeated.Issue?.Code);
        Assert.Equal(1, Volatile.Read(ref searchCallCount));
        Assert.Equal(1, session.AsciiSearchSnapshotCaptureCount);
        Assert.True(firstInvocationAllocation >= LargeDocumentLength);
        Assert.True(
            repeatedInvocationAllocation < LargeDocumentLength / 4,
            $"Repeated search synchronously allocated {repeatedInvocationAllocation} bytes before dispatch.");
    }

    /// <summary>Verifies Search/Next reuses the retained index instead of rescanning an unchanged document.</summary>
    [Fact]
    public async Task RepeatedSearchWithinRetainedIndexReusesCompletedResult()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-result");
        string sourcePath = workspace.Write("source.bin", [.. Enumerable.Repeat((byte)'A', LargeDocumentLength)]);
        int searchCallCount = 0;
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor, (snapshot, state, text, startOffset, cancellationToken) =>
        {
            _ = Interlocked.Increment(ref searchCallCount);
            return RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken);
        });
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);

        RawBinaryEditorSearchResult first = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult next = await session.FindAsciiAsync(
            "A",
            1,
            TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult wrapped = await session.FindAsciiAsync(
            "A",
            LargeDocumentLength,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref searchCallCount));
        Assert.Same(first.Matches, next.Matches);
        Assert.Same(first.Matches, wrapped.Matches);
        Assert.Equal(1, next.MatchIndex);
        Assert.Equal(1, next.Address);
        Assert.False(next.Wrapped);
        Assert.Equal(0, wrapped.MatchIndex);
        Assert.Equal(0, wrapped.Address);
        Assert.True(wrapped.Wrapped);
        Assert.Equal(LargeDocumentLength, next.TotalMatchCount);
        Assert.True(next.IsTruncated);
    }

    /// <summary>Uses the exact ordinal query as part of the completed-result cache identity.</summary>
    [Fact]
    public async Task DifferentQueryRunsAnAuthoritativeSearch()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-query-key");
        string sourcePath = workspace.Write("source.bin", "ABAB"u8.ToArray());
        int searchCallCount = 0;
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor, (snapshot, state, text, startOffset, cancellationToken) =>
        {
            _ = Interlocked.Increment(ref searchCallCount);
            return RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken);
        });
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);

        RawBinaryEditorSearchResult first = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult different = await session.FindAsciiAsync(
            "B",
            0,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, Volatile.Read(ref searchCallCount));
        Assert.Equal(0, first.Address);
        Assert.Equal(1, different.Address);
    }

    /// <summary>Applies the same retained-result reuse to repeated adapter-owned background searches.</summary>
    [Fact]
    public async Task RepeatedBackgroundSearchReusesCompletedResult()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-sync-result");
        string sourcePath = workspace.Write("source.bin", "AAAA"u8.ToArray());
        int searchCallCount = 0;
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor, (snapshot, state, text, startOffset, cancellationToken) =>
        {
            _ = Interlocked.Increment(ref searchCallCount);
            return RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken);
        });
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);

        RawBinaryEditorSearchResult first = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult next = await session.FindAsciiAsync(
            "A",
            1,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref searchCallCount));
        Assert.Equal(0, first.Address);
        Assert.Equal(1, next.Address);
        Assert.Same(first.Matches, next.Matches);
    }

    /// <summary>Prevents callers from mutating the retained address index shared by later searches.</summary>
    [Fact]
    public async Task CompletedResultCacheExposesAnImmutableMatchIndex()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-immutable-result");
        string sourcePath = workspace.Write("source.bin", "AAAA"u8.ToArray());
        int searchCallCount = 0;
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor, (snapshot, state, text, startOffset, cancellationToken) =>
        {
            _ = Interlocked.Increment(ref searchCallCount);
            return RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken);
        });
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);

        RawBinaryEditorSearchResult first = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);
        var exposed = first.Matches as IList<long>;
        Assert.NotNull(exposed);

        _ = Assert.Throws<NotSupportedException>(() => exposed[0] = 99);
        RawBinaryEditorSearchResult next = await session.FindAsciiAsync(
            "A",
            1,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref searchCallCount));
        Assert.Equal(0, first.Matches[0]);
        Assert.Equal(1, next.Address);
        Assert.Same(first.Matches, next.Matches);
    }

    /// <summary>Verifies a start beyond the bounded highlight index keeps the authoritative full-search fallback.</summary>
    [Fact]
    public async Task SearchBeyondRetainedIndexFallsBackToAuthoritativeScan()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-retained-boundary");
        int documentLength = RawBinaryEditorSearch.MaximumRetainedMatches + 16;
        string sourcePath = workspace.Write("source.bin", [.. Enumerable.Repeat((byte)'A', documentLength)]);
        int searchCallCount = 0;
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor, (snapshot, state, text, startOffset, cancellationToken) =>
        {
            _ = Interlocked.Increment(ref searchCallCount);
            return RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken);
        });
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);

        RawBinaryEditorSearchResult first = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult beyondRetained = await session.FindAsciiAsync(
            "A",
            RawBinaryEditorSearch.MaximumRetainedMatches,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, Volatile.Read(ref searchCallCount));
        Assert.Equal(0, first.MatchIndex);
        Assert.Equal(RawBinaryEditorSearch.MaximumRetainedMatches, beyondRetained.MatchIndex);
        Assert.Equal(RawBinaryEditorSearch.MaximumRetainedMatches, beyondRetained.Address);
        Assert.False(beyondRetained.Wrapped);
        Assert.Equal(documentLength, beyondRetained.TotalMatchCount);
    }

    /// <summary>Verifies a successful edit invalidates the cached search snapshot.</summary>
    [Fact]
    public async Task SuccessfulEditInvalidatesSearchSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-invalidation");
        string sourcePath = workspace.Write("source.bin", Encoding.ASCII.GetBytes("AAAA"));
        int searchCallCount = 0;
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor, (snapshot, state, text, startOffset, cancellationToken) =>
        {
            _ = Interlocked.Increment(ref searchCallCount);
            return RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken);
        });
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);
        RawBinaryEditorSearchResult before = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult cachedNext = await session.FindAsciiAsync(
            "A",
            1,
            TestContext.Current.CancellationToken);

        RawBinaryEditorOperationResult edit = editor.OverwriteByte("0x0", "42");
        RawBinaryEditorSearchResult after = await session.FindAsciiAsync(
            "B",
            0,
            TestContext.Current.CancellationToken);

        Assert.True(before.Succeeded);
        Assert.Equal(1, cachedNext.Address);
        Assert.True(edit.Succeeded);
        Assert.True(after.Succeeded);
        Assert.Equal(0, after.Address);
        Assert.Equal(2, Volatile.Read(ref searchCallCount));
        Assert.Equal(2, session.AsciiSearchSnapshotCaptureCount);
    }

    /// <summary>A rejected Application edit leaves the unchanged adapter search snapshot reusable.</summary>
    [Fact]
    public async Task FailedEditAttemptKeepsSearchSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-failed-edit");
        string sourcePath = workspace.Write("source.bin", Encoding.ASCII.GetBytes("AAAA"));
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor);
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);
        RawBinaryEditorSearchResult before = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);

        RawBinaryEditorOperationResult invalidEdit = editor.OverwriteByte("not-an-address", "42");
        RawBinaryEditorSearchResult after = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);

        Assert.True(before.Succeeded);
        Assert.False(invalidEdit.Succeeded);
        Assert.True(after.Succeeded);
        Assert.Equal(1, session.AsciiSearchSnapshotCaptureCount);
    }

    /// <summary>Verifies cancellation is observed before a full-document search snapshot is captured.</summary>
    [Fact]
    public async Task CanceledSearchDoesNotCaptureDocumentSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-canceled");
        string sourcePath = workspace.Write("source.bin", [.. Enumerable.Repeat((byte)'A', LargeDocumentLength)]);
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor);
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        Task<RawBinaryEditorSearchResult> canceledTask = session.FindAsciiAsync("Z", 0, cancellation.Token);
        long invocationAllocation = GC.GetAllocatedBytesForCurrentThread() - allocationStart;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await canceledTask);
        Assert.Equal(0, session.AsciiSearchSnapshotCaptureCount);
        Assert.True(
            invocationAllocation < LargeDocumentLength / 4,
            $"Canceled search synchronously allocated {invocationAllocation} bytes before failing.");
    }

    /// <summary>Verifies cancellation does not replace the existing typed no-document result.</summary>
    [Fact]
    public async Task CanceledSearchWithoutDocumentKeepsTypedIssue()
    {
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        RawBinaryEditorSearchResult result = await session.FindAsciiAsync("Z", 0, cancellation.Token);

        Assert.Equal(RawBinaryEditorIssueCode.NoDocument, result.Issue?.Code);
        Assert.Equal(0, session.AsciiSearchSnapshotCaptureCount);
    }

    /// <summary>Verifies a later edit cancels a background result from the preceding document revision.</summary>
    [Fact]
    public async Task LaterEditCancelsRunningSearchSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-stale-result");
        string sourcePath = workspace.Write("source.bin", Encoding.ASCII.GetBytes("AAAA"));
        using var searchStarted = new ManualResetEventSlim();
        using var releaseSearch = new ManualResetEventSlim();
        var editor = new RawBinaryEditorSession();
        var session = new WorkbenchRawBinaryEditorSession(editor, (snapshot, state, text, startOffset, cancellationToken) =>
        {
            searchStarted.Set();
            releaseSearch.Wait(cancellationToken);
            return RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken);
        });
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);

        Task<RawBinaryEditorSearchResult> search = session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);
        Assert.True(
            searchStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            "Background search did not start.");
        RawBinaryEditorOperationResult edit = editor.OverwriteByte("0x0", "42");
        releaseSearch.Set();

        Assert.True(edit.Succeeded);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await search);
    }
}
