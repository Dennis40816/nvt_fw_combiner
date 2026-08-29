using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class ManagedFirstInstallationExperienceTests
{
    /// <summary>A pre-cancelled plan attempt is typed and touches no external source.</summary>
    [Fact]
    public async Task PrepareAlreadyCancelledTouchesNoPayloadOrCandidate()
    {
        SequencedStateStore state = new(MissingState());
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource candidate = new(Candidate());
        ManagedFirstInstallationExperience experience = Create(
            state,
            new SequencedRootProbe(ManagedInstallationRootStatus.Absent),
            payload,
            candidate,
            new RecordingRootMaterializer(state),
            new SetupBootstrapHandoff(state));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            Root("prepare-cancelled"),
            cancellation.Token);

        Assert.Equal(ManagedFirstInstallationOutcome.Cancelled, result.Outcome);
        Assert.Null(result.Plan);
        Assert.Equal(0, state.LoadCount);
        Assert.Equal(0, payload.InspectCount);
        Assert.Equal(0, candidate.InspectCount);
    }

    /// <summary>State failures and an invalid destination stop before root or source access.</summary>
    [Theory]
    [InlineData(VersionManagerStateLoadIssue.Invalid, ManagedFirstInstallationOutcome.RecoveryRequired)]
    [InlineData(VersionManagerStateLoadIssue.ManagedRootMismatch, ManagedFirstInstallationOutcome.RecoveryRequired)]
    [InlineData(VersionManagerStateLoadIssue.Unavailable, ManagedFirstInstallationOutcome.StateUnavailable)]
    public async Task PrepareMapsStateFailureWithoutRootOrSourceAccess(
        VersionManagerStateLoadIssue issue,
        ManagedFirstInstallationOutcome expected)
    {
        SequencedStateStore state = new(new VersionManagerStateLoadResult(null, issue));
        SequencedRootProbe roots = new(ManagedInstallationRootStatus.Absent);
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource candidate = new(Candidate());
        ManagedFirstInstallationExperience experience = Create(
            state,
            roots,
            payload,
            candidate,
            new RecordingRootMaterializer(state),
            new SetupBootstrapHandoff(state));

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            Root($"prepare-state-{issue}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(0, roots.ObserveCount);
        Assert.Equal(0, payload.InspectCount);
        Assert.Equal(0, candidate.InspectCount);
    }

    /// <summary>An invalid destination is rejected before all adapter access.</summary>
    [Fact]
    public async Task PrepareInvalidDestinationTouchesNoAdapter()
    {
        SequencedStateStore state = new(MissingState());
        SequencedRootProbe roots = new(ManagedInstallationRootStatus.Absent);
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource candidate = new(Candidate());
        ManagedFirstInstallationExperience experience = Create(
            state,
            roots,
            payload,
            candidate,
            new RecordingRootMaterializer(state),
            new SetupBootstrapHandoff(state));

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            "relative-root",
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationOutcome.InvalidDestination, result.Outcome);
        Assert.Equal(0, state.LoadCount);
        Assert.Equal(0, roots.ObserveCount);
        Assert.Equal(0, payload.InspectCount);
        Assert.Equal(0, candidate.InspectCount);
    }

    /// <summary>An overlong absolute destination is typed invalid before any port is touched.</summary>
    [Fact]
    public async Task PrepareOverlongDestinationTouchesNoAdapter()
    {
        SequencedStateStore state = new(MissingState());
        SequencedRootProbe roots = new(ManagedInstallationRootStatus.Absent);
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource candidate = new(Candidate());
        ManagedFirstInstallationExperience experience = Create(
            state,
            roots,
            payload,
            candidate,
            new RecordingRootMaterializer(state),
            new SetupBootstrapHandoff(state));
        string destination = $"C:\\{new string('x', 40000)}";

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationOutcome.InvalidDestination, result.Outcome);
        Assert.Equal(0, state.LoadCount);
        Assert.Equal(0, roots.ObserveCount);
        Assert.Equal(0, payload.InspectCount);
        Assert.Equal(0, candidate.InspectCount);
    }

    /// <summary>Root admission failures remain typed and stop before source access.</summary>
    [Theory]
    [InlineData(ManagedInstallationRootStatus.InvalidDestination, ManagedFirstInstallationOutcome.InvalidDestination)]
    [InlineData(ManagedInstallationRootStatus.PermissionDenied, ManagedFirstInstallationOutcome.PermissionDenied)]
    [InlineData(ManagedInstallationRootStatus.Unavailable, ManagedFirstInstallationOutcome.StateUnavailable)]
    [InlineData(ManagedInstallationRootStatus.Present, ManagedFirstInstallationOutcome.RecoveryRequired)]
    [InlineData(ManagedInstallationRootStatus.Residue, ManagedFirstInstallationOutcome.RecoveryRequired)]
    public async Task PrepareMapsRootFailureWithoutSourceAccess(
        ManagedInstallationRootStatus status,
        ManagedFirstInstallationOutcome expected)
    {
        SequencedStateStore state = new(MissingState());
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource candidate = new(Candidate());
        ManagedFirstInstallationExperience experience = Create(
            state,
            new SequencedRootProbe(status),
            payload,
            candidate,
            new RecordingRootMaterializer(state),
            new SetupBootstrapHandoff(state));

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            Root($"prepare-root-{status}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(0, payload.InspectCount);
        Assert.Equal(0, candidate.InspectCount);
    }

    /// <summary>Destination admission is typed and always precedes payload or source observation.</summary>
    [Theory]
    [InlineData(ManagedFirstInstallationMaterializationIssue.InvalidDestination, ManagedFirstInstallationOutcome.InvalidDestination)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.PermissionDenied, ManagedFirstInstallationOutcome.PermissionDenied)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.StateUnavailable, ManagedFirstInstallationOutcome.StateUnavailable)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, ManagedFirstInstallationOutcome.RecoveryRequired)]
    public async Task PrepareMapsDestinationAdmissionWithoutSourceAccess(
        ManagedFirstInstallationMaterializationIssue issue,
        ManagedFirstInstallationOutcome expected)
    {
        SequencedStateStore state = new(MissingState());
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource candidate = new(Candidate());
        var materializer = new RecordingRootMaterializer(state) { AdmissionIssue = issue };
        ManagedFirstInstallationExperience experience = Create(
            state,
            new SequencedRootProbe(ManagedInstallationRootStatus.Absent),
            payload,
            candidate,
            materializer,
            new SetupBootstrapHandoff(state));

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            Root($"prepare-admission-{issue}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(1, materializer.AdmissionCount);
        Assert.Equal(0, payload.InspectCount);
        Assert.Equal(0, candidate.InspectCount);
    }

    /// <summary>Payload failures remain typed and stop before Registry/Catalog candidate access.</summary>
    [Theory]
    [InlineData(ManagedDistributionPayloadIssue.Unavailable, ManagedFirstInstallationOutcome.PayloadUnavailable)]
    [InlineData(ManagedDistributionPayloadIssue.Invalid, ManagedFirstInstallationOutcome.PayloadInvalid)]
    [InlineData(ManagedDistributionPayloadIssue.Changed, ManagedFirstInstallationOutcome.SourceChanged)]
    public async Task PrepareMapsPayloadFailureWithoutCandidateAccess(
        ManagedDistributionPayloadIssue issue,
        ManagedFirstInstallationOutcome expected)
    {
        SequencedStateStore state = new(MissingState());
        RecordingPayloadSource payload = new(PayloadIdentity()) { InspectIssue = issue };
        RecordingCandidateSource candidate = new(Candidate());
        ManagedFirstInstallationExperience experience = Create(
            state,
            new SequencedRootProbe(ManagedInstallationRootStatus.Absent),
            payload,
            candidate,
            new RecordingRootMaterializer(state),
            new SetupBootstrapHandoff(state));

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            Root($"prepare-payload-{issue}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(1, payload.InspectCount);
        Assert.Equal(0, candidate.InspectCount);
    }

    /// <summary>Candidate admission failures retain the existing typed source policy.</summary>
    [Theory]
    [InlineData(FreshInstallationCandidateIssue.RegistryNotConfigured, ManagedFirstInstallationOutcome.SourceUnavailable)]
    [InlineData(FreshInstallationCandidateIssue.SourceUnavailable, ManagedFirstInstallationOutcome.SourceUnavailable)]
    [InlineData(FreshInstallationCandidateIssue.SourceRejected, ManagedFirstInstallationOutcome.SourceRejected)]
    [InlineData(FreshInstallationCandidateIssue.CandidateUnavailable, ManagedFirstInstallationOutcome.CandidateUnavailable)]
    [InlineData(FreshInstallationCandidateIssue.SourceChanged, ManagedFirstInstallationOutcome.SourceChanged)]
    public async Task PrepareMapsCandidateFailure(
        FreshInstallationCandidateIssue issue,
        ManagedFirstInstallationOutcome expected)
    {
        SequencedStateStore state = new(MissingState());
        RecordingCandidateSource candidate = new(Candidate()) { InspectIssue = issue };
        ManagedFirstInstallationExperience experience = Create(
            state,
            new SequencedRootProbe(ManagedInstallationRootStatus.Absent),
            new RecordingPayloadSource(PayloadIdentity()),
            candidate,
            new RecordingRootMaterializer(state),
            new SetupBootstrapHandoff(state));

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            Root($"prepare-candidate-{issue}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(1, candidate.InspectCount);
    }

    /// <summary>Materialization failures preserve their stable public outcome and never start Bootstrap.</summary>
    [Theory]
    [InlineData(ManagedFirstInstallationMaterializationIssue.InvalidDestination, ManagedFirstInstallationOutcome.InvalidDestination)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.PermissionDenied, ManagedFirstInstallationOutcome.PermissionDenied)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.SourceUnavailable, ManagedFirstInstallationOutcome.SourceUnavailable)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.SourceChanged, ManagedFirstInstallationOutcome.SourceChanged)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.PromotionFailed, ManagedFirstInstallationOutcome.StateUnavailable)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.RecoveryRequired, ManagedFirstInstallationOutcome.RecoveryRequired)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.StateUnavailable, ManagedFirstInstallationOutcome.StateUnavailable)]
    public async Task InstallMapsMaterializationFailureBeforeBootstrap(
        ManagedFirstInstallationMaterializationIssue issue,
        ManagedFirstInstallationOutcome expected)
    {
        InstallHarness harness = await CreateInstallHarnessAsync(issue);

        ManagedFirstInstallationResult result = await harness.Experience.InstallAndLaunchAsync(
            harness.Plan,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(1, harness.Materializer.MaterializeCount);
        Assert.Equal(0, harness.Handoff.StartCount);
    }

    /// <summary>Writer contention and writer failure remain distinct typed outcomes.</summary>
    [Theory]
    [InlineData(VersionManagerWriteLeaseIssue.Busy, ManagedFirstInstallationOutcome.Busy)]
    [InlineData(VersionManagerWriteLeaseIssue.Unavailable, ManagedFirstInstallationOutcome.StateUnavailable)]
    public async Task InstallMapsWriterLeaseFailure(
        VersionManagerWriteLeaseIssue issue,
        ManagedFirstInstallationOutcome expected)
    {
        InstallHarness harness = await CreateInstallHarnessAsync();
        harness.State.LeaseIssue = issue;

        ManagedFirstInstallationResult result = await harness.Experience.InstallAndLaunchAsync(
            harness.Plan,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(0, harness.Materializer.MaterializeCount);
        Assert.Equal(0, harness.Handoff.StartCount);
    }

    /// <summary>The lease-held admission recheck stops before payload capture and source reverify.</summary>
    [Theory]
    [InlineData(ManagedFirstInstallationMaterializationIssue.InvalidDestination, ManagedFirstInstallationOutcome.InvalidDestination)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.PermissionDenied, ManagedFirstInstallationOutcome.PermissionDenied)]
    [InlineData(ManagedFirstInstallationMaterializationIssue.StateUnavailable, ManagedFirstInstallationOutcome.StateUnavailable)]
    public async Task InstallRechecksDestinationBeforeCapturingSources(
        ManagedFirstInstallationMaterializationIssue issue,
        ManagedFirstInstallationOutcome expected)
    {
        InstallHarness harness = await CreateInstallHarnessAsync();
        harness.Materializer.AdmissionIssue = issue;

        ManagedFirstInstallationResult result = await harness.Experience.InstallAndLaunchAsync(
            harness.Plan,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(2, harness.Materializer.AdmissionCount);
        Assert.Equal(0, harness.Payload.CaptureCount);
        Assert.Equal(0, harness.CandidateSource.ReverifyCount);
        Assert.Equal(0, harness.Materializer.MaterializeCount);
        Assert.Equal(0, harness.Handoff.StartCount);
    }

    /// <summary>Admission failures preserve their stable outcome after promoted-root evidence exists.</summary>
    [Theory]
    [InlineData(ImmutableBootstrapAdmissionOutcome.Busy, ManagedFirstInstallationOutcome.InstalledButLaunchFailed)]
    [InlineData(ImmutableBootstrapAdmissionOutcome.RecoveryRequired, ManagedFirstInstallationOutcome.RecoveryRequired)]
    [InlineData(ImmutableBootstrapAdmissionOutcome.LaunchFailed, ManagedFirstInstallationOutcome.InstalledButLaunchFailed)]
    [InlineData(ImmutableBootstrapAdmissionOutcome.HealthUnavailable, ManagedFirstInstallationOutcome.InstalledButLaunchFailed)]
    [InlineData(ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed, ManagedFirstInstallationOutcome.InstalledButLaunchFailed)]
    public async Task InstallMapsBootstrapAdmissionFailure(
        ImmutableBootstrapAdmissionOutcome admission,
        ManagedFirstInstallationOutcome expected)
    {
        InstallHarness harness = await CreateInstallHarnessAsync(admissionOutcome: admission);

        ManagedFirstInstallationResult result = await harness.Experience.InstallAndLaunchAsync(
            harness.Plan,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(1, harness.Materializer.Transaction.RecordLaunchCount);
        Assert.Equal(0, harness.Materializer.Transaction.CompleteCount);
        if (admission == ImmutableBootstrapAdmissionOutcome.Busy)
        {
            _ = await harness.Experience.InstallAndLaunchAsync(
                harness.Plan,
                TestContext.Current.CancellationToken);
            Assert.Equal(1, harness.Handoff.StartCount);
        }
    }

    /// <summary>Caller cancellation during either Bootstrap wait drains the launch adapter first.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InstallBootstrapCancellationWithoutTypedCleanupIsLaunchFailure(bool duringAdmission)
    {
        using var cancellation = new CancellationTokenSource();
        InstallHarness harness = await CreateInstallHarnessAsync();
        var cancellingHandoff = new CancellingBootstrapHandoff(
            harness.State,
            cancellation,
            duringAdmission);
        ManagedFirstInstallationExperience experience = Create(
            harness.State,
            harness.Roots,
            harness.Payload,
            harness.CandidateSource,
            harness.Materializer,
            cancellingHandoff);

        ManagedFirstInstallationResult result = await experience.InstallAndLaunchAsync(
            harness.Plan,
            cancellation.Token);

        Assert.Equal(ManagedFirstInstallationOutcome.InstalledButLaunchFailed, result.Outcome);
        Assert.True(cancellingHandoff.Launch.CleanupReturned);
        Assert.True(cancellingHandoff.Launch.Disposed);
        Assert.Equal(1, harness.Materializer.Transaction.RecordLaunchCount);
        Assert.Equal(0, harness.Materializer.Transaction.CompleteCount);
    }

    /// <summary>Caller cancellation cannot hide that root promotion already completed.</summary>
    [Fact]
    public async Task InstallReportsPromotedRootWhenAdapterReturnsTypedFailureAfterCancellingCaller()
    {
        using var cancellation = new CancellationTokenSource();
        InstallHarness harness = await CreateInstallHarnessAsync();
        var handoff = new TypedFailureCancellingHandoff(harness.State, cancellation);
        ManagedFirstInstallationExperience experience = Create(
            harness.State,
            harness.Roots,
            harness.Payload,
            harness.CandidateSource,
            harness.Materializer,
            handoff);

        ManagedFirstInstallationResult result = await experience.InstallAndLaunchAsync(
            harness.Plan,
            cancellation.Token);

        Assert.Equal(ManagedFirstInstallationOutcome.InstalledButLaunchFailed, result.Outcome);
        Assert.True(handoff.Launch.Disposed);
        Assert.Equal(1, harness.Materializer.Transaction.DisposeCount);
    }

    /// <summary>A start adapter that cancels after creating a receipt cannot leak that receipt.</summary>
    [Fact]
    public async Task InstallOwnsStartReceiptBeforeCallerCancellationWins()
    {
        using var cancellation = new CancellationTokenSource();
        InstallHarness harness = await CreateInstallHarnessAsync();
        var handoff = new CancellingStartBootstrapHandoff(harness.State, cancellation);
        ManagedFirstInstallationExperience experience = Create(
            harness.State,
            harness.Roots,
            harness.Payload,
            harness.CandidateSource,
            harness.Materializer,
            handoff);

        ManagedFirstInstallationResult result = await experience.InstallAndLaunchAsync(
            harness.Plan,
            cancellation.Token);

        Assert.Equal(ManagedFirstInstallationOutcome.InstalledButLaunchFailed, result.Outcome);
        Assert.True(handoff.Launch.Disposed);
        Assert.Equal(1, handoff.Launch.AdmissionWaitCount);
        Assert.Equal(1, harness.Materializer.Transaction.DisposeCount);
    }

    /// <summary>Cancellation cannot hide a contradictory start receipt and issue.</summary>
    [Fact]
    public async Task InstallCancellationCannotHideMalformedStartReceipt()
    {
        using var cancellation = new CancellationTokenSource();
        InstallHarness harness = await CreateInstallHarnessAsync();
        var handoff = new MalformedStartBootstrapHandoff(
            harness.State,
            attachLaunch: true,
            ImmutableBootstrapStartIssue.StartFailed,
            cancellation);
        ManagedFirstInstallationExperience experience = Create(
            harness.State,
            harness.Roots,
            harness.Payload,
            harness.CandidateSource,
            harness.Materializer,
            handoff);

        ManagedFirstInstallationResult result = await experience.InstallAndLaunchAsync(
            harness.Plan,
            cancellation.Token);

        Assert.Equal(ManagedFirstInstallationOutcome.InstalledButLaunchFailed, result.Outcome);
        Assert.True(handoff.Launch.Disposed);
        Assert.Equal(0, handoff.Launch.AdmissionWaitCount);
    }

    /// <summary>Cancellation cannot hide failure to prove the Bootstrap job empty.</summary>
    [Fact]
    public async Task InstallCancellationCannotHideTerminationUnconfirmed()
    {
        using var cancellation = new CancellationTokenSource();
        InstallHarness harness = await CreateInstallHarnessAsync();
        harness.Handoff.AdmissionOutcome = ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed;
        harness.Handoff.AdmissionAction = cancellation.Cancel;

        ManagedFirstInstallationResult result = await harness.Experience.InstallAndLaunchAsync(
            harness.Plan,
            cancellation.Token);

        Assert.Equal(ManagedFirstInstallationOutcome.InstalledButLaunchFailed, result.Outcome);
        Assert.Equal(0, harness.Materializer.Transaction.CompleteCount);
    }

    /// <summary>READY commits before a concurrent caller cancellation and still removes the marker.</summary>
    [Fact]
    public async Task InstallReadyWinsConcurrentCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        InstallHarness harness = await CreateInstallHarnessAsync();
        harness.Handoff.CompletionAction = cancellation.Cancel;

        ManagedFirstInstallationResult result = await harness.Experience.InstallAndLaunchAsync(
            harness.Plan,
            cancellation.Token);

        Assert.Equal(ManagedFirstInstallationOutcome.Completed, result.Outcome);
        Assert.Equal(1, harness.Materializer.Transaction.CompleteCount);
        Assert.Equal(2, harness.State.LeaseCount);
    }

    /// <summary>Final writer contention after READY is recovery, never a retryable second launch.</summary>
    [Fact]
    public async Task InstallFinalizeBusyIsRecoveryAndCannotStartAgain()
    {
        InstallHarness harness = await CreateInstallHarnessAsync();
        harness.State.SecondLeaseIssue = VersionManagerWriteLeaseIssue.Busy;

        ManagedFirstInstallationResult first = await harness.Experience.InstallAndLaunchAsync(
            harness.Plan,
            TestContext.Current.CancellationToken);
        int startsAfterFailure = harness.Handoff.StartCount;
        _ = await harness.Experience.InstallAndLaunchAsync(
            harness.Plan,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationOutcome.RecoveryRequired, first.Outcome);
        Assert.Equal(1, startsAfterFailure);
        Assert.Equal(startsAfterFailure, harness.Handoff.StartCount);
        Assert.Equal(0, harness.Materializer.Transaction.CompleteCount);
    }
}
