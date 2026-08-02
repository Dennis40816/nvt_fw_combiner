using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Ports;
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
    private readonly DeferredShellState _deferredState = new();
    private readonly IFileRevealService _fileRevealService;
    private readonly bool _isInitializing = true;

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
            WorkbenchCompositionService.InspectFirmwareBatch,
            WorkbenchHostServices.CreateFileRevealService())
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
            WorkbenchCompositionService.InspectFirmwareBatch,
            WorkbenchHostServices.CreateFileRevealService())
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
            IReadOnlyList<WorkbenchFirmwareInspectionResult>> firmwareInspectionReader,
        IFileRevealService? fileRevealService = null)
    {
        ArgumentNullException.ThrowIfNull(firmwareConfigMetadataReader);
        ArgumentNullException.ThrowIfNull(firmwareInspectionReader);
        _fileRevealService = fileRevealService ?? WorkbenchHostServices.CreateFileRevealService();
        ShellVersion = shellVersion;
        AppVersion = appVersion;
        Settings = new SettingsViewModel(appVersion);
        Merge = new MergePresentationViewModel(
            () => Text,
            new MergeStateBindings(
                () => SelectedIc,
                () => SelectedNumber,
                () => IsRunInProgress,
                IsFirmwareInspectionLoading,
                () => _deferredState.IsWorkflowLoaded,
                () => _deferredState.IsLoadingWorkflow,
                GetInspectedFileLength,
                GetReportPresentation,
                CreateFlashCodeOutputFileName,
                RunCompositionAsync,
                PublishLastRunResult,
                RefreshNumberChoicesForSelectedIc,
                NotifyMergeSharedContextChanged,
                RefreshSelectedMergeFirmwareInspectionsAsync,
                ResetRunResultForContextChange,
                RefreshCommandState));
        Merge.PropertyChanged += Merge_OnPropertyChanged;
        Replace = new ReplacePresentationViewModel(
            new ReplaceStateBindings(
                () => Text,
                () => SelectedIc,
                () => SelectedNumber,
                () => IsRunInProgress,
                IsFirmwareInspectionLoading,
                () => _deferredState.IsWorkflowLoaded,
                GetInspectedFileLength,
                GetSelectedReplaceBaseInspection,
                GetReportPresentation,
                CreateFlashCodeOutputFileName,
                CreateCtrlRamReplaceOutputFileName,
                RunCompositionAsync,
                ReplaceModeChanged,
                ResetRunResultForContextChange,
                RefreshSelectedReplaceFirmwareInspectionsAsync,
                RefreshCommandState),
            firmwareConfigMetadataReader);
        Replace.PropertyChanged += Replace_OnPropertyChanged;
        CompositionProgress = new CompositionRunProgressViewModel(language);
        SelectedLanguage = language == ShellLanguage.ChineseTraditional ? "Traditional Chinese" : "English";
        CompositionProgress.PropertyChanged += CompositionProgress_OnPropertyChanged;
        Reports = new ReportPresentationViewModel(() => Text, Replace.CloseSelectionForRun);
        Reports.PropertyChanged += Reports_OnPropertyChanged;
        WorkflowSession = new WorkflowSessionPresentationViewModel(
            () => Text,
            () => SelectedIc,
            () => SelectedNumber,
            () => IcChoices,
            () => NumberSelectionChoices,
            () => IsReplaceVisible,
            ApplyWorkflowContext,
            value => SelectedIc = value,
            ApplyDetectedFirmwareNumber,
            Reports.SetShellToast,
            firmwareInspectionReader,
            new WorkflowInspectionBindings(
                () => RefreshContextState(),
                () => _deferredState.IsWorkflowLoaded,
                () => Replace.IsCtrlRamReplaceModeSelected,
                () => IsReplaceVisible && Replace.SelectedReplaceMode == DpReplaceMode,
                () => IsNumberSelectorVisible,
                () => Merge.IsAbCodeMergeModeSelected,
                () => Merge.HasAbMergeTopologyChoices,
                () => Merge.SelectedMergeMode,
                () => Replace.SelectedReplaceMode,
                () => Merge.MergeDpSlot,
                () => Merge.MergeTpSlot,
                () => Replace.ReplaceBaseSlot,
                () => Merge.MergeSlots,
                () => Replace.ReplaceSlots,
                () => Merge.AbMergeSlots,
                () => Merge.AbMergeAddressSpaceBySlotId,
                Merge.GetSelectedAbMergeTopologyToken,
                SelectSlotFile,
                FindSlot,
                Replace.ApplyCtrlRamInspectionDisplay,
                Merge.RefreshMergeMemoryMapState,
                Replace.RefreshReplaceMemoryMapState,
                RefreshCommandState,
                NotifySlotFileOutputNames));
        WorkflowSession.PropertyChanged += WorkflowSession_OnPropertyChanged;
        BuildResult = new BuildResultViewModel(_fileRevealService, () => Text.BuildCompletedOpenFolderError);
        BuildResult.PropertyChanged += BuildResult_OnPropertyChanged;
        ApplyTextResources(language, notify: false);
        ShowHomeCommand = new RelayCommand(() => NavigateToPage(ShellPage.Home));
        ShowSettingsCommand = new RelayCommand(() => NavigateToPage(ShellPage.Settings));
        ShowMergeCommand = new RelayCommand(() => NavigateToPage(ShellPage.Merge));
        ShowReplaceCommand = new RelayCommand(() => NavigateToPage(ShellPage.Replace));
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);
        ConfirmNavigationAndClearCommand = new RelayCommand(ConfirmNavigationAndClear);
        CancelNavigationClearCommand = new RelayCommand(CancelNavigationClear);
        BeginDpReplaceFromHomeCommand = new RelayCommand(() => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, DpReplaceMode, showNumber: true));
        BeginCtrlRamReplaceFromHomeCommand = new RelayCommand(() => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, CtrlRamReplaceMode, showNumber: true));
        BeginGeneralReplaceFromHomeCommand = new RelayCommand(() => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, GeneralReplaceMode, showNumber: true));
        ShowHexEditorCommand = new RelayCommand(ShowHexEditor);
        RequestHexEditorSaveCommand = new RelayCommand(RequestHexEditorSave, CanRequestHexEditorSave);
        RequestHexEditorUndoCommand = new RelayCommand(RequestHexEditorUndo, CanRequestHexEditorUndo);
        RequestHexEditorRedoCommand = new RelayCommand(RequestHexEditorRedo, CanRequestHexEditorRedo);
        BeginNormalMergeFromHomeCommand = new RelayCommand(() => WorkflowSession.BeginWorkflowContext(ShellPage.Merge, NormalMergeMode, showNumber: false));
        BeginAbMergeFromHomeCommand = new RelayCommand(() => WorkflowSession.BeginWorkflowContext(
            ShellPage.Merge,
            AbCodeMergeMode,
            showNumber: false,
            [.. WorkbenchCompositionService.GetAbMergeProfileSummaries().Select(static profile => profile.IcId)]));
        BeginGeneralMergeFromHomeCommand = new RelayCommand(() => WorkflowSession.BeginWorkflowContext(ShellPage.Merge, GeneralMergeMode, showNumber: false));
        RevealFileCommand = new RelayCommand<string>(RevealFile);
        NavigationTrail.Add(CreateNavigationEntry(ShellPage.Home, isCurrent: true));
        PropertyChanged += MainWindowViewModel_OnPropertyChanged;
        _isInitializing = false;
    }
}
