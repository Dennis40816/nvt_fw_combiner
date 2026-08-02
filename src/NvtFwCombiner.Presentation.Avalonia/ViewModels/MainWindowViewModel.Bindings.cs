using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static readonly IReadOnlyList<string> s_abMergeIcChoices =
        Array.AsReadOnly([.. WorkbenchCompositionService.GetAbMergeProfileSummaries().Select(static profile => profile.IcId)]);

    private static string DefaultIcId => WorkbenchCompositionService.GetDefaultIcId();

    /// <summary>Gets the shell milestone label.</summary>
    public string ShellVersion { get; }

    /// <summary>Gets the product version.</summary>
    public string AppVersion { get; }

    /// <summary>Gets the active localized text bundle.</summary>
    public ShellTextResources Text { get; private set; } = ShellTextResources.For(ShellLanguage.English);

    /// <summary>Gets the standalone raw-BIN utility workspace exposed from Home Util Tools.</summary>
    public HexEditorWorkspaceViewModel HexEditorWorkspace =>
        _deferredState.GetHexEditorWorkspace(Text, HexEditorWorkspace_OnPropertyChanged);

    internal HexEditorWorkspaceViewModel? LoadedHexEditorWorkspace => _deferredState.LoadedHexEditorWorkspace;

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; private set; } = string.Empty;

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; private set; } = string.Empty;

    /// <summary>Gets the shared device context status text.</summary>
    public string DeviceContextStatus => IsNumberSelectorVisible
        ? $"{RunSession.DisplayedDeviceIc} / {RunSession.DisplayedDeviceNumber}: {RunSession.DisplayedDeviceContextRefreshSummary}"
        : $"{RunSession.DisplayedDeviceIc}: {RunSession.DisplayedDeviceContextRefreshSummary}";

    /// <summary>Gets IC choices admitted by the active authoring context.</summary>
    public IReadOnlyList<string> IcChoices => IsAbMergeContextActive
        ? s_abMergeIcChoices
        : WorkbenchCompositionService.GetSupportedIcIds();

    /// <summary>Gets settings card content.</summary>
    public PlanningCardText SettingsPreview { get; private set; } = ShellTextResources.For(ShellLanguage.English).SettingsPreview;

    /// <summary>Gets the selected shell page.</summary>
    public ShellPage SelectedPage { get; private set; } = ShellPage.Home;

    /// <summary>True when the clean home view is visible.</summary>
    public bool IsHomeVisible => SelectedPage == ShellPage.Home;

    /// <summary>True when the Settings page is visible.</summary>
    public bool IsSettingsVisible => SelectedPage == ShellPage.Settings;

    /// <summary>True when the Merge page is visible.</summary>
    public bool IsMergeVisible => SelectedPage == ShellPage.Merge;

    /// <summary>True when the Replace page is visible.</summary>
    public bool IsReplaceVisible => SelectedPage == ShellPage.Replace;

    /// <summary>True when the independent raw-BIN Hex Editor utility page is visible.</summary>
    public bool IsHexEditorVisible => SelectedPage == ShellPage.HexEditor;

    /// <summary>True only while the visible Merge page is authoring AB Code.</summary>
    private bool IsAbMergeContextActive => IsMergeVisible && Merge.IsAbCodeMergeModeSelected;

    /// <summary>Owner-defined IC-family relationship shown without changing firmware maps.</summary>
    public WorkbenchIcFamilySummary SelectedIcFamilySummary =>
        WorkbenchCompositionService.GetIcFamilySummary(SelectedIc);

    /// <summary>Localized label for an owner-defined IC family.</summary>
    public string SelectedIcFamilyLabel => Text.GetIcFamilyLabel(SelectedIcFamilySummary.Relationship);

    /// <summary>Localized boundary of reusable family facts.</summary>
    public string SelectedIcFamilyTooltip => Text.GetIcFamilyTooltip(SelectedIcFamilySummary);

    /// <summary>True when the selected IC has an owner-defined family relation.</summary>
    public bool HasSelectedIcFamily => SelectedIcFamilySummary.FamilyId is not null;

    /// <summary>Concise family value shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailFamily => Text.GetIcDetailFamilyValue(SelectedIcFamilySummary);

    /// <summary>Owner-declared fact reuse scope shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailReuse => Text.GetIcDetailReuseValue(SelectedIcFamilySummary);

    /// <summary>Typed executable workflow inventory shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailRuntime => Text.GetIcDetailRuntimeValue(
        Merge.IsStandardMergeSupported,
        Merge.IsAbMergeSupported,
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, DpReplaceMode).IsAvailable,
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, CtrlRamReplaceMode).IsAvailable,
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, GeneralReplaceMode).IsAvailable);

    /// <summary>Evidence summary shown without badge clusters.</summary>
    public string SelectedIcDetailEvidence => Text.GetIcDetailEvidenceValue(
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, DpReplaceMode),
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, CtrlRamReplaceMode),
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, GeneralReplaceMode));

    /// <summary>Support boundary shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailSupport => Text.GetIcDetailSupportValue(Merge.IsAbMergeSupported);

    /// <summary>Screen-reader equivalent of the visible IC detail card.</summary>
    public string SelectedIcDetailAutomationText => string.Join(
        Environment.NewLine,
        SelectedIc,
        $"{Text.IcDetailFamilyLabel}: {SelectedIcDetailFamily}",
        $"{Text.IcDetailReuseLabel}: {SelectedIcDetailReuse}",
        $"{Text.IcDetailRuntimeLabel}: {SelectedIcDetailRuntime}",
        $"{Text.IcDetailEvidenceLabel}: {SelectedIcDetailEvidence}",
        $"{Text.IcDetailSupportLabel}: {SelectedIcDetailSupport}");

    /// <summary>Command that returns to the clean home view.</summary>
    public IRelayCommand ShowHomeCommand { get; }

    /// <summary>Command that opens Settings.</summary>
    public IRelayCommand ShowSettingsCommand { get; }

    /// <summary>Command that opens Merge.</summary>
    public IRelayCommand ShowMergeCommand { get; }

    /// <summary>Command that opens Replace.</summary>
    public IRelayCommand ShowReplaceCommand { get; }

    /// <summary>Home entry command that collects Replace context before opening DP Replace.</summary>
    public IRelayCommand BeginDpReplaceFromHomeCommand { get; }

    /// <summary>Home entry command that collects Replace context before opening CtrlRAM Replace.</summary>
    public IRelayCommand BeginCtrlRamReplaceFromHomeCommand { get; }

    /// <summary>Home entry command that collects Replace context before opening General Replace.</summary>
    public IRelayCommand BeginGeneralReplaceFromHomeCommand { get; }

    /// <summary>Command that opens the independent Hex Editor workspace.</summary>
    public IRelayCommand ShowHexEditorCommand { get; }

    /// <summary>Window-level save shortcut scoped to the active raw-BIN Hex Editor page.</summary>
    public IRelayCommand RequestHexEditorSaveCommand { get; }

    /// <summary>Window-level undo shortcut scoped to the active raw-BIN Hex Editor page.</summary>
    public IRelayCommand RequestHexEditorUndoCommand { get; }

    /// <summary>Window-level redo shortcut scoped to the active raw-BIN Hex Editor page.</summary>
    public IRelayCommand RequestHexEditorRedoCommand { get; }

    /// <summary>Home entry command that collects Merge context before opening Standard Merge.</summary>
    public IRelayCommand BeginNormalMergeFromHomeCommand { get; }

    /// <summary>Home entry command that limits IC selection to the admitted AB pilot.</summary>
    public IRelayCommand BeginAbMergeFromHomeCommand { get; }

    /// <summary>Home entry command that collects Merge context before opening General Merge.</summary>
    public IRelayCommand BeginGeneralMergeFromHomeCommand { get; }

    /// <summary>Command that reveals one selected or recently generated BIN in Explorer.</summary>
    public IRelayCommand<string> RevealFileCommand { get; }

    /// <summary>Gets grouped display choices for the IC-count control.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<IcNumberChoiceViewModel> NumberSelectionChoices { get; set; } = [];

    /// <summary>Gets or sets the selected displayed IC-count choice while retaining its planner token.</summary>
    public IcNumberChoiceViewModel? SelectedNumberChoice
    {
        get => NumberSelectionChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal));
        set
        {
            if (value is not null && !string.Equals(SelectedNumber, value.Token, StringComparison.Ordinal))
            {
                SelectedNumber = value.Token;
            }
        }
    }

    /// <summary>Gets or sets the selected IC id in the shared context row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedIc { get; set; } = DefaultIcId;

    /// <summary>Gets or sets the selected IC count/variant in the shared context row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedNumber { get; set; } = WorkbenchIcNumberTokens.SingleChip;

}
