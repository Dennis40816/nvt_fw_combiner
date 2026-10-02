using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class ManagedLauncherEntryCoordinatorTests
{
    /// <summary>Every terminal return path assigns a reason and stage through real coordinator decisions.</summary>
    [Theory]
    [InlineData("payload-unavailable", ManagedLauncherEntryOutcome.PayloadUnavailable,
        ManagedLauncherEntryReason.Unavailable, ManagedLauncherEntryStage.PayloadAdmission)]
    [InlineData("payload-invalid", ManagedLauncherEntryOutcome.PayloadInvalid,
        ManagedLauncherEntryReason.PayloadInvalid, ManagedLauncherEntryStage.PayloadAdmission)]
    [InlineData("payload-changed", ManagedLauncherEntryOutcome.PayloadInvalid,
        ManagedLauncherEntryReason.PayloadInvalid, ManagedLauncherEntryStage.PayloadAdmission)]
    [InlineData("payload-version-mismatch", ManagedLauncherEntryOutcome.PayloadInvalid,
        ManagedLauncherEntryReason.PayloadInvalid, ManagedLauncherEntryStage.PayloadAdmission)]
    [InlineData("health-deadline", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.HealthDeadlineExceeded, ManagedLauncherEntryStage.PayloadAdmission)]
    [InlineData("state-invalid", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.StateLoad)]
    [InlineData("state-root-mismatch", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.StateLoad)]
    [InlineData("state-unbound", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.StateLoad)]
    [InlineData("state-unavailable", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.Unavailable, ManagedLauncherEntryStage.StateLoad)]
    [InlineData("missing-state-absent-root", ManagedLauncherEntryOutcome.SetupRequired,
        ManagedLauncherEntryReason.SetupRequired, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("missing-state-present-root", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("missing-state-residue", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("missing-state-invalid-root", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("missing-state-permission-denied", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.PermissionDenied, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("missing-state-unavailable-root", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.Unavailable, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("bound-absent-root", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("bound-residue", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("bound-invalid-root", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("bound-permission-denied", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.PermissionDenied, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("bound-unavailable-root", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.Unavailable, ManagedLauncherEntryStage.RootObservation)]
    [InlineData("start-invalid-receipt", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.InvalidReceipt, ManagedLauncherEntryStage.BootstrapStart)]
    [InlineData("start-busy", ManagedLauncherEntryOutcome.Busy,
        ManagedLauncherEntryReason.Busy, ManagedLauncherEntryStage.BootstrapStart)]
    [InlineData("start-damaged", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.BootstrapStart)]
    [InlineData("start-failed", ManagedLauncherEntryOutcome.LaunchFailed,
        ManagedLauncherEntryReason.StartFailed, ManagedLauncherEntryStage.BootstrapStart)]
    [InlineData("start-unavailable", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.Unavailable, ManagedLauncherEntryStage.BootstrapStart)]
    [InlineData("admission-invalid-receipt", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.InvalidReceipt, ManagedLauncherEntryStage.BootstrapAdmission)]
    [InlineData("admission-unconfirmed", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.TerminationUnconfirmed, ManagedLauncherEntryStage.BootstrapAdmission)]
    [InlineData("admission-busy", ManagedLauncherEntryOutcome.Busy,
        ManagedLauncherEntryReason.Busy, ManagedLauncherEntryStage.BootstrapAdmission)]
    [InlineData("admission-recovery", ManagedLauncherEntryOutcome.RecoveryRequired,
        ManagedLauncherEntryReason.RecoveryRequired, ManagedLauncherEntryStage.BootstrapAdmission)]
    [InlineData("admission-failed", ManagedLauncherEntryOutcome.LaunchFailed,
        ManagedLauncherEntryReason.LaunchFailed, ManagedLauncherEntryStage.BootstrapAdmission)]
    [InlineData("admission-unavailable", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.Unavailable, ManagedLauncherEntryStage.BootstrapAdmission)]
    [InlineData("cleanup-exhausted", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.AdmissionTimeout, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("cleanup-timeout", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.AdmissionTimeout, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("cleanup-invalid-receipt", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.InvalidReceipt, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("cleanup-unconfirmed", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.TerminationUnconfirmed, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("cleanup-confirmed", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.AdmissionTimeout, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("caller-cancelled-cleanup-exhausted", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.CallerCancelled, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("caller-cancelled-cleanup-timeout", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.CallerCancelled, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("caller-cancelled-cleanup-unconfirmed", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.CallerCancelled, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("caller-cancelled-invalid-cleanup", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.InvalidReceipt, ManagedLauncherEntryStage.AdmissionCleanup)]
    [InlineData("completion-timeout", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.CompletionTimeout, ManagedLauncherEntryStage.BootstrapCompletion)]
    [InlineData("completion-invalid-receipt", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.InvalidReceipt, ManagedLauncherEntryStage.BootstrapCompletion)]
    [InlineData("completion-unconfirmed", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.TerminationUnconfirmed, ManagedLauncherEntryStage.BootstrapCompletion)]
    [InlineData("completion-late", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.CompletionTimeout, ManagedLauncherEntryStage.BootstrapCompletion)]
    [InlineData("completion-ready", ManagedLauncherEntryOutcome.LaunchInstalled,
        ManagedLauncherEntryReason.Success, ManagedLauncherEntryStage.BootstrapCompletion)]
    [InlineData("completion-rollback", ManagedLauncherEntryOutcome.LaunchInstalled,
        ManagedLauncherEntryReason.Success, ManagedLauncherEntryStage.BootstrapCompletion)]
    [InlineData("completion-failed", ManagedLauncherEntryOutcome.LaunchFailed,
        ManagedLauncherEntryReason.LaunchFailed, ManagedLauncherEntryStage.BootstrapCompletion)]
    [InlineData("completion-unavailable", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.Unavailable, ManagedLauncherEntryStage.BootstrapCompletion)]
    [InlineData("admission-timeout-health", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.AdmissionTimeout, ManagedLauncherEntryStage.PayloadAdmission)]
    [InlineData("admission-timeout-start", ManagedLauncherEntryOutcome.HealthUnavailable,
        ManagedLauncherEntryReason.AdmissionTimeout, ManagedLauncherEntryStage.BootstrapStart)]
    [InlineData("admission-timeout-wait", ManagedLauncherEntryOutcome.TerminationUnconfirmed,
        ManagedLauncherEntryReason.AdmissionTimeout, ManagedLauncherEntryStage.BootstrapAdmission)]
    public async Task EveryTerminalPathSpecifiesReasonAndStage(
        string scenario,
        ManagedLauncherEntryOutcome expectedOutcome,
        ManagedLauncherEntryReason expectedReason,
        ManagedLauncherEntryStage expectedStage)
    {
        string root = Root($"typed-{scenario}");
        var time = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var payload = new EntryPayloadSource();
        var state = new EntryStateStore(BoundState(root));
        ManagedInstallationRootStatus rootStatus = ManagedInstallationRootStatus.Present;
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready);
        switch (scenario)
        {
            case "payload-unavailable":
                payload.AdmissionIssue = ManagedDistributionPayloadIssue.Unavailable;
                break;
            case "payload-invalid":
                payload.AdmissionIssue = ManagedDistributionPayloadIssue.Invalid;
                break;
            case "payload-changed":
                payload.AdmissionIssue = ManagedDistributionPayloadIssue.Changed;
                break;
            case "payload-version-mismatch":
                payload.LauncherVersion = ManagedAppVersion.Parse("1.0.5");
                break;
            case "health-deadline":
                payload.AdmissionAction = () => time.Advance(TimeSpan.FromMilliseconds(250));
                break;
            case "state-invalid":
                state = new(VersionManagerStateLoadIssue.Invalid);
                break;
            case "state-root-mismatch":
                state = new(VersionManagerStateLoadIssue.ManagedRootMismatch);
                break;
            case "state-unbound":
                state = new(VersionManagerState.Create(
                    updateSource: null,
                    activeVersion: null,
                    lastKnownGoodVersion: null,
                    admissions: [],
                    pendingActivation: null,
                    failedActivationVersion: null,
                    retentionReviewDue: false));
                break;
            case "state-unavailable":
                state = new(VersionManagerStateLoadIssue.Unavailable);
                break;
            case "missing-state-absent-root":
                state = new(state: null);
                rootStatus = ManagedInstallationRootStatus.Absent;
                break;
            case "missing-state-present-root":
                state = new(state: null);
                break;
            case "missing-state-residue":
            case "bound-residue":
                rootStatus = ManagedInstallationRootStatus.Residue;
                break;
            case "missing-state-invalid-root":
            case "bound-invalid-root":
                rootStatus = ManagedInstallationRootStatus.InvalidDestination;
                break;
            case "missing-state-permission-denied":
            case "bound-permission-denied":
                rootStatus = ManagedInstallationRootStatus.PermissionDenied;
                break;
            case "missing-state-unavailable-root":
            case "bound-unavailable-root":
                rootStatus = ManagedInstallationRootStatus.Unavailable;
                break;
            case "bound-absent-root":
                rootStatus = ManagedInstallationRootStatus.Absent;
                break;
            case "start-invalid-receipt":
                handoff = new((ImmutableBootstrapStartIssue)999);
                break;
            case "start-busy":
                handoff = new(ImmutableBootstrapStartIssue.Busy);
                break;
            case "start-damaged":
                handoff = new(ImmutableBootstrapStartIssue.Damaged);
                break;
            case "start-failed":
                handoff = new(ImmutableBootstrapStartIssue.StartFailed);
                break;
            case "start-unavailable":
                handoff = new(ImmutableBootstrapStartIssue.Unavailable);
                break;
            case "admission-invalid-receipt":
                handoff.AdmissionResultFactory = _ => new(
                    ImmutableBootstrapAdmissionOutcome.Admitted,
                    ImmutableBootstrapExitCodeCodec.Ready);
                break;
            case "admission-unconfirmed":
                handoff = new(ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed);
                break;
            case "admission-busy":
                handoff = new(ImmutableBootstrapAdmissionOutcome.Busy);
                break;
            case "admission-recovery":
                handoff = new(ImmutableBootstrapAdmissionOutcome.RecoveryRequired);
                break;
            case "admission-failed":
                handoff = new(ImmutableBootstrapAdmissionOutcome.LaunchFailed);
                break;
            case "admission-unavailable":
                handoff = new(ImmutableBootstrapAdmissionOutcome.HealthUnavailable);
                break;
            case "cleanup-exhausted":
                handoff.AdmissionAction = () => time.Advance(TimeSpan.FromMilliseconds(1500));
                break;
            case "cleanup-timeout":
                handoff.AdmissionAction = () => time.Advance(TimeSpan.FromSeconds(1));
                handoff.CompletionResultFactory = token =>
                {
                    time.Advance(TimeSpan.FromMilliseconds(500));
                    token.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Cleanup deadline must cancel its token.");
                };
                break;
            case "cleanup-invalid-receipt":
                handoff.AdmissionAction = () => time.Advance(TimeSpan.FromSeconds(1));
                handoff.CompletionResultFactory = _ => new(ImmutableBootstrapCompletionOutcome.Ready);
                break;
            case "cleanup-unconfirmed":
                handoff = new(ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed)
                {
                    AdmissionAction = () => time.Advance(TimeSpan.FromSeconds(1)),
                };
                break;
            case "cleanup-confirmed":
                handoff = new(ImmutableBootstrapCompletionOutcome.Unavailable)
                {
                    AdmissionAction = () => time.Advance(TimeSpan.FromSeconds(1)),
                };
                break;
            case "caller-cancelled-cleanup-exhausted":
                handoff.AdmissionAction = () =>
                {
                    cancellation.Cancel();
                    time.Advance(TimeSpan.FromMilliseconds(1500));
                };
                break;
            case "caller-cancelled-cleanup-timeout":
                handoff.AdmissionAction = cancellation.Cancel;
                handoff.CompletionResultFactory = token =>
                {
                    time.Advance(TimeSpan.FromMilliseconds(1500));
                    token.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Cleanup deadline must cancel its token.");
                };
                break;
            case "caller-cancelled-cleanup-unconfirmed":
                handoff = new(ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed)
                {
                    AdmissionAction = cancellation.Cancel,
                };
                break;
            case "caller-cancelled-invalid-cleanup":
                handoff.AdmissionAction = cancellation.Cancel;
                handoff.CompletionResultFactory = _ => new(ImmutableBootstrapCompletionOutcome.Ready);
                break;
            case "completion-timeout":
                handoff.CompletionResultFactory = token =>
                {
                    time.Advance(TimeSpan.FromSeconds(1));
                    token.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Completion deadline must cancel its token.");
                };
                break;
            case "completion-invalid-receipt":
                handoff.CompletionResultFactory = _ => new(ImmutableBootstrapCompletionOutcome.Ready);
                break;
            case "completion-unconfirmed":
                handoff = new(ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed);
                break;
            case "completion-late":
                handoff.CompletionAction = () => time.Advance(TimeSpan.FromSeconds(1));
                break;
            case "completion-ready":
                break;
            case "completion-rollback":
                handoff = new(ImmutableBootstrapCompletionOutcome.RolledBack);
                break;
            case "completion-failed":
                handoff = new(ImmutableBootstrapCompletionOutcome.Failed);
                break;
            case "completion-unavailable":
                handoff = new(ImmutableBootstrapCompletionOutcome.Unavailable);
                break;
            case "admission-timeout-health":
                payload.AdmissionAction = () => time.Advance(TimeSpan.FromSeconds(1));
                break;
            case "admission-timeout-start":
                handoff = new(ImmutableBootstrapStartIssue.Unavailable)
                {
                    StartAction = () => time.Advance(TimeSpan.FromSeconds(1)),
                };
                break;
            case "admission-timeout-wait":
                handoff.AdmissionResultFactory = token =>
                {
                    time.Advance(TimeSpan.FromSeconds(1));
                    token.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Admission deadline must cancel its token.");
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown terminal scenario.");
        }
        if (scenario.StartsWith("missing-state-", StringComparison.Ordinal))
        {
            state = new(state: null);
        }
        var roots = new RecordingRootProbe(rootStatus);
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            state,
            roots,
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            completionDeadline: TimeSpan.FromSeconds(1),
            timeProvider: time,
            payloadSource: payload);

        ManagedLauncherEntryResult result = await coordinator.RunAsync(cancellation.Token);

        Assert.NotEqual(ManagedLauncherEntryReason.NotSpecified, result.Reason);
        Assert.NotEqual(ManagedLauncherEntryStage.NotSpecified, result.Stage);
        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Equal(expectedStage, result.Stage);
        Assert.Equal(0, state.LeaseCount);
    }

    /// <summary>The existing four-argument constructor and deconstruction remain source compatible.</summary>
    [Fact]
    public void LegacyExternalEntryResultMayLeaveDiagnosticsUnspecified()
    {
        var result = new ManagedLauncherEntryResult(
            ManagedLauncherEntryOutcome.HealthUnavailable,
            ManagedRoot: null,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20));

        (ManagedLauncherEntryOutcome outcome, string? root, TimeSpan admission, TimeSpan total) = result;

        Assert.Equal(ManagedLauncherEntryOutcome.HealthUnavailable, outcome);
        Assert.Null(root);
        Assert.Equal(TimeSpan.FromMilliseconds(10), admission);
        Assert.Equal(TimeSpan.FromMilliseconds(20), total);
        Assert.Equal(ManagedLauncherEntryReason.NotSpecified, result.Reason);
        Assert.Equal(ManagedLauncherEntryStage.NotSpecified, result.Stage);
    }
}
