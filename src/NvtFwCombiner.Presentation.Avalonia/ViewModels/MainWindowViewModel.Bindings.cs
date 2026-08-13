using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public string ShellVersion { get; }

    public string AppVersion { get; }

    public ShellTextResources Text { get; private set; } = ShellTextResources.For(ShellLanguage.English);

    public HexEditorWorkspaceViewModel HexEditorWorkspace =>
        _deferredState.GetHexEditorWorkspace(
            Text,
            _rawBinaryEditorFileSessions,
            HexEditorWorkspace_OnPropertyChanged);

    internal HexEditorWorkspaceViewModel? LoadedHexEditorWorkspace => _deferredState.LoadedHexEditorWorkspace;

    public string WorkspaceTitle { get; private set; } = string.Empty;

    public string WorkspaceSummary { get; private set; } = string.Empty;

    public PlanningCardText SettingsPreview { get; private set; } = ShellTextResources.For(ShellLanguage.English).SettingsPreview;

    public ShellPage SelectedPage { get; private set; } = ShellPage.Home;

    public bool IsHomeVisible => SelectedPage == ShellPage.Home;

    public bool IsSettingsVisible => SelectedPage == ShellPage.Settings;

    public bool IsMergeVisible => SelectedPage == ShellPage.Merge;

    public bool IsReplaceVisible => SelectedPage == ShellPage.Replace;

    public bool IsHexEditorVisible => SelectedPage == ShellPage.HexEditor;

    public IRelayCommand ShowHomeCommand { get; }

    public IRelayCommand ShowSettingsCommand { get; }

    public IRelayCommand ShowMergeCommand { get; }

    public IRelayCommand ShowReplaceCommand { get; }

    /// <summary>Home entry command that collects Replace context before opening DP Replace.</summary>
    public IRelayCommand BeginDpReplaceFromHomeCommand { get; }

    /// <summary>Home entry command that collects Replace context before opening CtrlRAM Replace.</summary>
    public IRelayCommand BeginCtrlRamReplaceFromHomeCommand { get; }

    /// <summary>Home entry command that collects Replace context before opening General Replace.</summary>
    public IRelayCommand BeginGeneralReplaceFromHomeCommand { get; }

    public IRelayCommand ShowHexEditorCommand { get; }

    public IRelayCommand RequestHexEditorSaveCommand { get; }

    public IRelayCommand RequestHexEditorUndoCommand { get; }

    public IRelayCommand RequestHexEditorRedoCommand { get; }

    /// <summary>Home entry command that collects Merge context before opening Standard Merge.</summary>
    public IRelayCommand BeginNormalMergeFromHomeCommand { get; }

    public IRelayCommand BeginAbMergeFromHomeCommand { get; }

    /// <summary>Home entry command that collects Merge context before opening General Merge.</summary>
    public IRelayCommand BeginGeneralMergeFromHomeCommand { get; }

    public IRelayCommand<string> RevealFileCommand { get; }

}
