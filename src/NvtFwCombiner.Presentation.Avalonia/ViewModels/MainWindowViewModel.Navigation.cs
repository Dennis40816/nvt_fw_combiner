using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    [ObservableProperty]
    public partial bool IsSettingsModalOpen { get; private set; }

    public ShellNavigationViewModel Navigation { get; }

    public bool IsDeviceContextVisible => RunSession.IsRunInProgress ||
        SelectedPage is ShellPage.Merge or ShellPage.Replace;

    public bool IsCompositionActionRailVisible =>
        (SelectedPage is ShellPage.Merge or ShellPage.Replace) && !IsBlockingSurfaceOpen;

    public bool IsLatestOutputActionVisible =>
        IsCompositionActionRailVisible && BuildResult.HasLatestCommittedOutput;

    /// <summary>Whether a normal Settings close may return focus to its launcher.</summary>
    public bool CanRestoreSettingsFocus => !IsOtherBlockingSurfaceOpen;

    private bool IsBlockingSurfaceOpen =>
        IsSettingsModalOpen || IsOtherBlockingSurfaceOpen;

    private bool IsOtherBlockingSurfaceOpen =>
        OutputDelivery.IsOpen ||
        Replace.IsReplaceSelectionModalOpen ||
        Replace.IsCtrlRamFirmwareVersionModalOpen ||
        WorkflowSession.IsWorkflowContextModalOpen ||
        WorkflowSession.IsFirmwareIcMismatchModalOpen ||
        WorkflowSession.IsFirmwareNumberMismatchModalOpen ||
        Navigation.IsNavigationClearConfirmationOpen ||
        MessageCenter.IsOpen ||
        Reports.IsReportModalOpen ||
        Merge.IsAbAFlashCodeDeliveryPromptOpen ||
        Merge.IsAbSameTpConflictPromptOpen ||
        BuildResult.IsOpen ||
        LoadedHexEditorWorkspace?.IsInsertBytesPromptOpen == true ||
        LoadedHexEditorWorkspace?.IsSaveConfirmationOpen == true;

    private void MainWindowViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsSettingsModalOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }

    private void NotifyCompositionActionRailVisibilityChanged()
    {
        if (IsSettingsModalOpen && IsOtherBlockingSurfaceOpen)
        {
            IsSettingsModalOpen = false;
        }

        OnPropertyChanged(nameof(IsCompositionActionRailVisible));
        OnPropertyChanged(nameof(IsLatestOutputActionVisible));
    }

    private void OpenSettings()
    {
        if (IsBlockingSurfaceOpen)
        {
            return;
        }

        _deferredState.EnsureSettings(RefreshSettingsState);
        Settings.SelectSectionCommand.Execute(
            Settings.IsVersionConfirmationOpen || Settings.HasRetentionReview
                ? SettingsSection.Version
                : SettingsSection.Preferences);
        IsSettingsModalOpen = true;
        RecordDebugActivity(
            SystemActivityCodes.SettingsOpened,
            SystemActivityCategory.Navigation,
            Settings.SelectedSection.ToString());
    }

    private void CloseSettings()
    {
        IsSettingsModalOpen = false;
    }

    private string PageLabel(ShellPage page)
    {
        return page switch
        {
            ShellPage.Home => Text.HomeLabel,
            ShellPage.Merge => Merge.MergePreview.Title,
            ShellPage.Replace => Replace.ReplacePreview.Title,
            ShellPage.HexEditor => Text.HexEditorTitle,
            _ => page.ToString(),
        };
    }
}
