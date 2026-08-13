namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// Isolates failures from already-committed Presentation notification or projection sinks.
/// Never wrap validation, I/O, Application work, or another semantic state transition here.
/// </summary>
internal static class PresentationObserver
{
    internal static void Invoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Presentation observer failed: {0}", exception);
        }
    }
}
