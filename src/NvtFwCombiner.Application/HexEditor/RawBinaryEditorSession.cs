using System.Globalization;
using System.Runtime.InteropServices;

namespace NvtFwCombiner.Application.HexEditor;

/// <summary>
/// Owns one raw binary document in memory. It never reads or writes a file and never applies
/// firmware profile, IC, header, CRC, or postbuild policy.
/// </summary>
public sealed class RawBinaryEditorSession
{
    private const int BytesPerRow = 16;
    private const int ViewportRowCount = 32;
    private const int ViewportContextRows = 4;

    private readonly Stack<HistoryEntry> _redo = [];
    private readonly Stack<HistoryEntry> _undo = [];
    private List<int?>? _originalOffsets;
    private byte[]? _original;
    private List<byte>? _working;

    /// <summary>Replaces the session with a defensive in-memory copy of a loaded binary document.</summary>
    public RawBinaryEditorState Load(ReadOnlySpan<byte> source)
    {
        _original = source.ToArray();
        _working = [.. source];
        _originalOffsets = [.. Enumerable.Range(0, source.Length).Select(index => (int?)index)];
        _undo.Clear();
        _redo.Clear();
        return GetState();
    }

    /// <summary>Builds a bounded viewport around a user-entered offset without rereading the source file.</summary>
    public RawBinaryEditorViewport CreateViewport(string requestedAddress)
    {
        ArgumentNullException.ThrowIfNull(requestedAddress);
        return !TryRequireDocument(out RawBinaryEditorIssue? issue)
            ? CreateViewportFailure(issue!)
            : TryParseAddress(requestedAddress, out long requested)
            ? CreateViewport(requested)
            : CreateViewportFailure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidAddress));
    }

    /// <summary>Builds a bounded viewport around one checked working-buffer offset.</summary>
    public RawBinaryEditorViewport CreateViewport(long requestedAddress)
    {
        if (!TryRequireDocument(out RawBinaryEditorIssue? issue))
        {
            return CreateViewportFailure(issue!);
        }

        if (_working!.Count == 0 || requestedAddress < 0 || requestedAddress >= _working.Count)
        {
            return CreateViewportFailure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.AddressOutOfRange));
        }

        int requested = checked((int)requestedAddress);
        int requestedRow = requested - (requested % BytesPerRow);
        int contextualStart = Math.Max(0, requestedRow - (ViewportContextRows * BytesPerRow));
        int finalRowStart = (_working.Count - 1) / BytesPerRow * BytesPerRow;
        int start = Math.Min(contextualStart, finalRowStart);
        int length = Math.Min(_working.Count - start, BytesPerRow * ViewportRowCount);
        return CreateViewportWindow(start, length);
    }

    /// <summary>Builds one aligned forward page for progressive rendering without any source-file read.</summary>
    public RawBinaryEditorViewport CreatePage(long requestedAddress, int maximumRows)
    {
        if (maximumRows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRows), maximumRows, "Page row count must be positive.");
        }

        if (!TryRequireDocument(out RawBinaryEditorIssue? issue))
        {
            return CreateViewportFailure(issue!);
        }

        if (_working!.Count == 0 || requestedAddress < 0 || requestedAddress >= _working.Count)
        {
            return CreateViewportFailure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.AddressOutOfRange));
        }

        int requested = checked((int)requestedAddress);
        int start = requested - (requested % BytesPerRow);
        int length = Math.Min(_working.Count - start, checked(maximumRows * BytesPerRow));
        return CreateViewportWindow(start, length);
    }

    private RawBinaryEditorViewport CreateViewportWindow(int start, int length)
    {
        byte[] originalDocument = _original ?? throw new InvalidOperationException("A raw BIN source must be loaded before rendering.");
        List<int?> originalOffsets = _originalOffsets ?? throw new InvalidOperationException("Raw BIN source offsets must be loaded before rendering.");
        List<byte> workingDocument = _working ?? throw new InvalidOperationException("A raw BIN work buffer must be loaded before rendering.");
        var rows = new List<RawBinaryEditorViewportRow>();
        for (int offset = 0; offset < length; offset += BytesPerRow)
        {
            int rowLength = Math.Min(BytesPerRow, length - offset);
            var bytes = new List<RawBinaryEditorByte>(rowLength);
            char[] originalAscii = new char[rowLength];
            char[] currentAscii = new char[rowLength];
            for (int index = 0; index < rowLength; index++)
            {
                int documentIndex = start + offset + index;
                int? originalIndex = originalOffsets[documentIndex];
                bool hasOriginal = originalIndex is not null;
                byte original = hasOriginal ? originalDocument[originalIndex!.Value] : (byte)0x00;
                byte current = workingDocument[documentIndex];
                bytes.Add(new RawBinaryEditorByte(documentIndex, originalIndex, original, current));
                originalAscii[index] = hasOriginal ? FormatAscii(original) : ' ';
                currentAscii[index] = FormatAscii(current);
            }

            rows.Add(new RawBinaryEditorViewportRow(
                start + offset,
                bytes,
                new string(originalAscii),
                new string(currentAscii)));
        }

        return new RawBinaryEditorViewport(rows, GetState(), start, length);
    }

    /// <summary>Overwrites one exact byte in the in-memory work buffer.</summary>
    public RawBinaryEditorOperationResult OverwriteByte(string address, string value)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(value);
        return TryParseAddress(address, out long offset) &&
               TryParseSingleByte(value, out byte parsed)
            ? Overwrite(offset, [parsed])
            : CreateParseFailure(address, requiresSingleByte: true);
    }

    /// <summary>Overwrites an inclusive range with an exact number of hexadecimal bytes.</summary>
    public RawBinaryEditorOperationResult OverwriteRange(string startAddress, string endAddress, string values)
    {
        ArgumentNullException.ThrowIfNull(startAddress);
        ArgumentNullException.ThrowIfNull(endAddress);
        ArgumentNullException.ThrowIfNull(values);
        return !TryParseRange(startAddress, endAddress, out int start, out int length, out RawBinaryEditorIssue? issue)
            ? Failure(issue!)
            : !TryParseBytes(values, out byte[]? parsed)
            ? Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidHexBytes))
            : parsed!.Length != length
            ? Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidRange))
            : Overwrite(start, parsed);
    }

    /// <summary>Fills an inclusive range with one hexadecimal byte in the in-memory work buffer.</summary>
    public RawBinaryEditorOperationResult FillRange(string startAddress, string endAddress, string value)
    {
        ArgumentNullException.ThrowIfNull(startAddress);
        ArgumentNullException.ThrowIfNull(endAddress);
        ArgumentNullException.ThrowIfNull(value);
        if (!TryParseRange(startAddress, endAddress, out int start, out int length, out RawBinaryEditorIssue? issue))
        {
            return Failure(issue!);
        }

        if (!TryParseSingleByte(value, out byte parsed))
        {
            return Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidHexByte));
        }

        byte[] values = new byte[length];
        values.AsSpan().Fill(parsed);
        return Overwrite(start, values);
    }

    /// <summary>Inserts an explicit zero byte immediately before the selected byte.</summary>
    public RawBinaryEditorOperationResult InsertZeroBefore(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return TryParseAddress(address, out long offset)
            ? Insert(offset, before: true)
            : Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidAddress));
    }

    /// <summary>Inserts an explicit zero byte immediately after the selected byte.</summary>
    public RawBinaryEditorOperationResult InsertZeroAfter(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return TryParseAddress(address, out long offset)
            ? Insert(offset, before: false)
            : Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidAddress));
    }

    /// <summary>Deletes the selected byte and shifts later work-buffer bytes toward lower offsets.</summary>
    public RawBinaryEditorOperationResult DeleteByte(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!TryRequireDocument(out RawBinaryEditorIssue? issue))
        {
            return Failure(issue!);
        }

        if (!TryParseAddress(address, out long parsed))
        {
            return Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidAddress));
        }

        if (parsed < 0 || parsed >= _working!.Count)
        {
            return Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.AddressOutOfRange));
        }

        int offset = checked((int)parsed);
        byte removed = _working[offset];
        int? removedOriginalOffset = _originalOffsets![offset];
        var entry = new HistoryEntry(
            new DeleteAction(offset, 1),
            new InsertAction(offset, [removed], [removedOriginalOffset]));
        Apply(entry.Forward);
        Track(entry);
        return Success();
    }

    /// <summary>Reverts the most recent in-memory operation without touching a file.</summary>
    public RawBinaryEditorOperationResult Undo()
    {
        if (!TryRequireDocument(out RawBinaryEditorIssue? issue))
        {
            return Failure(issue!);
        }

        if (!_undo.TryPop(out HistoryEntry? entry))
        {
            return Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.NothingToUndo));
        }

        Apply(entry.Reverse);
        _redo.Push(entry);
        return Success();
    }

    /// <summary>Reapplies the most recently reverted in-memory operation without touching a file.</summary>
    public RawBinaryEditorOperationResult Redo()
    {
        if (!TryRequireDocument(out RawBinaryEditorIssue? issue))
        {
            return Failure(issue!);
        }

        if (!_redo.TryPop(out HistoryEntry? entry))
        {
            return Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.NothingToRedo));
        }

        Apply(entry.Forward);
        _undo.Push(entry);
        return Success();
    }

    /// <summary>Copies the current work buffer for a host-owned Save As adapter.</summary>
    public bool TryCopyWorkingBytes(out byte[]? bytes)
    {
        bytes = _working?.ToArray();
        return bytes is not null;
    }

    private RawBinaryEditorOperationResult Overwrite(long offset, byte[] values)
    {
        if (!TryRequireDocument(out RawBinaryEditorIssue? issue))
        {
            return Failure(issue!);
        }

        if (offset < 0 || offset > _working!.Count || values.Length == 0 || values.Length > _working.Count - offset)
        {
            return Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.AddressOutOfRange));
        }

        int start = checked((int)offset);
        byte[] previous = [.. _working.GetRange(start, values.Length)];
        var entry = new HistoryEntry(
            new ReplaceAction(start, [.. values]),
            new ReplaceAction(start, previous));
        Apply(entry.Forward);
        Track(entry);
        return Success();
    }

    private RawBinaryEditorOperationResult Insert(long offset, bool before)
    {
        if (!TryRequireDocument(out RawBinaryEditorIssue? issue))
        {
            return Failure(issue!);
        }

        if (offset < 0 || offset >= _working!.Count)
        {
            return Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.AddressOutOfRange));
        }

        int index = checked((int)offset + (before ? 0 : 1));
        var entry = new HistoryEntry(
            new InsertAction(index, [0], [null]),
            new DeleteAction(index, 1));
        Apply(entry.Forward);
        Track(entry);
        return Success();
    }

    private void Apply(EditAction action)
    {
        switch (action)
        {
            case ReplaceAction replace:
                replace.Bytes.AsSpan().CopyTo(CollectionsMarshal.AsSpan(_working!).Slice(replace.Start, replace.Bytes.Length));
                break;
            case InsertAction insert:
                _working!.InsertRange(insert.Start, insert.Bytes);
                _originalOffsets!.InsertRange(insert.Start, insert.OriginalOffsets);
                break;
            case DeleteAction delete:
                _working!.RemoveRange(delete.Start, delete.Length);
                _originalOffsets!.RemoveRange(delete.Start, delete.Length);
                break;
            default:
                throw new InvalidOperationException("Unknown raw binary editor action.");
        }
    }

    private void Track(HistoryEntry entry)
    {
        _undo.Push(entry);
        _redo.Clear();
    }

    private bool TryParseRange(
        string startAddress,
        string endAddress,
        out int start,
        out int length,
        out RawBinaryEditorIssue? issue)
    {
        start = 0;
        length = 0;
        if (!TryRequireDocument(out issue))
        {
            return false;
        }

        if (!TryParseAddress(startAddress, out long startOffset) || !TryParseAddress(endAddress, out long endOffset))
        {
            issue = new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidAddress);
            return false;
        }

        if (endOffset < startOffset)
        {
            issue = new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidRange);
            return false;
        }

        if (startOffset < 0 || endOffset >= _working!.Count)
        {
            issue = new RawBinaryEditorIssue(RawBinaryEditorIssueCode.AddressOutOfRange);
            return false;
        }

        try
        {
            start = checked((int)startOffset);
            length = checked((int)(endOffset - startOffset + 1));
            issue = null;
            return true;
        }
        catch (OverflowException)
        {
            issue = new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidRange);
            return false;
        }
    }

    private static bool TryParseAddress(string value, out long address)
    {
        address = 0;
        string trimmed = value.Trim();
        bool parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address)
            : long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out address);
        return parsed && address >= 0;
    }

    private static bool TryParseSingleByte(string value, out byte parsed)
    {
        parsed = 0;
        string trimmed = value.Trim();
        return trimmed.Length == 2 &&
               byte.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryParseBytes(string value, out byte[]? bytes)
    {
        string[] parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            bytes = null;
            return false;
        }

        bytes = new byte[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            if (!TryParseSingleByte(parts[index], out bytes[index]))
            {
                bytes = null;
                return false;
            }
        }

        return true;
    }

    private bool TryRequireDocument(out RawBinaryEditorIssue? issue)
    {
        issue = _working is null || _original is null || _originalOffsets is null
            ? new RawBinaryEditorIssue(RawBinaryEditorIssueCode.NoDocument)
            : null;
        return issue is null;
    }

    private RawBinaryEditorState GetState()
    {
        return new RawBinaryEditorState(
            _working is not null,
            _original?.LongLength ?? 0,
            _working?.Count ?? 0,
            _undo.Count,
            _redo.Count);
    }

    private RawBinaryEditorOperationResult Success()
    {
        return new RawBinaryEditorOperationResult(GetState());
    }

    private RawBinaryEditorOperationResult Failure(RawBinaryEditorIssue issue)
    {
        return new RawBinaryEditorOperationResult(GetState(), issue);
    }

    private RawBinaryEditorOperationResult CreateParseFailure(string address, bool requiresSingleByte)
    {
        return !TryParseAddress(address, out _)
            ? Failure(new RawBinaryEditorIssue(RawBinaryEditorIssueCode.InvalidAddress))
            : Failure(new RawBinaryEditorIssue(requiresSingleByte
                ? RawBinaryEditorIssueCode.InvalidHexByte
                : RawBinaryEditorIssueCode.InvalidHexBytes));
    }

    private RawBinaryEditorViewport CreateViewportFailure(RawBinaryEditorIssue issue)
    {
        return new RawBinaryEditorViewport([], GetState(), 0, 0, issue);
    }

    private static char FormatAscii(byte value)
    {
        return value is >= 0x20 and <= 0x7E ? (char)value : '.';
    }

    private sealed record HistoryEntry(EditAction Forward, EditAction Reverse);

    private abstract record EditAction;

    private sealed record ReplaceAction(int Start, byte[] Bytes) : EditAction;

    private sealed record InsertAction(int Start, byte[] Bytes, int?[] OriginalOffsets) : EditAction;

    private sealed record DeleteAction(int Start, int Length) : EditAction;
}
