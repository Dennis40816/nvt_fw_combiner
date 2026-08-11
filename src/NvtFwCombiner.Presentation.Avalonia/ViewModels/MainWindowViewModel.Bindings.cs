using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Gets the shell milestone label.</summary>
    public string ShellVersion { get; }

    /// <summary>Gets the product version.</summary>
    public string AppVersion { get; }

    /// <summary>Gets the active localized text bundle.</summary>
    public ShellTextResources Text { get; private set; } = ShellTextResources.For(ShellLanguage.English);

    /// <summary>Gets the standalone raw-BIN utility workspace exposed from Home Util Tools.</summary>
    public HexEditorWorkspaceViewModel HexEditorWorkspace =>
        _deferredState.GetHexEditorWorkspace(
            Text,
            _rawBinaryEditorFileSessions,
            HexEditorWorkspace_OnPropertyChanged);

    internal HexEditorWorkspaceViewModel? LoadedHexEditorWorkspace => _deferredState.LoadedHexEditorWorkspace;

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; private set; } = string.Empty;

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; private set; } = string.Empty;

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

}
