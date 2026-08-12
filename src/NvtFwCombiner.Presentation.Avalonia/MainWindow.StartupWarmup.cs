using Avalonia.Controls;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private async Task WarmDeferredShellAsync(
        MainWindowViewModel viewModel,
        CancellationToken cancellationToken)
    {
        await WarmContentAsync(
            DeviceContextHost,
            viewModel,
            "startup-warmup.device-context",
            cancellationToken);
        await WarmContentAsync(
            ReplacePageHost,
            viewModel.Replace,
            "startup-warmup.replace-view",
            cancellationToken);
        await WarmContentAsync(
            MergePageHost,
            viewModel.Merge,
            "startup-warmup.merge-view",
            cancellationToken);
        await WarmContentAsync(
            SettingsPageHost,
            viewModel,
            "startup-warmup.settings-view",
            cancellationToken);
        await WarmContentAsync(
            HexEditorPageHost,
            viewModel,
            "startup-warmup.hex-editor-view",
            cancellationToken);
    }

    private async Task WarmContentAsync(
        ContentControl host,
        object dataContext,
        string traceStage,
        CancellationToken cancellationToken)
    {
        await RunWarmupStepAsync(
            () => MaterializeContent(host, dataContext),
            traceStage,
            cancellationToken);
    }

    private static void MaterializeContent(ContentControl host, object dataContext)
    {
        if (host.Content is not null)
        {
            return;
        }

        Control? content = host.ContentTemplate?.Build(dataContext);
        if (content is null)
        {
            LoadContent(host, shouldLoad: true, dataContext);
            return;
        }

        content.DataContext = dataContext;
        host.ContentTemplate = null;
        host.Content = content;
    }

    private async Task RunWarmupStepAsync(
        Action action,
        string traceStage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (DataContext is MainWindowViewModel { RunSession.IsRunInProgress: true })
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                _startupTrace.Mark($"{traceStage}.started");
                action();
                _startupTrace.Mark($"{traceStage}.ready");
            },
            DispatcherPriority.Background,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
