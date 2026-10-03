using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>One capture-run boundary for History insertion and all shell local-state queues.</summary>
internal sealed class CapturePersistenceScope(bool capture)
{
    internal bool AllowHistoryInsertion => !capture;
    internal bool AllowLocalStateWrites => !capture;

    /// <summary>Slice 3 may install a saved-value exporter; no appearance override installs it in slice 1.</summary>
    internal Func<MainWindowViewModel, ShellPreferenceSnapshot> PreferenceExporter { get; set; } =
        static viewModel => viewModel.ExportShellPreferences();

    internal void QueueLocalState(Action enqueue)
    {
        if (AllowLocalStateWrites)
        {
            enqueue();
        }
    }

    internal ShellPreferenceSnapshot ExportPreferences(MainWindowViewModel viewModel)
    {
        return PreferenceExporter(viewModel);
    }
}
