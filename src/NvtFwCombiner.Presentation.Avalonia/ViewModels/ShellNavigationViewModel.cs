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
    private Action? _pendingExit;

    internal ShellNavigationViewModel(ShellNavigationBindings bindings)
    {
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        RefreshNavigationTrail();
    }

    [ObservableProperty]
    public partial bool IsNavigationClearConfirmationOpen { get; set; }

    public ShellTextResources Text => _bindings.Text();

    public bool IsExitConfirmationOpen => _pendingExit is not null;
    private bool IsRetainedPageNavigation => !IsExitConfirmationOpen && _bindings.SelectedPage() == ShellPage.HexEditor;
    public string ConfirmationTitle => IsExitConfirmationOpen ? Text.ExitTitle :
        IsRetainedPageNavigation ? Text.LeaveEditorTitle : Text.NavigationClearTitle;
    public string ConfirmationDetail => IsExitConfirmationOpen ? Text.ExitDetail :
        IsRetainedPageNavigation ? Text.LeaveEditorDetail : Text.NavigationClearDetail;
    public string ConfirmationCancelLabel => IsExitConfirmationOpen ? Text.OutputDeliveryCancelLabel : Text.NavigationClearCancelLabel;
    public string ConfirmationAcceptLabel => IsExitConfirmationOpen ? Text.ExitConfirmLabel :
        IsRetainedPageNavigation ? Text.LeaveEditorConfirmLabel : Text.NavigationClearConfirmLabel;

    public ObservableCollection<ShellNavigationEntryViewModel> NavigationTrail { get; } = [];

    public string NavigationPath => string.Join(" > ", NavigationTrail.Select(static entry => entry.Label));

    public string NavigationClearRoute => IsExitConfirmationOpen ? "NVT FW Combiner" : _pendingNavigation is { } pending
        ? $"{_bindings.PageLabel(_bindings.SelectedPage())} → {_bindings.PageLabel(pending.Target)}" : NavigationPath;

    public bool CanGoBack => _pageHistory.Count > 1;

    internal void RequestExitConfirmation(Action exit)
    {
        ArgumentNullException.ThrowIfNull(exit);
        if (_pendingExit is not null)
        {
            return;
        }
        // Exit replaces a pending page change with a fresh safe-focus confirmation.
        IsNavigationClearConfirmationOpen = false;
        _pendingNavigation = null;
        _pendingExit = exit;
        RefreshConfirmation();
        IsNavigationClearConfirmationOpen = true;
    }

    private void RefreshConfirmation()
    {
        OnPropertyChanged(nameof(IsExitConfirmationOpen));
        OnPropertyChanged(nameof(ConfirmationTitle));
        OnPropertyChanged(nameof(ConfirmationDetail));
        OnPropertyChanged(nameof(ConfirmationCancelLabel));
        OnPropertyChanged(nameof(ConfirmationAcceptLabel));
        OnPropertyChanged(nameof(NavigationClearRoute));
    }

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
            RefreshConfirmation();
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
        RefreshConfirmation();
        IsNavigationClearConfirmationOpen = true;
        return true;
    }

    [RelayCommand]
    private void ConfirmNavigationAndClear()
    {
        if (_pendingExit is { } exit)
        {
            CancelNavigationClear();
            exit();
            return;
        }
        if (_pendingNavigation is not { } pending)
        {
            IsNavigationClearConfirmationOpen = false;
            return;
        }

        ShellPage source = _bindings.SelectedPage();
        _pendingNavigation = null;
        IsNavigationClearConfirmationOpen = false;
        CompleteNavigation(
            pending.Target,
            pending.IsBack,
            () => _bindings.ClearSelectedInputs(source));
    }

    [RelayCommand]
    private void CancelNavigationClear()
    {
        _pendingNavigation = null;
        _pendingExit = null;
        IsNavigationClearConfirmationOpen = false;
    }

    private void CompleteNavigation(
        ShellPage target,
        bool isBack,
        Action? afterActivation = null)
    {
        ShellPage source = _bindings.SelectedPage();
        ShellPage[] previousHistory = [.. _pageHistory];
        if (isBack && _pageHistory.Count > 1)
        {
            _pageHistory.RemoveAt(_pageHistory.Count - 1);
        }
        else if (!isBack && _bindings.SelectedPage() != target)
        {
            _pageHistory.Add(target);
        }

        try
        {
            _bindings.ApplySelectedPage(target);
            afterActivation?.Invoke();
        }
        catch
        {
            if (_bindings.SelectedPage() != source)
            {
                _bindings.ApplySelectedPage(source);
            }
            _pageHistory.Clear();
            _pageHistory.AddRange(previousHistory);
            UpdateState();
            throw;
        }
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
