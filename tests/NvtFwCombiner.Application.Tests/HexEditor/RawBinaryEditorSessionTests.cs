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

    /// <summary>Requires an overwrite byte sequence to match the inclusive selected range exactly.</summary>
    [Fact]
    public void OverwriteRangeRejectsMismatchedLengthWithoutMutatingTheDocument()
    {
        var session = new RawBinaryEditorSession();
        _ = session.Load([0x10, 0x20, 0x30]);

        RawBinaryEditorOperationResult result = session.OverwriteRange("0x0", "0x1", "A5");

        Assert.False(result.Succeeded);
        Assert.Equal(RawBinaryEditorIssueCode.InvalidRange, result.Issue?.Code);
        Assert.True(session.TryCopyWorkingBytes(out byte[]? bytes));
        Assert.Equal([0x10, 0x20, 0x30], bytes);
    }

    /// <summary>Preserves original source identity after insertion instead of comparing shifted data by display address.</summary>
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
        Assert.True(row.Bytes[1].IsChanged);
        Assert.Equal((byte)0x22, row.Bytes[2].CurrentValue);
        Assert.Equal(1, row.Bytes[2].OriginalAddress);
        Assert.False(row.Bytes[2].IsChanged);
        Assert.True(row.HasChanges);
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
    }
}
