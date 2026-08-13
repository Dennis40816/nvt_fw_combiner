using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    private string? _firmwareNumberMismatchToken;
    private string? _firmwareNumberMismatchSlotId;
    private string? _firmwareNumberMismatchPath;

    [ObservableProperty]
    public partial bool IsFirmwareNumberMismatchModalOpen { get; set; }

    public string FirmwareNumberMismatchFileName { get; private set; } = string.Empty;

    public string FirmwareNumberMismatchCurrentNumber { get; private set; } = string.Empty;

    public string FirmwareNumberMismatchDetectedNumber { get; private set; } = string.Empty;

    public byte FirmwareNumberMismatchDetectedChipCount { get; private set; }

    public IRelayCommand AcceptFirmwareNumberMismatchCommand { get; }

    /// <summary>Command that keeps the current Number without authorizing a mismatched Build.</summary>
    public IRelayCommand DismissFirmwareNumberMismatchCommand { get; }

    internal void PromptForFirmwareNumberMismatch(
        FirmwareSlotViewModel slot,
        FirmwareContextSuggestion? suggestion)
    {
        if (suggestion is null ||
            !slot.HasFile ||
            string.IsNullOrWhiteSpace(slot.FilePath) ||
            string.Equals(SelectedNumber, suggestion.NumberToken, StringComparison.Ordinal))
        {
            return;
        }

        if (IsFirmwareNumberMismatchModalOpen &&
            string.Equals(_firmwareNumberMismatchSlotId, slot.SlotId, StringComparison.Ordinal) &&
            string.Equals(_firmwareNumberMismatchPath, slot.FilePath, StringComparison.Ordinal) &&
            string.Equals(_firmwareNumberMismatchToken, suggestion.NumberToken, StringComparison.Ordinal) &&
            FirmwareNumberMismatchDetectedChipCount == suggestion.ChipNumber)
        {
            return;
        }

        _firmwareNumberMismatchToken = suggestion.NumberToken;
        _firmwareNumberMismatchSlotId = slot.SlotId;
        _firmwareNumberMismatchPath = slot.FilePath;
        FirmwareNumberMismatchFileName = Path.GetFileName(slot.FilePath);
        FirmwareNumberMismatchCurrentNumber = GetNumberDisplayLabel(SelectedNumber);
        FirmwareNumberMismatchDetectedNumber = GetNumberDisplayLabel(suggestion.NumberToken);
        FirmwareNumberMismatchDetectedChipCount = suggestion.ChipNumber;
        OnPropertyChanged(nameof(FirmwareNumberMismatchFileName));
        OnPropertyChanged(nameof(FirmwareNumberMismatchCurrentNumber));
        OnPropertyChanged(nameof(FirmwareNumberMismatchDetectedNumber));
        OnPropertyChanged(nameof(FirmwareNumberMismatchDetectedChipCount));
        IsFirmwareNumberMismatchModalOpen = true;
    }

    private void AcceptFirmwareNumberMismatch()
    {
        string? numberToken = _firmwareNumberMismatchToken;
        byte detectedChipCount = FirmwareNumberMismatchDetectedChipCount;
        InvalidateFirmwareNumberMismatch();
        if (string.IsNullOrWhiteSpace(numberToken) ||
            string.Equals(SelectedNumber, numberToken, StringComparison.Ordinal))
        {
            return;
        }

        IsApplyingFirmwareInspectionContext = true;
        try
        {
            SelectedNumber = numberToken;
        }
        finally
        {
            IsApplyingFirmwareInspectionContext = false;
        }
        _showToast(
            Text.ContextUpdatedToastTitle,
            Text.FormatVerifiedFirmwareContextToast(
                GetNumberDisplayLabel(numberToken),
                detectedChipCount));
    }

    private void DismissFirmwareNumberMismatch()
    {
        InvalidateFirmwareNumberMismatch();
    }

    internal void InvalidateFirmwareNumberMismatch()
    {
        if (IsFirmwareNumberMismatchModalOpen)
        {
            IsFirmwareNumberMismatchModalOpen = false;
        }

        _firmwareNumberMismatchToken = null;
        _firmwareNumberMismatchSlotId = null;
        _firmwareNumberMismatchPath = null;
    }

    private string GetNumberDisplayLabel(string numberToken)
    {
        return NumberSelectionChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, numberToken, StringComparison.Ordinal))?.DisplayLabel ?? numberToken;
    }
}
