using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class WorkflowSessionPresentationViewModel
{
    private string? _firmwareNumberMismatchToken;
    private string? _firmwareNumberMismatchSlotId;
    private string? _firmwareNumberMismatchPath;

    /// <summary>True while readable FWConfig chip count suggests another Number selection.</summary>
    [ObservableProperty]
    public partial bool IsFirmwareNumberMismatchModalOpen { get; set; }

    /// <summary>Gets the loaded file name used in the Number mismatch prompt.</summary>
    public string FirmwareNumberMismatchFileName { get; private set; } = string.Empty;

    /// <summary>Gets the currently selected Number label.</summary>
    public string FirmwareNumberMismatchCurrentNumber { get; private set; } = string.Empty;

    /// <summary>Gets the Number label derived by Bootstrap from readable FWConfig chip count.</summary>
    public string FirmwareNumberMismatchDetectedNumber { get; private set; } = string.Empty;

    /// <summary>Gets the readable FWConfig chip count shown to the user.</summary>
    public byte FirmwareNumberMismatchDetectedChipCount { get; private set; }

    /// <summary>Command that switches to the FWConfig-compatible Number selection.</summary>
    public IRelayCommand AcceptFirmwareNumberMismatchCommand { get; }

    /// <summary>Command that keeps the current Number without authorizing a mismatched Build.</summary>
    public IRelayCommand DismissFirmwareNumberMismatchCommand { get; }

    internal void PromptForFirmwareNumberMismatch(
        FirmwareSlotViewModel slot,
        WorkbenchFirmwareContextSuggestion? suggestion)
    {
        if (suggestion is null ||
            !slot.HasFile ||
            string.IsNullOrWhiteSpace(slot.FilePath) ||
            string.Equals(_selectedNumber(), suggestion.NumberToken, StringComparison.Ordinal))
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
        FirmwareNumberMismatchCurrentNumber = GetNumberDisplayLabel(_selectedNumber());
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
            string.Equals(_selectedNumber(), numberToken, StringComparison.Ordinal))
        {
            return;
        }

        _applyDetectedNumber(numberToken);
        _refreshCtrlRamDisplay();
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
        return _numberChoices().FirstOrDefault(choice =>
            string.Equals(choice.Token, numberToken, StringComparison.Ordinal))?.DisplayLabel ?? numberToken;
    }
}
