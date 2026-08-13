using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    private string? _firmwareIcMismatchSlotId;
    private string? _firmwareIcMismatchPath;
    private AcceptedFirmwareMismatchSelection? _acceptedFirmwareMismatchSelection;

    [ObservableProperty]
    public partial bool IsFirmwareIcMismatchModalOpen { get; set; }

    public string FirmwareIcMismatchFileName { get; private set; } = string.Empty;

    /// <summary>Gets the non-authoritative IC marker detected from the selected BIN.</summary>
    public string FirmwareIcMismatchDetectedIc { get; private set; } = string.Empty;

    public string FirmwareIcMismatchCurrentIc => SelectedIc;

    /// <summary>Command that adopts the prompted IC context and retains the selected BIN.</summary>
    public IRelayCommand AcceptFirmwareIcMismatchCommand { get; }

    /// <summary>Command that retains the current IC context despite the prompt.</summary>
    public IRelayCommand DismissFirmwareIcMismatchCommand { get; }

    internal bool ReconcileFirmwareIcMismatch(FirmwareSlotViewModel slot, string? detectedIc)
    {
        if (IsFirmwareIcMismatchModalOpen ||
            !slot.HasFile ||
            string.IsNullOrWhiteSpace(slot.FilePath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(detectedIc) ||
            !IcChoices.Contains(detectedIc, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(detectedIc, SelectedIc, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_compositionServices.Capabilities.ArePerfectFamilyMembers(SelectedIc, detectedIc))
        {
            SelectDetectedFirmwareIc(detectedIc, slot.SlotId, slot.FilePath);
            return true;
        }

        FirmwareIcMismatchFileName = Path.GetFileName(slot.FilePath);
        FirmwareIcMismatchDetectedIc = detectedIc;
        _firmwareIcMismatchSlotId = slot.SlotId;
        _firmwareIcMismatchPath = slot.FilePath;
        OnPropertyChanged(nameof(FirmwareIcMismatchFileName));
        OnPropertyChanged(nameof(FirmwareIcMismatchDetectedIc));
        OnPropertyChanged(nameof(FirmwareIcMismatchCurrentIc));
        IsFirmwareIcMismatchModalOpen = true;
        return false;
    }

    private void AcceptFirmwareIcMismatch()
    {
        IsFirmwareIcMismatchModalOpen = false;
        if (!string.IsNullOrWhiteSpace(FirmwareIcMismatchDetectedIc))
        {
            SelectDetectedFirmwareIc(
                FirmwareIcMismatchDetectedIc,
                _firmwareIcMismatchSlotId,
                _firmwareIcMismatchPath);
        }
        _firmwareIcMismatchSlotId = null;
        _firmwareIcMismatchPath = null;
    }

    private void SelectDetectedFirmwareIc(string detectedIc, string? slotId, string? path)
    {
        _acceptedFirmwareMismatchSelection =
            slotId is not null && path is not null
                ? new AcceptedFirmwareMismatchSelection(slotId, path)
                : null;
        SelectedIc = detectedIc;
    }

    private void DismissFirmwareIcMismatch()
    {
        IsFirmwareIcMismatchModalOpen = false;
        _firmwareIcMismatchSlotId = null;
        _firmwareIcMismatchPath = null;
    }

    internal void InvalidateFirmwareIcMismatch()
    {
        if (IsFirmwareIcMismatchModalOpen)
        {
            IsFirmwareIcMismatchModalOpen = false;
        }

        _firmwareIcMismatchSlotId = null;
        _firmwareIcMismatchPath = null;
        _acceptedFirmwareMismatchSelection = null;
    }

    internal AcceptedFirmwareMismatchSelection? ConsumeAcceptedFirmwareMismatchSelection()
    {
        AcceptedFirmwareMismatchSelection? selection = _acceptedFirmwareMismatchSelection;
        _acceptedFirmwareMismatchSelection = null;
        return selection;
    }
}
