namespace NvtFwCombiner.Application.HexEditor;

/// <summary>Creates platform-backed file sessions around Application-owned raw-BIN editors.</summary>
public interface IRawBinaryEditorFileSessionFactory
{
    /// <summary>Creates one file session for the supplied in-memory editor.</summary>
    IRawBinaryEditorFileSession Create(RawBinaryEditorSession editor);
}

/// <summary>Loads, searches, and atomically exports one raw-BIN editor document.</summary>
public interface IRawBinaryEditorFileSession
{
    /// <summary>Gets the normalized source path of the loaded document.</summary>
    string? SourcePath { get; }

    /// <summary>Gets the suggested non-destructive output file name.</summary>
    string SuggestedOutputFileName { get; }

    /// <summary>Loads a source artifact into the in-memory editor.</summary>
    Task<RawBinaryEditorFileResult> LoadAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);

    /// <summary>Searches printable ASCII against a defensive immutable snapshot.</summary>
    Task<RawBinaryEditorSearchResult> FindAsciiAsync(
        string text,
        long startOffset,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically exports the working bytes to a new file.</summary>
    Task<RawBinaryEditorFileResult> SaveAsAsync(
        string outputPath,
        CancellationToken cancellationToken = default);
}

/// <summary>One platform file load or Save As result for the raw-BIN utility.</summary>
public sealed record RawBinaryEditorFileResult(
    bool Succeeded,
    string? Path,
    RawBinaryEditorState? State,
    string? ErrorMessage)
{
    /// <summary>Creates a successful file result.</summary>
    public static RawBinaryEditorFileResult Success(string path, RawBinaryEditorState state)
    {
        return new RawBinaryEditorFileResult(true, path, state, null);
    }

    /// <summary>Creates a user-visible, non-throwing file result.</summary>
    public static RawBinaryEditorFileResult Failure(string errorMessage)
    {
        return new RawBinaryEditorFileResult(false, null, null, errorMessage);
    }
}
