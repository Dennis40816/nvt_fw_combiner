using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string DpReplaceMode = WorkbenchReplaceModes.Dp;
    private const string CtrlRamReplaceMode = WorkbenchReplaceModes.CtrlRam;
    private const string GeneralReplaceMode = WorkbenchReplaceModes.General;
    private const string NormalMergeMode = WorkbenchMergeModes.Standard;
    private const string AbCodeMergeMode = WorkbenchMergeModes.AbCode;
    private const string GeneralMergeMode = WorkbenchMergeModes.General;
    private const string MergeDpSlotId = WorkbenchSlotIds.MergeDp;
    private const string MergeTpSlotId = WorkbenchSlotIds.MergeTp;
    private const string MergeLdSlotId = WorkbenchSlotIds.MergeLd;
    private const string ReplaceBaseSlotId = WorkbenchSlotIds.ReplaceBase;

    private readonly FirmwareSlotViewModel _mergeDpSlot = new(
        MergeDpSlotId,
        "DP BIN",
        "Display payload for Standard Merge",
        kind: FirmwareSlotKind.Dp);
    private readonly FirmwareSlotViewModel _mergeTpSlot = new(
        MergeTpSlotId,
        "TP BIN",
        "Touch payload for Standard Merge",
        kind: FirmwareSlotKind.Tp);
    private readonly FirmwareSlotViewModel _mergeLdSlot = new(
        MergeLdSlotId,
        "LD BIN",
        "Required only when the selected profile uses LD",
        isOptional: true,
        kind: FirmwareSlotKind.Dp);
    private int _generalReplaceMappingCounter;
    private int _generalMergeMappingCounter;
    private string _selectedMergeMode = NormalMergeMode;

    /// <summary>Initializes the main workbench view model.</summary>
    public MainWindowViewModel(
        string shellVersion,
        string appVersion,
        ShellLanguage language = ShellLanguage.English)
    {
        ShellVersion = shellVersion;
        AppVersion = appVersion;
        HexEditorWorkspace = new HexEditorWorkspaceViewModel(Text);
        ApplyTextResources(language, notify: false);
        ShowHomeCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Home));
        ShowSettingsCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Settings));
        ShowMergeCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Merge));
        ShowReplaceCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Replace));
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);
        ShowDpReplaceCommand = new RelayCommand(() => SelectReplaceMode(DpReplaceMode));
        ShowCtrlRamReplaceCommand = new RelayCommand(() => SelectReplaceMode(CtrlRamReplaceMode));
        ShowGeneralReplaceCommand = new RelayCommand(() => SelectReplaceMode(GeneralReplaceMode));
        BeginDpReplaceFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Replace, DpReplaceMode, showNumber: true));
        BeginCtrlRamReplaceFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Replace, CtrlRamReplaceMode, showNumber: true));
        BeginGeneralReplaceFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Replace, GeneralReplaceMode, showNumber: true));
        ShowHexEditorCommand = new RelayCommand(ShowHexEditor);
        RequestHexEditorSaveCommand = new RelayCommand(RequestHexEditorSave, CanRequestHexEditorSave);
        ShowNormalMergeCommand = new RelayCommand(() => SelectMergeMode(NormalMergeMode));
        ShowGeneralMergeCommand = new RelayCommand(() => SelectMergeMode(GeneralMergeMode));
        BeginNormalMergeFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Merge, NormalMergeMode, showNumber: false));
        BeginGeneralMergeFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Merge, GeneralMergeMode, showNumber: false));
        ConfirmWorkflowContextCommand = new RelayCommand(ConfirmWorkflowContext);
        CancelWorkflowContextCommand = new RelayCommand(CancelWorkflowContext);
        AcceptFirmwareIcMismatchCommand = new RelayCommand(AcceptFirmwareIcMismatch);
        DismissFirmwareIcMismatchCommand = new RelayCommand(DismissFirmwareIcMismatch);
        AddGeneralReplaceMappingCommand = new RelayCommand(AddGeneralReplaceMapping);
        RemoveGeneralReplaceMappingCommand = new RelayCommand<GeneralReplaceMappingViewModel>(
            RemoveGeneralReplaceMapping);
        AddGeneralMergeMappingCommand = new RelayCommand(AddGeneralMergeMapping);
        RemoveGeneralMergeMappingCommand = new RelayCommand<GeneralMergeMappingViewModel>(
            RemoveGeneralMergeMapping);
        PreviewMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: false),
            CanRunMerge);
        BuildMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: true),
            () => CanBuildMerge);
        PreviewReplaceCommand = new AsyncRelayCommand(
            () => RunReplaceAsync(build: false),
            CanRunReplace);
        BuildReplaceCommand = new AsyncRelayCommand(
            () => RunReplaceAsync(build: true),
            () => CanBuildReplace);
        ShowReportCommand = new RelayCommand(ShowReport, () => CanOpenReport);
        CloseReportCommand = new RelayCommand(CloseReport);
        DismissReportToastCommand = new RelayCommand(DismissReportToast);
        ShowReportHistoryCommand = new RelayCommand(ShowReportHistory, () => CanOpenReportHistory);
        CloseReportHistoryCommand = new RelayCommand(CloseReportHistory);
        ClearReportHistoryCommand = new RelayCommand(ClearReportHistory, () => CanClearReportHistory);
        OpenReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(OpenReportHistoryEntry);
        RemoveReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(RemoveReportHistoryEntry);
        ShowReplaceSelectionCommand = new RelayCommand(ShowReplaceSelection);
        CloseReplaceSelectionCommand = new RelayCommand(CloseReplaceSelection);

        AddGeneralReplaceMapping();
        AddGeneralMergeMapping();
        NavigationTrail.Add(CreateNavigationEntry(ShellPage.Home, isCurrent: true));
        RefreshContextState();
        RefreshSettingsState();
    }
}
