using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns Replace-page presentation state, commands, and workflow-specific lifetime.</summary>
internal sealed partial class ReplacePresentationViewModel : ObservableObject
{
    private readonly PresentationCompositionServices _compositionServices;
    private readonly ReplaceStateBindings _stateBindings;

    internal ReplacePresentationViewModel(
        PresentationCompositionServices compositionServices,
        ReplaceStateBindings stateBindings)
    {
        _compositionServices = compositionServices ??
            throw new ArgumentNullException(nameof(compositionServices));
        _stateBindings = stateBindings ?? throw new ArgumentNullException(nameof(stateBindings));
        _firmwareInspection = _compositionServices.FirmwareInspection;
        InspectionLifecycles = new(NotifyCommandStateChanged, CtrlRamReplaceMode, GeneralReplaceMode);
        ShowReplaceSelectionCommand = new RelayCommand(ShowReplaceSelection);
        CloseReplaceSelectionCommand = new RelayCommand(CloseReplaceSelection);
        AddGeneralReplaceMappingCommand = new RelayCommand(AddGeneralReplaceMapping);
        PreviewReplaceCommand = new AsyncRelayCommand(
            PreviewReplaceAsync,
            CanRunReplace);
        BuildReplaceCommand = new AsyncRelayCommand(
            () => RunBuildReplaceAsync(outputPath: null, ctrlRamFirmwareVersionEdit: null),
            () => CanBuildReplace);
        SelectCtrlRamFirmwareVersionPreserveCommand = new RelayCommand(SelectCtrlRamFirmwareVersionPreserve);
        SelectCtrlRamFirmwareVersionEditCommand = new RelayCommand(SelectCtrlRamFirmwareVersionEdit);
        CloseCtrlRamFirmwareVersionCommand = new RelayCommand(CloseCtrlRamFirmwareVersionModal);
    }

    /// <summary>Gets the current localized text used by Replace-only presentation.</summary>
    public ShellTextResources Text => _stateBindings.Text();

    public IRelayCommand ShowReplaceSelectionCommand { get; }

    public IRelayCommand CloseReplaceSelectionCommand { get; }

    internal void ApplyLanguageChanged()
    {
        ApplyFirmwareSlotText();
        RefreshReplaceSlotGroups();
        InspectionLifecycles.ForEach(lifecycle => lifecycle.ApplyText(Text));
        RefreshDpReplaceInputSelectionReadiness();
        foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings)
        {
            mapping.SetFileSelectionAvailability(
                mapping.CanSelectFile,
                Text.FirmwareSlotPendingFactDetail);
        }
        RelocalizeReplaceMemoryMapState();
        OnPropertyChanged(nameof(Text));
        NotifyContextChanged();
        RefreshSelectionState();
    }
}
