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
    private readonly Func<byte[], RawBinaryEditorState, string, long, CancellationToken, RawBinaryEditorSearchResult>
        _asciiSearch;
    private AsciiSearchSnapshot? _asciiSearchSnapshot;
    private int _asciiSearchSnapshotCaptureCount;
    private long _asciiSearchSnapshotRevision;

    /// <summary>Creates one raw-BIN editor host session.</summary>
    public WorkbenchRawBinaryEditorSession()
        : this(static (snapshot, state, text, startOffset, cancellationToken) =>
            RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken))
    {
    }

    internal WorkbenchRawBinaryEditorSession(
        Func<byte[], RawBinaryEditorState, string, long, CancellationToken, RawBinaryEditorSearchResult> asciiSearch)
    {
        _asciiSearch = asciiSearch ?? throw new ArgumentNullException(nameof(asciiSearch));
    }

    internal int AsciiSearchSnapshotCaptureCount => Volatile.Read(ref _asciiSearchSnapshotCaptureCount);

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
    public RawBinaryEditorState State => _editor.State;

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
            InvalidateAsciiSearchSnapshot();
            _ = _editor.Load(bytes.Span);
            SourcePath = fullPath;
            return WorkbenchRawBinaryEditorFileResult.Success(fullPath, _editor.State);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return WorkbenchRawBinaryEditorFileResult.Failure("The selected BIN could not be opened.");
        }
    }

    /// <summary>Returns a bounded viewport from the in-memory work buffer.</summary>
    public RawBinaryEditorViewport CreateViewport(string requestedAddress)
    {
        return _editor.CreateViewport(requestedAddress);
    }

    /// <summary>Returns one aligned bounded page from the in-memory work buffer for the document viewport.</summary>
    public RawBinaryEditorViewport CreatePage(long requestedAddress, int maximumRows)
    {
        return _editor.CreatePage(requestedAddress, maximumRows);
    }

    /// <summary>Finds printable ASCII text in the editor-owned memory buffer without reading the source file again.</summary>
    public RawBinaryEditorSearchResult FindAscii(string text, long startOffset)
    {
        ArgumentNullException.ThrowIfNull(text);
        RawBinaryEditorState state = _editor.State;
        AsciiSearchSnapshot? snapshot = GetOrCreateAsciiSearchSnapshot();
        return snapshot is null
            ? _editor.FindAscii(text, startOffset)
            : _asciiSearch(snapshot.Bytes, state, text, startOffset, CancellationToken.None);
    }

    /// <summary>
    /// Finds printable ASCII on a defensive memory snapshot so large searches do not block the UI
    /// or race a later editor mutation.
    /// </summary>
    public async Task<RawBinaryEditorSearchResult> FindAsciiAsync(
        string text,
        long startOffset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        RawBinaryEditorState state = _editor.State;
        if (state.HasDocument)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        AsciiSearchSnapshot? snapshot = GetOrCreateAsciiSearchSnapshot();
        if (snapshot is null)
        {
            return new RawBinaryEditorSearchResult(
                state,
                [],
                Issue: new RawBinaryEditorIssue(RawBinaryEditorIssueCode.NoDocument));
        }

        RawBinaryEditorSearchResult result = await Task.Run(
            () =>
            {
                ThrowIfAsciiSearchSnapshotInvalidated(snapshot);
                return _asciiSearch(snapshot.Bytes, state, text, startOffset, cancellationToken);
            },
            cancellationToken).ConfigureAwait(false);
        ThrowIfAsciiSearchSnapshotInvalidated(snapshot);
        return result;
    }

    /// <summary>Returns contiguous changed blocks from the editor-owned memory buffer.</summary>
    public IReadOnlyList<RawBinaryEditorChangedRange> GetChangedRanges()
    {
        return _editor.GetChangedRanges();
    }

    /// <summary>Writes one byte only to the session-owned work buffer.</summary>
    public RawBinaryEditorOperationResult OverwriteByte(string address, string value)
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.OverwriteByte(address, value);
    }

    /// <summary>Writes a hexadecimal sequence from Start without crossing the selected inclusive End.</summary>
    public RawBinaryEditorOperationResult OverwriteRange(string startAddress, string endAddress, string values)
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.OverwriteRange(startAddress, endAddress, values);
    }

    /// <summary>Fills one inclusive range only in the session-owned work buffer.</summary>
    public RawBinaryEditorOperationResult FillRange(string startAddress, string endAddress, string value)
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.FillRange(startAddress, endAddress, value);
    }

    /// <summary>Inserts an explicit zero byte before the selected working-buffer byte.</summary>
    public RawBinaryEditorOperationResult InsertZeroBefore(string address)
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.InsertZeroBefore(address);
    }

    /// <summary>Inserts an explicit zero byte after the selected working-buffer byte.</summary>
    public RawBinaryEditorOperationResult InsertZeroAfter(string address)
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.InsertZeroAfter(address);
    }

    /// <summary>Inserts a bounded zero-filled run before the selected byte.</summary>
    public RawBinaryEditorOperationResult InsertZeroBytesBefore(string address, int count)
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.InsertZeroBytesBefore(address, count);
    }

    /// <summary>Inserts a bounded zero-filled run after the selected byte.</summary>
    public RawBinaryEditorOperationResult InsertZeroBytesAfter(string address, int count)
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.InsertZeroBytesAfter(address, count);
    }

    /// <summary>Deletes one working-buffer byte and shifts later bytes toward lower offsets.</summary>
    public RawBinaryEditorOperationResult DeleteByte(string address)
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.DeleteByte(address);
    }

    /// <summary>Reverts the most recent session-owned operation.</summary>
    public RawBinaryEditorOperationResult Undo()
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.Undo();
    }

    /// <summary>Reapplies the most recently reverted session-owned operation.</summary>
    public RawBinaryEditorOperationResult Redo()
    {
        InvalidateAsciiSearchSnapshot();
        return _editor.Redo();
    }

    private AsciiSearchSnapshot? GetOrCreateAsciiSearchSnapshot()
    {
        if (_asciiSearchSnapshot is null && _editor.TryCopyWorkingBytes(out byte[]? snapshot))
        {
            _asciiSearchSnapshot = new AsciiSearchSnapshot(
                snapshot!,
                Volatile.Read(ref _asciiSearchSnapshotRevision));
            _ = Interlocked.Increment(ref _asciiSearchSnapshotCaptureCount);
        }

        return _asciiSearchSnapshot;
    }

    private void InvalidateAsciiSearchSnapshot()
    {
        _ = Interlocked.Increment(ref _asciiSearchSnapshotRevision);
        _asciiSearchSnapshot = null;
    }

    private void ThrowIfAsciiSearchSnapshotInvalidated(AsciiSearchSnapshot snapshot)
    {
        if (snapshot.Revision != Volatile.Read(ref _asciiSearchSnapshotRevision))
        {
            throw new OperationCanceledException("The raw-BIN document changed while the ASCII search was running.");
        }
    }

    private sealed record AsciiSearchSnapshot(byte[] Bytes, long Revision);

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
            return WorkbenchRawBinaryEditorFileResult.Success(savedPath, _editor.State);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return WorkbenchRawBinaryEditorFileResult.Failure("The output BIN was not written. Choose a new writable path that does not already exist.");
        }
    }

}

/// <summary>One adapter-owned file load or Save As result for the raw-BIN utility.</summary>
public sealed record WorkbenchRawBinaryEditorFileResult(
    bool Succeeded,
    string? Path,
    RawBinaryEditorState? State,
    string? ErrorMessage)
{
    /// <summary>Creates a successful file result.</summary>
    public static WorkbenchRawBinaryEditorFileResult Success(string path, RawBinaryEditorState state)
    {
        return new WorkbenchRawBinaryEditorFileResult(true, path, state, null);
    }

    /// <summary>Creates a user-visible, non-throwing file result.</summary>
    public static WorkbenchRawBinaryEditorFileResult Failure(string errorMessage)
    {
        return new WorkbenchRawBinaryEditorFileResult(false, null, null, errorMessage);
    }
}
