namespace NvtFwCombiner.Application.HexEditor;

/// <summary>Stable outcomes for a raw-BIN editor request.</summary>
public enum RawBinaryEditorIssueCode
{
    /// <summary>No document has been loaded into the editor session.</summary>
    NoDocument,

    /// <summary>An address was not a valid non-negative hexadecimal or decimal byte offset.</summary>
    InvalidAddress,

    /// <summary>A requested address or range is outside the current working document.</summary>
    AddressOutOfRange,

    /// <summary>A hexadecimal byte value was malformed.</summary>
    InvalidHexByte,

    /// <summary>A hexadecimal byte sequence was malformed or empty.</summary>
    InvalidHexBytes,

    /// <summary>The supplied inclusive range is reversed or incompatible with the requested data.</summary>
    InvalidRange,

    /// <summary>There is no prior in-memory change to undo.</summary>
    NothingToUndo,

    /// <summary>There is no reverted in-memory change to redo.</summary>
    NothingToRedo,
}

/// <summary>One structured user-data issue returned by the raw-BIN editor service.</summary>
public sealed record RawBinaryEditorIssue(RawBinaryEditorIssueCode Code);

/// <summary>Current non-file state of one raw-BIN editing session.</summary>
public sealed record RawBinaryEditorState(
    bool HasDocument,
    long OriginalLength,
    long WorkingLength,
    int UndoCount,
    int RedoCount)
{
    /// <summary>True when the work buffer differs through one or more retained editor operations.</summary>
    public bool HasUnsavedChanges => UndoCount > 0;
}

/// <summary>Result of an editor operation that may mutate only the session-owned work buffer.</summary>
public sealed record RawBinaryEditorOperationResult(
    RawBinaryEditorState State,
    RawBinaryEditorIssue? Issue = null)
{
    /// <summary>True when the requested editor operation completed.</summary>
    public bool Succeeded => Issue is null;
}

/// <summary>One byte in a raw-BIN viewport, including its original source identity when one remains.</summary>
public sealed record RawBinaryEditorByte(
    long Address,
    long? OriginalAddress,
    byte OriginalValue,
    byte CurrentValue)
{
    /// <summary>True when this current byte originated from the loaded source document.</summary>
    public bool HasOriginalValue => OriginalAddress is not null;

    /// <summary>True when this byte is newly inserted or differs from its originating source byte.</summary>
    public bool IsChanged => !HasOriginalValue || OriginalValue != CurrentValue;
}

/// <summary>One fixed-width raw-BIN viewport row.</summary>
public sealed record RawBinaryEditorViewportRow(
    long Address,
    IReadOnlyList<RawBinaryEditorByte> Bytes,
    string OriginalAscii,
    string CurrentAscii)
{
    /// <summary>True when at least one byte in the displayed row changed.</summary>
    public bool HasChanges => Bytes.Any(value => value.IsChanged);
}

/// <summary>A bounded, fixed-width raw-BIN viewport built from the editor-owned work buffer.</summary>
public sealed record RawBinaryEditorViewport(
    IReadOnlyList<RawBinaryEditorViewportRow> Rows,
    RawBinaryEditorState State,
    long Start,
    long Length,
    RawBinaryEditorIssue? Issue = null)
{
    /// <summary>True when the requested viewport could be read.</summary>
    public bool Succeeded => Issue is null;
}
