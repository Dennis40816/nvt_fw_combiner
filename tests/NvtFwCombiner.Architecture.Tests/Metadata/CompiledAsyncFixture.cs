namespace Avalonia.Controls
{
    // Synthetic forbidden symbol; no Avalonia setup or runtime dependency is needed.
    internal sealed class ForbiddenAsyncControl
    {
        internal static void Touch() { }
    }
}
namespace NvtFwCombiner.Presentation.Avalonia.ViewModels
{
    internal sealed class AsyncBoundaryFixture
    {
        internal static Action WithClosure(object capture)
        {
            return () => { GC.KeepAlive(capture); global::Avalonia.Controls.ForbiddenAsyncControl.Touch(); };
        }
        internal static Func<Task> AsyncClosure(Task completion)
        {
            return async () => { await completion.ConfigureAwait(false); global::Avalonia.Controls.ForbiddenAsyncControl.Touch(); };
        }
        internal static async Task AfterAwait(Task completion)
        {
            await completion.ConfigureAwait(false);
            global::Avalonia.Controls.ForbiddenAsyncControl.Touch();
        }
    }
}
