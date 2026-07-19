using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Host-owned file adapter for an application-owned raw-BIN memory editor. It performs source
/// loading, defensive-snapshot search scheduling, and atomic export without forwarding edits.
/// </summary>
public sealed class WorkbenchRawBinaryEditorSession
{
    private readonly RawBinaryEditorSession _editor;

    /// <summary>Creates a file adapter for one application-owned memory editor.</summary>
    public WorkbenchRawBinaryEditorSession(RawBinaryEditorSession editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _editor = editor;
    }

    /// <summary>Gets the normalized source path of the currently loaded document.</summary>
    public string? SourcePath { get; private set; }

    /// <summary>Gets the suggested, non-destructive output file name for the loaded document.</summary>
    public string SuggestedOutputFileName => string.IsNullOrWhiteSpace(SourcePath)
        ? "edited.bin"
        : $"{Path.GetFileNameWithoutExtension(SourcePath)}-edited{Path.GetExtension(SourcePath)}";

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

            if (new FileInfo(fullPath).Length > RawBinaryEditorSession.MaximumDocumentLength)
            {
                return WorkbenchRawBinaryEditorFileResult.Failure(
                    $"The selected BIN exceeds the {RawBinaryEditorSession.MaximumDocumentLength} byte Hex Editor limit.");
            }

            var reader = new FileArtifactReader([directory]);
            ReadOnlyMemory<byte> bytes = await reader.ReadAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            _ = _editor.Load(bytes.Span);
            SourcePath = fullPath;
            return WorkbenchRawBinaryEditorFileResult.Success(fullPath, _editor.State);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return WorkbenchRawBinaryEditorFileResult.Failure("The selected BIN could not be opened.");
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
        if (!_editor.TryCopyWorkingBytes(out byte[]? snapshot))
        {
            return new RawBinaryEditorSearchResult(
                state,
                [],
                Issue: new RawBinaryEditorIssue(RawBinaryEditorIssueCode.NoDocument));
        }

        RawBinaryEditorSearchResult result = await Task.Run(
            () => RawBinaryEditorSearch.Find(snapshot, state, text, startOffset, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return result;
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
