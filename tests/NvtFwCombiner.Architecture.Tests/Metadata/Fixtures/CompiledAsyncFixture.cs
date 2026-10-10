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
        internal static System.Func<System.Threading.Tasks.Task> AsyncClosure(System.Threading.Tasks.Task completion)
        {
            return async () => { GC.KeepAlive(completion); await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false); global::Avalonia.Controls.ForbiddenAsyncControl.Touch(); };
        }
        internal static Action Pick(string value)
        {
            return () => { GC.KeepAlive(value); global::Avalonia.Controls.ForbiddenAsyncControl.Touch(); };
        }
        internal static Action Pick(int value)
        {
            return () => { GC.KeepAlive(value); global::Avalonia.Controls.ForbiddenAsyncControl.Touch(); };
        }
        internal static Action Generic<T>(T value)
        {
            return () => { GC.KeepAlive(value); global::Avalonia.Controls.ForbiddenAsyncControl.Touch(); };
        }
        internal static Action Generic<T, TIgnored>(T value)
        {
            return () => { GC.KeepAlive(value); global::Avalonia.Controls.ForbiddenAsyncControl.Touch(); };
        }
        internal static async Task AfterAwaitAsync(Task completion)
        {
            GC.KeepAlive(completion);
            await Task.CompletedTask.ConfigureAwait(false);
            global::Avalonia.Controls.ForbiddenAsyncControl.Touch();
        }
    }
}
