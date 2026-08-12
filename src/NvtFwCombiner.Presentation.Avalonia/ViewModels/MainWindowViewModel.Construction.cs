using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string DpReplaceMode = ExperienceIds.DpReplace;
    private const string CtrlRamReplaceMode = ExperienceIds.CtrlRamReplace;
    private const string GeneralReplaceMode = ExperienceIds.GeneralReplace;
    private const string NormalMergeMode = ExperienceIds.StandardMerge;
    private const string AbCodeMergeMode = ExperienceIds.AbMerge;
    private const string GeneralMergeMode = ExperienceIds.GeneralMerge;
    private readonly PresentationCompositionServices _compositionServices;
    private readonly DeferredShellState _deferredState = new();
    private readonly IFileRevealService _fileRevealService;
    private readonly IRawBinaryEditorFileSessionFactory _rawBinaryEditorFileSessions;
    private readonly ISystemInformationService _systemInformationService;
    private readonly bool _isInitializing = true;

    /// <summary>Initializes the main desktop view model.</summary>
    internal MainWindowViewModel(
        string shellVersion,
        string appVersion,
        ShellLanguage language,
        PresentationHostServices hostServices)
        : this(
            shellVersion,
            appVersion,
            language,
            hostServices,
            hostServices.Composition.FirmwareInspection)
    {
    }

    /// <summary>Initializes the shell with a deterministic firmware-inspection adapter.</summary>
    internal MainWindowViewModel(
        string shellVersion,
        string appVersion,
        ShellLanguage language,
        PresentationHostServices hostServices,
        IFirmwareInspection firmwareInspection,
        IFileRevealService? fileRevealService = null,
        ICanonicalSupportMatrixQuery? supportMatrixQuery = null,
        ISystemInformationService? systemInformationService = null,
        ISystemDiagnosticsExporter? systemDiagnosticsExporter = null)
    {
        ArgumentNullException.ThrowIfNull(hostServices);
        ArgumentNullException.ThrowIfNull(firmwareInspection);
        _compositionServices = hostServices.Composition.WithFirmwareInspection(firmwareInspection);
        _fileRevealService = fileRevealService ?? hostServices.FileReveal;
        _rawBinaryEditorFileSessions = hostServices.RawBinaryEditorFileSessions;
        _systemInformationService = systemInformationService ?? hostServices.SystemInformation;
        ShellVersion = shellVersion;
        AppVersion = appVersion;
        Settings = new SettingsViewModel(
            appVersion,
            supportMatrixQuery ?? hostServices.SupportMatrix,
            () => Text);
        Merge = new MergePresentationViewModel(
            _compositionServices,
            () => Text,
            new MergeStateBindings(
                GetWorkflowSelectedIc,
                GetWorkflowSelectedNumber,
                IsCompositionRunInProgress,
                IsFirmwareInspectionLoading,
                IsGlobalBuildBlocked,
                IsWorkflowLoaded,
                IsWorkflowLoading,
                GetInspectedFileLength,
                GetReportPresentation,
                RunCompositionAsync,
                PublishLastRunResult,
                RefreshWorkflowNumberChoices,
                NotifyMergeSharedContextChanged,
                RefreshSelectedMergeFirmwareInspectionsAsync,
                ResetRunResultForContextChange,
                RefreshCommandState));
        Merge.PropertyChanged += Merge_OnPropertyChanged;
        Replace = new ReplacePresentationViewModel(
            _compositionServices,
            new ReplaceStateBindings(
                () => Text,
                GetWorkflowSelectedIc,
                GetWorkflowSelectedNumber,
                IsCompositionRunInProgress,
                IsFirmwareInspectionLoading,
                IsGlobalBuildBlocked,
                IsWorkflowLoaded,
                GetInspectedFileLength,
                GetSelectedReplaceBaseInspection,
                 GetReportPresentation,
                 RunCompositionAsync,
                 ShowDiagnosticPreviewAsync,
                 ShowActionReadiness,
                 WorkflowReplaceModeChanged,
                ResetRunResultForContextChange,
                RefreshSelectedReplaceFirmwareInspectionsAsync,
                RefreshCommandState));
        Replace.PropertyChanged += Replace_OnPropertyChanged;
        SelectedLanguage = language == ShellLanguage.ChineseTraditional ? "Traditional Chinese" : "English";
        Reports = new ReportPresentationViewModel(() => Text, Replace.CloseSelectionForRun);
        Reports.PropertyChanged += Reports_OnPropertyChanged;
        WorkflowSession = new WorkflowSessionPresentationViewModel(
            _compositionServices,
            () => Text,
            Merge,
            Replace,
            ApplyWorkflowContext,
            Reports.SetShellToast,
            new WorkflowSessionStateBindings(
                () => SelectedPage,
                IsCompositionRunInProgress,
                ActiveRunShowsNumberSelector,
                GetDisplayedDeviceIc,
                GetDisplayedDeviceNumber,
                GetDisplayedDeviceContextRefreshSummary,
                ResetRunResultForContextChange,
                RefreshCommandState,
                NotifyRunContextChanged));
        WorkflowSession.PropertyChanged += WorkflowSession_OnPropertyChanged;
        BuildResult = new BuildResultViewModel(_fileRevealService, () => Text.BuildCompletedOpenFolderError);
        BuildResult.PropertyChanged += BuildResult_OnPropertyChanged;
        RunSession = new CompositionRunPresentationViewModel(
            language,
            new CompositionRunStateBindings(
                () => Text,
                GetWorkflowSelectedIc,
                GetWorkflowSelectedNumber,
                GetSelectedRunMode,
                WorkflowSession.ShouldShowNumberSelectorForSelectedPage,
                () => WorkflowSession.DeviceContextRefreshSummary,
                () => IsReducedMotionEnabled,
                () => Reports,
                TryShowBuildCompleted,
                RefreshCommandState,
                NotifyShellRunStateChanged));
        RunSession.PropertyChanged += RunSession_OnPropertyChanged;
        MessageCenter = new MessageCenterViewModel(
            () => Text,
            _systemInformationService,
            systemDiagnosticsExporter ?? hostServices.SystemDiagnosticsExporter,
            Reports,
            MessageCenterDiagnosticsChanged);
        MessageCenter.PropertyChanged += MessageCenter_OnPropertyChanged;
        ApplyTextResources(language, notify: false);
        RelayCommand CreateCatalogCommand(Action execute)
        {
            return new RelayCommand(execute, () => WorkflowSession.IsCanonicalCatalogReady);
        }
        ShowHomeCommand = new RelayCommand(() => NavigateToPage(ShellPage.Home));
        ShowSettingsCommand = new RelayCommand(() => NavigateToPage(ShellPage.Settings));
        ShowMergeCommand = CreateCatalogCommand(() => NavigateToPage(ShellPage.Merge));
        ShowReplaceCommand = CreateCatalogCommand(() => NavigateToPage(ShellPage.Replace));
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);
        ConfirmNavigationAndClearCommand = new RelayCommand(ConfirmNavigationAndClear);
        CancelNavigationClearCommand = new RelayCommand(CancelNavigationClear);
        BeginDpReplaceFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, DpReplaceMode, showNumber: true));
        BeginCtrlRamReplaceFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, CtrlRamReplaceMode, showNumber: true));
        BeginGeneralReplaceFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, GeneralReplaceMode, showNumber: true));
        ShowHexEditorCommand = new RelayCommand(ShowHexEditor);
        RequestHexEditorSaveCommand = new RelayCommand(RequestHexEditorSave, CanRequestHexEditorSave);
        RequestHexEditorUndoCommand = new RelayCommand(RequestHexEditorUndo, CanRequestHexEditorUndo);
        RequestHexEditorRedoCommand = new RelayCommand(RequestHexEditorRedo, CanRequestHexEditorRedo);
        BeginNormalMergeFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Merge, NormalMergeMode, showNumber: false));
        BeginAbMergeFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(
                ShellPage.Merge,
                AbCodeMergeMode,
                showNumber: false));
        BeginGeneralMergeFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Merge, GeneralMergeMode, showNumber: false));
        RevealFileCommand = new RelayCommand<string>(RevealFile);
        NavigationTrail.Add(CreateNavigationEntry(ShellPage.Home, isCurrent: true));
        PropertyChanged += MainWindowViewModel_OnPropertyChanged;
        _isInitializing = false;
    }
}
