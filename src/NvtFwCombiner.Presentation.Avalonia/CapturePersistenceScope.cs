using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>One capture-run boundary for History insertion and all shell local-state queues.</summary>
internal sealed class CapturePersistenceScope(bool capture)
{
    // UI thread: nested, synchronous temporary UI changes suppress local-state queues.
    private int _localStateWriteSuppressionDepth;

    internal bool AllowHistoryInsertion => !capture;
    internal bool AllowLocalStateWrites => !capture && _localStateWriteSuppressionDepth == 0;

    /// <summary>UI-thread-owned exporter preserves saved values while effective launch appearance differs.</summary>
    internal Func<MainWindowViewModel, ShellPreferenceSnapshot> PreferenceExporter { get; set; } =
        static viewModel => viewModel.ExportShellPreferences();

    internal void WithoutLocalStateWrites(Action apply)
    {
        Dispatcher.UIThread.VerifyAccess();
        _localStateWriteSuppressionDepth++;
        try { apply(); }
        finally { _localStateWriteSuppressionDepth--; }
    }

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
