using System.Globalization;
using NvtFwCombiner.Application.HexEditor;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class HexEditorWorkspaceViewModel
{
    private long _loadGeneration;
    private long _sourceSelectionGeneration;

    public bool HasSelectedFile { get; private set; }

    internal long BeginSourceSelection()
    {
        return Interlocked.Increment(ref _sourceSelectionGeneration);
    }

    internal Task LoadFromSelectionAsync(
        long selectionGeneration,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return selectionGeneration == Volatile.Read(ref _sourceSelectionGeneration)
            ? LoadAsync(path, cancellationToken)
            : Task.CompletedTask;
    }

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _ = Interlocked.Increment(ref _sourceSelectionGeneration);
        long generation = Interlocked.Increment(ref _loadGeneration);
        HasSelectedFile = true;
        FindAsciiCommand.Cancel();

        RawBinaryEditorFileResult result = await _files.LoadAsync(path, cancellationToken);
        if (generation != Volatile.Read(ref _loadGeneration))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded || result.State is null || string.IsNullOrWhiteSpace(result.Path))
        {
            EditorStatus = result.ErrorMessage ?? Text.HexEditorFileOperationFailedDetail;
            return;
        }

        SourcePath = result.Path;
        ViewportAddress = "0x000000";
        AsciiSearchText = string.Empty;
        RangeStartAddress = "0x000000";
        RangeEndAddress = "0x000000";
        RangeValue = string.Empty;
        ClearSelection();
        UpdateState(result.State);
        ResetSearchAndChanges();
        ResetHistoryFeedback();
        ClearEditFeedback();
        CancelInsertBytes();
        RefreshChangeTracking();
        ViewportStartRow = 0;
        RefreshViewportSnapshot();
        EditorStatus = CreateReadyStatus();
    }

    /// <summary>Exports the current memory work buffer as a new BIN and never overwrites the opened source BIN.</summary>
    public async Task SaveAsAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!CanSave)
        {
            return;
        }

        RawBinaryEditorFileResult result = await _files.SaveAsAsync(outputPath, cancellationToken);
        if (!result.Succeeded || result.State is null || string.IsNullOrWhiteSpace(result.Path))
        {
            EditorStatus = result.ErrorMessage ?? Text.HexEditorFileOperationFailedDetail;
            return;
        }

        UpdateState(result.State);
        EditorStatus = string.Format(
            CultureInfo.InvariantCulture,
            Text.HexEditorSaveCompletedDetail,
            FirmwarePathDisplay.Normalize(result.Path));
    }
}
