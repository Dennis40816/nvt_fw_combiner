using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests the genuinely-absent-only first-install orchestration.</summary>
public sealed partial class ManagedFirstInstallationExperienceTests
{
    private const string HashA =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly ManagedImmutableBootstrapIdentity Bootstrap = new(
        "NvtFwCombiner.Bootstrap.exe",
        1024,
        HashB);

    /// <summary>Residue is rejected before payload, Registry, Catalog, or package access.</summary>
    [Fact]
    public async Task PrepareWithResidueTouchesNoPayloadOrSource()
    {
        string root = Root("prepare-residue");
        SequencedStateStore state = new(MissingState());
        SequencedRootProbe roots = new(ManagedInstallationRootStatus.Residue);
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource source = new(Candidate());
        ManagedFirstInstallationExperience experience = Create(
            state,
            roots,
            payload,
            source,
            new RecordingRootMaterializer(state),
            new SetupBootstrapHandoff(state));

        ManagedFirstInstallationPlanResult result = await experience.PrepareAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.Null(result.Plan);
        Assert.Equal(ManagedFirstInstallationOutcome.RecoveryRequired, result.Outcome);
        Assert.Equal(0, payload.InspectCount);
        Assert.Equal(0, source.InspectCount);
    }

    /// <summary>The happy path rechecks under one lease, releases it before start, and completes after READY.</summary>
    [Fact]
    public async Task CompletedInstallationUsesExactRechecksAndLeaseOrdering()
    {
        string root = Root("completed");
        FreshInstallationCandidate candidate = Candidate();
        SequencedStateStore state = new(
            MissingState(),
            MissingState(),
            BoundState(root, candidate));
        SequencedRootProbe roots = new(
            ManagedInstallationRootStatus.Absent,
            ManagedInstallationRootStatus.Absent);
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource source = new(candidate);
        RecordingRootMaterializer materializer = new(state);
        SetupBootstrapHandoff handoff = new(state);
        ManagedFirstInstallationExperience experience = Create(
            state,
            roots,
            payload,
            source,
            materializer,
            handoff);
        ManagedFirstInstallationPlanResult prepared = await experience.PrepareAsync(
            root,
            TestContext.Current.CancellationToken);

        ManagedFirstInstallationResult result = await experience.InstallAndLaunchAsync(
            Assert.IsType<ManagedFirstInstallationPlan>(prepared.Plan),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationOutcome.Completed, result.Outcome);
        Assert.Equal(2, state.LeaseCount);
        Assert.Equal(0, state.SaveCount);
        Assert.Equal(1, payload.CaptureCount);
        Assert.Equal(1, source.ReverifyCount);
        Assert.Equal(2, materializer.AdmissionCount);
        Assert.Equal(1, materializer.MaterializeCount);
        Assert.Equal(1, materializer.Transaction.RecordLaunchCount);
        Assert.Equal(1, materializer.Transaction.CompleteCount);
        Assert.Equal(1, handoff.StartCount);
        Assert.Equal(1, materializer.Transaction.DisposeCount);
    }

    /// <summary>Authority drift stops before any root materialization or Bootstrap start.</summary>
    [Fact]
    public async Task SourceDriftStopsBeforeRootMutation()
    {
        string root = Root("source-drift");
        FreshInstallationCandidate candidate = Candidate();
        SequencedStateStore state = new(MissingState(), MissingState());
        RecordingCandidateSource source = new(candidate)
        {
            ReverifyIssue = FreshInstallationCandidateIssue.SourceChanged,
        };
        RecordingRootMaterializer materializer = new(state);
        SetupBootstrapHandoff handoff = new(state);
        ManagedFirstInstallationExperience experience = Create(
            state,
            new SequencedRootProbe(
                ManagedInstallationRootStatus.Absent,
                ManagedInstallationRootStatus.Absent),
            new RecordingPayloadSource(PayloadIdentity()),
            source,
            materializer,
            handoff);
        ManagedFirstInstallationPlanResult prepared = await experience.PrepareAsync(
            root,
            TestContext.Current.CancellationToken);

        ManagedFirstInstallationResult result = await experience.InstallAndLaunchAsync(
            Assert.IsType<ManagedFirstInstallationPlan>(prepared.Plan),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationOutcome.SourceChanged, result.Outcome);
        Assert.Equal(0, materializer.MaterializeCount);
        Assert.Equal(0, handoff.StartCount);
    }

    /// <summary>A promoted root remains recovery evidence when exact Bootstrap start fails.</summary>
    [Fact]
    public async Task BootstrapStartFailureDoesNotClearTransactionMarker()
    {
        string root = Root("start-failed");
        FreshInstallationCandidate candidate = Candidate();
        SequencedStateStore state = new(MissingState(), MissingState());
        RecordingRootMaterializer materializer = new(state);
        SetupBootstrapHandoff handoff = new(state)
        {
            StartIssue = ImmutableBootstrapStartIssue.StartFailed,
        };
        ManagedFirstInstallationExperience experience = Create(
            state,
            new SequencedRootProbe(
                ManagedInstallationRootStatus.Absent,
                ManagedInstallationRootStatus.Absent),
            new RecordingPayloadSource(PayloadIdentity()),
            new RecordingCandidateSource(candidate),
            materializer,
            handoff);
        ManagedFirstInstallationPlanResult prepared = await experience.PrepareAsync(
            root,
            TestContext.Current.CancellationToken);

        ManagedFirstInstallationResult result = await experience.InstallAndLaunchAsync(
            Assert.IsType<ManagedFirstInstallationPlan>(prepared.Plan),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedFirstInstallationOutcome.InstalledButLaunchFailed, result.Outcome);
        Assert.Equal(1, materializer.Transaction.RecordLaunchCount);
        Assert.Equal(0, materializer.Transaction.CompleteCount);
    }

    private static ManagedFirstInstallationExperience Create(
        IVersionManagerStateStore state,
        IManagedInstallationRootProbe roots,
        IManagedDistributionPayloadSource payload,
        IFreshInstallationCandidateSource source,
        IManagedFirstInstallationRootMaterializer materializer,
        IImmutableBootstrapHandoff handoff,
        TimeSpan? admissionOperationCutoff = null,
        TimeSpan? completionOperationCutoff = null,
        TimeProvider? timeProvider = null)
    {
        return new(
            StatePath(),
            state,
            roots,
            payload,
            source,
            materializer,
            handoff,
            admissionOperationCutoff,
            completionOperationCutoff,
            timeProvider);
    }

    private static ManagedDistributionPayloadIdentity PayloadIdentity()
    {
        return new(
            ManagedAppVersion.Parse("1.0.4"),
            new string('c', 40),
            2048,
            HashA,
            512,
            HashB,
            Bootstrap);
    }

    private static FreshInstallationCandidate Candidate()
    {
        string sourceRoot = Root("source");
        var document = new UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [new(
                "1.0.4",
                "2026-08-29T00:00:00Z",
                "packages/NvtFwCombiner-v1.0.4-win-x64.zip",
                4096,
                HashA,
                HashB,
                "Release 1.0.4")]);
        UpdateCatalogVersionSnapshot package = Assert.IsType<UpdateCatalogSnapshot>(
            UpdateCatalogValidator.Validate(document).Snapshot).Versions[0];
        var identity = new FreshInstallationCandidateIdentity(
            "nvt-fw-combiner-production",
            1,
            HashA,
            1,
            package.Version,
            HashB,
            Path.Combine(sourceRoot, "update-catalog.v1.json"),
            sourceRoot,
            UpdateSourceRegistryEntryStatus.Latest,
            package.PackagePath.Value,
            package.PackageSize,
            package.PackageSha256,
            package.ReleaseManifestSha256);
        return new(
            identity,
            package,
            new VerifiedUpdateCandidate(package.Version, package.Identity, package.ReleaseNotes));
    }

    private static VersionManagerStateLoadResult MissingState()
    {
        return new(null, VersionManagerStateLoadIssue.Missing);
    }

    private static VersionManagerStateLoadResult BoundState(
        string root,
        FreshInstallationCandidate candidate)
    {
        ManagedVersionAdmission admission = Admission(candidate);
        return new(
            VersionManagerState.Create(
                updateSource: null,
                activeVersion: admission.Version,
                lastKnownGoodVersion: admission.Version,
                [admission],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: root),
            VersionManagerStateLoadIssue.None);
    }

    private static ManagedVersionAdmission Admission(FreshInstallationCandidate candidate)
    {
        return new(
            candidate.Package.Version,
            candidate.Package.Identity,
            candidate.Package.ReleaseManifestSha256);
    }

    private static string Root(string name)
    {
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nfc-setup-tests", name));
    }

    private static string StatePath()
    {
        return Path.Combine(Root("state"), "version-manager.v1.json");
    }

    private sealed class SequencedStateStore(params VersionManagerStateLoadResult[] loads)
        : IVersionManagerStateStore
    {
        private int _loadIndex;
        internal bool LeaseActive { get; private set; }
        internal VersionManagerWriteLeaseIssue LeaseIssue { get; set; }
        internal VersionManagerWriteLeaseIssue SecondLeaseIssue { get; set; }
        internal int LeaseCount { get; private set; }
        internal int LeaseAttemptCount { get; private set; }
        internal int LoadCount { get; private set; }
        internal int SaveCount { get; private set; }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
            "Ownership transfers into the returned write-lease result and the coordinator disposes it.")]
        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LeaseAttemptCount++;
            VersionManagerWriteLeaseIssue issue = LeaseAttemptCount == 2 &&
                SecondLeaseIssue != VersionManagerWriteLeaseIssue.None
                    ? SecondLeaseIssue
                    : LeaseIssue;
            if (issue != VersionManagerWriteLeaseIssue.None)
            {
                return ValueTask.FromResult(new VersionManagerWriteLeaseResult(issue, null));
            }
            Assert.False(LeaseActive);
            LeaseActive = true;
            LeaseCount++;
            return ValueTask.FromResult(new VersionManagerWriteLeaseResult(
                VersionManagerWriteLeaseIssue.None,
                new ActionLease(() => LeaseActive = false)));
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            int index = Math.Min(_loadIndex++, loads.Length - 1);
            return ValueTask.FromResult(loads[index]);
        }

        public ValueTask SaveAsync(VersionManagerState state, CancellationToken cancellationToken)
        {
            SaveCount++;
            throw new InvalidOperationException("Setup must not write per-user version state.");
        }
    }

    private sealed class ActionLease(Action release) : IDisposable
    {
        public void Dispose()
        {
            release();
        }
    }

    private sealed class SequencedRootProbe(params ManagedInstallationRootStatus[] statuses)
        : IManagedInstallationRootProbe
    {
        private int _index;

        internal int ObserveCount { get; private set; }

        public ValueTask<ManagedInstallationRootObservation> ObserveAsync(
            string managedRoot,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObserveCount++;
            int index = Math.Min(_index++, statuses.Length - 1);
            return ValueTask.FromResult(new ManagedInstallationRootObservation(statuses[index]));
        }
    }

    private sealed class RecordingPayloadSource(ManagedDistributionPayloadIdentity identity)
        : IManagedDistributionPayloadSource
    {
        internal ManagedDistributionPayloadIssue CaptureIssue { get; set; }
        internal int CaptureCount { get; private set; }
        internal ManagedDistributionPayloadIssue InspectIssue { get; init; }
        internal int InspectCount { get; private set; }
        internal PayloadCapture? LastCapture { get; private set; }
        internal bool ReturnCaptureOnFailure { get; set; }
        internal bool ReturnMismatchedCapture { get; set; }
        internal bool ReturnNullCaptureOnSuccess { get; set; }

        public ValueTask<ManagedDistributionPayloadEntryAdmissionResult> AdmitEntryAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(InspectIssue == ManagedDistributionPayloadIssue.None
                ? new ManagedDistributionPayloadEntryAdmissionResult(
                    identity.LauncherVersion,
                    identity.Bootstrap,
                    ManagedDistributionPayloadIssue.None)
                : new ManagedDistributionPayloadEntryAdmissionResult(default, null, InspectIssue));
        }

        public ValueTask<ManagedDistributionPayloadInspectionResult> InspectAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectCount++;
            return ValueTask.FromResult(InspectIssue == ManagedDistributionPayloadIssue.None
                ? new ManagedDistributionPayloadInspectionResult(
                    identity,
                    ManagedDistributionPayloadIssue.None)
                : new ManagedDistributionPayloadInspectionResult(null, InspectIssue));
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
            "Ownership transfers into the returned payload capture and the coordinator disposes it.")]
        public ValueTask<ManagedDistributionPayloadCaptureResult> CaptureExactAsync(
            ManagedDistributionPayloadIdentity expected,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureCount++;
            Assert.Equal(identity, expected);
            ManagedDistributionPayloadIdentity capturedIdentity = ReturnMismatchedCapture
                ? new(
                    identity.LauncherVersion,
                    new string('d', 40),
                    identity.LauncherSize,
                    identity.LauncherSha256,
                    identity.DescriptorSize,
                    identity.DescriptorSha256,
                    identity.Bootstrap)
                : identity;
            LastCapture = !ReturnNullCaptureOnSuccess &&
                (CaptureIssue == ManagedDistributionPayloadIssue.None || ReturnCaptureOnFailure)
                    ? new PayloadCapture(capturedIdentity)
                    : null;
            return ValueTask.FromResult(new ManagedDistributionPayloadCaptureResult(
                LastCapture,
                CaptureIssue));
        }
    }

    private sealed class PayloadCapture(ManagedDistributionPayloadIdentity identity)
        : IManagedDistributionPayloadCapture
    {
        internal bool Disposed { get; private set; }
        public ManagedDistributionPayloadIdentity Identity => identity;

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class RecordingCandidateSource(FreshInstallationCandidate candidate)
        : IFreshInstallationCandidateSource
    {
        internal FreshInstallationCandidateIssue InspectIssue { get; init; }
        internal FreshInstallationCandidateIssue ReverifyIssue { get; init; }
        internal int InspectCount { get; private set; }
        internal int ReverifyCount { get; private set; }

        public ValueTask<FreshInstallationCandidateResult> InspectFreshInstallationAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectCount++;
            return ValueTask.FromResult(InspectIssue == FreshInstallationCandidateIssue.None
                ? new FreshInstallationCandidateResult(
                    candidate,
                    FreshInstallationCandidateIssue.None)
                : new FreshInstallationCandidateResult(null, InspectIssue));
        }

        public ValueTask<FreshInstallationCandidateResult> ReverifyFreshInstallationAsync(
            FreshInstallationCandidate expected,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReverifyCount++;
            Assert.Equal(candidate.Identity, expected.Identity);
            return ValueTask.FromResult(ReverifyIssue == FreshInstallationCandidateIssue.None
                ? new FreshInstallationCandidateResult(candidate, FreshInstallationCandidateIssue.None)
                : new FreshInstallationCandidateResult(null, ReverifyIssue));
        }
    }

    private sealed class RecordingRootMaterializer(SequencedStateStore state)
        : IManagedFirstInstallationRootMaterializer
    {
        internal ManagedFirstInstallationMaterializationIssue AdmissionIssue { get; set; }
        internal int AdmissionCount { get; private set; }
        internal ManagedFirstInstallationMaterializationIssue Issue { get; init; }
        internal int MaterializeCount { get; private set; }
        internal RecordingPromotedInstallation Transaction { get; } = new(state);
        internal bool ReturnInstallationOnFailure { get; set; }

        public ValueTask<ManagedFirstInstallationMaterializationIssue> AdmitDestinationAsync(
            string managedRoot,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AdmissionCount++;
            return ValueTask.FromResult(AdmissionIssue);
        }

        public ValueTask<ManagedFirstInstallationMaterializationResult> MaterializeAsync(
            string managedRoot,
            string statePathIdentity,
            IManagedDistributionPayloadCapture payload,
            FreshInstallationCandidate candidate,
            VersionManagerState seed,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(state.LeaseActive);
            Assert.True(ManagedVersionSeedPolicy.IsCanonicalFirstRunSeed(seed));
            Assert.Equal(candidate.Package.Version, seed.ActiveVersion);
            Assert.Equal(payload.Identity, PayloadIdentity());
            MaterializeCount++;
            if (Issue != ManagedFirstInstallationMaterializationIssue.None)
            {
                if (ReturnInstallationOnFailure)
                {
                    Transaction.Initialize(managedRoot, Admission(candidate));
                }
                return ValueTask.FromResult(new ManagedFirstInstallationMaterializationResult(
                    ReturnInstallationOnFailure ? Transaction : null,
                    Issue));
            }
            Transaction.Initialize(managedRoot, Admission(candidate));
            return ValueTask.FromResult(new ManagedFirstInstallationMaterializationResult(
                Transaction,
                ManagedFirstInstallationMaterializationIssue.None));
        }
    }

    private sealed class RecordingPromotedInstallation(SequencedStateStore state)
        : IManagedPromotedFirstInstallation
    {
        public string ManagedRoot { get; private set; } = string.Empty;
        public ManagedVersionAdmission Admission { get; private set; } = null!;
        internal int CompleteCount { get; private set; }
        internal int DisposeCount { get; private set; }
        internal ManagedFirstInstallationTransactionIssue CompleteIssue { get; set; }
        internal int RecordLaunchCount { get; private set; }
        internal ManagedFirstInstallationTransactionIssue RecordLaunchIssue { get; set; }

        public void Dispose()
        {
            DisposeCount++;
        }

        internal void Initialize(string managedRoot, ManagedVersionAdmission admission)
        {
            ManagedRoot = managedRoot;
            Admission = admission;
        }

        public ValueTask<ManagedFirstInstallationTransactionIssue> RecordBootstrapLaunchAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(state.LeaseActive);
            RecordLaunchCount++;
            return ValueTask.FromResult(RecordLaunchIssue);
        }

        public ValueTask<ManagedFirstInstallationTransactionIssue> CompleteAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(state.LeaseActive);
            CompleteCount++;
            return ValueTask.FromResult(CompleteIssue);
        }
    }

    private sealed class SetupBootstrapHandoff(SequencedStateStore state)
        : IImmutableBootstrapHandoff
    {
        internal Action? AdmissionAction { get; set; }
        internal Action<ImmutableBootstrapWaitBudget>? AdmissionBudgetObserved { get; set; }
        internal ImmutableBootstrapAdmissionOutcome AdmissionOutcome { get; set; } =
            ImmutableBootstrapAdmissionOutcome.Admitted;
        internal Action? CompletionAction { get; set; }
        internal ImmutableBootstrapCompletionOutcome CompletionOutcome { get; set; } =
            ImmutableBootstrapCompletionOutcome.Ready;
        internal bool IgnoreAdmissionCancellation { get; set; }
        internal SetupBootstrapLaunch? LastLaunch { get; private set; }
        internal Action? StartAction { get; set; }
        internal ImmutableBootstrapStartIssue StartIssue { get; set; }
        internal int StartCount { get; private set; }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
            "Ownership transfers into the returned launch receipt and the coordinator disposes it.")]
        public ValueTask<ImmutableBootstrapStartResult> StartAsync(
            string managedRoot,
            ManagedImmutableBootstrapIdentity expectedIdentity,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.False(state.LeaseActive);
            Assert.Equal(Bootstrap, expectedIdentity);
            StartAction?.Invoke();
            StartCount++;
            if (StartIssue == ImmutableBootstrapStartIssue.None)
            {
                LastLaunch = new SetupBootstrapLaunch(
                        AdmissionOutcome,
                        CompletionOutcome,
                        AdmissionAction,
                        CompletionAction,
                        AdmissionBudgetObserved,
                        IgnoreAdmissionCancellation);
                return ValueTask.FromResult(new ImmutableBootstrapStartResult(
                    LastLaunch,
                    ImmutableBootstrapStartIssue.None));
            }
            return ValueTask.FromResult(new ImmutableBootstrapStartResult(null, StartIssue));
        }
    }

    private sealed class SetupBootstrapLaunch(
        ImmutableBootstrapAdmissionOutcome admissionOutcome,
        ImmutableBootstrapCompletionOutcome completionOutcome,
        Action? admissionAction,
        Action? completionAction,
        Action<ImmutableBootstrapWaitBudget>? admissionBudgetObserved,
        bool ignoreAdmissionCancellation) : IImmutableBootstrapLaunch
    {
        internal bool Disposed { get; private set; }

        public ValueTask<ImmutableBootstrapAdmissionResult> WaitForAdmissionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            if (!ignoreAdmissionCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            admissionBudgetObserved?.Invoke(budget);
            admissionAction?.Invoke();
            return ValueTask.FromResult(new ImmutableBootstrapAdmissionResult(
                admissionOutcome));
        }

        public ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            completionAction?.Invoke();
            return ValueTask.FromResult(new ImmutableBootstrapCompletionResult(
                completionOutcome));
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
