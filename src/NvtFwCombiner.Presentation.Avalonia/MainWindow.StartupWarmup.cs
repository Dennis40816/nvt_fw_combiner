using Avalonia.Controls;
using Avalonia.Threading;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using System.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private void PrimeDeferredCatalogs(
        string icId,
        string number,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _hostServices.WarmCanonicalCapabilities(cancellationToken);
        PresentationCompositionServices composition = _hostServices.Composition;
        try
        {
            _ = composition.Capabilities.GetCatalogSummary();
            _ = composition.Capabilities.GetNumberSelectionChoices(icId);
            _ = composition.Authoring.GetGeneralMergeDefaultOutputLength(icId);
            _ = composition.Authoring.GetStandardMergeRequiredAddressSpaces(icId);
            _ = composition.Authoring.GetCtrlRamRegions(icId, number, basePath: null);
            _ = composition.Memory.GetReplaceInputSlots(
                icId,
                number,
                WorkbenchReplaceModes.Dp,
                basePath: null);
            _ = composition.Memory.GetReplaceInputSlots(
                icId,
                number,
                WorkbenchReplaceModes.CtrlRam,
                basePath: null);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Trace.TraceWarning(
                "Deferred legacy catalog warm-up did not complete: {0}",
                exception.Message);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

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
            viewModel,
            "startup-warmup.replace-view",
            cancellationToken);
        await WarmContentAsync(
            MergePageHost,
            viewModel,
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
        MainWindowViewModel viewModel,
        string traceStage,
        CancellationToken cancellationToken)
    {
        await RunWarmupStepAsync(
            () => MaterializeContent(host, viewModel),
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
