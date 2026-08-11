namespace NvtFwCombiner.Application.HexEditor;

/// <summary>Stable outcomes for a raw-BIN editor request.</summary>
public enum RawBinaryEditorIssueCode
{
    /// <summary>No document has been loaded into the editor session.</summary>
    NoDocument,

    /// <summary>An address was not a valid non-negative hexadecimal byte offset with a lowercase 0x prefix.</summary>
    InvalidAddress,

    /// <summary>A requested address or range is outside the current working document.</summary>
    AddressOutOfRange,

    /// <summary>A hexadecimal byte value was malformed.</summary>
    InvalidHexByte,

    /// <summary>A hexadecimal byte sequence was malformed or empty.</summary>
    InvalidHexBytes,

    /// <summary>The supplied inclusive range is reversed or outside the current working document.</summary>
    InvalidRange,

    /// <summary>The supplied overwrite sequence would continue past the selected inclusive end.</summary>
    InputExceedsRange,

    /// <summary>The requested insertion count is outside the supported bounded operation size.</summary>
    InvalidByteCount,

    /// <summary>There is no prior in-memory change to undo.</summary>
    NothingToUndo,

    /// <summary>There is no reverted in-memory change to redo.</summary>
    NothingToRedo,

    /// <summary>The requested ASCII search text is empty or contains non-ASCII characters.</summary>
    InvalidAsciiText,

    /// <summary>The requested ASCII sequence does not occur in the current in-memory work buffer.</summary>
    AsciiTextNotFound,
}

/// <summary>One structured user-data issue returned by the raw-BIN editor service.</summary>
public sealed record RawBinaryEditorIssue(RawBinaryEditorIssueCode Code);

/// <summary>Current non-file state of one raw-BIN editing session.</summary>
public sealed record RawBinaryEditorState(
    bool HasDocument,
    long OriginalLength,
    long WorkingLength,
    int UndoCount,
    int RedoCount,
    bool HasUnsavedChanges = false);

/// <summary>Result of an editor operation that may mutate only the session-owned work buffer.</summary>
public sealed record RawBinaryEditorOperationResult(
    RawBinaryEditorState State,
    RawBinaryEditorIssue? Issue = null)
{
    /// <summary>True when the requested editor operation completed.</summary>
    public bool Succeeded => Issue is null;
}

/// <summary>Result of a bounded ASCII search against the session-owned work buffer.</summary>
public sealed record RawBinaryEditorSearchResult(
    RawBinaryEditorState State,
    IReadOnlyList<long> Matches,
    int MatchIndex = -1,
    int Length = 0,
    bool Wrapped = false,
    int TotalMatchCount = 0,
    long SelectedAddress = -1,
    bool IsTruncated = false,
    RawBinaryEditorIssue? Issue = null)
{
    /// <summary>True when a matching ASCII sequence was found.</summary>
    public bool Succeeded => Issue is null;

    /// <summary>Address of the currently selected search match, or -1 when no match exists.</summary>
    public long Address => SelectedAddress;
}

/// <summary>Independent reasons why a raw-BIN byte belongs to a changed block.</summary>
[Flags]
public enum RawBinaryEditorChangeKind
{
    /// <summary>The byte is unchanged in value and source-address mapping.</summary>
    None = 0,

    /// <summary>The current byte value differs from its retained source identity.</summary>
    Data = 1,

    /// <summary>The current byte originated from another source address because of insert/delete shifting.</summary>
    Structural = 2,
}

/// <summary>Structural edit that changes how current addresses map to the opened source.</summary>
public enum RawBinaryEditorStructuralChangeKind
{
    /// <summary>New zero-filled bytes were inserted into the memory document.</summary>
    Insert,

    /// <summary>Source bytes were removed from the memory document.</summary>
    Delete,
}

/// <summary>One contiguous run whose current values differ from their retained source identities.</summary>
public sealed record RawBinaryEditorValueChange(
    long Start,
    long EndExclusive,
    byte FirstOriginalValue,
    byte FirstCurrentValue)
{
    /// <summary>Number of value-edited bytes in this run.</summary>
    public long Length => EndExclusive - Start;
}

/// <summary>One current structural mapping change derived from source-byte identity.</summary>
public sealed record RawBinaryEditorStructuralChange(
    RawBinaryEditorStructuralChangeKind Kind,
    long Address,
    int Count);

/// <summary>One half-open changed range in the original/current comparison address space.</summary>
public sealed record RawBinaryEditorChangedRange(
    long Start,
    long EndExclusive,
    RawBinaryEditorChangeKind ChangeKind,
    IReadOnlyList<RawBinaryEditorValueChange> ValueChanges,
    IReadOnlyList<RawBinaryEditorStructuralChange> StructuralChanges)
{
    /// <summary>Number of comparison addresses represented by this changed block.</summary>
    public long Length => EndExclusive - Start;
}

/// <summary>One byte in a raw-BIN viewport, including its original source identity when one remains.</summary>
public sealed record RawBinaryEditorByte(
    long Address,
    long? OriginalAddress,
    byte OriginalValue,
    byte? OriginalValueAtAddress,
    byte CurrentValue,
    RawBinaryEditorChangeKind ChangeKind)
{
    /// <summary>True when the current value differs from its retained source identity.</summary>
    public bool IsDataChanged => (ChangeKind & RawBinaryEditorChangeKind.Data) != 0;

    /// <summary>True when insert/delete changed this byte's source-address identity.</summary>
    public bool IsStructuralChanged => (ChangeKind & RawBinaryEditorChangeKind.Structural) != 0;

    /// <summary>True when either value or source-address mapping differs.</summary>
    public bool IsChanged => ChangeKind != RawBinaryEditorChangeKind.None;
}

/// <summary>One fixed-width raw-BIN viewport row.</summary>
public sealed record RawBinaryEditorViewportRow(
    long Address,
    IReadOnlyList<RawBinaryEditorByte> Bytes,
    string OriginalAscii,
    string CurrentAscii);

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
