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

    /// <summary>An inclusive range is reversed or incompatible with supplied values.</summary>
    InvalidRange,

    /// <summary>No retained operation can be undone.</summary>
    NothingToUndo,

    /// <summary>No reverted operation can be redone.</summary>
    NothingToRedo,
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

/// <summary>One current byte and its originating source identity, when retained.</summary>
public sealed record WorkbenchRawBinaryEditorByte(
    long Address,
    long? OriginalAddress,
    byte OriginalValue,
    byte CurrentValue)
{
    /// <summary>True when this byte still maps to a byte from the opened source BIN.</summary>
    public bool HasOriginalValue => OriginalAddress is not null;

    /// <summary>True when a value differs or when the byte was inserted into the work buffer.</summary>
    public bool IsChanged => !HasOriginalValue || OriginalValue != CurrentValue;
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
