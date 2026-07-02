using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Gets the clickable shell navigation history.</summary>
    public ObservableCollection<ShellNavigationEntryViewModel> NavigationTrail { get; } = [];

    /// <summary>Gets a compact text version of the current navigation path.</summary>
    public string NavigationPath => string.Join(
        " > ",
        NavigationTrail.Select(entry => entry.Label));

    /// <summary>True when the shell can return to an earlier navigation entry.</summary>
    public bool CanGoBack => NavigationTrail.Count > 1;

    /// <summary>True when the selected page needs IC and Number context.</summary>
    public bool IsDeviceContextVisible => SelectedPage is ShellPage.Merge or ShellPage.Replace;

    /// <summary>True when the shared context row should expose the IC Number selector.</summary>
    public bool IsNumberSelectorVisible => IsDeviceContextVisible && !(IsMergeVisible && IsNormalMergeModeSelected);

    /// <summary>True when the hidden IC Number selector should keep its layout space.</summary>
    public bool IsNumberSelectorPlaceholderVisible => IsDeviceContextVisible && !IsNumberSelectorVisible;

    /// <summary>Command that returns to the previous navigation entry.</summary>
    public IRelayCommand GoBackCommand { get; }

    private void NavigateToPage(ShellPage page)
    {
        int existingIndex = IndexOfNavigationPage(page);
        if (existingIndex >= 0)
        {
            while (NavigationTrail.Count > existingIndex + 1)
            {
                NavigationTrail.RemoveAt(NavigationTrail.Count - 1);
            }
        }
        else
        {
            NavigationTrail.Add(CreateNavigationEntry(page, isCurrent: false));
        }

        ApplySelectedPage(page);
    }

    private void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        NavigationTrail.RemoveAt(NavigationTrail.Count - 1);
        ApplySelectedPage(NavigationTrail[^1].Page);
    }

    private ShellNavigationEntryViewModel CreateNavigationEntry(ShellPage page, bool isCurrent)
    {
        return new ShellNavigationEntryViewModel(page, PageLabel(page), NavigateToPage, isCurrent);
    }

    private int IndexOfNavigationPage(ShellPage page)
    {
        for (int index = 0; index < NavigationTrail.Count; index++)
        {
            if (NavigationTrail[index].Page == page)
            {
                return index;
            }
        }

        return -1;
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
        foreach (ShellNavigationEntryViewModel entry in NavigationTrail)
        {
            entry.SetCurrent(entry.Page == SelectedPage);
        }

        OnPropertyChanged(nameof(NavigationPath));
        OnPropertyChanged(nameof(CanGoBack));
        GoBackCommand.NotifyCanExecuteChanged();
        RefreshSettingsState();
    }
}
