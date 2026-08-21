using Avalonia.Platform.Storage;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private bool _restartThroughStableLauncher;

    private async Task ReportManagedApplicationReadyAsync(CancellationToken cancellationToken)
    {
        if (_hostServices.ApplicationReadySignal is null ||
            _hostServices.ManagedAppVersion is not { } version)
        {
            return;
        }
        _ = await _hostServices.ApplicationReadySignal.TryReportReadyAsync(
            version,
            cancellationToken);
    }

    private async Task RunVersionDiscoveryAfterReadyAsync(CancellationToken cancellationToken)
    {
        if (_hostServices.VersionManagement is not { } versionManagement)
        {
            return;
        }
        try
        {
            VersionManagementSnapshot initialized = await versionManagement.InitializeAsync(cancellationToken);
            if (DataContext is MainWindowViewModel initialViewModel)
            {
                initialViewModel.Settings.ApplyVersionSnapshot(initialized);
            }
            VersionManagementSnapshot checkedSnapshot = await versionManagement.CheckAsync(
                isAutomatic: true,
                cancellationToken);
            if (DataContext is MainWindowViewModel checkedViewModel)
            {
                checkedViewModel.Settings.ApplyVersionSnapshot(checkedSnapshot);
                if (checkedSnapshot.ShouldPromptForUpdate ||
                    checkedSnapshot.State?.RetentionReviewDue == true)
                {
                    checkedViewModel.OpenSettingsCommand.Execute(null);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async void Settings_UpdateSourceBrowseRequested(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = viewModel.Settings.UpdateSourceHeading,
            });
        if (folders.Count == 1 && folders[0].TryGetLocalPath() is { } path)
        {
            viewModel.Settings.SetUpdateSourceDraft(path);
        }
    }

    private void Settings_ActivationRequested(object? sender, EventArgs e)
    {
        _restartThroughStableLauncher = true;
        Close();
    }

    private void RestartThroughStableLauncherIfRequested()
    {
        if (!_restartThroughStableLauncher || _hostServices.StableLauncherHandoff is not { } handoff)
        {
            return;
        }
        _ = handoff.TryStartLauncherAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }
}
