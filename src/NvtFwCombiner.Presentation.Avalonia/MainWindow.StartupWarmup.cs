using Avalonia.Controls;
using Avalonia.Threading;
using System.ComponentModel;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private async Task WarmDeferredShellAsync(
        MainWindowViewModel viewModel,
        Action<int, int> progress,
        Func<bool> isCurrent,
        CancellationToken cancellationToken)
    {
        (ContentControl Host, object Context, string Trace)[] views =
        [
            (DeviceContextHost, viewModel, "startup-warmup.device-context"),
            (ReplacePageHost, viewModel.Replace, "startup-warmup.replace-view"),
            (MergePageHost, viewModel.Merge, "startup-warmup.merge-view"),
            (SettingsModalHost, viewModel, "startup-warmup.settings-view"),
            (HexEditorPageHost, viewModel, "startup-warmup.hex-editor-view"),
        ];
        progress(0, views.Length);
        for (int index = 0; index < views.Length; index++)
        {
            (ContentControl host, object context, string trace) = views[index];
            do
            {
                await WaitForRunIdleAsync(viewModel.RunSession, cancellationToken);
            }
            while (!await TryRunWarmupStepAsync(
                () => MaterializeContent(host, context),
                viewModel.RunSession,
                isCurrent,
                trace,
                cancellationToken));
            progress(index + 1, views.Length);
        }
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

    private async Task<bool> TryRunWarmupStepAsync(
        Action action,
        CompositionRunPresentationViewModel runSession,
        Func<bool> isCurrent,
        string traceStage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return TryApplyWarmupStep(
                    () =>
                    {
                        _startupTrace.Mark($"{traceStage}.started");
                        action();
                        _startupTrace.Mark($"{traceStage}.ready");
                    },
                    runSession,
                    isCurrent);
            },
            DispatcherPriority.Background,
            cancellationToken);
    }

    internal static bool TryApplyWarmupStep(
        Action action,
        CompositionRunPresentationViewModel runSession,
        Func<bool> isCurrent)
    {
        if (!isCurrent() || runSession.IsRunInProgress)
        {
            return false;
        }
        action();
        return true;
    }

    internal static async Task WaitForRunIdleAsync(
        CompositionRunPresentationViewModel runSession,
        CancellationToken cancellationToken)
    {
        if (!runSession.IsRunInProgress)
        {
            return;
        }

        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void handler(object? _, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(CompositionRunPresentationViewModel.IsRunInProgress) &&
                !runSession.IsRunInProgress)
            {
                _ = idle.TrySetResult();
            }
        }
        runSession.PropertyChanged += handler;
        try
        {
            if (runSession.IsRunInProgress)
            {
                await idle.Task.WaitAsync(cancellationToken);
            }
        }
        finally
        {
            runSession.PropertyChanged -= handler;
        }
    }
}
