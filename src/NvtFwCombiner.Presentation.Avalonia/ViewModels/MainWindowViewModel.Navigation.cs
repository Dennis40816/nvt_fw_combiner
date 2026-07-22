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
    public bool IsDeviceContextVisible => IsRunInProgress || SelectedPage is ShellPage.Merge or ShellPage.Replace;

    /// <summary>True when the fixed composition action rail belongs to the active page.</summary>
    public bool IsCompositionActionRailVisible =>
        (SelectedPage is ShellPage.Merge or ShellPage.Replace) && !IsBlockingSurfaceOpen;

    /// <summary>True when the current composition page can reopen the latest committed output.</summary>
    public bool IsLatestOutputActionVisible => IsCompositionActionRailVisible && HasLatestCommittedOutput;

    private bool IsBlockingSurfaceOpen =>
        IsReplaceSelectionModalOpen ||
        IsCtrlRamFirmwareVersionModalOpen ||
        IsWorkflowContextModalOpen ||
        IsFirmwareIcMismatchModalOpen ||
        IsFirmwareNumberMismatchModalOpen ||
        IsNavigationClearConfirmationOpen ||
        IsReportModalOpen ||
        IsBuildCompletedModalOpen ||
        LoadedHexEditorWorkspace?.IsInsertBytesPromptOpen == true ||
        LoadedHexEditorWorkspace?.IsSaveConfirmationOpen == true;

    private void MainWindowViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IsReplaceSelectionModalOpen) or
            nameof(IsCtrlRamFirmwareVersionModalOpen) or
            nameof(IsWorkflowContextModalOpen) or
            nameof(IsFirmwareIcMismatchModalOpen) or
            nameof(IsFirmwareNumberMismatchModalOpen) or
            nameof(IsNavigationClearConfirmationOpen) or
            nameof(IsReportModalOpen) or
            nameof(IsBuildCompletedModalOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }

    private void NotifyCompositionActionRailVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsCompositionActionRailVisible));
        OnPropertyChanged(nameof(IsLatestOutputActionVisible));
    }

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

    /// <summary>True when the selected-family badge describes the visible mutable context.</summary>
    public bool IsDeviceContextFamilyBadgeVisible => !IsRunInProgress && HasSelectedIcFamily;

    /// <summary>Command that returns to the previous navigation entry.</summary>
    public IRelayCommand GoBackCommand { get; }

    /// <summary>Command that clears the current page file selections and completes navigation.</summary>
    public IRelayCommand ConfirmNavigationAndClearCommand { get; }

    /// <summary>Command that keeps the current page and all of its selections.</summary>
    public IRelayCommand CancelNavigationClearCommand { get; }

    private bool ShouldShowNumberSelectorForSelectedPage()
    {
        return IsReplaceVisible;
    }

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
        if (IsNavigationClearConfirmationOpen || !HasSelectedInputs(SelectedPage))
        {
            return IsNavigationClearConfirmationOpen;
        }

        InvalidateFirmwareNumberMismatch();
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
        ClearSelectedInputs(pageBeingLeft);
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

    private bool HasSelectedInputs(ShellPage page)
    {
        return page switch
        {
            ShellPage.Merge =>
                _mergeDpSlot.HasFile ||
                _mergeTpSlot.HasFile ||
                _mergeLdSlot.HasFile ||
                _abMergeSlotsByAddressSpace.Values.Any(static slot => slot.HasFile) ||
                MergeSlots.Any(static slot => slot.HasFile) ||
                GeneralMergeMappings.Any(static mapping => mapping.HasFile),
            ShellPage.Replace =>
                ReplaceBaseSlot.HasFile ||
                ReplaceSlots.Any(static slot => slot.HasFile) ||
                GeneralReplaceMappings.Any(static mapping => mapping.HasFile),
            ShellPage.Home or ShellPage.Settings or ShellPage.HexEditor => false,
            _ => false,
        };
    }

    private void ClearSelectedInputs(ShellPage page)
    {
        InvalidateFirmwareInspection(clearBaseCache: true, clearFileProjections: true);
        InvalidateCtrlRamFirmwareVersionContext();
        InvalidateFirmwareIcMismatch();
        InvalidateFirmwareNumberMismatch();

        if (page == ShellPage.Merge)
        {
            foreach (FirmwareSlotViewModel slot in MergeSlots
                         .Concat(_abMergeSlotsByAddressSpace.Values)
                         .Concat([_mergeDpSlot, _mergeTpSlot, _mergeLdSlot])
                         .Distinct())
            {
                ClearFirmwareSlot(slot);
            }

            foreach (GeneralMergeMappingViewModel mapping in GeneralMergeMappings)
            {
                mapping.FilePath = null;
            }

            RefreshMergeMemoryMapState();
        }
        else if (page == ShellPage.Replace)
        {
            foreach (FirmwareSlotViewModel slot in ReplaceSlots.Concat([ReplaceBaseSlot]).Distinct())
            {
                ClearFirmwareSlot(slot);
            }

            foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings)
            {
                mapping.FilePath = null;
            }

            ClearCtrlRamInspectionDisplay();
            RefreshReplaceMemoryMapState();
        }

        NotifySlotFileOutputNames();
        ResetRunResultForContextChange();
        RefreshCommandState();
    }

    private static void ClearFirmwareSlot(FirmwareSlotViewModel slot)
    {
        slot.FilePath = null;
        slot.SetFirmwareFacts([]);
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

    private readonly record struct PendingNavigation(ShellPage Target, bool IsBack);
}
