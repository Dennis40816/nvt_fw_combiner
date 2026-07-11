using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Host adapter for one raw-BIN Hex Editor session. It performs one adapter-backed source read,
/// delegates every edit to the application-owned memory session, and only exports through a new
/// atomic output path.
/// </summary>
public sealed class WorkbenchRawBinaryEditorSession
{
    private readonly RawBinaryEditorSession _editor = new();

    /// <summary>Maximum zero-filled bytes accepted by one insert request.</summary>
    public static int MaximumInsertByteCount => RawBinaryEditorSession.MaximumInsertByteCount;

    /// <summary>Maximum raw-BIN document length supported by the in-memory utility.</summary>
    public static int MaximumDocumentLength => RawBinaryEditorSession.MaximumDocumentLength;

    /// <summary>Gets the normalized source path of the currently loaded document.</summary>
    public string? SourcePath { get; private set; }

    /// <summary>Gets the suggested, non-destructive output file name for the loaded document.</summary>
    public string SuggestedOutputFileName => string.IsNullOrWhiteSpace(SourcePath)
        ? "edited.bin"
        : $"{Path.GetFileNameWithoutExtension(SourcePath)}-edited{Path.GetExtension(SourcePath)}";

    /// <summary>Gets the current in-memory document state.</summary>
    public WorkbenchRawBinaryEditorState State => ToWorkbenchState(_editor.State);

    /// <summary>Reads a source BIN once through the file adapter and starts a fresh in-memory session.</summary>
    public async Task<WorkbenchRawBinaryEditorFileResult> LoadAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return WorkbenchRawBinaryEditorFileResult.Failure("Select a BIN file to open in Hex Editor.");
        }

        try
        {
            string fullPath = Path.GetFullPath(sourcePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return WorkbenchRawBinaryEditorFileResult.Failure("The selected BIN path has no usable parent folder.");
            }

            if (new FileInfo(fullPath).Length > MaximumDocumentLength)
            {
                return WorkbenchRawBinaryEditorFileResult.Failure(
                    $"The selected BIN exceeds the {MaximumDocumentLength} byte Hex Editor limit.");
            }

            var reader = new FileArtifactReader([directory]);
            ReadOnlyMemory<byte> bytes = await reader.ReadAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            _ = _editor.Load(bytes.Span);
            SourcePath = fullPath;
            return WorkbenchRawBinaryEditorFileResult.Success(fullPath, ToWorkbenchState(_editor.CreateViewport(0).State));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return WorkbenchRawBinaryEditorFileResult.Failure("The selected BIN could not be opened.");
        }
    }

    /// <summary>Returns a bounded viewport from the in-memory work buffer.</summary>
    public WorkbenchRawBinaryEditorViewport CreateViewport(string requestedAddress)
    {
        return ToWorkbenchViewport(_editor.CreateViewport(requestedAddress));
    }

    /// <summary>Returns one aligned bounded page from the in-memory work buffer for the document viewport.</summary>
    public WorkbenchRawBinaryEditorViewport CreatePage(long requestedAddress, int maximumRows)
    {
        return ToWorkbenchViewport(_editor.CreatePage(requestedAddress, maximumRows));
    }

    /// <summary>Finds printable ASCII text in the editor-owned memory buffer without reading the source file again.</summary>
    public WorkbenchRawBinaryEditorSearchResult FindAscii(string text, long startOffset)
    {
        return ToWorkbenchSearchResult(_editor.FindAscii(text, startOffset));
    }

    /// <summary>
    /// Finds printable ASCII on a defensive memory snapshot so large searches do not block the UI
    /// or race a later editor mutation.
    /// </summary>
    public async Task<WorkbenchRawBinaryEditorSearchResult> FindAsciiAsync(
        string text,
        long startOffset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        RawBinaryEditorState state = _editor.State;
        if (!_editor.TryCopyWorkingBytes(out byte[]? snapshot))
        {
            return ToWorkbenchSearchResult(new RawBinaryEditorSearchResult(
                state,
                [],
                Issue: new RawBinaryEditorIssue(RawBinaryEditorIssueCode.NoDocument)));
        }

        RawBinaryEditorSearchResult result = await Task.Run(
            () => RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ToWorkbenchSearchResult(result);
    }

    /// <summary>Returns contiguous changed blocks from the editor-owned memory buffer.</summary>
    public IReadOnlyList<WorkbenchRawBinaryEditorChangedRange> GetChangedRanges()
    {
        return [.. _editor.GetChangedRanges().Select(range =>
            new WorkbenchRawBinaryEditorChangedRange(
                range.Start,
                range.EndExclusive,
                ToWorkbenchChangeKind(range.ChangeKind),
                [.. range.ValueChanges.Select(change => new WorkbenchRawBinaryEditorValueChange(
                    change.Start,
                    change.EndExclusive,
                    change.FirstOriginalValue,
                    change.FirstCurrentValue))],
                [.. range.StructuralChanges.Select(change => new WorkbenchRawBinaryEditorStructuralChange(
                    change.Kind == RawBinaryEditorStructuralChangeKind.Insert
                        ? WorkbenchRawBinaryEditorStructuralChangeKind.Insert
                        : WorkbenchRawBinaryEditorStructuralChangeKind.Delete,
                    change.Address,
                    change.Count))]))];
    }

    /// <summary>Writes one byte only to the session-owned work buffer.</summary>
    public WorkbenchRawBinaryEditorOperationResult OverwriteByte(string address, string value)
    {
        return ToWorkbenchResult(_editor.OverwriteByte(address, value));
    }

    /// <summary>Writes a hexadecimal sequence from Start without crossing the selected inclusive End.</summary>
    public WorkbenchRawBinaryEditorOperationResult OverwriteRange(string startAddress, string endAddress, string values)
    {
        return ToWorkbenchResult(_editor.OverwriteRange(startAddress, endAddress, values));
    }

    /// <summary>Fills one inclusive range only in the session-owned work buffer.</summary>
    public WorkbenchRawBinaryEditorOperationResult FillRange(string startAddress, string endAddress, string value)
    {
        return ToWorkbenchResult(_editor.FillRange(startAddress, endAddress, value));
    }

    /// <summary>Inserts an explicit zero byte before the selected working-buffer byte.</summary>
    public WorkbenchRawBinaryEditorOperationResult InsertZeroBefore(string address)
    {
        return ToWorkbenchResult(_editor.InsertZeroBefore(address));
    }

    /// <summary>Inserts an explicit zero byte after the selected working-buffer byte.</summary>
    public WorkbenchRawBinaryEditorOperationResult InsertZeroAfter(string address)
    {
        return ToWorkbenchResult(_editor.InsertZeroAfter(address));
    }

    /// <summary>Inserts a bounded zero-filled run before the selected byte.</summary>
    public WorkbenchRawBinaryEditorOperationResult InsertZeroBytesBefore(string address, int count)
    {
        return ToWorkbenchResult(_editor.InsertZeroBytesBefore(address, count));
    }

    /// <summary>Inserts a bounded zero-filled run after the selected byte.</summary>
    public WorkbenchRawBinaryEditorOperationResult InsertZeroBytesAfter(string address, int count)
    {
        return ToWorkbenchResult(_editor.InsertZeroBytesAfter(address, count));
    }

    /// <summary>Deletes one working-buffer byte and shifts later bytes toward lower offsets.</summary>
    public WorkbenchRawBinaryEditorOperationResult DeleteByte(string address)
    {
        return ToWorkbenchResult(_editor.DeleteByte(address));
    }

    /// <summary>Reverts the most recent session-owned operation.</summary>
    public WorkbenchRawBinaryEditorOperationResult Undo()
    {
        return ToWorkbenchResult(_editor.Undo());
    }

    /// <summary>Reapplies the most recently reverted session-owned operation.</summary>
    public WorkbenchRawBinaryEditorOperationResult Redo()
    {
        return ToWorkbenchResult(_editor.Redo());
    }

    /// <summary>
    /// Exports the current memory buffer through the atomic writer. The loaded source path is never
    /// a valid output target, and output files are never overwritten.
    /// </summary>
    public async Task<WorkbenchRawBinaryEditorFileResult> SaveAsAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || !_editor.TryCopyWorkingBytes(out byte[]? bytes))
        {
            return WorkbenchRawBinaryEditorFileResult.Failure("Open a BIN before exporting an edited copy.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return WorkbenchRawBinaryEditorFileResult.Failure("Choose a new BIN output path.");
        }

        try
        {
            string fullOutputPath = Path.GetFullPath(outputPath);
            if (string.Equals(fullOutputPath, SourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return WorkbenchRawBinaryEditorFileResult.Failure("Save As must use a new path; the opened BIN is read-only.");
            }

            string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
            string outputFileName = Path.GetFileName(fullOutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory) || string.IsNullOrWhiteSpace(outputFileName))
            {
                return WorkbenchRawBinaryEditorFileResult.Failure("Choose a valid new BIN output path.");
            }

            var writer = new AtomicFileCompositionOutputWriter(outputDirectory);
            string savedPath = await writer.CommitAsync(outputFileName, bytes, cancellationToken).ConfigureAwait(false);
            return WorkbenchRawBinaryEditorFileResult.Success(savedPath, ToWorkbenchState(_editor.CreateViewport(0).State));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return WorkbenchRawBinaryEditorFileResult.Failure("The output BIN was not written. Choose a new writable path that does not already exist.");
        }
    }

    private static WorkbenchRawBinaryEditorOperationResult ToWorkbenchResult(RawBinaryEditorOperationResult result)
    {
        return new WorkbenchRawBinaryEditorOperationResult(
            ToWorkbenchState(result.State),
            ToWorkbenchIssue(result.Issue));
    }

    private static WorkbenchRawBinaryEditorViewport ToWorkbenchViewport(RawBinaryEditorViewport viewport)
    {
        return new WorkbenchRawBinaryEditorViewport(
            [.. viewport.Rows.Select(row => new WorkbenchRawBinaryEditorViewportRow(
                row.Address,
                [.. row.Bytes.Select(value => new WorkbenchRawBinaryEditorByte(
                    value.Address,
                    value.OriginalAddress,
                    value.OriginalValue,
                    value.OriginalValueAtAddress,
                    value.CurrentValue,
                    ToWorkbenchChangeKind(value.ChangeKind)))],
                row.OriginalAscii,
                row.CurrentAscii))],
            ToWorkbenchState(viewport.State),
            viewport.Start,
            viewport.Length,
            ToWorkbenchIssue(viewport.Issue));
    }

    private static WorkbenchRawBinaryEditorState ToWorkbenchState(RawBinaryEditorState state)
    {
        return new WorkbenchRawBinaryEditorState(
            state.HasDocument,
            state.OriginalLength,
            state.WorkingLength,
            state.UndoCount,
            state.RedoCount,
            state.HasUnsavedChanges);
    }

    private static WorkbenchRawBinaryEditorSearchResult ToWorkbenchSearchResult(
        RawBinaryEditorSearchResult result)
    {
        return new WorkbenchRawBinaryEditorSearchResult(
            ToWorkbenchState(result.State),
            [.. result.Matches],
            result.MatchIndex,
            result.Length,
            result.Wrapped,
            result.TotalMatchCount,
            result.SelectedAddress,
            result.IsTruncated,
            ToWorkbenchIssue(result.Issue));
    }

    private static WorkbenchRawBinaryEditorChangeKind ToWorkbenchChangeKind(RawBinaryEditorChangeKind changeKind)
    {
        WorkbenchRawBinaryEditorChangeKind result = WorkbenchRawBinaryEditorChangeKind.None;
        if ((changeKind & RawBinaryEditorChangeKind.Data) != 0)
        {
            result |= WorkbenchRawBinaryEditorChangeKind.Data;
        }

        if ((changeKind & RawBinaryEditorChangeKind.Structural) != 0)
        {
            result |= WorkbenchRawBinaryEditorChangeKind.Structural;
        }

        return result;
    }

    private static WorkbenchRawBinaryEditorIssue? ToWorkbenchIssue(RawBinaryEditorIssue? issue)
    {
        return issue is null
            ? null
            : new WorkbenchRawBinaryEditorIssue((WorkbenchRawBinaryEditorIssueCode)issue.Code);
    }
}

/// <summary>One adapter-owned file load or Save As result for the raw-BIN utility.</summary>
public sealed record WorkbenchRawBinaryEditorFileResult(
    bool Succeeded,
    string? Path,
    WorkbenchRawBinaryEditorState? State,
    string? ErrorMessage)
{
    /// <summary>Creates a successful file result.</summary>
    public static WorkbenchRawBinaryEditorFileResult Success(string path, WorkbenchRawBinaryEditorState state)
    {
        return new WorkbenchRawBinaryEditorFileResult(true, path, state, null);
    }

    /// <summary>Creates a user-visible, non-throwing file result.</summary>
    public static WorkbenchRawBinaryEditorFileResult Failure(string errorMessage)
    {
        return new WorkbenchRawBinaryEditorFileResult(false, null, null, errorMessage);
    }
}

/// <summary>Adapter projection of a raw in-memory ASCII search result.</summary>
public sealed record WorkbenchRawBinaryEditorSearchResult(
    WorkbenchRawBinaryEditorState State,
    IReadOnlyList<long> Matches,
    int MatchIndex,
    int Length,
    bool Wrapped,
    int TotalMatchCount,
    long SelectedAddress,
    bool IsTruncated,
    WorkbenchRawBinaryEditorIssue? Issue = null)
{
    /// <summary>True when a matching ASCII sequence was found.</summary>
    public bool Succeeded => Issue is null;

    /// <summary>Address of the currently selected search match, or -1 when no match exists.</summary>
    public long Address => SelectedAddress;
}
