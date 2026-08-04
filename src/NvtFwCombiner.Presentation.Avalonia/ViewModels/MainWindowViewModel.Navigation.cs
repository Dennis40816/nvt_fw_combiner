using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly List<ShellPage> _pageHistory = [ShellPage.Home];
    private PendingNavigation? _pendingNavigation;

    /// <summary>True while leaving a composition page awaits confirmation to clear selected files.</summary>
    [ObservableProperty]
    public partial bool IsNavigationClearConfirmationOpen { get; set; }

    /// <summary>Gets the clickable shell navigation hierarchy.</summary>
    public ObservableCollection<ShellNavigationEntryViewModel> NavigationTrail { get; } = [];

    /// <summary>Gets a compact text version of the current navigation path.</summary>
    public string NavigationPath => string.Join(
        " > ",
        NavigationTrail.Select(entry => entry.Label));

    /// <summary>Gets the current and requested pages shown by the navigation clear confirmation.</summary>
    public string NavigationClearRoute => _pendingNavigation is { } pending
        ? $"{PageLabel(SelectedPage)} → {PageLabel(pending.Target)}" : NavigationPath;

    /// <summary>True when the shell can return to the previous visited page.</summary>
    public bool CanGoBack => _pageHistory.Count > 1;

    /// <summary>True when the selected page or active run needs the captured device context.</summary>
    public bool IsDeviceContextVisible => RunSession.IsRunInProgress ||
        SelectedPage is ShellPage.Merge or ShellPage.Replace;

    /// <summary>True when the fixed composition action rail belongs to the active page.</summary>
    public bool IsCompositionActionRailVisible =>
        (SelectedPage is ShellPage.Merge or ShellPage.Replace) && !IsBlockingSurfaceOpen;

    /// <summary>True when the current composition page can reopen the latest committed output.</summary>
    public bool IsLatestOutputActionVisible => IsCompositionActionRailVisible && BuildResult.HasLatestCommittedOutput;

    private bool IsBlockingSurfaceOpen =>
        Replace.IsReplaceSelectionModalOpen ||
        Replace.IsCtrlRamFirmwareVersionModalOpen ||
        WorkflowSession.IsWorkflowContextModalOpen ||
        WorkflowSession.IsFirmwareIcMismatchModalOpen ||
        WorkflowSession.IsFirmwareNumberMismatchModalOpen ||
        IsNavigationClearConfirmationOpen ||
        Reports.IsReportModalOpen ||
        Merge.IsAbAFlashCodeDeliveryPromptOpen ||
        BuildResult.IsOpen ||
        LoadedHexEditorWorkspace?.IsInsertBytesPromptOpen == true ||
        LoadedHexEditorWorkspace?.IsSaveConfirmationOpen == true;

    private void MainWindowViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsNavigationClearConfirmationOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }

    private void NotifyCompositionActionRailVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsCompositionActionRailVisible));
        OnPropertyChanged(nameof(IsLatestOutputActionVisible));
    }

    /// <summary>Command that returns to the previous navigation entry.</summary>
    public IRelayCommand GoBackCommand { get; }

    /// <summary>Command that clears the current page file selections and completes navigation.</summary>
    public IRelayCommand ConfirmNavigationAndClearCommand { get; }

    /// <summary>Command that keeps the current page and all of its selections.</summary>
    public IRelayCommand CancelNavigationClearCommand { get; }

    private void NavigateToPage(ShellPage page)
    {
        if (SelectedPage == page)
        {
            ApplySelectedPage(page);
            return;
        }

        if (!RequestNavigation(page, isBack: false))
        {
            CompleteNavigation(page, isBack: false);
        }
    }

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
        if (IsNavigationClearConfirmationOpen || !WorkflowSession.HasSelectedInputs(SelectedPage))
        {
            return IsNavigationClearConfirmationOpen;
        }

        WorkflowSession.InvalidateFirmwareNumberMismatch();
        _pendingNavigation = new PendingNavigation(target, isBack);
        OnPropertyChanged(nameof(NavigationClearRoute));
        IsNavigationClearConfirmationOpen = true;
        return true;
    }

    private void ConfirmNavigationAndClear()
    {
        if (_pendingNavigation is not { } pending)
        {
            IsNavigationClearConfirmationOpen = false;
            return;
        }

        ShellPage pageBeingLeft = SelectedPage;
        _pendingNavigation = null;
        IsNavigationClearConfirmationOpen = false;
        WorkflowSession.ClearSelectedInputs(pageBeingLeft);
        CompleteNavigation(pending.Target, pending.IsBack);
    }

    private void CancelNavigationClear()
    {
        _pendingNavigation = null;
        IsNavigationClearConfirmationOpen = false;
    }

    private void CompleteNavigation(ShellPage target, bool isBack)
    {
        if (isBack)
        {
            if (_pageHistory.Count > 1)
            {
                _pageHistory.RemoveAt(_pageHistory.Count - 1);
            }
        }
        else if (SelectedPage != target)
        {
            _pageHistory.Add(target);
        }

        ApplySelectedPage(target);
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
            ShellPage.Merge => Merge.MergePreview.Title,
            ShellPage.Replace => Replace.ReplacePreview.Title,
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

    private readonly record struct PendingNavigation(ShellPage Target, bool IsBack);
}
