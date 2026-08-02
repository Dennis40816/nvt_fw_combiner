using System.Globalization;
using NvtFwCombiner.Application.HexEditor;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class HexEditorWorkspaceViewModel
{
    private string CreateReadyStatus()
    {
        return string.Format(CultureInfo.InvariantCulture, Text.HexEditorSourceReadyDetail, _state.WorkingLength, _state.UndoCount);
    }

    private string DescribeIssue(RawBinaryEditorIssue issue)
    {
        return issue.Code switch
        {
            RawBinaryEditorIssueCode.NoDocument => Text.HexEditorSourceEmptyDetail,
            RawBinaryEditorIssueCode.InvalidAddress => Text.HexEditorInvalidAddressDetail,
            RawBinaryEditorIssueCode.AddressOutOfRange => Text.HexEditorInvalidAddressDetail,
            RawBinaryEditorIssueCode.InvalidHexByte => Text.HexEditorInvalidByteDetail,
            RawBinaryEditorIssueCode.InvalidHexBytes => Text.HexEditorInvalidByteDetail,
            RawBinaryEditorIssueCode.InvalidRange => Text.HexEditorInvalidRangeDetail,
            RawBinaryEditorIssueCode.InputExceedsRange => Text.HexEditorInputExceedsRangeDetail,
            RawBinaryEditorIssueCode.InvalidByteCount => Text.HexEditorInvalidByteCountDetail,
            RawBinaryEditorIssueCode.NothingToUndo => Text.HexEditorNothingToUndoDetail,
            RawBinaryEditorIssueCode.NothingToRedo => Text.HexEditorNothingToRedoDetail,
            RawBinaryEditorIssueCode.InvalidAsciiText => Text.HexEditorInvalidAsciiSearchDetail,
            RawBinaryEditorIssueCode.AsciiTextNotFound => Text.HexEditorAsciiSearchNotFoundDetail,
            _ => Text.HexEditorFileOperationFailedDetail,
        };
    }

    private static string FormatAddress(long address)
    {
        return FormattableString.Invariant($"0x{address:X6}");
    }

    private static bool TryParseAddressLabel(string value, out long address)
    {
        address = 0;
        return value.StartsWith("0x", StringComparison.Ordinal) &&
               long.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
    }

    partial void OnIsOriginalRowsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(VisibleRowCount));
        OnPropertyChanged(nameof(DocumentScrollMaximum));
        if (HasDocument)
        {
            ViewportStartRow = Math.Min(ViewportStartRow, DocumentScrollMaximum);
            RefreshViewportSnapshot();
        }
    }

    partial void OnRangeStartAddressChanged(string value)
    {
        ClearEditFeedback();
        ApplyOverwriteRangeCommand.NotifyCanExecuteChanged();
        ApplyFillRangeCommand.NotifyCanExecuteChanged();
        ApplyRangeEditCommand.NotifyCanExecuteChanged();
    }

    partial void OnRangeEndAddressChanged(string value)
    {
        ClearEditFeedback();
        ApplyOverwriteRangeCommand.NotifyCanExecuteChanged();
        ApplyFillRangeCommand.NotifyCanExecuteChanged();
        ApplyRangeEditCommand.NotifyCanExecuteChanged();
    }

    partial void OnRangeValueChanged(string value)
    {
        ClearEditFeedback();
        ApplyOverwriteRangeCommand.NotifyCanExecuteChanged();
        ApplyFillRangeCommand.NotifyCanExecuteChanged();
        ApplyRangeEditCommand.NotifyCanExecuteChanged();
    }

    partial void OnAsciiSearchTextChanged(string value)
    {
        FindAsciiCommand.Cancel();
        ClearAsciiSearchResults(refreshViewport: true);
        FindAsciiCommand.NotifyCanExecuteChanged();
    }
}
