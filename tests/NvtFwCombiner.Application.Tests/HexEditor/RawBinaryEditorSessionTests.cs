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

        RawBinaryEditorViewport viewport = session.CreatePage(0, maximumRows: 1);
        RawBinaryEditorViewportRow row = Assert.Single(viewport.Rows);

        Assert.Equal((byte)0x11, row.Bytes[0].CurrentValue);
        Assert.Equal((byte)0x11, row.Bytes[0].OriginalValue);
        Assert.Equal((byte)0x00, row.Bytes[1].CurrentValue);
        Assert.Null(row.Bytes[1].OriginalAddress);
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
        Assert.Contains(row.Bytes, static value => value.IsChanged);
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
    }

    /// <summary>Every memory-editor command fails closed before mutation when input is absent or malformed.</summary>
    [Fact]
    public void MissingDocumentAndMalformedCommandsReturnTypedIssues()
    {
        var session = new RawBinaryEditorSession();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => session.CreatePage(0, 0));
        Assert.Equal(RawBinaryEditorIssueCode.NoDocument, session.CreatePage(0, 1).Issue?.Code);
        AssertIssue(session.OverwriteByte("0x0", "00"), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.OverwriteRange("0x0", "0x0", "00"), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.FillRange("0x0", "0x0", "00"), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.InsertZeroBefore("0x0"), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.InsertZeroAfter("0x0"), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.InsertZeroBytesBefore("0x0", 1), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.InsertZeroBytesAfter("0x0", 1), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.DeleteByte("0x0"), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.Undo(), RawBinaryEditorIssueCode.NoDocument);
        AssertIssue(session.Redo(), RawBinaryEditorIssueCode.NoDocument);
        Assert.False(session.TryCopyWorkingBytes(out byte[]? missing));
        Assert.Null(missing);

        _ = session.Load([]);
        Assert.Equal(
            RawBinaryEditorIssueCode.AddressOutOfRange,
            session.CreatePage(0, 1).Issue?.Code);

        _ = session.Load([0x10, 0x20]);
        AssertIssue(session.OverwriteByte("bad", "00"), RawBinaryEditorIssueCode.InvalidAddress);
        AssertIssue(session.OverwriteByte("0x0", "0000"), RawBinaryEditorIssueCode.InvalidHexByte);
        AssertIssue(session.OverwriteByte("0x2", "00"), RawBinaryEditorIssueCode.AddressOutOfRange);
        AssertIssue(session.OverwriteRange("bad", "0x1", "00"), RawBinaryEditorIssueCode.InvalidAddress);
        AssertIssue(session.OverwriteRange("0x1", "bad", "00"), RawBinaryEditorIssueCode.InvalidAddress);
        AssertIssue(session.OverwriteRange("0x1", "0x0", "00"), RawBinaryEditorIssueCode.InvalidRange);
        AssertIssue(session.OverwriteRange("0x0", "0x2", "00"), RawBinaryEditorIssueCode.AddressOutOfRange);
        AssertIssue(session.OverwriteRange("0x0", "0x1", " "), RawBinaryEditorIssueCode.InvalidHexBytes);
        AssertIssue(session.OverwriteRange("0x0", "0x1", "0"), RawBinaryEditorIssueCode.InvalidHexBytes);
        AssertIssue(session.OverwriteRange("0x0", "0x1", "GG"), RawBinaryEditorIssueCode.InvalidHexBytes);
        AssertIssue(session.FillRange("0x1", "0x0", "00"), RawBinaryEditorIssueCode.InvalidRange);
        AssertIssue(session.FillRange("0x0", "0x1", "GG"), RawBinaryEditorIssueCode.InvalidHexByte);
        AssertIssue(session.InsertZeroBefore("bad"), RawBinaryEditorIssueCode.InvalidAddress);
        AssertIssue(session.InsertZeroAfter("bad"), RawBinaryEditorIssueCode.InvalidAddress);
        AssertIssue(session.InsertZeroBytesBefore("bad", 1), RawBinaryEditorIssueCode.InvalidAddress);
        AssertIssue(session.InsertZeroBytesAfter("bad", 1), RawBinaryEditorIssueCode.InvalidAddress);
        AssertIssue(session.InsertZeroBytesBefore("0x2", 1), RawBinaryEditorIssueCode.AddressOutOfRange);
        AssertIssue(session.InsertZeroBytesAfter("0x0", -1), RawBinaryEditorIssueCode.InvalidByteCount);
        AssertIssue(session.DeleteByte("bad"), RawBinaryEditorIssueCode.InvalidAddress);
        AssertIssue(session.DeleteByte("0x2"), RawBinaryEditorIssueCode.AddressOutOfRange);
        AssertIssue(session.Undo(), RawBinaryEditorIssueCode.NothingToUndo);
        AssertIssue(session.Redo(), RawBinaryEditorIssueCode.NothingToRedo);
    }

    /// <summary>Finds printable ASCII in the memory buffer and wraps only after the requested starting point.</summary>
    [Fact]
    public void FindAsciiUsesTheCurrentWorkBufferAndCyclesMatches()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load("NVT first NVT second"u8);

        RawBinaryEditorSearchResult first = FindAscii(session, "NVT", 0, TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult second = FindAscii(session, "NVT", first.Address + 1, TestContext.Current.CancellationToken);
        RawBinaryEditorSearchResult wrapped = FindAscii(session, "NVT", second.Address + 1, TestContext.Current.CancellationToken);

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
            FindAscii(session, "測試", 0, TestContext.Current.CancellationToken).Issue?.Code);
        Assert.Equal(
            RawBinaryEditorIssueCode.AsciiTextNotFound,
            FindAscii(session, "missing", 0, TestContext.Current.CancellationToken).Issue?.Code);
    }

    /// <summary>Wraps from the document start when the next-search offset is exactly at EOF.</summary>
    [Fact]
    public void FindAsciiAtEndOfDocumentWrapsInsteadOfClampingToTheLastByte()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x00, (byte)'T']);

        RawBinaryEditorSearchResult result = FindAscii(session, "T", 2, TestContext.Current.CancellationToken);

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

        RawBinaryEditorSearchResult result = FindAscii(session, "A", 7000, TestContext.Current.CancellationToken);

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
            _ = FindAscii(session, "NVT", 0, cancellation.Token);
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

    /// <summary>Keeps a stable immutable changed-range snapshot while local value edits split and merge runs.</summary>
    [Fact]
    public void ValueOnlyChangedRangesSplitMergeAndReuseTheCurrentSnapshot()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load(new byte[8]);

        Assert.True(session.FillRange("0x2", "0x5", "FF").Succeeded);
        IReadOnlyList<RawBinaryEditorChangedRange> merged = session.GetChangedRanges();
        RawBinaryEditorChangedRange initial = Assert.Single(merged);
        Assert.Equal((2L, 6L), (initial.Start, initial.EndExclusive));
        IList<RawBinaryEditorChangedRange> immutableRanges =
            Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<RawBinaryEditorChangedRange>>(merged);
        _ = Assert.Throws<NotSupportedException>(() => immutableRanges[0] = immutableRanges[0]);
        IList<RawBinaryEditorValueChange> immutableValues =
            Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<RawBinaryEditorValueChange>>(
                initial.ValueChanges);
        _ = Assert.Throws<NotSupportedException>(() => immutableValues[0] = immutableValues[0]);
        Assert.Same(merged, session.GetChangedRanges());

        Assert.True(session.OverwriteRange("0x3", "0x4", "0000").Succeeded);
        IReadOnlyList<RawBinaryEditorChangedRange> split = session.GetChangedRanges();
        Assert.Collection(
            split,
            first => Assert.Equal((2L, 3L), (first.Start, first.EndExclusive)),
            second => Assert.Equal((5L, 6L), (second.Start, second.EndExclusive)));
        Assert.Same(split, session.GetChangedRanges());

        Assert.True(session.FillRange("0x3", "0x4", "FF").Succeeded);
        RawBinaryEditorChangedRange remerged = Assert.Single(session.GetChangedRanges());
        Assert.Equal((2L, 6L), (remerged.Start, remerged.EndExclusive));

        Assert.True(session.Undo().Succeeded);
        Assert.Equal(2, session.GetChangedRanges().Count);
        Assert.True(session.Redo().Succeeded);
        _ = Assert.Single(session.GetChangedRanges());
    }

    /// <summary>Caches structural fallbacks and restores incremental value tracking after an exact undo.</summary>
    [Fact]
    public void StructuralChangedRangeFallbackIsCachedAndExactUndoRestoresValueTracking()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0x20, 0x30]);

        Assert.True(session.InsertZeroBefore("0x1").Succeeded);
        IReadOnlyList<RawBinaryEditorChangedRange> structural = session.GetChangedRanges();
        Assert.Same(structural, session.GetChangedRanges());
        RawBinaryEditorChangedRange structuralRange = Assert.Single(structural);
        Assert.NotEmpty(structuralRange.StructuralChanges);
        IList<RawBinaryEditorStructuralChange> immutableCauses =
            Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<RawBinaryEditorStructuralChange>>(
                structuralRange.StructuralChanges);
        _ = Assert.Throws<NotSupportedException>(() => immutableCauses[0] = immutableCauses[0]);

        Assert.True(session.Undo().Succeeded);
        IReadOnlyList<RawBinaryEditorChangedRange> restored = session.GetChangedRanges();
        Assert.Empty(restored);
        Assert.Same(restored, session.GetChangedRanges());

        Assert.True(session.OverwriteByte("0x1", "A5").Succeeded);
        IReadOnlyList<RawBinaryEditorChangedRange> valueOnly = session.GetChangedRanges();
        Assert.Empty(Assert.Single(valueOnly).StructuralChanges);
        Assert.Same(valueOnly, session.GetChangedRanges());
    }

    /// <summary>Matches every incrementally retained value run against a complete byte comparison.</summary>
    [Fact]
    public void IncrementalValueRangesMatchCompleteComparisonAcrossOverlappingEdits()
    {
        const int byteCount = 256;
        var session = new RawBinaryEditorSession();
        _ = session.Load(new byte[byteCount]);

        for (int edit = 0; edit < 64; edit++)
        {
            int start = edit * 29 % byteCount;
            int end = Math.Min(byteCount - 1, start + (edit * 11 % 32));
            string value = edit % 3 == 0 ? "00" : "A5";
            Assert.True(session.FillRange($"0x{start:X}", $"0x{end:X}", value).Succeeded);
            Assert.True(session.TryCopyWorkingBytes(out byte[]? working));

            var expected = new List<(long Start, long EndExclusive)>();
            int? runStart = null;
            for (int index = 0; index < working!.Length; index++)
            {
                if (working[index] != 0 && runStart is null)
                {
                    runStart = index;
                }
                else if (working[index] == 0 && runStart is int changedStart)
                {
                    expected.Add((changedStart, index));
                    runStart = null;
                }
            }

            if (runStart is int finalStart)
            {
                expected.Add((finalStart, working.Length));
            }

            IReadOnlyList<RawBinaryEditorChangedRange> actual = session.GetChangedRanges();
            Assert.Equal(expected, actual.Select(range => (range.Start, range.EndExclusive)));
            Assert.All(actual, range =>
            {
                Assert.Equal(RawBinaryEditorChangeKind.Data, range.ChangeKind);
                RawBinaryEditorValueChange valueChange = Assert.Single(range.ValueChanges);
                Assert.Equal((range.Start, range.EndExclusive), (valueChange.Start, valueChange.EndExclusive));
                Assert.Empty(range.StructuralChanges);
            });
        }
    }

    /// <summary>Locks repeated large-document changed-range reads to one cached immutable snapshot.</summary>
    [Fact]
    public void SingleByteEditReusesChangedRangesAtTheMaximumDocumentLength()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load(new byte[RawBinaryEditorSession.MaximumDocumentLength]);

        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(session.OverwriteByte("0x400000", "A5").Succeeded);
        IReadOnlyList<RawBinaryEditorChangedRange> snapshot = session.GetChangedRanges();
        for (int invocation = 0; invocation < 100; invocation++)
        {
            Assert.Same(snapshot, session.GetChangedRanges());
        }

        timer.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        RawBinaryEditorChangedRange change = Assert.Single(snapshot);
        Assert.Equal((0x400000L, 0x400001L), (change.Start, change.EndExclusive));
        Assert.InRange(allocated, 0, 128 * 1024);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"RAW_EDITOR_CHANGED_RANGES bytes={RawBinaryEditorSession.MaximumDocumentLength} " +
            $"lookups=101 elapsedMs={timer.Elapsed.TotalMilliseconds:F3} allocated={allocated}");
    }

    /// <summary>Reuses unchanged immutable range records when one byte changes inside a fragmented document.</summary>
    [Fact]
    public void FragmentedSingleByteEditReusesUnchangedRangeRecords()
    {
        const int documentLength = 20_000;
        const int expectedRangeCount = documentLength / 2;
        var session = new RawBinaryEditorSession();
        _ = session.Load(new byte[documentLength]);
        string alternatingValues = string.Join(
            ' ',
            Enumerable.Range(0, documentLength).Select(static index => index % 2 == 0 ? "FF" : "00"));
        Assert.True(session.OverwriteRange("0x0", "0x4E1F", alternatingValues).Succeeded);
        IReadOnlyList<RawBinaryEditorChangedRange> before = session.GetChangedRanges();
        Assert.Equal(expectedRangeCount, before.Count);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(session.OverwriteByte("0x0", "FE").Succeeded);
        IReadOnlyList<RawBinaryEditorChangedRange> after = session.GetChangedRanges();
        timer.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.NotSame(before, after);
        Assert.Equal(expectedRangeCount, after.Count);
        Assert.NotSame(before[0], after[0]);
        Assert.Equal((byte)0xFF, before[0].ValueChanges[0].FirstCurrentValue);
        Assert.Equal((byte)0xFE, after[0].ValueChanges[0].FirstCurrentValue);
        Assert.Same(before[1], after[1]);
        Assert.Same(before[^1], after[^1]);
        Assert.Same(after, session.GetChangedRanges());
        Assert.InRange(allocated, 0, 128 * 1024);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"RAW_EDITOR_FRAGMENTED_RANGES ranges={expectedRangeCount} " +
            $"elapsedMs={timer.Elapsed.TotalMilliseconds:F3} allocated={allocated}");
    }

    /// <summary>Bounds the retained source-address map to one compact integer per document byte.</summary>
    [Fact]
    public void LoadUsesCompactOriginalOffsetMap()
    {
        const int byteCount = 1024 * 1024;
        byte[] source = new byte[byteCount];
        var session = new RawBinaryEditorSession();

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        _ = session.Load(source);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"RAW_EDITOR_LOAD bytes={byteCount} allocated={allocated}");
        Assert.InRange(allocated, 0, (byteCount * 7L) + 32_768);
    }

    /// <summary>Records a non-gating large-document structural edit observation while locking dirty state.</summary>
    [Fact]
    public void LengthChangingEditPreservesLargeDocumentState()
    {
        const int byteCount = 1024 * 1024;
        var session = new RawBinaryEditorSession();
        _ = session.Load(new byte[byteCount]);

        var timer = System.Diagnostics.Stopwatch.StartNew();
        RawBinaryEditorOperationResult result = session.InsertZeroAfter("0x0");
        timer.Stop();

        Assert.True(result.Succeeded);
        Assert.Equal(byteCount + 1, result.State.WorkingLength);
        Assert.True(result.State.HasUnsavedChanges);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"RAW_EDITOR_INSERT bytes={byteCount} elapsedMs={timer.Elapsed.TotalMilliseconds:F3}");
    }

    private static RawBinaryEditorSearchResult FindAscii(
        RawBinaryEditorSession session,
        string text,
        long startOffset,
        CancellationToken cancellationToken)
    {
        Assert.True(session.TryCopyWorkingBytes(out byte[]? bytes));
        return RawBinaryEditorSearch.Find(bytes, session.State, text, startOffset, cancellationToken);
    }

    private static void AssertIssue(
        RawBinaryEditorOperationResult result,
        RawBinaryEditorIssueCode expected)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Issue?.Code);
    }
}
