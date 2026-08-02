using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns Merge-page presentation state, commands, and workflow-specific lifetime.</summary>
public sealed partial class MergePresentationViewModel : ObservableObject
{
    private readonly Func<ShellTextResources> _textProvider;

    internal MergePresentationViewModel(Func<ShellTextResources> textProvider)
    {
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        AcceptAbAFlashCodeDeliveryPromptCommand = new RelayCommand(AcceptAbAFlashCodeDeliveryPrompt);
        DeclineAbAFlashCodeDeliveryPromptCommand = new RelayCommand(DeclineAbAFlashCodeDeliveryPrompt);
    }

    /// <summary>Gets the current localized text used by Merge-only presentation.</summary>
    public ShellTextResources Text => _textProvider();

    internal void ApplyLanguageChanged()
    {
        OnPropertyChanged(nameof(Text));
    }
}
