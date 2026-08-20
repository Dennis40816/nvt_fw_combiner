using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns Merge-page presentation state, commands, and workflow-specific lifetime.</summary>
internal sealed partial class MergePresentationViewModel : ObservableObject
{
    private readonly PresentationCompositionServices _compositionServices;
    private readonly Func<ShellTextResources> _textProvider;

    internal MergePresentationViewModel(
        PresentationCompositionServices compositionServices,
        Func<ShellTextResources> textProvider,
        MergeStateBindings stateBindings)
    {
        _compositionServices = compositionServices ??
            throw new ArgumentNullException(nameof(compositionServices));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        _stateBindings = stateBindings ?? throw new ArgumentNullException(nameof(stateBindings));
        InspectionLifecycles = new(NotifyCommandStateChanged, AbCodeMergeMode, GeneralMergeMode);
        AcceptAbAFlashCodeDeliveryPromptCommand = new RelayCommand(AcceptAbAFlashCodeDeliveryPrompt);
        DeclineAbAFlashCodeDeliveryPromptCommand = new RelayCommand(DeclineAbAFlashCodeDeliveryPrompt);
        AddGeneralMergeMappingCommand = new RelayCommand(AddGeneralMergeMapping);
        PreviewMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: false, outputPath: null),
            CanRunMerge);
        BuildMergeCommand = new AsyncRelayCommand(
            RequestBuildOutputDeliveryAsync,
            CanRunMerge);
    }

    /// <summary>Gets the current localized text used by Merge-only presentation.</summary>
    public ShellTextResources Text => _textProvider();

    internal void ApplyLanguageChanged()
    {
        ApplyFirmwareSlotText();
        InspectionLifecycles.ForEach(lifecycle => lifecycle.ApplyText(Text));
        RefreshMergeMemoryMapState(refreshAuthoring: false);
        OnPropertyChanged(nameof(Text));
        NotifyContextChanged();
    }
}
