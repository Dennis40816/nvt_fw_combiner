using System.Globalization;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class HexEditorWorkspaceViewModel
{
    private string CreateReadyStatus()
    {
        return string.Format(CultureInfo.InvariantCulture, Text.HexEditorSourceReadyDetail, _state.WorkingLength, _state.UndoCount);
    }

    private string DescribeIssue(WorkbenchRawBinaryEditorIssue issue)
    {
        return issue.Code switch
        {
            WorkbenchRawBinaryEditorIssueCode.NoDocument => Text.HexEditorSourceEmptyDetail,
            WorkbenchRawBinaryEditorIssueCode.InvalidAddress => Text.HexEditorInvalidAddressDetail,
            WorkbenchRawBinaryEditorIssueCode.AddressOutOfRange => Text.HexEditorInvalidAddressDetail,
            WorkbenchRawBinaryEditorIssueCode.InvalidHexByte => Text.HexEditorInvalidByteDetail,
            WorkbenchRawBinaryEditorIssueCode.InvalidHexBytes => Text.HexEditorInvalidByteDetail,
            WorkbenchRawBinaryEditorIssueCode.InvalidRange => Text.HexEditorInvalidRangeDetail,
            WorkbenchRawBinaryEditorIssueCode.NothingToUndo => Text.HexEditorNothingToUndoDetail,
            WorkbenchRawBinaryEditorIssueCode.NothingToRedo => Text.HexEditorNothingToRedoDetail,
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
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               long.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
    }

    partial void OnIsOriginalRowsVisibleChanged(bool value)
    {
        if (HasDocument)
        {
            RefreshViewport();
        }
    }

    partial void OnRangeStartAddressChanged(string value)
    {
        ApplyOverwriteRangeCommand.NotifyCanExecuteChanged();
        ApplyFillRangeCommand.NotifyCanExecuteChanged();
    }

    partial void OnRangeEndAddressChanged(string value)
    {
        ApplyOverwriteRangeCommand.NotifyCanExecuteChanged();
        ApplyFillRangeCommand.NotifyCanExecuteChanged();
    }

    partial void OnRangeValueChanged(string value)
    {
        ApplyOverwriteRangeCommand.NotifyCanExecuteChanged();
        ApplyFillRangeCommand.NotifyCanExecuteChanged();
    }
}
