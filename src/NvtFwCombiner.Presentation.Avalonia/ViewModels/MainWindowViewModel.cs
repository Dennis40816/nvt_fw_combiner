using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>View model for the 0.1.1 planning shell.</summary>
public sealed class MainWindowViewModel : ObservableObject
{
    /// <summary>Initializes the planning shell view model.</summary>
    public MainWindowViewModel(
        string shellVersion,
        string workspaceTitle,
        string workspaceSummary,
        string previewActionLabel,
        string buildActionLabel,
        string reportModalActionLabel,
        string deviceContextTitle,
        string icLabel,
        string icNumberLabel,
        string icNumberModeLabel,
        string deviceContextStatus,
        PlanningCardViewModel settingsPreview,
        PlanningCardViewModel mergePreview,
        PlanningCardViewModel replacePreview,
        string footerStatus)
    {
        ShellVersion = shellVersion;
        WorkspaceTitle = workspaceTitle;
        WorkspaceSummary = workspaceSummary;
        PreviewActionLabel = previewActionLabel;
        BuildActionLabel = buildActionLabel;
        ReportModalActionLabel = reportModalActionLabel;
        DeviceContextTitle = deviceContextTitle;
        IcLabel = icLabel;
        IcNumberLabel = icNumberLabel;
        IcNumberModeLabel = icNumberModeLabel;
        DeviceContextStatus = deviceContextStatus;
        SettingsPreview = settingsPreview;
        MergePreview = mergePreview;
        ReplacePreview = replacePreview;
        FooterStatus = footerStatus;
        ShowHomeCommand = new RelayCommand(() => SetSelectedPage(DemoShellPage.Home));
        ShowSettingsCommand = new RelayCommand(() => SetSelectedPage(DemoShellPage.Settings));
        ShowMergeCommand = new RelayCommand(() => SetSelectedPage(DemoShellPage.Merge));
        ShowReplaceCommand = new RelayCommand(() => SetSelectedPage(DemoShellPage.Replace));
        ShowDpReplaceCommand = new RelayCommand(() => SelectReplaceMode("DP"));
        ShowCtrlRamReplaceCommand = new RelayCommand(() => SelectReplaceMode("CtrlRAM"));
        ShowGeneralReplaceCommand = new RelayCommand(() => SelectReplaceMode("General"));
        ShowNormalMergeCommand = new RelayCommand(() => SelectMergeMode("Normal"));
    }

    /// <summary>Gets the shell milestone label.</summary>
    public string ShellVersion { get; }

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; }

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; }

    /// <summary>Gets the preview action label.</summary>
    public string PreviewActionLabel { get; }

    /// <summary>Gets the build action label.</summary>
    public string BuildActionLabel { get; }

    /// <summary>Gets the report modal action label.</summary>
    public string ReportModalActionLabel { get; }

    /// <summary>Gets the shared device context heading.</summary>
    public string DeviceContextTitle { get; }

    /// <summary>Gets the IC field label.</summary>
    public string IcLabel { get; }

    /// <summary>Gets the IC number field label.</summary>
    public string IcNumberLabel { get; }

    /// <summary>Gets the IC number mode field label.</summary>
    public string IcNumberModeLabel { get; }

    /// <summary>Gets the shared device context status text.</summary>
    public string DeviceContextStatus { get; }

    /// <summary>Gets supported demo IC number modes.</summary>
    public IReadOnlyList<string> IcNumberModes { get; } = ["single", "cascade"];

    /// <summary>Gets or sets the selected IC id in the shared context row.</summary>
    public string SelectedIcLabel { get; set; } = "NT51950";

    /// <summary>Gets or sets the selected IC number in the shared context row.</summary>
    public string IcNumberText { get; set; } = "51950";

    /// <summary>Gets or sets the IC number input mode shown in the shared context row.</summary>
    public string SelectedIcNumberMode { get; set; } = "single";

    /// <summary>Gets settings sample content.</summary>
    public PlanningCardViewModel SettingsPreview { get; }

    /// <summary>Gets merge preview sample content.</summary>
    public PlanningCardViewModel MergePreview { get; }

    /// <summary>Gets replace preview sample content.</summary>
    public PlanningCardViewModel ReplacePreview { get; }

    /// <summary>Gets footer status content.</summary>
    public string FooterStatus { get; }

    /// <summary>Gets the selected demo-shell page.</summary>
    public DemoShellPage SelectedPage { get; private set; } = DemoShellPage.Home;

    /// <summary>Gets the selected Replace quick-jump mode.</summary>
    public string SelectedReplaceMode { get; private set; } = "DP";

    /// <summary>Gets the selected Merge quick-jump mode.</summary>
    public string SelectedMergeMode { get; private set; } = "Normal";

    /// <summary>True when the clean home view is visible.</summary>
    public bool IsHomeVisible => SelectedPage == DemoShellPage.Home;

    /// <summary>True when the Settings page is visible.</summary>
    public bool IsSettingsVisible => SelectedPage == DemoShellPage.Settings;

    /// <summary>True when the Merge page is visible.</summary>
    public bool IsMergeVisible => SelectedPage == DemoShellPage.Merge;

    /// <summary>True when the Replace page is visible.</summary>
    public bool IsReplaceVisible => SelectedPage == DemoShellPage.Replace;

    /// <summary>Command that returns to the clean home view.</summary>
    public IRelayCommand ShowHomeCommand { get; }

    /// <summary>Command that opens Settings.</summary>
    public IRelayCommand ShowSettingsCommand { get; }

    /// <summary>Command that opens Merge.</summary>
    public IRelayCommand ShowMergeCommand { get; }

    /// <summary>Command that opens Replace.</summary>
    public IRelayCommand ShowReplaceCommand { get; }

    /// <summary>Command that opens DP Replace.</summary>
    public IRelayCommand ShowDpReplaceCommand { get; }

    /// <summary>Command that opens CtrlRAM Replace.</summary>
    public IRelayCommand ShowCtrlRamReplaceCommand { get; }

    /// <summary>Command that opens General Replace.</summary>
    public IRelayCommand ShowGeneralReplaceCommand { get; }

    /// <summary>Command that opens Normal Merge.</summary>
    public IRelayCommand ShowNormalMergeCommand { get; }

    private void SelectReplaceMode(string mode)
    {
        if (!string.Equals(SelectedReplaceMode, mode, StringComparison.Ordinal))
        {
            SelectedReplaceMode = mode;
            OnPropertyChanged(nameof(SelectedReplaceMode));
        }

        SetSelectedPage(DemoShellPage.Replace);
    }

    private void SelectMergeMode(string mode)
    {
        if (!string.Equals(SelectedMergeMode, mode, StringComparison.Ordinal))
        {
            SelectedMergeMode = mode;
            OnPropertyChanged(nameof(SelectedMergeMode));
        }

        SetSelectedPage(DemoShellPage.Merge);
    }

    private void SetSelectedPage(DemoShellPage page)
    {
        if (SelectedPage == page)
        {
            return;
        }

        SelectedPage = page;
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(IsMergeVisible));
        OnPropertyChanged(nameof(IsReplaceVisible));
    }
}

/// <summary>Top-level demo-shell page state.</summary>
public enum DemoShellPage
{
    /// <summary>Clean home view with three entry cards.</summary>
    Home,

    /// <summary>Settings planning page.</summary>
    Settings,

    /// <summary>Merge planning page.</summary>
    Merge,

    /// <summary>Replace planning page.</summary>
    Replace,
}
