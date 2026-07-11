using NvtFwCombiner.Application.HexEditor;

namespace NvtFwCombiner.Application.Tests.HexEditor;

/// <summary>Behavioral coverage for the profile-independent, memory-only raw BIN editor.</summary>
public sealed class RawBinaryEditorSessionTests
{
    /// <summary>Locks direct byte edits, length-changing commands, and undo/redo to the owned work buffer.</summary>
    [Fact]
    public void EditsRemainInMemoryAndUndoRedoPreserveInsertDeleteSemantics()
    {
        byte[] source = [0x10, 0x20, 0x30, 0x40];
        var session = new RawBinaryEditorSession();
        _ = session.Load(source);

        Assert.True(session.OverwriteByte("0x0", "A5").Succeeded);
        Assert.True(session.InsertZeroAfter("0x1").Succeeded);
        Assert.True(session.DeleteByte("0x3").Succeeded);
        Assert.True(session.FillRange("0x1", "0x2", "FF").Succeeded);

        Assert.True(session.TryCopyWorkingBytes(out byte[]? changed));
        Assert.Equal([0xA5, 0xFF, 0xFF, 0x40], changed);
        Assert.Equal([0x10, 0x20, 0x30, 0x40], source);

        Assert.True(session.Undo().Succeeded);
        Assert.True(session.TryCopyWorkingBytes(out byte[]? afterUndo));
        Assert.Equal([0xA5, 0x20, 0x00, 0x40], afterUndo);

        Assert.True(session.Redo().Succeeded);
        Assert.True(session.TryCopyWorkingBytes(out byte[]? afterRedo));
        Assert.Equal(changed, afterRedo);
    }

    /// <summary>Writes a supplied sequence from Start and leaves unused selected bytes unchanged.</summary>
    [Fact]
    public void OverwriteRangeAllowsSequenceShorterThanTheInclusiveSelectedRange()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0x20, 0x30]);

        RawBinaryEditorOperationResult result = session.OverwriteRange("0x0", "0x1", "A5");

        Assert.True(result.Succeeded);
        Assert.True(session.TryCopyWorkingBytes(out byte[]? bytes));
        Assert.Equal([0xA5, 0x20, 0x30], bytes);
    }

    /// <summary>Derives dirty state from byte identity instead of retained undo history.</summary>
    [Fact]
    public void UnsavedStateReflectsTheCurrentMemoryDocument()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0x20]);

        RawBinaryEditorOperationResult noOp = session.OverwriteByte("0x0", "10");
        Assert.False(noOp.State.HasUnsavedChanges);
        Assert.Equal(0, noOp.State.UndoCount);

        Assert.True(session.OverwriteByte("0x0", "A5").State.HasUnsavedChanges);
        RawBinaryEditorOperationResult restoredByEdit = session.OverwriteByte("0x0", "10");
        Assert.False(restoredByEdit.State.HasUnsavedChanges);
        Assert.Equal(2, restoredByEdit.State.UndoCount);

        Assert.True(session.Undo().State.HasUnsavedChanges);
        Assert.False(session.Undo().State.HasUnsavedChanges);

        Assert.True(session.InsertZeroBefore("0x1").State.HasUnsavedChanges);
        RawBinaryEditorOperationResult restoredByDelete = session.DeleteByte("0x1");
        Assert.False(restoredByDelete.State.HasUnsavedChanges);
    }

    /// <summary>Rejects an overwrite sequence that would continue past the selected inclusive end.</summary>
    [Fact]
    public void OverwriteRangeRejectsSequenceThatWouldCrossTheInclusiveEnd()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0x20, 0x30]);

        RawBinaryEditorOperationResult result = session.OverwriteRange("0x0", "0x1", "A5 B6 CC");

        Assert.False(result.Succeeded);
        Assert.Equal(RawBinaryEditorIssueCode.InputExceedsRange, result.Issue?.Code);
        Assert.True(session.TryCopyWorkingBytes(out byte[]? bytes));
        Assert.Equal([0x10, 0x20, 0x30], bytes);
    }

    /// <summary>Separates retained source identity from the opened source value at the same display address.</summary>
    [Fact]
    public void ViewportShowsOriginalAndWorkingValuesAtDisplayedOffsets()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x11, 0x22, 0x33]);
        Assert.True(session.InsertZeroBefore("0x1").Succeeded);

        RawBinaryEditorViewport viewport = session.CreateViewport("0x0");
        RawBinaryEditorViewportRow row = Assert.Single(viewport.Rows);

        Assert.Equal((byte)0x11, row.Bytes[0].CurrentValue);
        Assert.Equal((byte)0x11, row.Bytes[0].OriginalValue);
        Assert.Equal((byte)0x00, row.Bytes[1].CurrentValue);
        Assert.False(row.Bytes[1].HasOriginalValue);
        Assert.False(row.Bytes[1].IsDataChanged);
        Assert.True(row.Bytes[1].IsChanged);
        Assert.Equal((byte)0x22, row.Bytes[2].CurrentValue);
        Assert.Equal(1, row.Bytes[2].OriginalAddress);
        Assert.Equal((byte)0x33, row.Bytes[2].OriginalValueAtAddress);
        Assert.False(row.Bytes[2].IsDataChanged);
        Assert.True(row.Bytes[2].IsStructuralChanged);
        Assert.True(row.Bytes[2].IsChanged);
        Assert.Equal((byte)0x33, row.Bytes[3].CurrentValue);
        Assert.Equal(2, row.Bytes[3].OriginalAddress);
        Assert.Null(row.Bytes[3].OriginalValueAtAddress);
        Assert.False(row.Bytes[3].HasOriginalValueAtAddress);
        Assert.True(row.HasChanges);
    }

    /// <summary>Keeps one structural block after deletion even when shifted byte values happen to match by address.</summary>
    [Fact]
    public void DeleteSeparatesStructuralShiftFromSameAddressDataDiff()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0xAA, 0xAA, 0xAA]);

        Assert.True(session.DeleteByte("0x1").Succeeded);

        RawBinaryEditorViewport viewport = session.CreatePage(0, 1);
        Assert.False(viewport.Rows[0].Bytes[1].IsDataChanged);
        Assert.True(viewport.Rows[0].Bytes[1].IsStructuralChanged);
        Assert.False(viewport.Rows[0].Bytes[2].IsDataChanged);
        Assert.True(viewport.Rows[0].Bytes[2].IsStructuralChanged);

        RawBinaryEditorChangedRange range = Assert.Single(session.GetChangedRanges());
        Assert.Equal(1, range.Start);
        Assert.Equal(4, range.EndExclusive);
        Assert.Equal(RawBinaryEditorChangeKind.Structural, range.ChangeKind);
    }

    /// <summary>Keeps equal shifted bytes in an insert tail without falsely reporting each value as modified.</summary>
    [Fact]
    public void InsertAggregatesStructuralTailIndependentlyFromDataEquality()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0xAA, 0xAA, 0xAA]);

        Assert.True(session.InsertZeroBefore("0x1").Succeeded);

        RawBinaryEditorViewport viewport = session.CreatePage(0, 1);
        Assert.False(viewport.Rows[0].Bytes[1].IsDataChanged);
        Assert.True(viewport.Rows[0].Bytes[1].IsStructuralChanged);
        Assert.False(viewport.Rows[0].Bytes[2].IsDataChanged);
        Assert.True(viewport.Rows[0].Bytes[2].IsStructuralChanged);

        RawBinaryEditorChangedRange range = Assert.Single(session.GetChangedRanges());
        Assert.Equal(1, range.Start);
        Assert.Equal(4, range.EndExclusive);
        Assert.Equal(RawBinaryEditorChangeKind.Structural, range.ChangeKind);
        Assert.Empty(range.ValueChanges);
        RawBinaryEditorStructuralChange insertion = Assert.Single(range.StructuralChanges);
        Assert.Equal(RawBinaryEditorStructuralChangeKind.Insert, insertion.Kind);
        Assert.Equal(1, insertion.Address);
        Assert.Equal(1, insertion.Count);
    }

    /// <summary>Keeps an adjacent deletion attached to the structural block that caused it.</summary>
    [Fact]
    public void SameLengthInsertDeleteReportsBothStructuralCauses()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0x20, 0x30, 0x40]);

        Assert.True(session.InsertZeroBefore("0x1").Succeeded);
        Assert.True(session.DeleteByte("0x2").Succeeded);

        RawBinaryEditorChangedRange range = Assert.Single(session.GetChangedRanges());
        Assert.Equal(1, range.Start);
        Assert.Equal(2, range.EndExclusive);
        Assert.Collection(
            range.StructuralChanges,
            insertion =>
            {
                Assert.Equal(RawBinaryEditorStructuralChangeKind.Insert, insertion.Kind);
                Assert.Equal(1, insertion.Address);
            },
            deletion =>
            {
                Assert.Equal(RawBinaryEditorStructuralChangeKind.Delete, deletion.Kind);
                Assert.Equal(2, deletion.Address);
            });
    }

    /// <summary>Tracks one bounded multi-byte insertion as one undoable structural cause.</summary>
    [Fact]
    public void InsertManyBytesReportsStructuralCauseAndRetainedValueEdits()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0x20, 0x30]);

        Assert.True(session.InsertZeroBytesBefore("0x1", 2).Succeeded);
        Assert.True(session.OverwriteByte("0x3", "A5").Succeeded);

        Assert.True(session.TryCopyWorkingBytes(out byte[]? bytes));
        Assert.Equal([0x10, 0x00, 0x00, 0xA5, 0x30], bytes);
        RawBinaryEditorChangedRange range = Assert.Single(session.GetChangedRanges());
        RawBinaryEditorStructuralChange insertion = Assert.Single(range.StructuralChanges);
        Assert.Equal(RawBinaryEditorStructuralChangeKind.Insert, insertion.Kind);
        Assert.Equal(1, insertion.Address);
        Assert.Equal(2, insertion.Count);
        RawBinaryEditorValueChange valueChange = Assert.Single(range.ValueChanges);
        Assert.Equal(3, valueChange.Start);
        Assert.Equal(4, valueChange.EndExclusive);
        Assert.Equal((byte)0x20, valueChange.FirstOriginalValue);
        Assert.Equal((byte)0xA5, valueChange.FirstCurrentValue);

        Assert.True(session.Undo().Succeeded);
        Assert.True(session.Undo().Succeeded);
        Assert.True(session.TryCopyWorkingBytes(out byte[]? restored));
        Assert.Equal([0x10, 0x20, 0x30], restored);
    }

    /// <summary>Reports exact deletion address/count and rejects unbounded insert counts.</summary>
    [Fact]
    public void DeleteReportsStructuralCauseAndInsertCountIsBounded()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0x20, 0x30]);

        Assert.True(session.DeleteByte("0x1").Succeeded);

        RawBinaryEditorChangedRange range = Assert.Single(session.GetChangedRanges());
        RawBinaryEditorStructuralChange deletion = Assert.Single(range.StructuralChanges);
        Assert.Equal(RawBinaryEditorStructuralChangeKind.Delete, deletion.Kind);
        Assert.Equal(1, deletion.Address);
        Assert.Equal(1, deletion.Count);
        Assert.Equal(
            RawBinaryEditorIssueCode.InvalidByteCount,
            session.InsertZeroBytesAfter("0x0", 0).Issue?.Code);
        Assert.Equal(
            RawBinaryEditorIssueCode.InvalidByteCount,
            session.InsertZeroBytesAfter("0x0", RawBinaryEditorSession.MaximumInsertByteCount + 1).Issue?.Code);
    }

    /// <summary>Rejects malformed input through typed issues rather than mutating a caller-owned byte array.</summary>
    [Fact]
    public void InvalidInputReturnsStableIssues()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x00]);

        Assert.Equal(
            RawBinaryEditorIssueCode.InvalidHexByte,
            session.OverwriteByte("0x0", "G0").Issue?.Code);
        Assert.Equal(
            RawBinaryEditorIssueCode.AddressOutOfRange,
            session.DeleteByte("0x1").Issue?.Code);
        Assert.Equal(
            RawBinaryEditorIssueCode.InvalidAddress,
            session.CreateViewport("bad").Issue?.Code);
        Assert.Equal(
            RawBinaryEditorIssueCode.InvalidAddress,
            session.CreateViewport("10").Issue?.Code);
        Assert.Equal(
            RawBinaryEditorIssueCode.InvalidAddress,
            session.CreateViewport("0X0").Issue?.Code);
    }

    /// <summary>Finds printable ASCII in the memory buffer and wraps only after the requested starting point.</summary>
    [Fact]
    public void FindAsciiUsesTheCurrentWorkBufferAndCyclesMatches()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load("NVT first NVT second"u8);

        RawBinaryEditorSearchResult first = session.FindAscii("NVT", 0, TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult second = session.FindAscii("NVT", first.Address + 1, TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult wrapped = session.FindAscii("NVT", second.Address + 1, TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded);
        Assert.Equal(0, first.Address);
        Assert.Equal([0L, 10L], first.Matches);
        Assert.Equal(0, first.MatchIndex);
        Assert.True(second.Succeeded);
        Assert.Equal(10, second.Address);
        Assert.Equal(1, second.MatchIndex);
        Assert.True(wrapped.Succeeded);
        Assert.Equal(0, wrapped.Address);
        Assert.True(wrapped.Wrapped);
        Assert.Equal(
            RawBinaryEditorIssueCode.InvalidAsciiText,
            session.FindAscii("測試", 0, TestContext.Current.CancellationToken).Issue?.Code);
        Assert.Equal(
            RawBinaryEditorIssueCode.AsciiTextNotFound,
            session.FindAscii("missing", 0, TestContext.Current.CancellationToken).Issue?.Code);
    }

    /// <summary>Wraps from the document start when the next-search offset is exactly at EOF.</summary>
    [Fact]
    public void FindAsciiAtEndOfDocumentWrapsInsteadOfClampingToTheLastByte()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x00, (byte)'T']);

        RawBinaryEditorSearchResult result = session.FindAscii("T", 2, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Address);
        Assert.True(result.Wrapped);
    }

    /// <summary>Keeps dense search highlights bounded while preserving the complete result index.</summary>
    [Fact]
    public void FindAsciiBoundsRetainedMatchesAndKeepsTheSelectedOccurrence()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load(Enumerable.Repeat((byte)'A', 8192).ToArray());

        RawBinaryEditorSearchResult result = session.FindAscii("A", 7000, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(8192, result.TotalMatchCount);
        Assert.Equal(7000, result.MatchIndex);
        Assert.Equal(7000, result.Address);
        Assert.Equal(RawBinaryEditorSearch.MaximumRetainedMatches, result.Matches.Count);
        Assert.Contains(7000, result.Matches);
        Assert.True(result.IsTruncated);
    }

    /// <summary>Honors cancellation and the explicit document-memory boundary.</summary>
    [Fact]
    public void SearchCancellationAndDocumentLengthAreBounded()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load("NVT"u8);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
        {
            _ = session.FindAscii("NVT", 0, cancellation.Token);
        });
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = session.Load(new byte[RawBinaryEditorSession.MaximumDocumentLength + 1]);
        });
    }

    /// <summary>Accepts compact and spreadsheet-pasted byte strings while exposing contiguous in-memory changed blocks.</summary>
    [Fact]
    public void RangeOverwriteAcceptsCompactAndSpreadsheetSeparatedBytes()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        Assert.True(session.OverwriteRange("0x0", "0x3", "A5\t5A\r\n01 FF").Succeeded);
        Assert.True(session.OverwriteRange("0x4", "0x5", "1234").Succeeded);

        IReadOnlyList<RawBinaryEditorChangedRange> ranges = session.GetChangedRanges();
        RawBinaryEditorChangedRange range = Assert.Single(ranges);
        Assert.Equal(0, range.Start);
        Assert.Equal(6, range.EndExclusive);
        Assert.Equal(RawBinaryEditorChangeKind.Data, range.ChangeKind);
        Assert.True(session.TryCopyWorkingBytes(out byte[]? bytes));
        Assert.Equal([0xA5, 0x5A, 0x01, 0xFF, 0x12, 0x34], bytes);
    }
}
