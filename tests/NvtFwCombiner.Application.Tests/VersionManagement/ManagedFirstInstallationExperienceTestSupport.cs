using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Provides shared helpers for first-install experience tests.</summary>
internal static class ManagedFirstInstallationExperienceTestSupport
{
    internal const string HashA =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string HashB =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    internal static readonly ManagedImmutableBootstrapIdentity Bootstrap = new(
        "NvtFwCombiner.Bootstrap.exe",
        1024,
        HashB);

    internal static ManagedFirstInstallationExperience Create(
        IVersionManagerStateStore state,
        IManagedInstallationRootProbe roots,
        IManagedDistributionPayloadSource payload,
        IFreshInstallationCandidateSource source,
        IManagedFirstInstallationRootMaterializer materializer,
        IImmutableBootstrapLeaseHandoff handoff,
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

    internal static ManagedDistributionPayloadIdentity PayloadIdentity()
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

    internal static FreshInstallationCandidate Candidate()
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

    internal static VersionManagerStateLoadResult MissingState()
    {
        return new(null, VersionManagerStateLoadIssue.Missing);
    }

    internal static VersionManagerStateLoadResult BoundState(
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

    internal static ManagedVersionAdmission Admission(FreshInstallationCandidate candidate)
    {
        return new(
            candidate.Package.Version,
            candidate.Package.Identity,
            candidate.Package.ReleaseManifestSha256);
    }

    internal static string Root(string name)
    {
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nfc-setup-tests", name));
    }

    internal static string StatePath()
    {
        return Path.Combine(Root("state"), "version-manager.v1.json");
    }

    internal sealed class SequencedStateStore(params VersionManagerStateLoadResult[] loads)
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
        internal List<string> Events { get; } = [];

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
            int leaseNumber = LeaseCount;
            Events.Add($"writer-acquire:{leaseNumber}");
            return ValueTask.FromResult(new VersionManagerWriteLeaseResult(
                VersionManagerWriteLeaseIssue.None,
                new ActionLease(() =>
                {
                    LeaseActive = false;
                    Events.Add($"writer-release:{leaseNumber}");
                })));
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

    internal sealed class ActionLease(Action release) : IDisposable
    {
        public void Dispose()
        {
            release();
        }
    }

    internal sealed class SequencedRootProbe(params ManagedInstallationRootStatus[] statuses)
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

    internal sealed class RecordingPayloadSource(ManagedDistributionPayloadIdentity identity)
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

    internal sealed class PayloadCapture(ManagedDistributionPayloadIdentity identity)
        : IManagedDistributionPayloadCapture
    {
        internal bool Disposed { get; private set; }
        public ManagedDistributionPayloadIdentity Identity => identity;

        public void Dispose()
        {
            Disposed = true;
        }
    }

    internal sealed class RecordingCandidateSource(FreshInstallationCandidate candidate)
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

    internal sealed class RecordingRootMaterializer(SequencedStateStore state)
        : IManagedFirstInstallationRootMaterializer
    {
        internal ManagedFirstInstallationMaterializationIssue AdmissionIssue { get; set; }
        internal int AdmissionCount { get; private set; }
        internal ManagedFirstInstallationMaterializationIssue Issue { get; init; }
        internal int MaterializeCount { get; private set; }
        internal IReadOnlyList<ManagedFirstInstallationProgress> ProgressUpdates { get; init; } = [];
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

        public ValueTask<ManagedFirstInstallationMaterializationResult> MaterializeAsync(
            string managedRoot,
            string statePathIdentity,
            IManagedDistributionPayloadCapture payload,
            FreshInstallationCandidate candidate,
            VersionManagerState seed,
            IProgress<ManagedFirstInstallationProgress>? progress,
            CancellationToken cancellationToken)
        {
            foreach (ManagedFirstInstallationProgress update in ProgressUpdates)
            {
                progress?.Report(update);
            }
            return MaterializeAsync(
                managedRoot,
                statePathIdentity,
                payload,
                candidate,
                seed,
                cancellationToken);
        }
    }

    internal sealed class RecordingPromotedInstallation(SequencedStateStore state)
        : IManagedPromotedFirstInstallation
    {
        public string ManagedRoot { get; private set; } = string.Empty;
        public ManagedVersionAdmission Admission { get; private set; } = null!;
        internal int CompleteCount { get; private set; }
        internal int BootstrapLeaseAcquireCount { get; private set; }
        internal Action? BootstrapLeaseAcquireAction { get; set; }
        internal Action? BootstrapLeaseAcquiredAction { get; set; }
        internal ManagedExecutableLaunchIssue BootstrapLeaseIssue { get; set; }
        internal ManagedExecutableLaunchLeaseResult? BootstrapLeaseResultOverride { get; set; }
        internal int DisposeCount { get; private set; }
        internal RecordingExecutableLaunchLease? LastBootstrapLease { get; private set; }
        internal ManagedFirstInstallationTransactionIssue CompleteIssue { get; set; }
        internal int RecordLaunchCount { get; private set; }
        internal Action? RecordLaunchAction { get; set; }
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
            RecordLaunchCount++;
            state.Events.Add("record-bootstrap-launch");
            RecordLaunchAction?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(state.LeaseActive);
            return ValueTask.FromResult(RecordLaunchIssue);
        }

        public ValueTask<ManagedExecutableLaunchLeaseResult> AcquireBootstrapLaunchLeaseAsync(
            ManagedImmutableBootstrapIdentity expectedIdentity,
            CancellationToken cancellationToken)
        {
            Assert.True(state.LeaseActive);
            Assert.Equal(Bootstrap, expectedIdentity);
            Assert.Equal(1, RecordLaunchCount);
            BootstrapLeaseAcquireCount++;
            state.Events.Add("acquire-bootstrap-lease");
            BootstrapLeaseAcquireAction?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (BootstrapLeaseResultOverride is not null)
            {
                LastBootstrapLease = BootstrapLeaseResultOverride.Lease as
                    RecordingExecutableLaunchLease;
                return ValueTask.FromResult(BootstrapLeaseResultOverride);
            }
            if (BootstrapLeaseIssue != ManagedExecutableLaunchIssue.None)
            {
                return ValueTask.FromResult(new ManagedExecutableLaunchLeaseResult(
                    null,
                    BootstrapLeaseIssue));
            }
            LastBootstrapLease = new RecordingExecutableLaunchLease(
                Path.Combine(ManagedRoot, expectedIdentity.FileName),
                ManagedRoot);
            BootstrapLeaseAcquiredAction?.Invoke();
            return ValueTask.FromResult(new ManagedExecutableLaunchLeaseResult(
                LastBootstrapLease,
                ManagedExecutableLaunchIssue.None));
        }

        public ValueTask<ManagedFirstInstallationTransactionIssue> CompleteAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(state.LeaseActive);
            CompleteCount++;
            state.Events.Add("complete");
            return ValueTask.FromResult(CompleteIssue);
        }
    }

    internal sealed class RecordingExecutableLaunchLease(
        string executablePath,
        string workingDirectory) : IManagedExecutableLaunchLease
    {
        internal bool Disposed { get; private set; }
        public string ExecutablePath => executablePath;
        public string WorkingDirectory => workingDirectory;
        public bool TryValidateForStart()
        {
            return !Disposed;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Ownership transfers through the returned Bootstrap start receipt and its launch wrapper.")]
    internal static ImmutableBootstrapStartResult StartResultOwningLease(
        IImmutableBootstrapLaunch launch,
        IManagedExecutableLaunchLease ownedLease,
        ImmutableBootstrapStartIssue issue = ImmutableBootstrapStartIssue.None)
    {
        return new(new LeaseOwningBootstrapLaunch(launch, ownedLease), issue);
    }

    internal sealed class LeaseOwningBootstrapLaunch(
        IImmutableBootstrapLaunch launch,
        IManagedExecutableLaunchLease ownedLease) : IImmutableBootstrapLaunch
    {
        public ValueTask<ImmutableBootstrapAdmissionResult> WaitForAdmissionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            return launch.WaitForAdmissionAsync(budget, cancellationToken);
        }

        public ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            return launch.WaitForCompletionAsync(budget, cancellationToken);
        }

        public void Dispose()
        {
            try
            {
                launch.Dispose();
            }
            finally
            {
                ownedLease.Dispose();
            }
        }
    }

    internal sealed class SetupBootstrapHandoff(SequencedStateStore state)
        : IImmutableBootstrapLeaseHandoff
    {
        internal Action? AdmissionAction { get; set; }
        internal Action<ImmutableBootstrapWaitBudget>? AdmissionBudgetObserved { get; set; }
        internal ImmutableBootstrapAdmissionOutcome AdmissionOutcome { get; set; } =
            ImmutableBootstrapAdmissionOutcome.Admitted;
        internal int? AdmissionExitCode { get; set; }
        internal ImmutableBootstrapExitIssue AdmissionExitIssue { get; set; }
        internal Action? CompletionAction { get; set; }
        internal ImmutableBootstrapCompletionOutcome CompletionOutcome { get; set; } =
            ImmutableBootstrapCompletionOutcome.Ready;
        internal int? CompletionExitCode { get; set; } = 0;
        internal ImmutableBootstrapExitIssue CompletionExitIssue { get; set; }
        internal bool IgnoreAdmissionCancellation { get; set; }
        internal SetupBootstrapLaunch? LastLaunch { get; private set; }
        internal Action? StartAction { get; set; }
        internal ImmutableBootstrapStartIssue StartIssue { get; set; }
        internal int StartCount { get; private set; }
        internal IManagedExecutableLaunchLease? ReceivedLease { get; private set; }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
            "Ownership transfers into the returned launch receipt and the coordinator disposes it.")]
        public ValueTask<ImmutableBootstrapStartResult> StartAsync(
            string managedRoot,
            ManagedImmutableBootstrapIdentity expectedIdentity,
            IManagedExecutableLaunchLease ownedLease,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.False(state.LeaseActive);
            Assert.Equal(Bootstrap, expectedIdentity);
            Assert.True(ownedLease.TryValidateForStart());
            ReceivedLease = ownedLease;
            state.Events.Add("handoff-owned-lease");
            StartAction?.Invoke();
            StartCount++;
            if (StartIssue == ImmutableBootstrapStartIssue.None)
            {
                LastLaunch = new SetupBootstrapLaunch(
                        state,
                        ownedLease,
                        AdmissionOutcome,
                        AdmissionExitCode,
                        AdmissionExitIssue,
                        CompletionOutcome,
                        CompletionExitCode,
                        CompletionExitIssue,
                        AdmissionAction,
                        CompletionAction,
                        AdmissionBudgetObserved,
                        IgnoreAdmissionCancellation);
                return ValueTask.FromResult(new ImmutableBootstrapStartResult(
                    LastLaunch,
                    ImmutableBootstrapStartIssue.None));
            }
            ownedLease.Dispose();
            return ValueTask.FromResult(new ImmutableBootstrapStartResult(null, StartIssue));
        }
    }

    internal sealed class SetupBootstrapLaunch(
        SequencedStateStore state,
        IManagedExecutableLaunchLease ownedLease,
        ImmutableBootstrapAdmissionOutcome admissionOutcome,
        int? admissionExitCode,
        ImmutableBootstrapExitIssue admissionExitIssue,
        ImmutableBootstrapCompletionOutcome completionOutcome,
        int? completionExitCode,
        ImmutableBootstrapExitIssue completionExitIssue,
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
            state.Events.Add("admitted");
            return ValueTask.FromResult(new ImmutableBootstrapAdmissionResult(
                admissionOutcome,
                admissionExitCode,
                admissionExitIssue));
        }

        public ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            completionAction?.Invoke();
            state.Events.Add("ready");
            return ValueTask.FromResult(new ImmutableBootstrapCompletionResult(
                completionOutcome,
                completionExitCode,
                completionExitIssue));
        }

        public void Dispose()
        {
            Disposed = true;
            ownedLease.Dispose();
        }
    }
    internal static async Task<InstallHarness> CreateInstallHarnessAsync(
        ManagedFirstInstallationMaterializationIssue materializationIssue =
            ManagedFirstInstallationMaterializationIssue.None,
        ImmutableBootstrapAdmissionOutcome admissionOutcome =
            ImmutableBootstrapAdmissionOutcome.Admitted,
        VersionManagerStateLoadResult? finalState = null)
    {
        string root = Root($"install-{Guid.NewGuid():N}");
        FreshInstallationCandidate candidate = Candidate();
        SequencedStateStore state = new(
            MissingState(),
            MissingState(),
            finalState ?? BoundState(root, candidate));
        SequencedRootProbe roots = new(
            ManagedInstallationRootStatus.Absent,
            ManagedInstallationRootStatus.Absent);
        RecordingPayloadSource payload = new(PayloadIdentity());
        RecordingCandidateSource source = new(candidate);
        RecordingRootMaterializer materializer = new(state) { Issue = materializationIssue };
        SetupBootstrapHandoff handoff = new(state) { AdmissionOutcome = admissionOutcome };
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
        return new(
            experience,
            Assert.IsType<ManagedFirstInstallationPlan>(prepared.Plan),
            state,
            roots,
            payload,
            source,
            materializer,
            handoff);
    }

    internal sealed record InstallHarness(
        ManagedFirstInstallationExperience Experience,
        ManagedFirstInstallationPlan Plan,
        SequencedStateStore State,
        SequencedRootProbe Roots,
        RecordingPayloadSource Payload,
        RecordingCandidateSource CandidateSource,
        RecordingRootMaterializer Materializer,
        SetupBootstrapHandoff Handoff);

    internal sealed class CancellingBootstrapHandoff(
        SequencedStateStore state,
        CancellationTokenSource cancellation,
        bool duringAdmission) : IImmutableBootstrapLeaseHandoff
    {
        internal CancellingBootstrapLaunch Launch { get; } = new(cancellation, duringAdmission);

        public ValueTask<ImmutableBootstrapStartResult> StartAsync(
            string managedRoot,
            ManagedImmutableBootstrapIdentity expectedIdentity,
            IManagedExecutableLaunchLease ownedLease,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.False(state.LeaseActive);
            return ValueTask.FromResult(StartResultOwningLease(Launch, ownedLease));
        }
    }

    internal sealed class CancellingBootstrapLaunch(
        CancellationTokenSource cancellation,
        bool duringAdmission) : IImmutableBootstrapLaunch
    {
        internal bool CleanupReturned { get; private set; }
        internal bool Disposed { get; private set; }

        public ValueTask<ImmutableBootstrapAdmissionResult> WaitForAdmissionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            return duringAdmission
                ? CancelAdmission()
                : ValueTask.FromResult(new ImmutableBootstrapAdmissionResult(
                    ImmutableBootstrapAdmissionOutcome.Admitted));
        }

        public ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            return CancelCompletion();
        }

        public void Dispose()
        {
            Disposed = true;
        }

        private ValueTask<ImmutableBootstrapAdmissionResult> CancelAdmission()
        {
            cancellation.Cancel();
            CleanupReturned = true;
            return ValueTask.FromException<ImmutableBootstrapAdmissionResult>(
                new OperationCanceledException(cancellation.Token));
        }

        private ValueTask<ImmutableBootstrapCompletionResult> CancelCompletion()
        {
            cancellation.Cancel();
            CleanupReturned = true;
            return ValueTask.FromException<ImmutableBootstrapCompletionResult>(
                new OperationCanceledException(cancellation.Token));
        }
    }

    internal sealed class TypedFailureCancellingHandoff(
        SequencedStateStore state,
        CancellationTokenSource cancellation) : IImmutableBootstrapLeaseHandoff
    {
        internal TypedFailureCancellingLaunch Launch { get; } = new(cancellation);

        public ValueTask<ImmutableBootstrapStartResult> StartAsync(
            string managedRoot,
            ManagedImmutableBootstrapIdentity expectedIdentity,
            IManagedExecutableLaunchLease ownedLease,
            CancellationToken cancellationToken)
        {
            Assert.False(state.LeaseActive);
            return ValueTask.FromResult(StartResultOwningLease(Launch, ownedLease));
        }
    }

    internal sealed class TypedFailureCancellingLaunch(CancellationTokenSource cancellation)
        : IImmutableBootstrapLaunch
    {
        internal bool Disposed { get; private set; }

        public ValueTask<ImmutableBootstrapAdmissionResult> WaitForAdmissionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(new ImmutableBootstrapAdmissionResult(
                ImmutableBootstrapAdmissionOutcome.HealthUnavailable));
        }

        public ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Completion must not run after caller cancellation.");
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    internal sealed class CancellingStartBootstrapHandoff(
        SequencedStateStore state,
        CancellationTokenSource cancellation) : IImmutableBootstrapLeaseHandoff
    {
        internal TrackingBootstrapLaunch Launch { get; } = new();

        public ValueTask<ImmutableBootstrapStartResult> StartAsync(
            string managedRoot,
            ManagedImmutableBootstrapIdentity expectedIdentity,
            IManagedExecutableLaunchLease ownedLease,
            CancellationToken cancellationToken)
        {
            Assert.False(state.LeaseActive);
            cancellation.Cancel();
            return ValueTask.FromResult(StartResultOwningLease(Launch, ownedLease));
        }
    }

    internal sealed class MalformedStartBootstrapHandoff(
        SequencedStateStore state,
        bool attachLaunch,
        ImmutableBootstrapStartIssue issue,
        CancellationTokenSource? cancellation = null) : IImmutableBootstrapLeaseHandoff
    {
        internal TrackingBootstrapLaunch Launch { get; } = new();

        public ValueTask<ImmutableBootstrapStartResult> StartAsync(
            string managedRoot,
            ManagedImmutableBootstrapIdentity expectedIdentity,
            IManagedExecutableLaunchLease ownedLease,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.False(state.LeaseActive);
            cancellation?.Cancel();
            if (attachLaunch)
            {
                return ValueTask.FromResult(StartResultOwningLease(Launch, ownedLease, issue));
            }
            ownedLease.Dispose();
            return ValueTask.FromResult(new ImmutableBootstrapStartResult(null, issue));
        }
    }

    internal sealed class TrackingBootstrapLaunch : IImmutableBootstrapLaunch
    {
        internal int AdmissionWaitCount { get; private set; }

        internal bool Disposed { get; private set; }

        public ValueTask<ImmutableBootstrapAdmissionResult> WaitForAdmissionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            AdmissionWaitCount++;
            Assert.True(cancellationToken.IsCancellationRequested);
            return ValueTask.FromResult(new ImmutableBootstrapAdmissionResult(
                ImmutableBootstrapAdmissionOutcome.HealthUnavailable));
        }

        public ValueTask<ImmutableBootstrapCompletionResult> WaitForCompletionAsync(
            ImmutableBootstrapWaitBudget budget,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Cancellation must win before completion.");
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

}
