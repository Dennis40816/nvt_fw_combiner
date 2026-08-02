using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.HexEditor;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class HexEditorWorkspaceViewModel
{
    /// <summary>True while a context action requests a bounded multi-byte insertion.</summary>
    [ObservableProperty]
    public partial bool IsInsertBytesPromptOpen { get; set; }

    /// <summary>Selected decimal number of zero-filled bytes to insert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitInsertBytes))]
    public partial decimal InsertByteCount { get; set; } = 1;

    /// <summary>Context-selected current memory address used as the insertion anchor.</summary>
    [ObservableProperty]
    public partial string InsertTargetAddress { get; set; } = string.Empty;

    /// <summary>Inline validation owned by the structural insert modal.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInsertBytesFeedback))]
    public partial string InsertBytesFeedback { get; set; } = string.Empty;

    private bool InsertBeforeSelectedByte { get; set; } = true;

    /// <summary>Maximum count shared from the Application raw-editor contract.</summary>
    public decimal MaximumInsertByteCount { get; } = RawBinaryEditorSession.MaximumInsertByteCount;

    /// <summary>Localized modal title for the selected before/after direction.</summary>
    public string InsertBytesPromptTitle => InsertBeforeSelectedByte
        ? Text.HexEditorInsertBytesBeforeTitle
        : Text.HexEditorInsertBytesAfterTitle;

    /// <summary>Localized display of the bounded insertion count.</summary>
    public string InsertBytesMaximumLabel => string.Format(
        CultureInfo.CurrentCulture,
        Text.HexEditorInsertBytesMaximumTemplate,
        MaximumInsertByteCount);

    /// <summary>True when the insertion count is a whole number inside the Application limit.</summary>
    public bool CanSubmitInsertBytes =>
        InsertByteCount == decimal.Truncate(InsertByteCount) &&
        InsertByteCount >= 1 &&
        InsertByteCount <= MaximumInsertByteCount &&
        !string.IsNullOrWhiteSpace(InsertTargetAddress);

    /// <summary>True when the insert modal has a validation result to show.</summary>
    public bool HasInsertBytesFeedback => !string.IsNullOrWhiteSpace(InsertBytesFeedback);

    /// <summary>Requests a bounded zero-filled insert before the selected byte.</summary>
    public IRelayCommand<long> RequestInsertBytesBeforeCommand { get; }

    /// <summary>Requests a bounded zero-filled insert after the selected byte.</summary>
    public IRelayCommand<long> RequestInsertBytesAfterCommand { get; }

    /// <summary>Applies the current bounded zero-filled insert as one undoable operation.</summary>
    public IRelayCommand ConfirmInsertBytesCommand { get; }

    /// <summary>Closes the insert prompt without changing memory.</summary>
    public IRelayCommand CancelInsertBytesCommand { get; }

    private void RequestInsertBytesBefore(long address)
    {
        RequestInsertBytes(address, before: true);
    }

    private void RequestInsertBytesAfter(long address)
    {
        RequestInsertBytes(address, before: false);
    }

    private void RequestInsertBytes(long address, bool before)
    {
        if (!TryGetRowIndex(address, out _))
        {
            return;
        }

        InsertBeforeSelectedByte = before;
        InsertTargetAddress = FormatAddress(address);
        InsertByteCount = 1;
        InsertBytesFeedback = string.Empty;
        IsInsertBytesPromptOpen = true;
        OnPropertyChanged(nameof(InsertBytesPromptTitle));
        ConfirmInsertBytesCommand.NotifyCanExecuteChanged();
    }

    private bool CanConfirmInsertBytes()
    {
        return IsInsertBytesPromptOpen && CanSubmitInsertBytes;
    }

    private void ConfirmInsertBytes()
    {
        if (!CanSubmitInsertBytes)
        {
            InsertBytesFeedback = Text.HexEditorInvalidByteCountDetail;
            return;
        }

        int count = decimal.ToInt32(InsertByteCount);
        RawBinaryEditorOperationResult result = InsertBeforeSelectedByte
            ? _editor.InsertZeroBytesBefore(InsertTargetAddress, count)
            : _editor.InsertZeroBytesAfter(InsertTargetAddress, count);
        if (!result.Succeeded)
        {
            InsertBytesFeedback = DescribeIssue(result.Issue!);
            return;
        }

        string selectedAddress = InsertTargetAddress;
        if (!InsertBeforeSelectedByte && TryParseAddressLabel(InsertTargetAddress, out long anchor))
        {
            selectedAddress = FormatAddress(checked(anchor + 1));
        }

        IsInsertBytesPromptOpen = false;
        InsertBytesFeedback = string.Empty;
        ApplySuccessfulOperation(result, selectedAddress);
    }

    private void CancelInsertBytes()
    {
        IsInsertBytesPromptOpen = false;
        InsertBytesFeedback = string.Empty;
    }

    partial void OnInsertByteCountChanged(decimal value)
    {
        InsertBytesFeedback = string.Empty;
        ConfirmInsertBytesCommand.NotifyCanExecuteChanged();
    }

    partial void OnInsertTargetAddressChanged(string value)
    {
        ConfirmInsertBytesCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInsertBytesPromptOpenChanged(bool value)
    {
        ConfirmInsertBytesCommand.NotifyCanExecuteChanged();
    }
}
