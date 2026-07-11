using System.Globalization;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class HexEditorWorkspaceViewModel
{
    /// <summary>Loads one BIN once through the Bootstrap adapter into the editor-owned memory buffer.</summary>
    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FindAsciiCommand.Cancel();

        WorkbenchRawBinaryEditorFileResult result = await _session.LoadAsync(path, cancellationToken);
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
        RefreshViewportRows();
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

        WorkbenchRawBinaryEditorFileResult result = await _session.SaveAsAsync(outputPath, cancellationToken);
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
