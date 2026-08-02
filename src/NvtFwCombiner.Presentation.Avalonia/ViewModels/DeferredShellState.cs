using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Loads page-specific shell projections once without weakening their source contracts.</summary>
internal sealed class DeferredShellState
{
    internal bool IsSettingsLoaded { get; private set; }

    internal HexEditorWorkspaceViewModel? LoadedHexEditorWorkspace { get; private set; }

    internal HexEditorWorkspaceViewModel GetHexEditorWorkspace(
        ShellTextResources text,
        PropertyChangedEventHandler propertyChangedHandler)
    {
        if (LoadedHexEditorWorkspace is null)
        {
            LoadedHexEditorWorkspace = new HexEditorWorkspaceViewModel(text);
            LoadedHexEditorWorkspace.PropertyChanged += propertyChangedHandler;
        }

        return LoadedHexEditorWorkspace;
    }

    internal void EnsureSettings(Action load)
    {
        if (IsSettingsLoaded)
        {
            return;
        }

        load();
        IsSettingsLoaded = true;
    }

}
