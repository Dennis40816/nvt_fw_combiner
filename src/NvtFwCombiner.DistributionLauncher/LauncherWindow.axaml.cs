using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Diagnostics.CodeAnalysis;
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
    private bool _operationProgressActive;
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
                SetOperationProgress(true);
                var progress = new Progress<ManagedFirstInstallationProgress>(PresentOperationProgressSafely);
                ManagedFirstInstallationResult result = await _setup!
                    .InstallAndLaunchAsync(installation, _lifetime.Token, progress);
                if (result.Outcome == ManagedFirstInstallationOutcome.Completed &&
                    result.LaunchFailure is null)
                {
                    _complete((int)DistributionLauncherExitCode.LaunchInstalled);
                    return;
                }
                PresentInstallationResult(result);
                return;
            }
            if (_recoveryPlan is { } recovery)
            {
                SetBusy(true, "Applying the approved recovery action…");
                SetOperationProgress(true);
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
        finally
        {
            SetOperationProgress(false);
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

    private void SetOperationProgress(bool isRunning)
    {
        _operationProgressActive = isRunning;
        OperationProgressPanel.IsVisible = isRunning;
        OperationProgressText.IsVisible = isRunning;
        OperationProgressBar.IsVisible = isRunning;
        OperationProgressBar.IsIndeterminate = isRunning;
        OperationProgressBar.Value = 0;
        OperationProgressText.Text = isRunning
            ? _recovery is null
                ? "Preparing installation…"
                : "Applying recovery…"
            : string.Empty;
    }

    private void PresentOperationProgress(ManagedFirstInstallationProgress progress)
    {
        if (!_operationProgressActive)
        {
            return;
        }
        OperationProgressPanel.IsVisible = true;
        OperationProgressText.IsVisible = true;
        OperationProgressBar.IsVisible = true;
        OperationProgressText.Text = Describe(progress);
        OperationProgressBar.IsIndeterminate = progress.Percent is null;
        OperationProgressBar.Value = progress.Percent ?? 0;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Queued progress presentation is non-authoritative and cannot change installation.")]
    private void PresentOperationProgressSafely(ManagedFirstInstallationProgress progress)
    {
        try
        {
            PresentOperationProgress(progress);
        }
        catch (Exception)
        {
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        SetOperationProgress(false);
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

    internal static string Describe(ManagedFirstInstallationProgress progress)
    {
        string stage = progress.Stage switch
        {
            ManagedFirstInstallationProgressStage.RevalidatingSource => "Checking verified source",
            ManagedFirstInstallationProgressStage.ReadingPackage => "Downloading update package",
            ManagedFirstInstallationProgressStage.VerifyingPackage => "Verifying package",
            ManagedFirstInstallationProgressStage.InstallingPackage => "Installing files",
            ManagedFirstInstallationProgressStage.VerifyingInstallation => "Verifying installation",
            ManagedFirstInstallationProgressStage.FinalizingInstallation => "Finalizing installation",
            ManagedFirstInstallationProgressStage.StartingApplication => "Starting NVT FW Combiner",
            _ => throw new InvalidOperationException("Setup returned an undefined progress stage."),
        };
        return progress.Percent is { } percent
            ? $"{stage} — {percent}%"
            : stage + "…";
    }

    internal static string Describe(ManagedFirstInstallationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return HasValidResultShape(result)
            ? result.LaunchFailure is { } failure
                ? Describe(failure)
                : Describe(result.Outcome)
            : "Setup returned an invalid post-install result. " +
                "Close Setup, then reopen the Launcher to run recovery.";
    }

    internal void PresentInstallationResult(ManagedFirstInstallationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        OutcomeText.Text = Describe(result);
        if (!HasValidResultShape(result) || result.IsRecoveryOwned)
        {
            _installationPlan = null;
            SourceStatusText.Text = "●  Recovery required";
            PrimaryButton.Content = "Recovery required";
            PrimaryButton.IsEnabled = false;
            EditLocationButton.IsEnabled = false;
            return;
        }
        PrimaryButton.IsEnabled = true;
        EditLocationButton.IsEnabled = true;
    }

    private static bool HasValidResultShape(ManagedFirstInstallationResult result)
    {
        return result.HasValidShape;
    }

    private static string Describe(ManagedFirstInstallationLaunchFailure failure)
    {
        string stage = failure.Stage switch
        {
            ManagedFirstInstallationLaunchStage.BootstrapStart => "Bootstrap start failed",
            ManagedFirstInstallationLaunchStage.LauncherAdmission => "Launcher admission failed",
            ManagedFirstInstallationLaunchStage.ApplicationReady => "Application READY failed",
            ManagedFirstInstallationLaunchStage.PostPromotion => "Post-install launch stopped",
            ManagedFirstInstallationLaunchStage.None => throw new InvalidOperationException(
                "Setup returned a launch failure without a stage."),
            _ => throw new InvalidOperationException("Setup returned an undefined launch-failure stage."),
        };
        string exit = failure.ExitCode is { } exitCode
            ? $" Bootstrap exit code: {exitCode}."
            : string.Empty;
        return $"{stage}: {DescribeReason(failure.Issue)}{exit} {DescribeRecoveryAction(failure.ExitCode)}";
    }

    private static string DescribeReason(ManagedFirstInstallationLaunchIssue issue)
    {
        return issue switch
        {
            ManagedFirstInstallationLaunchIssue.TimedOut => "the bounded operation timed out.",
            ManagedFirstInstallationLaunchIssue.InvalidReceipt =>
                "the Bootstrap process returned an invalid result receipt.",
            ManagedFirstInstallationLaunchIssue.Busy =>
                "another NVT FW Combiner process owns the launch transaction.",
            ManagedFirstInstallationLaunchIssue.Damaged =>
                "the immutable Bootstrap failed verification.",
            ManagedFirstInstallationLaunchIssue.StartFailed =>
                "the required process could not be started.",
            ManagedFirstInstallationLaunchIssue.Unavailable =>
                "the process result could not be observed safely.",
            ManagedFirstInstallationLaunchIssue.RecoveryRequired =>
                "the installed state requires recovery.",
            ManagedFirstInstallationLaunchIssue.LaunchFailed =>
                "Bootstrap could not start the exact version Launcher.",
            ManagedFirstInstallationLaunchIssue.HealthUnavailable =>
                "pre-launch health could not be observed safely.",
            ManagedFirstInstallationLaunchIssue.InvalidState =>
                "the managed version state is invalid.",
            ManagedFirstInstallationLaunchIssue.ManagedRootMismatch =>
                "the version state belongs to a different managed root.",
            ManagedFirstInstallationLaunchIssue.MutationPending =>
                "an application mutation transaction is still pending.",
            ManagedFirstInstallationLaunchIssue.DamagedLauncher =>
                "the installed version Launcher failed verification.",
            ManagedFirstInstallationLaunchIssue.ProtocolMismatch =>
                "the installed Launcher protocol is incompatible.",
            ManagedFirstInstallationLaunchIssue.RollbackUnavailable =>
                "no verified last-known-good rollback is available.",
            ManagedFirstInstallationLaunchIssue.StateChanged =>
                "the version state changed during launch.",
            ManagedFirstInstallationLaunchIssue.StateUnavailable =>
                "the version state could not be read or persisted.",
            ManagedFirstInstallationLaunchIssue.InvalidArguments =>
                "Bootstrap received invalid launch arguments.",
            ManagedFirstInstallationLaunchIssue.InvariantViolation =>
                "Bootstrap rejected an internal launch invariant.",
            ManagedFirstInstallationLaunchIssue.InvalidInheritedContext =>
                "Bootstrap inherited an incomplete process context.",
            ManagedFirstInstallationLaunchIssue.StartNotAuthorized =>
                "the inherited start gate did not authorize Bootstrap.",
            ManagedFirstInstallationLaunchIssue.UndefinedFailure =>
                "Bootstrap returned its reserved undefined-failure result.",
            ManagedFirstInstallationLaunchIssue.UnknownExit =>
                "Bootstrap returned an unrecognized failure result.",
            ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed =>
                "the launched process tree could not be proven terminated.",
            ManagedFirstInstallationLaunchIssue.RolledBack =>
                "only the previous last-known-good version reported READY.",
            ManagedFirstInstallationLaunchIssue.Cancelled =>
                "the operation was cancelled after the install root was promoted.",
            ManagedFirstInstallationLaunchIssue.None => throw new InvalidOperationException(
                "Setup returned a launch failure without a reason."),
            _ => throw new InvalidOperationException("Setup returned an undefined launch-failure reason."),
        };
    }

    private static string DescribeRecoveryAction(int? exitCode)
    {
        return exitCode is null
            ? "Close Setup, then reopen the Launcher to run recovery; " +
                "if recovery fails, report this stage."
            : "Close Setup, then reopen the Launcher to run recovery; " +
                "if recovery fails, report this stage and exit code.";
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
