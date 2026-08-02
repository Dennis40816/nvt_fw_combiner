using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns Replace-page presentation state, commands, and workflow-specific lifetime.</summary>
public sealed partial class ReplacePresentationViewModel : ObservableObject
{
    private readonly ReplaceSelectionBindings _selectionBindings;

    internal ReplacePresentationViewModel(ReplaceSelectionBindings selectionBindings)
    {
        _selectionBindings = selectionBindings ?? throw new ArgumentNullException(nameof(selectionBindings));
        ShowReplaceSelectionCommand = new RelayCommand(ShowReplaceSelection);
        CloseReplaceSelectionCommand = new RelayCommand(CloseReplaceSelection);
    }

    /// <summary>Gets the current localized text used by Replace-only presentation.</summary>
    public ShellTextResources Text => _selectionBindings.Text();

    /// <summary>Command that opens the compact Replace input selection overview.</summary>
    public IRelayCommand ShowReplaceSelectionCommand { get; }

    /// <summary>Command that closes the compact Replace input selection overview.</summary>
    public IRelayCommand CloseReplaceSelectionCommand { get; }

    internal void ApplyLanguageChanged()
    {
        OnPropertyChanged(nameof(Text));
        RefreshSelectionState();
    }
}
