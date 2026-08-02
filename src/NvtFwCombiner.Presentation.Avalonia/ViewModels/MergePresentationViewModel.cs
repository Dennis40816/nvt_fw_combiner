using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns Merge-page presentation state, commands, and workflow-specific lifetime.</summary>
public sealed partial class MergePresentationViewModel : ObservableObject
{
    private readonly Func<ShellTextResources> _textProvider;

    internal MergePresentationViewModel(
        Func<ShellTextResources> textProvider,
        MergeStateBindings stateBindings)
    {
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        _stateBindings = stateBindings ?? throw new ArgumentNullException(nameof(stateBindings));
        AcceptAbAFlashCodeDeliveryPromptCommand = new RelayCommand(AcceptAbAFlashCodeDeliveryPrompt);
        DeclineAbAFlashCodeDeliveryPromptCommand = new RelayCommand(DeclineAbAFlashCodeDeliveryPrompt);
        AddGeneralMergeMappingCommand = new RelayCommand(AddGeneralMergeMapping);
        PreviewMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: false, outputPath: null),
            CanRunMerge);
        BuildMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: true, outputPath: null),
            CanRunMerge);
    }

    /// <summary>Gets the current localized text used by Merge-only presentation.</summary>
    public ShellTextResources Text => _textProvider();

    internal void ApplyLanguageChanged()
    {
        OnPropertyChanged(nameof(Text));
        NotifyContextChanged();
    }
}
