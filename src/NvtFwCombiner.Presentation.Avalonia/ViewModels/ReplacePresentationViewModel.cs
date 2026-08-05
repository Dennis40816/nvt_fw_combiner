using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns Replace-page presentation state, commands, and workflow-specific lifetime.</summary>
public sealed partial class ReplacePresentationViewModel : ObservableObject
{
    private readonly PresentationCompositionServices _compositionServices;
    private readonly ReplaceStateBindings _stateBindings;

    internal ReplacePresentationViewModel(
        PresentationCompositionServices compositionServices,
        ReplaceStateBindings stateBindings,
        Func<string, string, WorkbenchFirmwareConfigMetadata?> firmwareConfigMetadataReader)
    {
        _compositionServices = compositionServices ??
            throw new ArgumentNullException(nameof(compositionServices));
        _stateBindings = stateBindings ?? throw new ArgumentNullException(nameof(stateBindings));
        _ctrlRamFirmwareVersionMetadataReader = firmwareConfigMetadataReader ??
            throw new ArgumentNullException(nameof(firmwareConfigMetadataReader));
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

    /// <summary>Command that opens the compact Replace input selection overview.</summary>
    public IRelayCommand ShowReplaceSelectionCommand { get; }

    /// <summary>Command that closes the compact Replace input selection overview.</summary>
    public IRelayCommand CloseReplaceSelectionCommand { get; }

    internal void ApplyLanguageChanged()
    {
        ApplyFirmwareSlotText();
        OnPropertyChanged(nameof(Text));
        NotifyContextChanged();
        RefreshSelectionState();
    }
}
