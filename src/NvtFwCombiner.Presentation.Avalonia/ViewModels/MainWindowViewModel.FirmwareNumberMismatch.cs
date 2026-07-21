using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string? _firmwareNumberMismatchToken;

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

    private void PromptForFirmwareNumberMismatch(
        FirmwareSlotViewModel slot,
        WorkbenchFirmwareContextSuggestion? suggestion)
    {
        if (IsFirmwareNumberMismatchModalOpen ||
            suggestion is null ||
            !slot.HasFile ||
            string.IsNullOrWhiteSpace(slot.FilePath) ||
            string.Equals(SelectedNumber, suggestion.NumberToken, StringComparison.Ordinal))
        {
            return;
        }

        _firmwareNumberMismatchToken = suggestion.NumberToken;
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

        _isApplyingFirmwareInspectionContext = true;
        try
        {
            SelectedNumber = numberToken;
        }
        finally
        {
            _isApplyingFirmwareInspectionContext = false;
        }

        RefreshCtrlRamDisplayFromInspection();
        SetShellToast(
            Text.ContextUpdatedToastTitle,
            Text.FormatVerifiedFirmwareContextToast(
                GetNumberDisplayLabel(numberToken),
                detectedChipCount));
    }

    private void DismissFirmwareNumberMismatch()
    {
        InvalidateFirmwareNumberMismatch();
    }

    private void InvalidateFirmwareNumberMismatch()
    {
        if (IsFirmwareNumberMismatchModalOpen)
        {
            IsFirmwareNumberMismatchModalOpen = false;
        }

        _firmwareNumberMismatchToken = null;
    }

    private string GetNumberDisplayLabel(string numberToken)
    {
        return NumberSelectionChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, numberToken, StringComparison.Ordinal))?.DisplayLabel ?? numberToken;
    }
}
