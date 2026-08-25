using Avalonia.Platform.Storage;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private bool _restartThroughStableLauncher;
    private bool _stableLauncherStarted;
    private bool _stableLauncherHandoffInProgress;

    private async Task ReportManagedApplicationReadyAsync(CancellationToken cancellationToken)
    {
        if (_hostServices.ManagedApplicationStartup is not { } startup)
        {
            return;
        }
        ManagedApplicationStartupResult result = await startup.CompleteStartupAsync(cancellationToken);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Settings.ApplyVersionSnapshot(result.Snapshot);
            viewModel.Settings.SetSourceChecking(result.Snapshot.State?.UpdateSource is not null);
        }
    }

    private async Task RunVersionDiscoveryAfterReadyAsync(CancellationToken cancellationToken)
    {
        if (_hostServices.VersionManagement is not { } versionManagement)
        {
            return;
        }
        try
        {
            VersionManagementSnapshot checkedSnapshot;
            try
            {
                checkedSnapshot = await versionManagement.CheckAsync(
                    isAutomatic: true,
                    cancellationToken);
            }
            finally
            {
                if (DataContext is MainWindowViewModel finalViewModel)
                {
                    finalViewModel.Settings.SetSourceChecking(false);
                }
            }
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
        RequestStableLauncherRestart();
        Close();
    }

    internal void RequestStableLauncherRestart()
    {
        _restartThroughStableLauncher = true;
    }

    internal async Task<bool> TryCompleteStableLauncherHandoffAsync()
    {
        bool started = await TryStartStableLauncherAsync();
        if (!started)
        {
            await ReportStableLauncherHandoffFailureAsync();
        }
        return started;
    }

    private async Task<bool> TryStartStableLauncherAsync()
    {
        if (!_restartThroughStableLauncher ||
            _stableLauncherStarted ||
            _hostServices.StableLauncherHandoff is not { } handoff)
        {
            return false;
        }
        bool started = await handoff.TryStartLauncherAsync(CancellationToken.None);
        _stableLauncherStarted = started;
        return started;
    }

    private async Task ReportStableLauncherHandoffFailureAsync()
    {
        IsEnabled = true;
        bool activationCleared = true;
        if (DataContext is MainWindowViewModel viewModel)
        {
            activationCleared = await viewModel.Settings.HandleLauncherHandoffFailureAsync();
        }
        _restartThroughStableLauncher = !activationCleared;
    }
}
