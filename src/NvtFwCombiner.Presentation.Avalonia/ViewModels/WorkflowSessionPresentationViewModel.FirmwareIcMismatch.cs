using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    private AcceptedFirmwareMismatchSelection? _firmwareIcMismatchSelection;
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

    internal bool ReconcileFirmwareIcMismatch(
        WorkflowInspectionContext context,
        FirmwareSlotViewModel slot,
        string? detectedIc)
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
            SelectDetectedFirmwareIc(
                new(context, slot.SlotId, slot.FilePath),
                detectedIc);
            return true;
        }

        FirmwareIcMismatchFileName = Path.GetFileName(slot.FilePath);
        FirmwareIcMismatchDetectedIc = detectedIc;
        _firmwareIcMismatchSelection = new(context, slot.SlotId, slot.FilePath);
        OnPropertyChanged(nameof(FirmwareIcMismatchFileName));
        OnPropertyChanged(nameof(FirmwareIcMismatchDetectedIc));
        OnPropertyChanged(nameof(FirmwareIcMismatchCurrentIc));
        IsFirmwareIcMismatchModalOpen = true;
        return false;
    }

    private void AcceptFirmwareIcMismatch()
    {
        IsFirmwareIcMismatchModalOpen = false;
        if (!string.IsNullOrWhiteSpace(FirmwareIcMismatchDetectedIc) &&
            _firmwareIcMismatchSelection is { } selection)
        {
            SelectDetectedFirmwareIc(selection, FirmwareIcMismatchDetectedIc);
        }
        _firmwareIcMismatchSelection = null;
    }

    private void SelectDetectedFirmwareIc(
        AcceptedFirmwareMismatchSelection selection,
        string detectedIc)
    {
        _acceptedFirmwareMismatchSelection = selection;
        SelectedIc = detectedIc;
    }

    private void DismissFirmwareIcMismatch()
    {
        IsFirmwareIcMismatchModalOpen = false;
        _firmwareIcMismatchSelection = null;
    }

    internal void InvalidateFirmwareIcMismatch()
    {
        if (IsFirmwareIcMismatchModalOpen)
        {
            IsFirmwareIcMismatchModalOpen = false;
        }

        _firmwareIcMismatchSelection = null;
        _acceptedFirmwareMismatchSelection = null;
    }

    internal AcceptedFirmwareMismatchSelection? ConsumeAcceptedFirmwareMismatchSelection()
    {
        AcceptedFirmwareMismatchSelection? selection = _acceptedFirmwareMismatchSelection;
        _acceptedFirmwareMismatchSelection = null;
        return selection;
    }
}
