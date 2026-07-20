using Avalonia.Controls;
using Avalonia.Threading;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private static void PrimeDeferredCatalogs(
        string icId,
        string number,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = WorkbenchCompositionService.GetSettingsSnapshot();
        _ = WorkbenchCompositionService.GetNumberSelectionChoices(icId);
        _ = WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(icId);
        _ = WorkbenchCompositionService.GetStandardMergeRequiredAddressSpaces(icId);
        _ = WorkbenchCompositionService.GetCtrlRamRegions(icId, number, basePath: null);
        _ = WorkbenchCompositionService.GetReplaceInputSlots(
            icId,
            number,
            WorkbenchReplaceModes.Dp,
            basePath: null);
        _ = WorkbenchCompositionService.GetReplaceInputSlots(
            icId,
            number,
            WorkbenchReplaceModes.CtrlRam,
            basePath: null);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task WarmDeferredShellAsync(
        MainWindowViewModel viewModel,
        CancellationToken cancellationToken)
    {
        await WarmContentAsync(
            DeviceContextHost,
            viewModel,
            "startup-warmup.device-context.ready",
            cancellationToken);
        await WarmContentAsync(
            ReplacePageHost,
            viewModel,
            "startup-warmup.replace-view.ready",
            cancellationToken);
        await WarmContentAsync(
            MergePageHost,
            viewModel,
            "startup-warmup.merge-view.ready",
            cancellationToken);
        await WarmContentAsync(
            SettingsPageHost,
            viewModel,
            "startup-warmup.settings-view.ready",
            cancellationToken);
        await WarmContentAsync(
            HexEditorPageHost,
            viewModel,
            "startup-warmup.hex-editor-view.ready",
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
        if (DataContext is MainWindowViewModel { IsRunInProgress: true })
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Background, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _startupTrace.Mark(traceStage);
    }
}
