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
    private const string MergeTpSlotId = WorkbenchSlotIds.MergeTp;
    private const string MergeLdcSlotId = WorkbenchSlotIds.MergeLdc;
    private const string ReplaceBaseSlotId = WorkbenchSlotIds.ReplaceBase;
    private static readonly IReadOnlyList<string> s_standardMergeModeChoices =
        Array.AsReadOnly([NormalMergeMode, GeneralMergeMode]);
    private static readonly IReadOnlyList<string> s_abMergeModeChoices =
        Array.AsReadOnly([NormalMergeMode, AbCodeMergeMode, GeneralMergeMode]);
    private readonly Dictionary<string, string> _abMergeAddressSpaceBySlotId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FirmwareSlotViewModel> _abMergeSlotsByAddressSpace = new(StringComparer.Ordinal);

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
    private readonly FirmwareSlotViewModel _mergeLdcSlot = new(
        MergeLdcSlotId,
        "LDC BIN",
        "Optional LDC payload when the selected profile exposes an LDC region",
        FirmwareSlotKind.Dp,
        isOptional: true);
    private int _generalReplaceMappingCounter;
    private int _generalMergeMappingCounter;
    private readonly DeferredShellState _deferredState = new();
    private readonly IFileRevealService _fileRevealService;
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
        _ctrlRamFirmwareVersionMetadataReader = firmwareConfigMetadataReader;
        _firmwareInspectionSession = new FirmwareInspectionSession(firmwareInspectionReader);
        ShellVersion = shellVersion;
        AppVersion = appVersion;
        Settings = new SettingsViewModel(appVersion);
        CompositionProgress = new CompositionRunProgressViewModel(language);
        SelectedLanguage = language == ShellLanguage.ChineseTraditional ? "Traditional Chinese" : "English";
        CompositionProgress.PropertyChanged += CompositionProgress_OnPropertyChanged;
        ApplyTextResources(language, notify: false);
        Reports = new ReportPresentationViewModel(() => Text, CloseReplaceSelectionForRun);
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
            RefreshCtrlRamDisplayFromInspection,
            Reports.SetShellToast);
        WorkflowSession.PropertyChanged += WorkflowSession_OnPropertyChanged;
        BuildResult = new BuildResultViewModel(_fileRevealService, () => Text.BuildCompletedOpenFolderError);
        BuildResult.PropertyChanged += BuildResult_OnPropertyChanged;
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
        ShowReplaceSelectionCommand = new RelayCommand(ShowReplaceSelection);
        CloseReplaceSelectionCommand = new RelayCommand(CloseReplaceSelection);
        SelectCtrlRamFirmwareVersionPreserveCommand = new RelayCommand(SelectCtrlRamFirmwareVersionPreserve);
        SelectCtrlRamFirmwareVersionEditCommand = new RelayCommand(SelectCtrlRamFirmwareVersionEdit);
        CloseCtrlRamFirmwareVersionCommand = new RelayCommand(CloseCtrlRamFirmwareVersionModal);
        AcceptAbAFlashCodeDeliveryPromptCommand = new RelayCommand(AcceptAbAFlashCodeDeliveryPrompt);
        DeclineAbAFlashCodeDeliveryPromptCommand = new RelayCommand(DeclineAbAFlashCodeDeliveryPrompt);
        RevealFileCommand = new RelayCommand<string>(RevealFile);
        NavigationTrail.Add(CreateNavigationEntry(ShellPage.Home, isCurrent: true));
        PropertyChanged += MainWindowViewModel_OnPropertyChanged;
        _isInitializing = false;
    }
}
