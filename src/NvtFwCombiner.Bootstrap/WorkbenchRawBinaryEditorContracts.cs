namespace NvtFwCombiner.Bootstrap;

/// <summary>Stable raw-BIN editor issue categories exposed to presentation hosts.</summary>
public enum WorkbenchRawBinaryEditorIssueCode
{
    /// <summary>No source document is loaded.</summary>
    NoDocument,

    /// <summary>An address could not be parsed.</summary>
    InvalidAddress,

    /// <summary>An address or range is outside the memory document.</summary>
    AddressOutOfRange,

    /// <summary>One hexadecimal byte is malformed.</summary>
    InvalidHexByte,

    /// <summary>A hexadecimal byte sequence is malformed.</summary>
    InvalidHexBytes,

    /// <summary>An inclusive range is reversed or outside the memory document.</summary>
    InvalidRange,

    /// <summary>An overwrite sequence would continue past the selected inclusive end.</summary>
    InputExceedsRange,

    /// <summary>An insertion count is outside the supported bounded operation size.</summary>
    InvalidByteCount,

    /// <summary>No retained operation can be undone.</summary>
    NothingToUndo,

    /// <summary>No reverted operation can be redone.</summary>
    NothingToRedo,

    /// <summary>ASCII search input is empty or includes characters outside printable ASCII.</summary>
    InvalidAsciiText,

    /// <summary>No matching ASCII sequence exists in the current memory buffer.</summary>
    AsciiTextNotFound,
}

/// <summary>One host-visible issue from the raw-BIN editor facade.</summary>
public sealed record WorkbenchRawBinaryEditorIssue(WorkbenchRawBinaryEditorIssueCode Code);

/// <summary>Non-file state of the editor-owned memory document.</summary>
public sealed record WorkbenchRawBinaryEditorState(
    bool HasDocument,
    long OriginalLength,
    long WorkingLength,
    int UndoCount,
    int RedoCount)
{
    /// <summary>True when one or more retained operations differ from the loaded source.</summary>
    public bool HasUnsavedChanges => UndoCount > 0;
}

/// <summary>Result of one memory-only edit operation.</summary>
public sealed record WorkbenchRawBinaryEditorOperationResult(
    WorkbenchRawBinaryEditorState State,
    WorkbenchRawBinaryEditorIssue? Issue = null)
{
    /// <summary>True when the requested operation completed.</summary>
    public bool Succeeded => Issue is null;
}

/// <summary>Host-visible raw-BIN change reasons.</summary>
[Flags]
public enum WorkbenchRawBinaryEditorChangeKind
{
    /// <summary>No data or source-address mapping difference.</summary>
    None = 0,

    /// <summary>Byte value differs from its retained source identity.</summary>
    Data = 1,

    /// <summary>Insert/delete shifted source-address identity.</summary>
    Structural = 2,
}

/// <summary>Host-visible structural source-address change.</summary>
public enum WorkbenchRawBinaryEditorStructuralChangeKind
{
    /// <summary>Zero-filled bytes were inserted.</summary>
    Insert,

    /// <summary>Source bytes were deleted.</summary>
    Delete,
}

/// <summary>One host-visible contiguous value-edit run.</summary>
public sealed record WorkbenchRawBinaryEditorValueChange(
    long Start,
    long EndExclusive,
    byte FirstOriginalValue,
    byte FirstCurrentValue)
{
    /// <summary>Number of value-edited bytes in this run.</summary>
    public long Length => EndExclusive - Start;
}

/// <summary>One host-visible structural mapping change.</summary>
public sealed record WorkbenchRawBinaryEditorStructuralChange(
    WorkbenchRawBinaryEditorStructuralChangeKind Kind,
    long Address,
    int Count);

/// <summary>One half-open changed range in the original/current comparison address space.</summary>
public sealed record WorkbenchRawBinaryEditorChangedRange(
    long Start,
    long EndExclusive,
    WorkbenchRawBinaryEditorChangeKind ChangeKind,
    IReadOnlyList<WorkbenchRawBinaryEditorValueChange> ValueChanges,
    IReadOnlyList<WorkbenchRawBinaryEditorStructuralChange> StructuralChanges)
{
    /// <summary>Number of comparison addresses represented by this changed block.</summary>
    public long Length => EndExclusive - Start;
}

/// <summary>One current byte and its originating source identity, when retained.</summary>
public sealed record WorkbenchRawBinaryEditorByte(
    long Address,
    long? OriginalAddress,
    byte OriginalValue,
    byte? OriginalValueAtAddress,
    byte CurrentValue,
    WorkbenchRawBinaryEditorChangeKind ChangeKind)
{
    /// <summary>True when this byte still maps to a byte from the opened source BIN.</summary>
    public bool HasOriginalValue => OriginalAddress is not null;

    /// <summary>True when the opened source BIN contains this same display address.</summary>
    public bool HasOriginalValueAtAddress => OriginalValueAtAddress is not null;

    /// <summary>True when the value differs from its retained source identity.</summary>
    public bool IsDataChanged => (ChangeKind & WorkbenchRawBinaryEditorChangeKind.Data) != 0;

    /// <summary>True when insert/delete shifted this byte's source address.</summary>
    public bool IsStructuralChanged => (ChangeKind & WorkbenchRawBinaryEditorChangeKind.Structural) != 0;

    /// <summary>True when value or source-address mapping differs.</summary>
    public bool IsChanged => ChangeKind != WorkbenchRawBinaryEditorChangeKind.None;
}

/// <summary>One fixed-width host-visible hexadecimal row.</summary>
public sealed record WorkbenchRawBinaryEditorViewportRow(
    long Address,
    IReadOnlyList<WorkbenchRawBinaryEditorByte> Bytes,
    string OriginalAscii,
    string CurrentAscii)
{
    /// <summary>True when one or more bytes in the row changed.</summary>
    public bool HasChanges => Bytes.Any(value => value.IsChanged);
}

/// <summary>A bounded viewport projected from the editor-owned memory document.</summary>
public sealed record WorkbenchRawBinaryEditorViewport(
    IReadOnlyList<WorkbenchRawBinaryEditorViewportRow> Rows,
    WorkbenchRawBinaryEditorState State,
    long Start,
    long Length,
    WorkbenchRawBinaryEditorIssue? Issue = null)
{
    /// <summary>True when the requested viewport is valid.</summary>
    public bool Succeeded => Issue is null;
}
