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

    /// <summary>True when the selected page or active run needs the captured device context.</summary>
    public bool IsDeviceContextVisible => IsRunInProgress || SelectedPage is ShellPage.Merge or ShellPage.Replace;

    /// <summary>True when the shared context row should expose the IC Number selector.</summary>
    public bool IsNumberSelectorVisible => IsRunInProgress
        ? ActiveRunShowsNumberSelector
        : ShouldShowNumberSelectorForSelectedPage();

    /// <summary>True when the hidden IC Number selector should keep its layout space.</summary>
    public bool IsNumberSelectorPlaceholderVisible => IsDeviceContextVisible && !IsNumberSelectorVisible;

    /// <summary>True when the mutable shell selection controls may be shown.</summary>
    public bool IsDeviceContextSelectionVisible => !IsRunInProgress;

    /// <summary>True when the mutable IC Number selection control may be shown.</summary>
    public bool IsDeviceContextNumberSelectionVisible => IsNumberSelectorVisible && !IsRunInProgress;

    /// <summary>True when the immutable IC Number captured for the active run may be shown.</summary>
    public bool IsDeviceContextRunNumberVisible => IsNumberSelectorVisible && IsRunInProgress;

    /// <summary>True when the selected-family badge describes the visible mutable context.</summary>
    public bool IsDeviceContextFamilyBadgeVisible => !IsRunInProgress && HasSelectedIcFamily;

    /// <summary>Command that returns to the previous navigation entry.</summary>
    public IRelayCommand GoBackCommand { get; }

    private bool ShouldShowNumberSelectorForSelectedPage()
    {
        return SelectedPage is ShellPage.Merge or ShellPage.Replace &&
            !(IsMergeVisible && (IsNormalMergeModeSelected || IsGeneralMergeModeSelected));
    }

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
            ShellPage.Home => Text.HomeLabel,
            ShellPage.Settings => SettingsPreview.Title,
            ShellPage.Merge => MergePreview.Title,
            ShellPage.Replace => ReplacePreview.Title,
            ShellPage.HexEditor => Text.HexEditorTitle,
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
        RequestHexEditorSaveCommand.NotifyCanExecuteChanged();
        RequestHexEditorUndoCommand.NotifyCanExecuteChanged();
        RequestHexEditorRedoCommand.NotifyCanExecuteChanged();
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
