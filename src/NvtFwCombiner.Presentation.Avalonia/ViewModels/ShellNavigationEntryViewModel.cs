using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One breadcrumb/history entry in the shell navigation path.</summary>
public sealed partial class ShellNavigationEntryViewModel : ObservableObject
{
    private readonly Action<ShellPage> _navigate;

    /// <summary>Initializes a shell navigation entry.</summary>
    public ShellNavigationEntryViewModel(
        ShellPage page,
        string label,
        Action<ShellPage> navigate,
        bool isCurrent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(navigate);

        Page = page;
        Label = label;
        _navigate = navigate;
        IsCurrent = isCurrent;
        NavigateCommand = new RelayCommand(Open);
    }

    /// <summary>Gets the shell page represented by this entry.</summary>
    public ShellPage Page { get; }

    /// <summary>Gets the display label.</summary>
    public string Label { get; }

    /// <summary>Gets the command that returns to this history entry.</summary>
    public IRelayCommand NavigateCommand { get; }

    /// <summary>True when a separator should be shown after this entry.</summary>
    public bool IsChevronVisible => !IsCurrent;

    /// <summary>Gets or sets whether this entry is the current page.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; private set; }

    /// <summary>Marks this entry as current or not current.</summary>
    public void SetCurrent(bool isCurrent)
    {
        IsCurrent = isCurrent;
    }

    partial void OnIsCurrentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsChevronVisible));
    }

    private void Open()
    {
        _navigate(Page);
    }
}
