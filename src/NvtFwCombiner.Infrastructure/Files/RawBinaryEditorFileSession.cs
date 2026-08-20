using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>
/// Host-owned file adapter for an application-owned raw-BIN memory editor. It performs source
/// loading, defensive-snapshot search scheduling, and atomic export without forwarding edits.
/// </summary>
public sealed class RawBinaryEditorFileSession : IRawBinaryEditorFileSession
{
    private readonly RawBinaryEditorSession _editor;
    private readonly Func<byte[], RawBinaryEditorState, string, long, CancellationToken, RawBinaryEditorSearchResult>
        _asciiSearch;
    private AsciiSearchResultCache? _asciiSearchResultCache;
    private AsciiSearchSnapshot? _asciiSearchSnapshot;
    private int _asciiSearchSnapshotCaptureCount;

    internal RawBinaryEditorFileSession(
        Func<byte[], RawBinaryEditorState, string, long, CancellationToken, RawBinaryEditorSearchResult> asciiSearch)
        : this(new RawBinaryEditorSession(), asciiSearch)
    {
    }

    internal RawBinaryEditorFileSession(
        RawBinaryEditorSession editor,
        Func<byte[], RawBinaryEditorState, string, long, CancellationToken, RawBinaryEditorSearchResult> asciiSearch)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _asciiSearch = asciiSearch ?? throw new ArgumentNullException(nameof(asciiSearch));
    }

    internal int AsciiSearchSnapshotCaptureCount => Volatile.Read(ref _asciiSearchSnapshotCaptureCount);

    /// <summary>Creates a file adapter for one application-owned memory editor.</summary>
    public RawBinaryEditorFileSession(RawBinaryEditorSession editor)
        : this(
            editor,
            static (snapshot, state, text, startOffset, cancellationToken) =>
                RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken))
    {
    }

    /// <summary>Gets the normalized source path of the currently loaded document.</summary>
    public string? SourcePath { get; private set; }

    /// <summary>Gets the suggested, non-destructive output file name for the loaded document.</summary>
    public string SuggestedOutputFileName => string.IsNullOrWhiteSpace(SourcePath)
        ? "edited.bin"
        : $"{Path.GetFileNameWithoutExtension(SourcePath)}-edited{Path.GetExtension(SourcePath)}";

    /// <summary>Reads a source BIN once through the file adapter and starts a fresh in-memory session.</summary>
    public async Task<RawBinaryEditorFileResult> LoadAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return RawBinaryEditorFileResult.Failure("Select a BIN file to open in Hex Editor.");
        }

        try
        {
            string fullPath = Path.GetFullPath(sourcePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return RawBinaryEditorFileResult.Failure("The selected BIN path has no usable parent folder.");
            }

            if (new FileInfo(fullPath).Length > RawBinaryEditorSession.MaximumDocumentLength)
            {
                return RawBinaryEditorFileResult.Failure(
                    $"The selected BIN exceeds the {RawBinaryEditorSession.MaximumDocumentLength} byte Hex Editor limit.");
            }

            var reader = new FileArtifactReader([directory]);
            ReadOnlyMemory<byte> bytes = await reader.ReadAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            InvalidateAsciiSearchSnapshot();
            _ = _editor.Load(bytes.Span);
            SourcePath = fullPath;
            return RawBinaryEditorFileResult.Success(fullPath, _editor.State);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return RawBinaryEditorFileResult.Failure("The selected BIN could not be opened.");
        }
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

        AsciiSearchSnapshot? snapshot = GetOrCreateAsciiSearchSnapshot(state);
        if (snapshot is null)
        {
            return new RawBinaryEditorSearchResult(
                state,
                [],
                Issue: new RawBinaryEditorIssue(RawBinaryEditorIssueCode.NoDocument));
        }

        if (TryFindCachedAsciiResult(snapshot, text, startOffset, out RawBinaryEditorSearchResult cached))
        {
            ThrowIfAsciiSearchSnapshotInvalidated(snapshot);
            return cached;
        }

        RawBinaryEditorSearchResult result = await Task.Run(
            () =>
            {
                ThrowIfAsciiSearchSnapshotInvalidated(snapshot);
                return _asciiSearch(snapshot.Bytes, state, text, startOffset, cancellationToken);
            },
            cancellationToken).ConfigureAwait(false);
        ThrowIfAsciiSearchSnapshotInvalidated(snapshot);
        return CacheAsciiSearchResult(snapshot, text, result);
    }

    private AsciiSearchSnapshot? GetOrCreateAsciiSearchSnapshot(RawBinaryEditorState state)
    {
        if ((_asciiSearchSnapshot is null || _asciiSearchSnapshot.State != state) &&
            _editor.TryCopyWorkingBytes(out byte[]? snapshot))
        {
            _asciiSearchSnapshot = new AsciiSearchSnapshot(snapshot!, state);
            _ = Interlocked.Increment(ref _asciiSearchSnapshotCaptureCount);
        }

        return _asciiSearchSnapshot;
    }

    private void InvalidateAsciiSearchSnapshot()
    {
        _asciiSearchSnapshot = null;
        Volatile.Write(ref _asciiSearchResultCache, null);
    }

    private void ThrowIfAsciiSearchSnapshotInvalidated(AsciiSearchSnapshot snapshot)
    {
        if (!ReferenceEquals(snapshot, _asciiSearchSnapshot) || snapshot.State != _editor.State)
        {
            throw new OperationCanceledException("The raw-BIN document changed while the ASCII search was running.");
        }
    }

    private bool TryFindCachedAsciiResult(
        AsciiSearchSnapshot snapshot,
        string text,
        long startOffset,
        out RawBinaryEditorSearchResult result)
    {
        result = null!;
        AsciiSearchResultCache? cache = Volatile.Read(ref _asciiSearchResultCache);
        return cache is not null &&
            ReferenceEquals(cache.Snapshot, snapshot) &&
            StringComparer.Ordinal.Equals(cache.Text, text) &&
            RawBinaryEditorSearch.TrySelectFromAnchoredResult(cache.Result, startOffset, out result);
    }

    private RawBinaryEditorSearchResult CacheAsciiSearchResult(
        AsciiSearchSnapshot snapshot,
        string text,
        RawBinaryEditorSearchResult result)
    {
        bool canAnchor = !result.Succeeded ||
            (result.MatchIndex == 0 &&
             result.Matches.Count > 0 &&
             result.Matches[0] == result.SelectedAddress);
        if (!canAnchor)
        {
            return result;
        }

        RawBinaryEditorSearchResult stable = result with
        {
            Matches = Array.AsReadOnly(result.Matches.ToArray()),
        };
        Volatile.Write(ref _asciiSearchResultCache, new AsciiSearchResultCache(text, snapshot, stable));
        return stable;
    }

    private sealed record AsciiSearchSnapshot(byte[] Bytes, RawBinaryEditorState State);

    private sealed record AsciiSearchResultCache(
        string Text,
        AsciiSearchSnapshot Snapshot,
        RawBinaryEditorSearchResult Result);
    /// <summary>
    /// Exports the current memory buffer through the atomic writer. The loaded source path is never
    /// a valid output target, and output files are never overwritten.
    /// </summary>
    public async Task<RawBinaryEditorFileResult> SaveAsAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || !_editor.TryCopyWorkingBytes(out byte[]? bytes))
        {
            return RawBinaryEditorFileResult.Failure("Open a BIN before exporting an edited copy.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return RawBinaryEditorFileResult.Failure("Choose a new BIN output path.");
        }

        try
        {
            string fullOutputPath = Path.GetFullPath(outputPath);
            if (string.Equals(fullOutputPath, SourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return RawBinaryEditorFileResult.Failure("Save As must use a new path; the opened BIN is read-only.");
            }

            string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
            string outputFileName = Path.GetFileName(fullOutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory) || string.IsNullOrWhiteSpace(outputFileName))
            {
                return RawBinaryEditorFileResult.Failure("Choose a valid new BIN output path.");
            }

            var writer = new AtomicFileCompositionOutputWriter(outputDirectory);
            CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
                    outputFileName,
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);
            string savedPath = receipt.OutputId;
            return RawBinaryEditorFileResult.Success(savedPath, _editor.State);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return RawBinaryEditorFileResult.Failure("The output BIN was not written. Choose a new writable path that does not already exist.");
        }
    }

}

/// <summary>Creates platform-backed raw-BIN file sessions.</summary>
public sealed class RawBinaryEditorFileSessionFactory : IRawBinaryEditorFileSessionFactory
{
    /// <inheritdoc />
    public IRawBinaryEditorFileSession Create(RawBinaryEditorSession editor)
    {
        return new RawBinaryEditorFileSession(editor);
    }
}
