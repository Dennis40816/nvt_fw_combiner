using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellNavigationEntryViewModel : ObservableObject
{
    private readonly Action<ShellPage> _navigate;

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

    public ShellPage Page { get; }

    public string Label { get; }

    public IRelayCommand NavigateCommand { get; }

    /// <summary>True when a separator should be shown after this entry.</summary>
    public bool IsChevronVisible => !IsCurrent;

    [ObservableProperty]
    public partial bool IsCurrent { get; private set; }

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
