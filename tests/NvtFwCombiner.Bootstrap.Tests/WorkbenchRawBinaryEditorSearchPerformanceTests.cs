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
        var session = new WorkbenchRawBinaryEditorSession();
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
        Assert.Equal(1, session.AsciiSearchSnapshotCaptureCount);
        Assert.True(firstInvocationAllocation >= LargeDocumentLength);
        Assert.True(
            repeatedInvocationAllocation < LargeDocumentLength / 4,
            $"Repeated search synchronously allocated {repeatedInvocationAllocation} bytes before dispatch.");
    }

    /// <summary>Verifies a successful edit invalidates the cached search snapshot.</summary>
    [Fact]
    public async Task SuccessfulEditInvalidatesSearchSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-invalidation");
        string sourcePath = workspace.Write("source.bin", Encoding.ASCII.GetBytes("AAAA"));
        var session = new WorkbenchRawBinaryEditorSession();
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);
        RawBinaryEditorSearchResult before = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);

        RawBinaryEditorOperationResult edit = session.OverwriteByte("0x0", "42");
        RawBinaryEditorSearchResult after = await session.FindAsciiAsync(
            "B",
            0,
            TestContext.Current.CancellationToken);

        Assert.True(before.Succeeded);
        Assert.True(edit.Succeeded);
        Assert.True(after.Succeeded);
        Assert.Equal(0, after.Address);
        Assert.Equal(2, session.AsciiSearchSnapshotCaptureCount);
    }

    /// <summary>Verifies every edit attempt invalidates before its outcome can publish across revisions.</summary>
    [Fact]
    public async Task FailedEditAttemptAlsoInvalidatesSearchSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-failed-edit");
        string sourcePath = workspace.Write("source.bin", Encoding.ASCII.GetBytes("AAAA"));
        var session = new WorkbenchRawBinaryEditorSession();
        WorkbenchRawBinaryEditorFileResult load = await session.LoadAsync(
            sourcePath,
            TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, load.ErrorMessage);
        RawBinaryEditorSearchResult before = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);

        RawBinaryEditorOperationResult invalidEdit = session.OverwriteByte("not-an-address", "42");
        RawBinaryEditorSearchResult after = await session.FindAsciiAsync(
            "A",
            0,
            TestContext.Current.CancellationToken);

        Assert.True(before.Succeeded);
        Assert.False(invalidEdit.Succeeded);
        Assert.True(after.Succeeded);
        Assert.Equal(2, session.AsciiSearchSnapshotCaptureCount);
    }

    /// <summary>Verifies cancellation is observed before a full-document search snapshot is captured.</summary>
    [Fact]
    public async Task CanceledSearchDoesNotCaptureDocumentSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-hex-search-canceled");
        string sourcePath = workspace.Write("source.bin", [.. Enumerable.Repeat((byte)'A', LargeDocumentLength)]);
        var session = new WorkbenchRawBinaryEditorSession();
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
        var session = new WorkbenchRawBinaryEditorSession();
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
        var session = new WorkbenchRawBinaryEditorSession((snapshot, state, text, startOffset, cancellationToken) =>
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
        RawBinaryEditorOperationResult edit = session.OverwriteByte("0x0", "42");
        releaseSearch.Set();

        Assert.True(edit.Succeeded);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await search);
    }
}
