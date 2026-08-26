using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
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
            () => Text,
            hostServices.VersionManagement);
        OutputDelivery = new OutputDeliveryConfirmationViewModel(
            _compositionServices.OutputNaming,
            () => Text);
        OutputDelivery.PropertyChanged += OutputDelivery_OnPropertyChanged;
        Merge = new MergePresentationViewModel(
            _compositionServices,
            () => Text,
            new MergeStateBindings(
                () => WorkflowSession!.GetWorkflowPageIc(WorkflowInspectionOwner.Merge),
                () => WorkflowSession!.GetWorkflowPageNumber(WorkflowInspectionOwner.Merge),
                (icId, workflowId) => WorkflowSession!.IsPublishedWorkflowAuthorable(icId, workflowId),
                icId => WorkflowSession!.GetPublishedAbMergeTopologyChoices(icId),
                IsCompositionRunInProgress,
                IsGlobalBuildBlocked,
                IsWorkflowLoaded,
                IsWorkflowLoading,
                static slot => slot.InspectedFileLength,
                () => Reports!,
                RunCompositionAsync,
                PublishLastRunResult,
                RefreshWorkflowNumberChoices,
                () => WorkflowSession!.NotifyContextTextChanged(),
                () => WorkflowSession!.RefreshRetainedMergeFirmwareInspectionsIfStaleAsync(),
                (path, cancellationToken) => WorkflowSession!.SetAbSameTpFileAsync(path, cancellationToken),
                ResetRunResultForContextChange,
                () => RefreshCommandState(),
                OutputDelivery));
        Merge.PropertyChanged += Merge_OnPropertyChanged;
        Replace = new ReplacePresentationViewModel(
            _compositionServices,
            new ReplaceStateBindings(
                () => Text,
                () => WorkflowSession!.GetWorkflowPageIc(WorkflowInspectionOwner.Replace),
                () => WorkflowSession!.GetWorkflowPageNumber(WorkflowInspectionOwner.Replace),
                (icId, workflowId) => WorkflowSession!.IsPublishedWorkflowAuthorable(icId, workflowId),
                IsCompositionRunInProgress,
                IsGlobalBuildBlocked,
                IsWorkflowLoaded,
                static slot => slot.InspectedFileLength,
                GetSelectedReplaceBaseInspection,
                 () => Reports!,
                 RunCompositionAsync,
                 ShowDiagnosticPreviewAsync,
                 ShowActionReadiness,
                 WorkflowReplaceModeChanged,
                ResetRunResultForContextChange,
                () => RefreshCommandState(refreshReplaceReadiness: false),
                OutputDelivery));
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
            RecordSystemActivity,
            new WorkflowSessionStateBindings(
                () => SelectedPage,
                IsCompositionRunInProgress,
                ActiveRunShowsNumberSelector,
                GetDisplayedDeviceIc,
                GetDisplayedDeviceNumber,
                GetDisplayedDeviceContextRefreshSummary,
                ResetRunResultForContextChange,
                () => RefreshCommandState(),
                () => RefreshCommandState(refreshReplaceReadiness: false),
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
                () => RefreshCommandState(),
                NotifyShellRunStateChanged));
        RunSession.PropertyChanged += RunSession_OnPropertyChanged;
        MessageCenter = new MessageCenterViewModel(
            () => Text,
            _systemInformationService,
            hostServices.ExternalEnvironmentLoader,
            systemDiagnosticsExporter ?? hostServices.SystemDiagnosticsExporter,
            Reports,
            MessageCenterDiagnosticsChanged);
        MessageCenter.PropertyChanged += MessageCenter_OnPropertyChanged;
        ApplyTextResources(language, notify: false);
        RelayCommand CreateCatalogCommand(Action execute, params string[] workflowIds)
        {
            return new RelayCommand(execute, () => WorkflowSession.IsCanonicalCatalogReady &&
                WorkflowSession.HasPublishedWorkflowAuthoringChoices(workflowIds));
        }
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        CloseSettingsCommand = new RelayCommand(CloseSettings);
        ShowHexEditorCommand = new RelayCommand(ShowHexEditor);
        RequestHexEditorSaveCommand = new RelayCommand(RequestHexEditorSave, CanRequestHexEditorSave);
        RequestHexEditorUndoCommand = new RelayCommand(RequestHexEditorUndo, CanRequestHexEditorUndo);
        RequestHexEditorRedoCommand = new RelayCommand(RequestHexEditorRedo, CanRequestHexEditorRedo);
        Navigation = new ShellNavigationViewModel(new ShellNavigationBindings(
            () => SelectedPage, () => Text, WorkflowSession.HasSelectedInputs,
            WorkflowSession.InvalidateFirmwareNumberMismatch, WorkflowSession.ClearSelectedInputs,
            ApplySelectedPage, PageLabel, NotifyCompositionActionRailVisibilityChanged));
        ShowHomeCommand = new RelayCommand(() => Navigation.NavigateToPage(ShellPage.Home));
        ShowMergeCommand = CreateCatalogCommand(
            () => Navigation.NavigateToPage(ShellPage.Merge),
            NormalMergeMode, AbCodeMergeMode, GeneralMergeMode);
        ShowReplaceCommand = CreateCatalogCommand(
            () => Navigation.NavigateToPage(ShellPage.Replace),
            DpReplaceMode, CtrlRamReplaceMode, GeneralReplaceMode);
        BeginDpReplaceFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, DpReplaceMode, showNumber: true), DpReplaceMode);
        BeginCtrlRamReplaceFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, CtrlRamReplaceMode, showNumber: true), CtrlRamReplaceMode);
        BeginGeneralReplaceFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Replace, GeneralReplaceMode, showNumber: true), GeneralReplaceMode);
        BeginNormalMergeFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Merge, NormalMergeMode, showNumber: false), NormalMergeMode);
        BeginAbMergeFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Merge, AbCodeMergeMode, showNumber: false),
            AbCodeMergeMode);
        BeginGeneralMergeFromHomeCommand = CreateCatalogCommand(
            () => WorkflowSession.BeginWorkflowContext(ShellPage.Merge, GeneralMergeMode, showNumber: false), GeneralMergeMode);
        RevealFileCommand = new RelayCommand<string>(RevealFile);
        PropertyChanged += MainWindowViewModel_OnPropertyChanged;
        _isInitializing = false;
    }
}
