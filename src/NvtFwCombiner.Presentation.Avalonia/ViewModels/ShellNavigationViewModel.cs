using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record ShellNavigationBindings(
    Func<ShellPage> SelectedPage, Func<ShellTextResources> Text, Func<ShellPage, bool> HasSelectedInputs,
    Action InvalidateFirmwareNumberMismatch, Action<ShellPage> ClearSelectedInputs,
    Action<ShellPage> ApplySelectedPage, Func<ShellPage, string> PageLabel, Action BlockingSurfaceChanged);

internal sealed partial class ShellNavigationViewModel : ObservableObject
{
    private readonly ShellNavigationBindings _bindings;
    private readonly List<ShellPage> _pageHistory = [ShellPage.Home];
    private PendingNavigation? _pendingNavigation;

    internal ShellNavigationViewModel(ShellNavigationBindings bindings)
    {
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        RefreshNavigationTrail();
    }

    [ObservableProperty]
    public partial bool IsNavigationClearConfirmationOpen { get; set; }

    public ShellTextResources Text => _bindings.Text();

    public ObservableCollection<ShellNavigationEntryViewModel> NavigationTrail { get; } = [];

    public string NavigationPath => string.Join(" > ", NavigationTrail.Select(static entry => entry.Label));

    public string NavigationClearRoute => _pendingNavigation is { } pending
        ? $"{_bindings.PageLabel(_bindings.SelectedPage())} → {_bindings.PageLabel(pending.Target)}" : NavigationPath;

    public bool CanGoBack => _pageHistory.Count > 1;

    internal void NavigateToPage(ShellPage page)
    {
        if (_bindings.SelectedPage() == page)
        {
            _bindings.ApplySelectedPage(page);
            return;
        }

        if (!RequestNavigation(page, isBack: false))
        {
            CompleteNavigation(page, isBack: false);
        }
    }

    internal void UpdateState(bool textChanged = false)
    {
        RefreshNavigationTrail();
        if (textChanged)
        {
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(NavigationClearRoute));
        }

        OnPropertyChanged(nameof(NavigationPath));
        OnPropertyChanged(nameof(CanGoBack));
        GoBackCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsNavigationClearConfirmationOpenChanged(bool value)
    {
        _bindings.BlockingSurfaceChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        ShellPage target = _pageHistory[^2];
        if (!RequestNavigation(target, isBack: true))
        {
            CompleteNavigation(target, isBack: true);
        }
    }

    private bool RequestNavigation(ShellPage target, bool isBack)
    {
        if (IsNavigationClearConfirmationOpen || !_bindings.HasSelectedInputs(_bindings.SelectedPage()))
        {
            return IsNavigationClearConfirmationOpen;
        }

        _bindings.InvalidateFirmwareNumberMismatch();
        _pendingNavigation = new PendingNavigation(target, isBack);
        OnPropertyChanged(nameof(NavigationClearRoute));
        IsNavigationClearConfirmationOpen = true;
        return true;
    }

    [RelayCommand]
    private void ConfirmNavigationAndClear()
    {
        if (_pendingNavigation is not { } pending)
        {
            IsNavigationClearConfirmationOpen = false;
            return;
        }

        _pendingNavigation = null;
        IsNavigationClearConfirmationOpen = false;
        _bindings.ClearSelectedInputs(_bindings.SelectedPage());
        CompleteNavigation(pending.Target, pending.IsBack);
    }

    [RelayCommand]
    private void CancelNavigationClear()
    {
        _pendingNavigation = null;
        IsNavigationClearConfirmationOpen = false;
    }

    private void CompleteNavigation(ShellPage target, bool isBack)
    {
        if (isBack && _pageHistory.Count > 1)
        {
            _pageHistory.RemoveAt(_pageHistory.Count - 1);
        }
        else if (!isBack && _bindings.SelectedPage() != target)
        {
            _pageHistory.Add(target);
        }

        _bindings.ApplySelectedPage(target);
    }

    private void RefreshNavigationTrail()
    {
        ShellPage selectedPage = _bindings.SelectedPage();
        NavigationTrail.Clear();
        NavigationTrail.Add(new ShellNavigationEntryViewModel(
            ShellPage.Home, _bindings.PageLabel(ShellPage.Home), NavigateToPage, selectedPage == ShellPage.Home));
        if (selectedPage != ShellPage.Home)
        {
            NavigationTrail.Add(new ShellNavigationEntryViewModel(
                selectedPage, _bindings.PageLabel(selectedPage), NavigateToPage, isCurrent: true));
        }
    }

    private readonly record struct PendingNavigation(ShellPage Target, bool IsBack);
}
