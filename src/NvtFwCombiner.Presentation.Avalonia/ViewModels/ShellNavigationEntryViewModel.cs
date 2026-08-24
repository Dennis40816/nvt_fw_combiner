using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class ShellNavigationEntryViewModel
{
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
        IsChevronVisible = !isCurrent;
        NavigateCommand = new RelayCommand(() => navigate(Page));
    }

    public ShellPage Page { get; }

    public string Label { get; }

    public IRelayCommand NavigateCommand { get; }

    /// <summary>True when a separator should be shown after this entry.</summary>
    public bool IsChevronVisible { get; }
}
