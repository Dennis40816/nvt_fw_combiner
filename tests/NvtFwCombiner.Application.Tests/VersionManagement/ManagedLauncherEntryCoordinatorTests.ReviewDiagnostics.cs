using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class ManagedLauncherEntryCoordinatorTests
{
    /// <summary>A returned admission receipt retains its outcome while the reached cutoff supplies its reason.</summary>
    [Theory]
    [InlineData(999, false, ManagedLauncherEntryReason.Unavailable)]
    [InlineData(1000, false, ManagedLauncherEntryReason.AdmissionTimeout)]
    [InlineData(999, true, ManagedLauncherEntryReason.LaunchFailed)]
    [InlineData(1000, true, ManagedLauncherEntryReason.AdmissionTimeout)]
    public async Task AdmissionReceiptWithoutExitAtCutoffReportsTimeout(
        int elapsedMilliseconds,
        bool startFailed,
        ManagedLauncherEntryReason expectedReason)
    {
        string root = Root($"returned-admission-{elapsedMilliseconds}-{startFailed}");
        var time = new ManualTimeProvider();
        var handoff = new RecordingBootstrapHandoff(startFailed
            ? ImmutableBootstrapAdmissionOutcome.LaunchFailed
            : ImmutableBootstrapAdmissionOutcome.HealthUnavailable)
        {
            AdmissionAction = () => time.Advance(TimeSpan.FromMilliseconds(elapsedMilliseconds)),
        };
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            new EntryStateStore(BoundState(root)),
            new RecordingRootProbe(ManagedInstallationRootStatus.Present),
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            timeProvider: time);

        ManagedLauncherEntryResult result = await coordinator.RunAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(startFailed
            ? ManagedLauncherEntryOutcome.LaunchFailed
            : ManagedLauncherEntryOutcome.HealthUnavailable, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Equal(ManagedLauncherEntryStage.LauncherAdmission, result.Stage);
        Assert.Null(result.UpstreamExitCode);
        Assert.Equal(startFailed
            ? ImmutableBootstrapExitIssue.StartFailed
            : ImmutableBootstrapExitIssue.None, result.BootstrapExitIssue);
        Assert.Equal(TimeSpan.FromMilliseconds(elapsedMilliseconds), result.AdmissionElapsed);
        Assert.Equal(result.AdmissionElapsed, result.TotalElapsed);
        Assert.Equal(0, handoff.CompletionWaitCount);
    }

    /// <summary>Distinct observed exit facts survive their shared entry reason even at the cutoff.</summary>
    [Theory]
    [InlineData(10, ImmutableBootstrapExitIssue.InvalidState, ManagedLauncherEntryReason.RecoveryRequired)]
    [InlineData(13, ImmutableBootstrapExitIssue.DamagedLauncher, ManagedLauncherEntryReason.RecoveryRequired)]
    [InlineData(14, ImmutableBootstrapExitIssue.ProtocolMismatch, ManagedLauncherEntryReason.RecoveryRequired)]
    [InlineData(18, ImmutableBootstrapExitIssue.StateUnavailable, ManagedLauncherEntryReason.Unavailable)]
    [InlineData(23, ImmutableBootstrapExitIssue.StartNotAuthorized, ManagedLauncherEntryReason.Unavailable)]
    [InlineData(777, ImmutableBootstrapExitIssue.Unknown, ManagedLauncherEntryReason.Unavailable)]
    public async Task AdmissionExitDetailsRemainDistinctWithinSharedReason(
        int exitCode,
        ImmutableBootstrapExitIssue issue,
        ManagedLauncherEntryReason expectedReason)
    {
        string root = Root($"upstream-admission-{exitCode}");
        var time = new ManualTimeProvider();
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready)
        {
            AdmissionAction = () => time.Advance(TimeSpan.FromSeconds(1)),
            AdmissionResultFactory = _ => new(
                ImmutableBootstrapExitCodeCodec.ClassifyAdmission(issue), exitCode, issue),
        };
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            new EntryStateStore(BoundState(root)),
            new RecordingRootProbe(ManagedInstallationRootStatus.Present),
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            timeProvider: time);

        ManagedLauncherEntryResult result = await coordinator.RunAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedReason == ManagedLauncherEntryReason.RecoveryRequired
            ? ManagedLauncherEntryOutcome.RecoveryRequired
            : ManagedLauncherEntryOutcome.HealthUnavailable, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Equal(ManagedLauncherEntryStage.LauncherAdmission, result.Stage);
        Assert.Equal(exitCode, result.UpstreamExitCode);
        Assert.Equal(issue, result.BootstrapExitIssue);
        Assert.Equal(0, handoff.CompletionWaitCount);
    }

    /// <summary>Interruption reasons consistently win while original uncertain or malformed facts survive.</summary>
    [Theory]
    [InlineData(ManagedLauncherEntryStage.LauncherAdmission, false, false)]
    [InlineData(ManagedLauncherEntryStage.LauncherAdmission, false, true)]
    [InlineData(ManagedLauncherEntryStage.LauncherAdmission, true, false)]
    [InlineData(ManagedLauncherEntryStage.LauncherAdmission, true, true)]
    [InlineData(ManagedLauncherEntryStage.AdmissionCleanup, false, false)]
    [InlineData(ManagedLauncherEntryStage.AdmissionCleanup, false, true)]
    [InlineData(ManagedLauncherEntryStage.AdmissionCleanup, true, false)]
    [InlineData(ManagedLauncherEntryStage.AdmissionCleanup, true, true)]
    [InlineData(ManagedLauncherEntryStage.ApplicationReady, false, false)]
    [InlineData(ManagedLauncherEntryStage.ApplicationReady, false, true)]
    [InlineData(ManagedLauncherEntryStage.ApplicationReady, true, false)]
    [InlineData(ManagedLauncherEntryStage.ApplicationReady, true, true)]
    public async Task InterruptedReceiptsPreserveWaitCauseAndUpstreamFacts(
        ManagedLauncherEntryStage stage,
        bool callerCancelled,
        bool invalidReceipt)
    {
        string root = Root($"interrupted-receipt-{stage}-{callerCancelled}-{invalidReceipt}");
        var time = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        void interrupt()
        {
            time.Advance(TimeSpan.FromSeconds(1));
            if (callerCancelled)
            {
                cancellation.Cancel();
            }
        }
        int? exitCode = invalidReceipt ? ImmutableBootstrapExitCodeCodec.Ready : null;
        ImmutableBootstrapExitIssue issue = invalidReceipt
            ? ImmutableBootstrapExitIssue.None
            : ImmutableBootstrapExitIssue.TerminationUnconfirmed;
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready);
        if (stage == ManagedLauncherEntryStage.LauncherAdmission)
        {
            handoff.AdmissionAction = interrupt;
            handoff.AdmissionResultFactory = _ => new(
                invalidReceipt
                    ? ImmutableBootstrapAdmissionOutcome.Admitted
                    : ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed,
                exitCode,
                issue);
        }
        else
        {
            if (stage == ManagedLauncherEntryStage.AdmissionCleanup)
            {
                handoff.AdmissionAction = () => time.Advance(TimeSpan.FromSeconds(1));
                if (callerCancelled)
                {
                    handoff.CompletionAction = cancellation.Cancel;
                }
            }
            else
            {
                handoff.CompletionAction = interrupt;
            }
            handoff.CompletionResultFactory = _ => new(
                invalidReceipt
                    ? ImmutableBootstrapCompletionOutcome.Unavailable
                    : ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed,
                exitCode,
                issue);
        }
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            new EntryStateStore(BoundState(root)),
            new RecordingRootProbe(ManagedInstallationRootStatus.Present),
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            completionDeadline: TimeSpan.FromSeconds(1),
            timeProvider: time);

        ManagedLauncherEntryResult result = await coordinator.RunAsync(cancellation.Token);

        Assert.Equal(ManagedLauncherEntryOutcome.TerminationUnconfirmed, result.Outcome);
        Assert.Equal(callerCancelled
            ? ManagedLauncherEntryReason.CallerCancelled
            : stage == ManagedLauncherEntryStage.ApplicationReady
                ? ManagedLauncherEntryReason.CompletionTimeout
                : ManagedLauncherEntryReason.AdmissionTimeout, result.Reason);
        Assert.Equal(stage, result.Stage);
        Assert.Equal(exitCode, result.UpstreamExitCode);
        Assert.Equal(issue, result.BootstrapExitIssue);
        Assert.Equal(stage == ManagedLauncherEntryStage.LauncherAdmission ? 0 : 1,
            handoff.CompletionWaitCount);
    }

    /// <summary>The outer admission catch retains the active step without borrowing completion's deadline.</summary>
    [Theory]
    [InlineData(ManagedLauncherEntryStage.StateLoad, ManagedLauncherEntryReason.AdmissionTimeout)]
    [InlineData(ManagedLauncherEntryStage.RootObservation, ManagedLauncherEntryReason.AdmissionTimeout)]
    [InlineData(ManagedLauncherEntryStage.AdmissionCleanup, ManagedLauncherEntryReason.AdmissionTimeout)]
    [InlineData(ManagedLauncherEntryStage.ApplicationReady, ManagedLauncherEntryReason.Unavailable)]
    public async Task OuterCatchReportsStageAndCorrectDeadline(
        ManagedLauncherEntryStage stage,
        ManagedLauncherEntryReason expectedReason)
    {
        string root = Root($"outer-catch-{stage}");
        var time = new ManualTimeProvider();
        var state = new EntryStateStore(BoundState(root));
        var roots = new RecordingRootProbe(ManagedInstallationRootStatus.Present);
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready);
        void expireAdmission()
        {
            time.Advance(TimeSpan.FromSeconds(1));
        }

        if (stage == ManagedLauncherEntryStage.StateLoad)
        {
            state.LoadAction = expireAdmission;
        }
        else if (stage == ManagedLauncherEntryStage.RootObservation)
        {
            roots.ObserveAction = expireAdmission;
        }
        else
        {
            if (stage == ManagedLauncherEntryStage.AdmissionCleanup)
            {
                handoff.AdmissionAction = expireAdmission;
            }
            else
            {
                handoff.CompletionAction = expireAdmission;
            }
            handoff.CompletionResultFactory = token =>
            {
                Assert.False(token.IsCancellationRequested);
                throw new OperationCanceledException(token);
            };
        }
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            state,
            roots,
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            completionDeadline: TimeSpan.FromSeconds(2),
            healthObservationDeadline: TimeSpan.FromSeconds(2),
            timeProvider: time);

        ManagedLauncherEntryResult result = await coordinator.RunAsync(
            TestContext.Current.CancellationToken);

        bool beforeStart = stage is ManagedLauncherEntryStage.StateLoad or
            ManagedLauncherEntryStage.RootObservation;
        Assert.Equal(beforeStart
            ? ManagedLauncherEntryOutcome.HealthUnavailable
            : ManagedLauncherEntryOutcome.TerminationUnconfirmed, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Equal(stage, result.Stage);
        Assert.Equal(TimeSpan.FromSeconds(1), result.TotalElapsed);
        Assert.Equal(result.TotalElapsed, result.AdmissionElapsed);
        Assert.Equal(beforeStart ? 0 : 1, handoff.StartCount);
        Assert.Null(result.UpstreamExitCode);
        Assert.Null(result.BootstrapExitIssue);
    }

    /// <summary>A simultaneous health/admission expiry reports health at every local observation step.</summary>
    [Theory]
    [InlineData(ManagedLauncherEntryStage.PayloadAdmission)]
    [InlineData(ManagedLauncherEntryStage.StateLoad)]
    [InlineData(ManagedLauncherEntryStage.RootObservation)]
    public async Task SimultaneousLocalDeadlinesRetainHealthReason(ManagedLauncherEntryStage stage)
    {
        string root = Root($"simultaneous-deadlines-{stage}");
        var time = new ManualTimeProvider();
        var payload = new EntryPayloadSource();
        var state = new EntryStateStore(BoundState(root));
        var roots = new RecordingRootProbe(ManagedInstallationRootStatus.Present);
        var handoff = new RecordingBootstrapHandoff(ImmutableBootstrapCompletionOutcome.Ready);
        void expireBoth()
        {
            time.Advance(TimeSpan.FromSeconds(1));
        }

        if (stage == ManagedLauncherEntryStage.PayloadAdmission)
        {
            payload.AdmissionAction = expireBoth;
        }
        else if (stage == ManagedLauncherEntryStage.StateLoad)
        {
            state.LoadAction = expireBoth;
        }
        else
        {
            roots.ObserveAction = expireBoth;
        }
        ManagedLauncherEntryCoordinator coordinator = Create(
            root,
            state,
            roots,
            handoff,
            admissionDeadline: TimeSpan.FromSeconds(1),
            healthObservationDeadline: TimeSpan.FromMilliseconds(250),
            timeProvider: time,
            payloadSource: payload);

        ManagedLauncherEntryResult result = await coordinator.RunAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherEntryOutcome.HealthUnavailable, result.Outcome);
        Assert.Equal(ManagedLauncherEntryReason.HealthDeadlineExceeded, result.Reason);
        Assert.Equal(stage, result.Stage);
        Assert.Equal(0, handoff.StartCount);
    }
}
