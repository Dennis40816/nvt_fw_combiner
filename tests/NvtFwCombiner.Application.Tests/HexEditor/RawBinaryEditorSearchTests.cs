using NvtFwCombiner.Application.HexEditor;

namespace NvtFwCombiner.Application.Tests.HexEditor;

/// <summary>Selection-policy contracts for bounded retained ASCII-search results.</summary>
public sealed class RawBinaryEditorSearchTests
{
    /// <summary>Reuses dense retained matches through the last authoritative retained index.</summary>
    [Fact]
    public void RetainedSelectionHandlesNegativeAndDenseBoundaryOffsets()
    {
        byte[] document = [.. Enumerable.Repeat((byte)'A', RawBinaryEditorSearch.MaximumRetainedMatches + 16)];
        RawBinaryEditorState state = State(document.Length);
        RawBinaryEditorSearchResult anchored = RawBinaryEditorSearch.Find(
            document,
            state,
            "A",
            0,
            TestContext.Current.CancellationToken);

        Assert.True(RawBinaryEditorSearch.TrySelectFromAnchoredResult(anchored, -1, out RawBinaryEditorSearchResult negative));
        Assert.Equal(0, negative.MatchIndex);
        Assert.Equal(0, negative.Address);
        Assert.False(negative.Wrapped);

        Assert.True(RawBinaryEditorSearch.TrySelectFromAnchoredResult(
            anchored,
            RawBinaryEditorSearch.MaximumRetainedMatches - 1,
            out RawBinaryEditorSearchResult lastRetained));
        Assert.Equal(RawBinaryEditorSearch.MaximumRetainedMatches - 1, lastRetained.MatchIndex);
        Assert.Equal(RawBinaryEditorSearch.MaximumRetainedMatches - 1, lastRetained.Address);
        Assert.False(lastRetained.Wrapped);

        Assert.False(RawBinaryEditorSearch.TrySelectFromAnchoredResult(
            anchored,
            RawBinaryEditorSearch.MaximumRetainedMatches,
            out _));
    }

    /// <summary>Preserves next and wrap semantics for sparse non-truncated results.</summary>
    [Fact]
    public void RetainedSelectionHandlesSparseTailAndDocumentEnd()
    {
        byte[] document = "A..A..A."u8.ToArray();
        RawBinaryEditorState state = State(document.Length);
        RawBinaryEditorSearchResult anchored = RawBinaryEditorSearch.Find(
            document,
            state,
            "A",
            0,
            TestContext.Current.CancellationToken);

        Assert.True(RawBinaryEditorSearch.TrySelectFromAnchoredResult(anchored, 2, out RawBinaryEditorSearchResult middle));
        Assert.Equal(1, middle.MatchIndex);
        Assert.Equal(3, middle.Address);
        Assert.False(middle.Wrapped);

        Assert.True(RawBinaryEditorSearch.TrySelectFromAnchoredResult(anchored, 7, out RawBinaryEditorSearchResult tailGap));
        Assert.Equal(0, tailGap.MatchIndex);
        Assert.Equal(0, tailGap.Address);
        Assert.True(tailGap.Wrapped);

        Assert.True(RawBinaryEditorSearch.TrySelectFromAnchoredResult(
            anchored,
            document.Length,
            out RawBinaryEditorSearchResult documentEnd));
        Assert.Equal(0, documentEnd.MatchIndex);
        Assert.Equal(0, documentEnd.Address);
        Assert.True(documentEnd.Wrapped);
    }

    /// <summary>Rejects noncanonical success anchors but safely reuses typed failures.</summary>
    [Fact]
    public void RetainedSelectionRequiresCanonicalSuccessAnchor()
    {
        byte[] document = "A..A"u8.ToArray();
        RawBinaryEditorState state = State(document.Length);
        RawBinaryEditorSearchResult nonanchored = RawBinaryEditorSearch.Find(
            document,
            state,
            "A",
            1,
            TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult failure = RawBinaryEditorSearch.Find(
            document,
            state,
            "Z",
            0,
            TestContext.Current.CancellationToken);

        Assert.False(RawBinaryEditorSearch.TrySelectFromAnchoredResult(nonanchored, 0, out _));
        Assert.True(RawBinaryEditorSearch.TrySelectFromAnchoredResult(failure, 3, out RawBinaryEditorSearchResult reused));
        Assert.Same(failure, reused);
        Assert.Equal(RawBinaryEditorIssueCode.AsciiTextNotFound, reused.Issue?.Code);
    }

    private static RawBinaryEditorState State(int length)
    {
        return new RawBinaryEditorState(
            HasDocument: true,
            OriginalLength: length,
            WorkingLength: length,
            UndoCount: 0,
            RedoCount: 0);
    }
}
