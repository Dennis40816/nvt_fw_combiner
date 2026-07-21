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
        FirmwareSlotKind.Dp);
    private readonly FirmwareSlotViewModel _mergeTpSlot = new(
        MergeTpSlotId,
        "TP BIN",
        "Touch payload for Standard Merge",
        FirmwareSlotKind.Tp);
    private readonly FirmwareSlotViewModel _mergeLdSlot = new(
        MergeLdSlotId,
        "LD BIN",
        "Required only when the selected profile uses LD",
        FirmwareSlotKind.Dp,
        isOptional: true);
    private int _generalReplaceMappingCounter;
    private int _generalMergeMappingCounter;
    private readonly DeferredShellState _deferredState = new();
    private readonly bool _isInitializing = true;
    private string _selectedMergeMode = NormalMergeMode;

    /// <summary>Initializes the main workbench view model.</summary>
    public MainWindowViewModel(
        string shellVersion,
        string appVersion,
        ShellLanguage language = ShellLanguage.English)
        : this(
            shellVersion,
            appVersion,
            language,
            static (icId, path) => WorkbenchCompositionService.TryReadFirmwareConfigMetadata(icId, path),
            WorkbenchCompositionService.InspectFirmwareBatch)
    {
    }

    /// <summary>Initializes the main workbench view model with a deterministic firmware metadata reader.</summary>
    internal MainWindowViewModel(
        string shellVersion,
        string appVersion,
        ShellLanguage language,
        Func<string, string, WorkbenchFirmwareConfigMetadata?> firmwareConfigMetadataReader)
        : this(
            shellVersion,
            appVersion,
            language,
            firmwareConfigMetadataReader,
            WorkbenchCompositionService.InspectFirmwareBatch)
    {
    }

    /// <summary>Initializes the shell with deterministic metadata and consolidated inspection readers.</summary>
    internal MainWindowViewModel(
        string shellVersion,
        string appVersion,
        ShellLanguage language,
        Func<string, string, WorkbenchFirmwareConfigMetadata?> firmwareConfigMetadataReader,
        Func<
            string,
            IReadOnlyList<WorkbenchFirmwareInspectionInput>,
            IReadOnlyList<WorkbenchFirmwareInspectionResult>> firmwareInspectionReader)
    {
        ArgumentNullException.ThrowIfNull(firmwareConfigMetadataReader);
        ArgumentNullException.ThrowIfNull(firmwareInspectionReader);
        _ctrlRamFirmwareVersionMetadataReader = firmwareConfigMetadataReader;
        _firmwareInspectionReader = firmwareInspectionReader;
        ShellVersion = shellVersion;
        AppVersion = appVersion;
        CompositionProgress = new CompositionRunProgressViewModel(language);
        SelectedLanguage = language == ShellLanguage.ChineseTraditional ? "Traditional Chinese" : "English";
        _relocalizeLoadedReportCommand = new AsyncRelayCommand(RelocalizeLoadedReportAsync);
        CompositionProgress.PropertyChanged += CompositionProgress_OnPropertyChanged;
        ApplyTextResources(language, notify: false);
        ShowHomeCommand = new RelayCommand(() => NavigateToPage(ShellPage.Home));
        ShowSettingsCommand = new RelayCommand(() => NavigateToPage(ShellPage.Settings));
        ShowMergeCommand = new RelayCommand(() => NavigateToPage(ShellPage.Merge));
        ShowReplaceCommand = new RelayCommand(() => NavigateToPage(ShellPage.Replace));
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);
        ConfirmNavigationAndClearCommand = new RelayCommand(ConfirmNavigationAndClear);
        CancelNavigationClearCommand = new RelayCommand(CancelNavigationClear);
        BeginDpReplaceFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Replace, DpReplaceMode, showNumber: true));
        BeginCtrlRamReplaceFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Replace, CtrlRamReplaceMode, showNumber: true));
        BeginGeneralReplaceFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Replace, GeneralReplaceMode, showNumber: true));
        ShowHexEditorCommand = new RelayCommand(ShowHexEditor);
        RequestHexEditorSaveCommand = new RelayCommand(RequestHexEditorSave, CanRequestHexEditorSave);
        RequestHexEditorUndoCommand = new RelayCommand(RequestHexEditorUndo, CanRequestHexEditorUndo);
        RequestHexEditorRedoCommand = new RelayCommand(RequestHexEditorRedo, CanRequestHexEditorRedo);
        BeginNormalMergeFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Merge, NormalMergeMode, showNumber: false));
        BeginGeneralMergeFromHomeCommand = new RelayCommand(() => BeginWorkflowContext(ShellPage.Merge, GeneralMergeMode, showNumber: false));
        ConfirmWorkflowContextCommand = new RelayCommand(ConfirmWorkflowContext);
        CancelWorkflowContextCommand = new RelayCommand(CancelWorkflowContext);
        AcceptFirmwareIcMismatchCommand = new RelayCommand(AcceptFirmwareIcMismatch);
        DismissFirmwareIcMismatchCommand = new RelayCommand(DismissFirmwareIcMismatch);
        AcceptFirmwareNumberMismatchCommand = new RelayCommand(AcceptFirmwareNumberMismatch);
        DismissFirmwareNumberMismatchCommand = new RelayCommand(DismissFirmwareNumberMismatch);
        AddGeneralReplaceMappingCommand = new RelayCommand(AddGeneralReplaceMapping);
        AddGeneralMergeMappingCommand = new RelayCommand(AddGeneralMergeMapping);
        PreviewMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: false, outputPath: null),
            CanRunMerge);
        BuildMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: true, outputPath: null),
            () => CanBuildMerge);
        PreviewReplaceCommand = new AsyncRelayCommand(
            () => RunReplaceAsync(build: false, outputPath: null, ctrlRamFirmwareVersionEdit: null),
            CanRunReplace);
        BuildReplaceCommand = new AsyncRelayCommand(
            () => RunReplaceAsync(build: true, outputPath: null, ctrlRamFirmwareVersionEdit: null),
            () => CanBuildReplace);
        ShowReportCommand = new RelayCommand(ShowReport, () => CanOpenReport);
        CloseReportCommand = new RelayCommand(CloseReport);
        DismissReportToastCommand = new RelayCommand(DismissReportToast);
        ShowReportHistoryCommand = new RelayCommand(ShowReportHistory, () => CanOpenReportHistory);
        CloseReportHistoryCommand = new RelayCommand(CloseReportHistory);
        ClearReportHistoryCommand = new RelayCommand(ClearReportHistory, () => CanClearReportHistory);
        OpenReportHistoryEntryAsyncCommand = new AsyncRelayCommand<ReportHistoryEntryViewModel>(OpenReportHistoryEntryAsync);
        OpenReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(
            entry => OpenReportHistoryEntryAsyncCommand.Execute(entry),
            entry => OpenReportHistoryEntryAsyncCommand.CanExecute(entry));
        OpenReportHistoryEntryAsyncCommand.CanExecuteChanged += OpenReportHistoryEntryAsyncCommand_CanExecuteChanged;
        RemoveReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(RemoveReportHistoryEntry);
        ShowReplaceSelectionCommand = new RelayCommand(ShowReplaceSelection);
        CloseReplaceSelectionCommand = new RelayCommand(CloseReplaceSelection);
        SelectCtrlRamFirmwareVersionPreserveCommand = new RelayCommand(SelectCtrlRamFirmwareVersionPreserve);
        SelectCtrlRamFirmwareVersionEditCommand = new RelayCommand(SelectCtrlRamFirmwareVersionEdit);
        CloseCtrlRamFirmwareVersionCommand = new RelayCommand(CloseCtrlRamFirmwareVersionModal);
        CloseBuildCompletedModalCommand = new RelayCommand(CloseBuildCompletedModal);
        NavigationTrail.Add(CreateNavigationEntry(ShellPage.Home, isCurrent: true));
        _isInitializing = false;
    }
}
