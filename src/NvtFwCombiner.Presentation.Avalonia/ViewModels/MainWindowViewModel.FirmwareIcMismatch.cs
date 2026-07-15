using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private FirmwareSlotViewModel? _firmwareIcMismatchSlot;
    /// <summary>True while a loaded BIN suggests a different IC context.</summary>
    [ObservableProperty]
    public partial bool IsFirmwareIcMismatchModalOpen { get; set; }

    /// <summary>Gets the loaded file name used in the mismatch prompt.</summary>
    public string FirmwareIcMismatchFileName { get; private set; } = string.Empty;

    /// <summary>Gets the non-authoritative IC marker detected from the selected BIN.</summary>
    public string FirmwareIcMismatchDetectedIc { get; private set; } = string.Empty;

    /// <summary>Gets the currently selected workbench IC.</summary>
    public string FirmwareIcMismatchCurrentIc => SelectedIc;

    /// <summary>Command that adopts the prompted IC context and retains the selected BIN.</summary>
    public IRelayCommand AcceptFirmwareIcMismatchCommand { get; }

    /// <summary>Command that retains the current IC context despite the prompt.</summary>
    public IRelayCommand DismissFirmwareIcMismatchCommand { get; }

    private void PromptForFirmwareIcMismatch(FirmwareSlotViewModel slot)
    {
        if (!slot.HasFile || string.IsNullOrWhiteSpace(slot.FilePath))
        {
            return;
        }

        string? detectedIc = FirmwareIcIdentifierDetector.TryDetect(slot.FilePath);
        if (string.IsNullOrWhiteSpace(detectedIc) ||
            !IcChoices.Contains(detectedIc, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(detectedIc, SelectedIc, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        FirmwareIcMismatchFileName = Path.GetFileName(slot.FilePath);
        FirmwareIcMismatchDetectedIc = detectedIc;
        _firmwareIcMismatchSlot = slot;
        OnPropertyChanged(nameof(FirmwareIcMismatchFileName));
        OnPropertyChanged(nameof(FirmwareIcMismatchDetectedIc));
        OnPropertyChanged(nameof(FirmwareIcMismatchCurrentIc));
        IsFirmwareIcMismatchModalOpen = true;
    }

    private void AcceptFirmwareIcMismatch()
    {
        if (!string.IsNullOrWhiteSpace(FirmwareIcMismatchDetectedIc))
        {
            SelectedIc = FirmwareIcMismatchDetectedIc;
        }

        IsFirmwareIcMismatchModalOpen = false;
        if (_firmwareIcMismatchSlot is { } slot)
        {
            TryApplyVerifiedFirmwareContext(slot);
        }

        _firmwareIcMismatchSlot = null;
    }

    private void DismissFirmwareIcMismatch()
    {
        IsFirmwareIcMismatchModalOpen = false;
        _firmwareIcMismatchSlot = null;
    }
}
