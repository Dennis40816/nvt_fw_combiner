using Avalonia.Controls;
using Avalonia.Threading;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real Report window lifecycle with isolated local state for control regressions.</summary>
internal static class ReportControlTestHost
{
    internal static async Task AwaitHistoryReadyAsync(Window window)
    {
        // Wait for the real startup restore before seeding or clicking through its blocking overlay.
        ShellPreloadSession session = Assert.IsType<ShellPreloadSession>(window.FindControl<Border>("OptionalPreloadStatusHost")!.DataContext);
        TaskCompletionSource restored = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (session.Stage(ShellPreloadSession.HistoryStageId).State is
                ShellPreloadStageState.Succeeded or ShellPreloadStageState.Failed or ShellPreloadStageState.Cancelled)
            {
                _ = restored.TrySetResult();
            }
        }
        session.PropertyChanged += Observe;
        try
        {
            Observe(null, new(null));
            await restored.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(ShellPreloadStageState.Succeeded, session.Stage(ShellPreloadSession.HistoryStageId).State);
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.FindControl<ContentControl>("CatalogLoadingSurfaceHost")!.IsVisible);
        }
        finally
        {
            session.PropertyChanged -= Observe;
        }
    }

    internal static async Task CloseAndFlushAsync(Window window)
    {
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        window.Close();
        if (window.DataContext is MainWindowViewModel shell && shell.Navigation.IsExitConfirmationOpen)
        {
            shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        }
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    internal static async Task<PresentationHostServices> CreateServicesAsync(
        TempWorkspace workspace, bool useRetainedDpReplacePolicy = true)
    {
        PresentationHostServices services = await Task.Run(
            () => PresentationTestHost.CreateServices("ui-smoke", static authoring => authoring,
                useRetainedDpReplacePolicy), TestContext.Current.CancellationToken);
        return new(services.Composition, services.FileReveal, services.SupportMatrix,
            services.SystemInformation, services.SystemDiagnosticsExporter, services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader, services.ExternalEnvironmentLoader,
            new IsolatedStateFiles(services.LocalFiles, workspace));
    }

    private sealed class IsolatedStateFiles(ILocalFileStore inner, TempWorkspace workspace) : ILocalFileStore
    {
        private string Redirect(string path)
        {
            // Fail closed: these UI tests may touch only their own Report/preferences state.
            Assert.True(path == ReportHistoryFileStore.DefaultHistoryPath || path == ShellPreferenceFileStore.DefaultPreferencesPath);
            return workspace.PathFor(Path.GetFileName(path));
        }

        public ValueTask<T> ReadAsync<T>(string path, long maximumBytes, Func<Stream, CancellationToken, ValueTask<T>> project, CancellationToken cancellationToken)
        {
            return inner.ReadAsync(Redirect(path), maximumBytes, project, cancellationToken);
        }

        public ValueTask<string> ReadTextAsync(string path, long maximumBytes, CancellationToken cancellationToken, Action<LocalFileReadProgress>? progress = null)
        {
            return inner.ReadTextAsync(Redirect(path), maximumBytes, cancellationToken, progress);
        }

        public ValueTask<string> ReadTextAsync(Func<CancellationToken, ValueTask<Stream>> openReadAsync, long maximumBytes, CancellationToken cancellationToken)
        {
            return inner.ReadTextAsync(openReadAsync, maximumBytes, cancellationToken);
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            return inner.WriteAsync(Redirect(path), bytes, cancellationToken);
        }
    }
}
