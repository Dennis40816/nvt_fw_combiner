using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.DistributionLauncher;

internal sealed partial class LauncherWindow : Window, IDisposable
{
    private readonly Action<int> _complete;
    private ManagedDistributionLauncherRecoverySession? _recovery;
    private ManagedFirstInstallationExperience? _setup;
    private readonly CancellationTokenSource _lifetime = new();
    private string _candidateRoot;
    private bool _disposed;
    private ManagedFirstInstallationPlan? _installationPlan;
    private ManagedSetupRecoveryPlan? _recoveryPlan;

    internal LauncherWindow()
        : this(startup: null, Path.Combine(Path.GetTempPath(), "NvtFwCombiner"), _ => { }, testOnly: true)
    {
    }

    internal LauncherWindow(
        ManagedDistributionLauncherHostResult startup,
        string initialRoot,
        Action<int> complete)
        : this((ManagedDistributionLauncherHostResult?)startup, initialRoot, complete, testOnly: false)
    {
    }

    private LauncherWindow(
        ManagedDistributionLauncherHostResult? startup,
        string initialRoot,
        Action<int> complete,
        bool testOnly)
    {
        InitializeComponent();
        _complete = complete ?? throw new ArgumentNullException(nameof(complete));
        _candidateRoot = Path.GetFullPath(initialRoot);
        _setup = startup?.Setup;
        _recovery = startup?.Recovery;
        _ = testOnly;
        InstallLocationText.Text = _candidateRoot;
        RecoveryRootText.Text = _recovery?.ManagedRoot ?? _candidateRoot;
        SetupPanel.IsVisible = _recovery is null;
        RecoveryPanel.IsVisible = _recovery is not null;
        PrimaryButton.Content = _recovery is null ? "Install" : "Check recovery";
        Opened += Window_Opened;
        Closed += Window_Closed;
    }

    private async void Window_Opened(object? sender, EventArgs e)
    {
        try
        {
            if (_setup is not null)
            {
                await PrepareSetupAsync();
            }
            else if (_recovery is not null)
            {
                await DiagnoseRecoveryAsync();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Dispose();
    }

    private async Task PrepareSetupAsync()
    {
        SetBusy(true, "Checking verified update source…");
        ManagedFirstInstallationPlanResult prepared = await _setup!
            .PrepareAsync(_candidateRoot, _lifetime.Token);
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }
        _installationPlan = prepared.Plan;
        PrimaryButton.IsEnabled = prepared.IsReady;
        EditLocationButton.IsEnabled = true;
        if (prepared.IsReady)
        {
            VersionText.Text = prepared.Plan!.Candidate.Package.Version.ToString();
            SourceStatusText.Text = "●  Update source verified";
            OutcomeText.Text = string.Empty;
        }
        else
        {
            VersionText.Text = "Unavailable";
            SourceStatusText.Text = "●  Update source not ready";
            OutcomeText.Text = Describe(prepared.Outcome);
        }
    }

    private async Task DiagnoseRecoveryAsync()
    {
        SetBusy(true, "Checking installation health…");
        ManagedSetupRecoveryDiagnosis diagnosis = await _recovery!
            .DiagnoseAsync(_lifetime.Token);
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }
        _recoveryPlan = diagnosis.Plan;
        RecoveryStatusText.Text = Describe(diagnosis.Outcome);
        PrimaryButton.Content = diagnosis.Plan?.Action switch
        {
            ManagedSetupRecoveryAction.RemoveIncompleteInstallation => "Remove incomplete setup",
            ManagedSetupRecoveryAction.ConvergeReady => "Finish recovery",
            _ => "Check recovery",
        };
        PrimaryButton.IsEnabled = diagnosis.Plan is not null;
        OutcomeText.Text = diagnosis.Outcome == ManagedSetupRecoveryOutcome.ManualInterventionRequired
            ? "No automatic action is safe. Contact support with the diagnostic report."
            : string.Empty;
    }

    private async void EditLocation_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Choose install location",
                    AllowMultiple = false,
                });
            if (folders.Count != 1 || folders[0].TryGetLocalPath() is not { } selected)
            {
                return;
            }
            _candidateRoot = Path.GetFullPath(selected);
            InstallLocationText.Text = _candidateRoot;
            _installationPlan = null;
            await PrepareSetupAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private async void Primary_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_installationPlan is { } installation)
            {
                SetBusy(true, "Installing verified version…");
                ManagedFirstInstallationResult result = await _setup!
                    .InstallAndLaunchAsync(installation, _lifetime.Token);
                if (result.Outcome == ManagedFirstInstallationOutcome.Completed)
                {
                    _complete((int)DistributionLauncherExitCode.LaunchInstalled);
                    return;
                }
                OutcomeText.Text = Describe(result.Outcome);
                PrimaryButton.IsEnabled = true;
                EditLocationButton.IsEnabled = true;
                return;
            }
            if (_recoveryPlan is { } recovery)
            {
                SetBusy(true, "Applying the approved recovery action…");
                ManagedDistributionLauncherRecoveryExecutionResult result = await _recovery!
                    .ExecuteAsync(
                        recovery,
                        recovery.Action,
                        ManagedFirstInstallationExperience.WriterLeaseTimeout,
                        _lifetime.Token);
                if (result.Execution.Outcome == ManagedSetupRecoveryExecutionOutcome.Completed)
                {
                    if (result.RefreshedHost?.Setup is { } refreshedSetup)
                    {
                        _setup = refreshedSetup;
                        _recovery = null;
                        _candidateRoot = result.RefreshedHost.Entry?.ManagedRoot ?? _candidateRoot;
                        InstallLocationText.Text = _candidateRoot;
                        _recoveryPlan = null;
                        SetupPanel.IsVisible = true;
                        RecoveryPanel.IsVisible = false;
                        PrimaryButton.Content = "Install";
                        await PrepareSetupAsync();
                        return;
                    }
                    int exitCode = (int)Program.MapExitCode(
                        result.RefreshedHost?.PayloadIssue ?? ManagedDistributionPayloadIssue.None,
                        result.RefreshedHost?.Entry?.Outcome,
                        result.RefreshedHost?.Setup is not null);
                    _complete(exitCode);
                    return;
                }
                OutcomeText.Text = result.Execution.Outcome.ToString();
                PrimaryButton.IsEnabled = true;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private void SetBusy(bool busy, string status)
    {
        PrimaryButton.IsEnabled = !busy;
        EditLocationButton.IsEnabled = !busy && _recovery is null;
        if (_recovery is null)
        {
            SourceStatusText.Text = "●  " + status;
        }
        else
        {
            RecoveryStatusText.Text = status;
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        _complete((int)(_recovery is null
            ? DistributionLauncherExitCode.SetupRequired
            : DistributionLauncherExitCode.RecoveryRequired));
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private static string Describe(ManagedFirstInstallationOutcome outcome)
    {
        return outcome switch
        {
            ManagedFirstInstallationOutcome.ReadyToInstall => "Ready to install.",
            ManagedFirstInstallationOutcome.Installing => "Installing verified version…",
            ManagedFirstInstallationOutcome.Completed => "Installation completed.",
            ManagedFirstInstallationOutcome.PayloadUnavailable => "The setup payload is unavailable.",
            ManagedFirstInstallationOutcome.PayloadInvalid => "The setup payload did not pass verification.",
            ManagedFirstInstallationOutcome.SourceUnavailable => "The update source is unavailable. Check the network and try again.",
            ManagedFirstInstallationOutcome.SourceRejected => "The update source did not pass verification.",
            ManagedFirstInstallationOutcome.CandidateUnavailable => "No compatible verified version is available.",
            ManagedFirstInstallationOutcome.SourceChanged => "The verified source changed. Check again before installing.",
            ManagedFirstInstallationOutcome.InvalidDestination => "Choose an empty local install folder.",
            ManagedFirstInstallationOutcome.PermissionDenied => "The selected folder cannot be written by this user.",
            ManagedFirstInstallationOutcome.Busy => "Another NVT FW Combiner process is active.",
            ManagedFirstInstallationOutcome.RecoveryRequired => "The installation needs recovery before continuing.",
            ManagedFirstInstallationOutcome.InstalledButLaunchFailed => "Installation completed, but the application did not report ready.",
            ManagedFirstInstallationOutcome.StateUnavailable => "Installation state is temporarily unavailable.",
            ManagedFirstInstallationOutcome.Cancelled => "The operation was cancelled.",
            _ => throw new InvalidOperationException("Setup returned an undefined outcome."),
        };
    }

    private static string Describe(ManagedSetupRecoveryOutcome outcome)
    {
        return outcome switch
        {
            ManagedSetupRecoveryOutcome.ActionAvailable => "A verified recovery action is available.",
            ManagedSetupRecoveryOutcome.Busy => "Another NVT FW Combiner process is active.",
            ManagedSetupRecoveryOutcome.HealthUnavailable => "Installation health could not be checked completely.",
            ManagedSetupRecoveryOutcome.ManualInterventionRequired => "Automatic recovery is not safe for this installation.",
            ManagedSetupRecoveryOutcome.NoRecoveryNeeded => "The installation is healthy.",
            _ => outcome.ToString(),
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
