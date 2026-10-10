using Avalonia.Platform.Storage;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private bool _stableLauncherStarted;

    private async Task ReportManagedApplicationReadyAsync(CancellationToken cancellationToken)
    {
        if (_hostServices.ManagedApplicationStartup is not { } startup)
        {
            return;
        }
        ManagedApplicationStartupResult result = await startup.CompleteStartupAsync(cancellationToken, isReadOnly: !LaunchCoordinator.Persistence.AllowLocalStateWrites);
        if (!cancellationToken.IsCancellationRequested &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Settings.ApplyVersionSnapshot(result.Snapshot);
            viewModel.Settings.SetSourceChecking(LaunchCoordinator.StartVersionDiscovery && result.Snapshot.State?.UpdateSource is not null);
        }
    }

    internal async Task RunVersionDiscoveryAfterReadyAsync(CancellationToken cancellationToken)
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
                if (!cancellationToken.IsCancellationRequested &&
                    DataContext is MainWindowViewModel finalViewModel)
                {
                    finalViewModel.Settings.SetSourceChecking(false);
                }
            }
            if (!cancellationToken.IsCancellationRequested &&
                DataContext is MainWindowViewModel checkedViewModel)
            {
                checkedViewModel.Settings.ApplyVersionSnapshot(checkedSnapshot);
                bool authorityAvailable =
                    checkedSnapshot.StateIssue == VersionManagerStateLoadIssue.None &&
                    checkedSnapshot.InventoryIssue == ManagedVersionInventoryReadIssue.None;
                if (authorityAvailable &&
                    (checkedSnapshot.ShouldPromptForUpdate ||
                     checkedSnapshot.State?.RetentionReviewDue == true))
                {
                    checkedViewModel.OpenSettingsCommand.Execute(null);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void Settings_UpdateSourceBrowseRequested(object? sender, EventArgs e)
    {
        UiEventAdapter.Run(this, "MainWindow.UpdateSourceBrowse", _ => BrowseUpdateSourceAsync(sender, StorageProvider));
    }

    internal async Task BrowseUpdateSourceAsync(object? sender, IStorageProvider storageProvider)
    {
        CancellationToken sessionToken = _startupLoadCancellation.Token;
        if (ClosePhase != WindowClosePhase.Open || sessionToken.IsCancellationRequested ||
            sender is not SettingsViewModel settings ||
            DataContext is not MainWindowViewModel viewModel ||
            !ReferenceEquals(viewModel.Settings, settings) ||
            !viewModel.IsSettingsModalOpen ||
            settings.BeginUpdateSourceBrowse() is not { } generation)
        {
            return;
        }
        IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = viewModel.Settings.UpdateSourceHeading,
            });
        if (!sessionToken.IsCancellationRequested && ClosePhase == WindowClosePhase.Open &&
            ReferenceEquals(DataContext, viewModel) &&
            ReferenceEquals(viewModel.Settings, settings) &&
            viewModel.IsSettingsModalOpen &&
            folders.Count == 1 && folders[0].TryGetLocalPath() is { } path)
        {
            settings.SetUpdateSourceDraft(path, generation);
        }
    }

    private void Settings_ActivationRequested(object? sender, EventArgs e)
    {
        _lifetime.RequestActivation();
    }

    internal void RequestStableLauncherRestart()
    {
        _lifetime.RequestStableLauncherRestart();
    }

    internal Task<bool> TryCompleteStableLauncherHandoffAsync()
    {
        return _lifetime.TryCompleteStableLauncherHandoffAsync();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The late launcher observer owns cancellation disposal after the start task settles.")]
    private async Task<bool> TryStartStableLauncherAsync()
    {
        if (!_lifetime.RestartRequested ||
            _stableLauncherStarted ||
            _hostServices.StableLauncherHandoff is not { } handoff)
        {
            return false;
        }
        Task deadline = CloseDeadlineFactory(TimeSpan.FromSeconds(5));
        if (deadline.IsCompleted)
        {
            return false;
        }
        var cancellation = new CancellationTokenSource();
        CancellationToken startToken = cancellation.Token;
        using var stopDeadlineObserver = new CancellationTokenSource();
        Task deadlineObserver = CancelLauncherAtDeadlineAsync(
            deadline, cancellation, stopDeadlineObserver.Token);
        Task<StableLauncherStartResult>? start = null;
        try
        {
            start = Task.Run(() => handoff.TryStartLauncherAsync(startToken).AsTask(), startToken);
            if (await Task.WhenAny(start, deadline) != start || deadline.IsCompleted)
            {
                cancellation.Cancel();
                return false;
            }
            StableLauncherStartResult result = await start;
            _stableLauncherStarted = result.IsStarted;
            if (!result.IsStarted)
            {
                System.Diagnostics.Trace.TraceError(
                    "Stable launcher handoff failed: {0}, exit code: {1}",
                    result.Outcome,
                    result.ExitCode);
            }
            return result.IsStarted;
        }
        finally
        {
            try
            {
                stopDeadlineObserver.Cancel();
                await deadlineObserver;
            }
            finally
            {
                if (start is null)
                {
                    cancellation.Dispose();
                }
                else
                {
                    // Late I/O retains its cancellation source until the actual launcher call settles.
                    _ = ObserveLauncherAndDisposeAsync(start, cancellation);
                }
            }
        }
    }

    private static async Task ObserveLauncherAndDisposeAsync(
        Task<StableLauncherStartResult> start, CancellationTokenSource cancellation)
    {
        try
        {
            _ = await start.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Late stable launcher work failed: {0}", exception);
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static async Task CancelLauncherAtDeadlineAsync(
        Task deadline, CancellationTokenSource cancellation, CancellationToken stop)
    {
        try
        {
            await deadline.WaitAsync(stop).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            // A failed host deadline must still revoke launcher admission.
        }
        cancellation.Cancel();
    }

    private void ReopenLocalStateAfterFailedHandoff()
    {
        CancellationTokenSource previousSession = _startupLoadCancellation;
        previousSession.Cancel();
        _preloadSession.StopAcceptingAndRevoke();
        _startupLoadCancellation = new();
        RetireSession(previousSession);
        OptionalPreloadStatusHost.DataContext = null;
        _reportHistoryPersistence.Reopen();
        _shellPreferencePersistence.Reopen();
    }

    private void ReplayLocalStateAfterFailedHandoff()
    {
        if (DataContext is MainWindowViewModel dirtyViewModel)
        {
            if (_preferenceChangedWhileSealed)
            {
                LaunchCoordinator.Persistence.QueueLocalState(() => _shellPreferencePersistence.Queue(LaunchCoordinator.Persistence.ExportPreferences(dirtyViewModel)));
                _preferenceChangedWhileSealed = false;
            }
            if (_historyChangedWhileSealed)
            {
                LaunchCoordinator.Persistence.QueueLocalState(() => _reportHistoryPersistence.Queue(dirtyViewModel.Reports.ExportReportHistory()));
                _historyChangedWhileSealed = false;
            }
        }
    }
}
