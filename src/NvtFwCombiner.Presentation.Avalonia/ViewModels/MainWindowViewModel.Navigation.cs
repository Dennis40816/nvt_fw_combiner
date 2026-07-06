using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly List<ShellPage> _pageHistory = [ShellPage.Home];

    /// <summary>Gets the clickable shell navigation hierarchy.</summary>
    public ObservableCollection<ShellNavigationEntryViewModel> NavigationTrail { get; } = [];

    /// <summary>Gets a compact text version of the current navigation path.</summary>
    public string NavigationPath => string.Join(
        " > ",
        NavigationTrail.Select(entry => entry.Label));

    /// <summary>True when the shell can return to the previous visited page.</summary>
    public bool CanGoBack => _pageHistory.Count > 1;

    /// <summary>True when the selected page needs IC and Number context.</summary>
    public bool IsDeviceContextVisible => SelectedPage is ShellPage.Merge or ShellPage.Replace;

    /// <summary>True when the shared context row should expose the IC Number selector.</summary>
    public bool IsNumberSelectorVisible => IsDeviceContextVisible &&
        !(IsMergeVisible && (IsNormalMergeModeSelected || IsGeneralMergeModeSelected));

    /// <summary>True when the hidden IC Number selector should keep its layout space.</summary>
    public bool IsNumberSelectorPlaceholderVisible => IsDeviceContextVisible && !IsNumberSelectorVisible;

    /// <summary>Command that returns to the previous navigation entry.</summary>
    public IRelayCommand GoBackCommand { get; }

    private void NavigateToPage(ShellPage page)
    {
        if (SelectedPage != page)
        {
            _pageHistory.Add(page);
        }

        ApplySelectedPage(page);
    }

    private void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        _pageHistory.RemoveAt(_pageHistory.Count - 1);
        ApplySelectedPage(_pageHistory[^1]);
    }

    private ShellNavigationEntryViewModel CreateNavigationEntry(ShellPage page, bool isCurrent)
    {
        return new ShellNavigationEntryViewModel(page, PageLabel(page), NavigateToPage, isCurrent);
    }

    private string PageLabel(ShellPage page)
    {
        return page switch
        {
            ShellPage.Home => "Home",
            ShellPage.Settings => SettingsPreview.Title,
            ShellPage.Merge => MergePreview.Title,
            ShellPage.Replace => ReplacePreview.Title,
            _ => page.ToString(),
        };
    }

    private void UpdateNavigationState()
    {
        RefreshNavigationTrail();

        foreach (ShellNavigationEntryViewModel entry in NavigationTrail)
        {
            entry.SetCurrent(entry.Page == SelectedPage);
        }

        OnPropertyChanged(nameof(NavigationPath));
        OnPropertyChanged(nameof(CanGoBack));
        GoBackCommand.NotifyCanExecuteChanged();
        RefreshSettingsState();
    }

    private void RefreshNavigationTrail()
    {
        NavigationTrail.Clear();
        NavigationTrail.Add(CreateNavigationEntry(ShellPage.Home, SelectedPage == ShellPage.Home));
        if (SelectedPage != ShellPage.Home)
        {
            NavigationTrail.Add(CreateNavigationEntry(SelectedPage, isCurrent: true));
        }
    }
}
