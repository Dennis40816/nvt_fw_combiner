using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Single Application owner for genuinely-absent first installation.</summary>
public sealed class ManagedFirstInstallationExperience
{
    /// <summary>Bounded wait for the canonical cross-process state writer.</summary>
    public static readonly TimeSpan WriterLeaseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Bounded first-install admission wait, including initial state seeding and package inventory.
    /// </summary>
    public static readonly TimeSpan DefaultAdmissionOperationCutoff = TimeSpan.FromSeconds(30);

    private readonly IFreshInstallationCandidateSource _candidateSource;
    private readonly TimeSpan _admissionOperationCutoff;
    private readonly TimeSpan _completionOperationCutoff;
    private readonly IImmutableBootstrapLeaseHandoff _handoff;
    private readonly IManagedDistributionPayloadSource _payloadSource;
    private readonly IManagedInstallationRootProbe _rootProbe;
    private readonly IManagedFirstInstallationRootMaterializer _rootMaterializer;
    private readonly string _statePathIdentity;
    private readonly IVersionManagerStateStore _stateStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the one first-install orchestration owner.</summary>
    public ManagedFirstInstallationExperience(
        string statePathIdentity,
        IVersionManagerStateStore stateStore,
        IManagedInstallationRootProbe rootProbe,
        IManagedDistributionPayloadSource payloadSource,
        IFreshInstallationCandidateSource candidateSource,
        IManagedFirstInstallationRootMaterializer rootMaterializer,
        IImmutableBootstrapLeaseHandoff handoff,
        TimeSpan? admissionOperationCutoff = null,
        TimeSpan? completionOperationCutoff = null,
        TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(statePathIdentity) ||
            !Path.IsPathFullyQualified(statePathIdentity))
        {
            throw new ArgumentException("State path identity must be absolute.", nameof(statePathIdentity));
        }
        _statePathIdentity = Path.GetFullPath(statePathIdentity);
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _rootProbe = rootProbe ?? throw new ArgumentNullException(nameof(rootProbe));
        _payloadSource = payloadSource ?? throw new ArgumentNullException(nameof(payloadSource));
        _candidateSource = candidateSource ?? throw new ArgumentNullException(nameof(candidateSource));
        _rootMaterializer = rootMaterializer ?? throw new ArgumentNullException(nameof(rootMaterializer));
        _handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
        _admissionOperationCutoff = admissionOperationCutoff ?? DefaultAdmissionOperationCutoff;
        _completionOperationCutoff = completionOperationCutoff ??
            ManagedLauncherEntryCoordinator.DefaultCompletionOperationCutoff;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_admissionOperationCutoff, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_completionOperationCutoff, TimeSpan.Zero);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Builds one immutable plan without persistent mutation.</summary>
    public async ValueTask<ManagedFirstInstallationPlanResult> PrepareAsync(
        string managedRoot,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return PlanFailure(ManagedFirstInstallationOutcome.Cancelled);
        }
        try
        {
            return await PrepareCoreAsync(managedRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PlanFailure(ManagedFirstInstallationOutcome.Cancelled);
        }
    }

    private async ValueTask<ManagedFirstInstallationPlanResult> PrepareCoreAsync(
        string managedRoot,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeRoot(managedRoot, out string root))
        {
            return PlanFailure(ManagedFirstInstallationOutcome.InvalidDestination);
        }

        VersionManagerStateLoadResult state = await _stateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (state.Issue != VersionManagerStateLoadIssue.Missing)
        {
            return PlanFailure(MapNonMissingState(state));
        }

        ManagedInstallationRootObservation observed = await _rootProbe.ObserveAsync(
            root,
            cancellationToken).ConfigureAwait(false);
        if (observed.Status != ManagedInstallationRootStatus.Absent)
        {
            return PlanFailure(MapRootStatus(observed.Status));
        }

        ManagedFirstInstallationMaterializationIssue destination = await _rootMaterializer
            .AdmitDestinationAsync(root, cancellationToken).ConfigureAwait(false);
        if (destination != ManagedFirstInstallationMaterializationIssue.None)
        {
            return PlanFailure(MapDestinationAdmissionIssue(destination));
        }

        ManagedDistributionPayloadInspectionResult payload = await _payloadSource
            .InspectAsync(cancellationToken).ConfigureAwait(false);
        if (!payload.IsSuccess)
        {
            return PlanFailure(MapPayloadIssue(payload.Issue));
        }

        FreshInstallationCandidateResult candidate = await _candidateSource
            .InspectFreshInstallationAsync(cancellationToken).ConfigureAwait(false);
        return candidate.IsSuccess
            ? new(
                new ManagedFirstInstallationPlan(
                    root,
                    _statePathIdentity,
                    payload.Identity!,
                    candidate.Candidate!),
                ManagedFirstInstallationOutcome.ReadyToInstall)
            : PlanFailure(MapCandidateIssue(candidate.Issue));
    }

    /// <summary>Revalidates and executes one exact user-reviewed first-install plan.</summary>
    public async ValueTask<ManagedFirstInstallationResult> InstallAndLaunchAsync(
        ManagedFirstInstallationPlan plan,
        CancellationToken cancellationToken,
        IProgress<ManagedFirstInstallationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (cancellationToken.IsCancellationRequested)
        {
            return Result(plan, ManagedFirstInstallationOutcome.Cancelled);
        }
        try
        {
            IProgress<ManagedFirstInstallationProgress>? safeProgress = progress is null
                ? null
                : new NonThrowingProgress(progress);
            return await InstallAndLaunchCoreAsync(plan, safeProgress, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(plan, ManagedFirstInstallationOutcome.Cancelled);
        }
    }

    private async ValueTask<ManagedFirstInstallationResult> InstallAndLaunchCoreAsync(
        ManagedFirstInstallationPlan plan,
        IProgress<ManagedFirstInstallationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(plan.StatePathIdentity, _statePathIdentity, StringComparison.Ordinal))
        {
            return Result(plan, ManagedFirstInstallationOutcome.RecoveryRequired);
        }

        IManagedPromotedFirstInstallation? promoted = null;
        IManagedExecutableLaunchLease? bootstrapLease = null;
        CancellationTokenSource? admissionDeadline = null;
        CancellationTokenSource? admissionLinked = null;
        long admissionStarted = 0;
        try
        {
            using (VersionManagerWriteLeaseResult lease = await _stateStore.TryAcquireWriteLeaseAsync(
                WriterLeaseTimeout,
                cancellationToken).ConfigureAwait(false))
            {
                if (!lease.IsAcquired)
                {
                    return Result(
                        plan,
                        lease.Issue == VersionManagerWriteLeaseIssue.Busy
                            ? ManagedFirstInstallationOutcome.Busy
                            : ManagedFirstInstallationOutcome.StateUnavailable);
                }

                VersionManagerStateLoadResult state = await _stateStore.LoadAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (state.Issue != VersionManagerStateLoadIssue.Missing)
                {
                    return Result(plan, MapNonMissingState(state));
                }

                ManagedInstallationRootObservation root = await _rootProbe.ObserveAsync(
                    plan.ManagedRoot,
                    cancellationToken).ConfigureAwait(false);
                if (root.Status != ManagedInstallationRootStatus.Absent)
                {
                    return Result(plan, MapRootStatus(root.Status));
                }

                ManagedFirstInstallationMaterializationIssue destination = await _rootMaterializer
                    .AdmitDestinationAsync(plan.ManagedRoot, cancellationToken).ConfigureAwait(false);
                if (destination != ManagedFirstInstallationMaterializationIssue.None)
                {
                    return Result(plan, MapDestinationAdmissionIssue(destination));
                }

                ManagedDistributionPayloadCaptureResult captured = await _payloadSource
                    .CaptureExactAsync(plan.Payload, cancellationToken).ConfigureAwait(false);
                if (!captured.IsSuccess || captured.Capture!.Identity != plan.Payload)
                {
                    captured.Capture?.Dispose();
                    return Result(plan, MapPayloadIssue(captured.Issue));
                }
                using IManagedDistributionPayloadCapture payload = captured.Capture;

                progress?.Report(ManagedFirstInstallationProgress.Indeterminate(
                    ManagedFirstInstallationProgressStage.RevalidatingSource));
                FreshInstallationCandidateResult reverified = await _candidateSource
                    .ReverifyFreshInstallationAsync(plan.Candidate, cancellationToken)
                    .ConfigureAwait(false);
                if (!reverified.IsSuccess || reverified.Candidate!.Identity != plan.Candidate.Identity)
                {
                    return Result(plan, MapCandidateIssue(reverified.Issue));
                }

                ManagedVersionAdmission admission = CreateAdmission(reverified.Candidate);
                VersionManagerState seed = ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission);
                ManagedFirstInstallationMaterializationResult materialized = await _rootMaterializer
                    .MaterializeAsync(
                        plan.ManagedRoot,
                        _statePathIdentity,
                        payload,
                        reverified.Candidate,
                        seed,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!materialized.IsSuccess)
                {
                    materialized.Installation?.Dispose();
                    return Result(plan, MapMaterializationIssue(materialized.Issue));
                }
                promoted = materialized.Installation!;
                if (!ManagedRootPathIdentity.Equals(promoted.ManagedRoot, plan.ManagedRoot) ||
                    promoted.Admission != admission)
                {
                    return Result(plan, ManagedFirstInstallationOutcome.RecoveryRequired);
                }

                progress?.Report(ManagedFirstInstallationProgress.Indeterminate(
                    ManagedFirstInstallationProgressStage.FinalizingInstallation));
                ManagedFirstInstallationTransactionIssue recorded = await promoted
                    .RecordBootstrapLaunchAsync(cancellationToken).ConfigureAwait(false);
                if (recorded != ManagedFirstInstallationTransactionIssue.None)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.RecoveryRequired,
                        ManagedFirstInstallationLaunchStage.PostPromotion,
                        MapTransactionFailure(recorded));
                }

                admissionStarted = _timeProvider.GetTimestamp();
                admissionDeadline = new CancellationTokenSource(
                    _admissionOperationCutoff,
                    _timeProvider);
                admissionLinked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    admissionDeadline.Token);
                ManagedExecutableLaunchLeaseResult acquired;
                try
                {
                    acquired = await promoted.AcquireBootstrapLaunchLeaseAsync(
                        plan.Payload.Bootstrap,
                        admissionLinked.Token).ConfigureAwait(false);
                    // Take ownership before cancellation/deadline checks; the adapter may
                    // create a valid lease and cancel the caller before this resumes.
                    bootstrapLease = acquired.Lease;
                }
                catch (OperationCanceledException) when (
                    admissionDeadline.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.BootstrapStart,
                        ManagedFirstInstallationLaunchIssue.TimedOut);
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (_timeProvider.GetElapsedTime(admissionStarted) >= _admissionOperationCutoff)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.BootstrapStart,
                        ManagedFirstInstallationLaunchIssue.TimedOut);
                }
                if (!acquired.IsAcquired)
                {
                    ImmutableBootstrapStartIssue issue = MapBootstrapLeaseFailure(acquired);
                    return LaunchFailure(
                        plan,
                        MapBootstrapStartIssue(issue),
                        ManagedFirstInstallationLaunchStage.BootstrapStart,
                        MapBootstrapStartFailure(issue));
                }
            }

            progress?.Report(ManagedFirstInstallationProgress.Indeterminate(
                ManagedFirstInstallationProgressStage.StartingApplication));
            CancellationTokenSource activeAdmissionDeadline = admissionDeadline ??
                throw new InvalidOperationException("Bootstrap admission deadline was not created.");
            CancellationTokenSource activeAdmissionLinked = admissionLinked ??
                throw new InvalidOperationException("Bootstrap admission token was not created.");
            ImmutableBootstrapStartResult started;
            try
            {
                IManagedExecutableLaunchLease ownedLease = bootstrapLease ??
                    throw new InvalidOperationException(
                        "Promoted Bootstrap launch custody was not acquired.");
                bootstrapLease = null;
                started = await _handoff.StartAsync(
                    plan.ManagedRoot,
                    plan.Payload.Bootstrap,
                    ownedLease,
                    activeAdmissionLinked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                activeAdmissionDeadline.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                return LaunchFailure(
                    plan,
                    ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                    ManagedFirstInstallationLaunchStage.BootstrapStart,
                    ManagedFirstInstallationLaunchIssue.TimedOut);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return LaunchFailure(
                    plan,
                    ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                    ManagedFirstInstallationLaunchStage.BootstrapStart,
                    ManagedFirstInstallationLaunchIssue.Cancelled);
            }
            ImmutableBootstrapCompletionResult completion;
            IImmutableBootstrapLaunch? launch = started.Launch;
            try
            {
                if (!started.HasValidShape)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.BootstrapStart,
                        ManagedFirstInstallationLaunchIssue.InvalidReceipt);
                }
                if (!started.IsStarted)
                {
                    return LaunchFailure(
                        plan,
                        cancellationToken.IsCancellationRequested
                            ? ManagedFirstInstallationOutcome.InstalledButLaunchFailed
                            : MapBootstrapStartIssue(started.Issue),
                        ManagedFirstInstallationLaunchStage.BootstrapStart,
                        cancellationToken.IsCancellationRequested
                            ? ManagedFirstInstallationLaunchIssue.Cancelled
                            : MapBootstrapStartFailure(started.Issue));
                }
                IImmutableBootstrapLaunch admittedLaunch = launch!;

                ImmutableBootstrapAdmissionResult admission;
                try
                {
                    admission = await admittedLaunch
                        .WaitForAdmissionAsync(
                            RemainingBudget(
                                admissionStarted,
                                _admissionOperationCutoff,
                                ManagedLauncherEntryCoordinator.DefaultCleanupObservationBudget),
                            activeAdmissionLinked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    activeAdmissionDeadline.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.LauncherAdmission,
                        ManagedFirstInstallationLaunchIssue.TimedOut);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.LauncherAdmission,
                        ManagedFirstInstallationLaunchIssue.Cancelled);
                }
                if (!admission.HasValidShape)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.LauncherAdmission,
                        ManagedFirstInstallationLaunchIssue.InvalidReceipt,
                        admission.ExitCode);
                }
                if (admission.Outcome == ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.LauncherAdmission,
                        ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed,
                        admission.ExitCode);
                }
                if (_timeProvider.GetElapsedTime(admissionStarted) >= _admissionOperationCutoff)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.LauncherAdmission,
                        ManagedFirstInstallationLaunchIssue.TimedOut,
                        exitCode: null);
                }
                if (admission.Outcome != ImmutableBootstrapAdmissionOutcome.Admitted)
                {
                    return LaunchFailure(
                        plan,
                        cancellationToken.IsCancellationRequested
                            ? ManagedFirstInstallationOutcome.InstalledButLaunchFailed
                            : MapBootstrapAdmissionIssue(admission.Outcome),
                        ManagedFirstInstallationLaunchStage.LauncherAdmission,
                        cancellationToken.IsCancellationRequested
                            ? ManagedFirstInstallationLaunchIssue.Cancelled
                            : MapBootstrapAdmissionFailure(admission),
                        cancellationToken.IsCancellationRequested ? null : admission.ExitCode);
                }

                long completionStarted = _timeProvider.GetTimestamp();
                using var completionDeadline = new CancellationTokenSource(
                    _completionOperationCutoff,
                    _timeProvider);
                using var completionLinked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    completionDeadline.Token);
                try
                {
                    completion = await admittedLaunch.WaitForCompletionAsync(
                        RemainingBudget(
                            completionStarted,
                            _completionOperationCutoff,
                            ManagedLauncherEntryCoordinator.DefaultCleanupObservationBudget),
                        completionLinked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    completionDeadline.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.ApplicationReady,
                        ManagedFirstInstallationLaunchIssue.TimedOut);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.ApplicationReady,
                        ManagedFirstInstallationLaunchIssue.Cancelled);
                }
                if (!completion.HasValidShape)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.ApplicationReady,
                        ManagedFirstInstallationLaunchIssue.InvalidReceipt,
                        completion.ExitCode);
                }
                if (completion.Outcome == ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.ApplicationReady,
                        ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed,
                        completion.ExitCode);
                }
                if (_timeProvider.GetElapsedTime(completionStarted) >= _completionOperationCutoff ||
                    completion.Outcome != ImmutableBootstrapCompletionOutcome.Ready)
                {
                    return LaunchFailure(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                        ManagedFirstInstallationLaunchStage.ApplicationReady,
                        _timeProvider.GetElapsedTime(completionStarted) >= _completionOperationCutoff
                            ? ManagedFirstInstallationLaunchIssue.TimedOut
                            : cancellationToken.IsCancellationRequested
                                ? ManagedFirstInstallationLaunchIssue.Cancelled
                                : MapBootstrapCompletionFailure(completion),
                        _timeProvider.GetElapsedTime(completionStarted) >= _completionOperationCutoff ||
                            cancellationToken.IsCancellationRequested
                                ? null
                                : completion.ExitCode);
                }
            }
            finally
            {
                launch?.Dispose();
            }
            using var finalizationDeadline = new CancellationTokenSource(
                WriterLeaseTimeout,
                _timeProvider);
            try
            {
                using VersionManagerWriteLeaseResult finalizeLease = await _stateStore
                    .TryAcquireWriteLeaseAsync(
                        WriterLeaseTimeout,
                        finalizationDeadline.Token).ConfigureAwait(false);
                if (!finalizeLease.IsAcquired)
                {
                    return Result(plan, ManagedFirstInstallationOutcome.RecoveryRequired);
                }

                VersionManagerStateLoadResult bound = await _stateStore
                    .LoadAsync(finalizationDeadline.Token).ConfigureAwait(false);
                if (!bound.IsSuccess ||
                    !ManagedVersionSeedPolicy.IsCanonicalBoundFirstRunState(
                        bound.State!,
                        plan.ManagedRoot,
                        promoted.Admission))
                {
                    return Result(plan, ManagedFirstInstallationOutcome.RecoveryRequired);
                }

                ManagedFirstInstallationTransactionIssue completed = await promoted
                    .CompleteAsync(finalizationDeadline.Token).ConfigureAwait(false);
                return completed == ManagedFirstInstallationTransactionIssue.None
                    ? Result(plan, ManagedFirstInstallationOutcome.Completed)
                    : Result(plan, ManagedFirstInstallationOutcome.RecoveryRequired);
            }
            catch (OperationCanceledException) when (finalizationDeadline.IsCancellationRequested)
            {
                return Result(plan, ManagedFirstInstallationOutcome.RecoveryRequired);
            }
        }
        catch (OperationCanceledException) when (
            promoted is not null && cancellationToken.IsCancellationRequested)
        {
            return LaunchFailure(
                plan,
                ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
                ManagedFirstInstallationLaunchStage.PostPromotion,
                ManagedFirstInstallationLaunchIssue.Cancelled);
        }
        finally
        {
            admissionLinked?.Dispose();
            admissionDeadline?.Dispose();
            bootstrapLease?.Dispose();
            promoted?.Dispose();
        }
    }

    private static ManagedVersionAdmission CreateAdmission(FreshInstallationCandidate candidate)
    {
        return new(
            candidate.Package.Version,
            candidate.Package.Identity,
            candidate.Package.ReleaseManifestSha256);
    }

    private static ManagedFirstInstallationOutcome MapCandidateIssue(
        FreshInstallationCandidateIssue issue)
    {
        return issue switch
        {
            FreshInstallationCandidateIssue.RegistryNotConfigured or
            FreshInstallationCandidateIssue.SourceUnavailable =>
                ManagedFirstInstallationOutcome.SourceUnavailable,
            FreshInstallationCandidateIssue.SourceRejected =>
                ManagedFirstInstallationOutcome.SourceRejected,
            FreshInstallationCandidateIssue.CandidateUnavailable =>
                ManagedFirstInstallationOutcome.CandidateUnavailable,
            FreshInstallationCandidateIssue.SourceChanged =>
                ManagedFirstInstallationOutcome.SourceChanged,
            FreshInstallationCandidateIssue.None =>
                ManagedFirstInstallationOutcome.SourceChanged,
            _ => throw new InvalidOperationException("Candidate source returned an undefined issue."),
        };
    }

    private static ManagedFirstInstallationOutcome MapMaterializationIssue(
        ManagedFirstInstallationMaterializationIssue issue)
    {
        return issue switch
        {
            ManagedFirstInstallationMaterializationIssue.InvalidDestination =>
                ManagedFirstInstallationOutcome.InvalidDestination,
            ManagedFirstInstallationMaterializationIssue.PermissionDenied =>
                ManagedFirstInstallationOutcome.PermissionDenied,
            ManagedFirstInstallationMaterializationIssue.SourceUnavailable =>
                ManagedFirstInstallationOutcome.SourceUnavailable,
            ManagedFirstInstallationMaterializationIssue.SourceChanged =>
                ManagedFirstInstallationOutcome.SourceChanged,
            ManagedFirstInstallationMaterializationIssue.PromotionFailed or
            ManagedFirstInstallationMaterializationIssue.StateUnavailable =>
                ManagedFirstInstallationOutcome.StateUnavailable,
            ManagedFirstInstallationMaterializationIssue.RecoveryRequired =>
                ManagedFirstInstallationOutcome.RecoveryRequired,
            ManagedFirstInstallationMaterializationIssue.None =>
                ManagedFirstInstallationOutcome.StateUnavailable,
            _ => throw new InvalidOperationException("Materializer returned an undefined issue."),
        };
    }

    private static ManagedFirstInstallationOutcome MapBootstrapStartIssue(
        ImmutableBootstrapStartIssue issue)
    {
        return issue switch
        {
            ImmutableBootstrapStartIssue.Busy =>
                ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
            ImmutableBootstrapStartIssue.Damaged => ManagedFirstInstallationOutcome.RecoveryRequired,
            ImmutableBootstrapStartIssue.StartFailed or
            ImmutableBootstrapStartIssue.Unavailable =>
                ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
            ImmutableBootstrapStartIssue.None => throw new InvalidOperationException(
                "A successful Bootstrap start did not return its launch receipt."),
            _ => throw new InvalidOperationException("Bootstrap start returned an undefined issue."),
        };
    }

    private static ImmutableBootstrapStartIssue MapBootstrapLeaseFailure(
        ManagedExecutableLaunchLeaseResult acquired)
    {
        return acquired.Lease is not null || !Enum.IsDefined(acquired.Issue) ||
            acquired.Issue == ManagedExecutableLaunchIssue.None
                ? ImmutableBootstrapStartIssue.Damaged
                : acquired.Issue switch
                {
                    ManagedExecutableLaunchIssue.Tampered or
                    ManagedExecutableLaunchIssue.UnsafePath =>
                        ImmutableBootstrapStartIssue.Damaged,
                    ManagedExecutableLaunchIssue.Unavailable =>
                        ImmutableBootstrapStartIssue.Unavailable,
                    ManagedExecutableLaunchIssue.None => throw new InvalidOperationException(
                        "A successful Bootstrap launch lease was mapped as a failure."),
                    _ => throw new InvalidOperationException(
                        "Bootstrap launch lease returned an undefined issue."),
                };
    }

    private static ManagedFirstInstallationLaunchIssue MapBootstrapStartFailure(
        ImmutableBootstrapStartIssue issue)
    {
        return issue switch
        {
            ImmutableBootstrapStartIssue.Busy => ManagedFirstInstallationLaunchIssue.Busy,
            ImmutableBootstrapStartIssue.Damaged => ManagedFirstInstallationLaunchIssue.Damaged,
            ImmutableBootstrapStartIssue.StartFailed => ManagedFirstInstallationLaunchIssue.StartFailed,
            ImmutableBootstrapStartIssue.Unavailable => ManagedFirstInstallationLaunchIssue.Unavailable,
            ImmutableBootstrapStartIssue.None => throw new InvalidOperationException(
                "A successful Bootstrap start was mapped as a launch failure."),
            _ => throw new InvalidOperationException("Bootstrap start returned an undefined issue."),
        };
    }

    private static ManagedFirstInstallationOutcome MapBootstrapAdmissionIssue(
        ImmutableBootstrapAdmissionOutcome outcome)
    {
        return outcome switch
        {
            ImmutableBootstrapAdmissionOutcome.RecoveryRequired =>
                ManagedFirstInstallationOutcome.RecoveryRequired,
            ImmutableBootstrapAdmissionOutcome.Busy or
            ImmutableBootstrapAdmissionOutcome.LaunchFailed or
            ImmutableBootstrapAdmissionOutcome.HealthUnavailable or
            ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed =>
                ManagedFirstInstallationOutcome.InstalledButLaunchFailed,
            ImmutableBootstrapAdmissionOutcome.Admitted => throw new InvalidOperationException(
                "An admitted Bootstrap was handled as an admission failure."),
            _ => throw new InvalidOperationException(
                "Bootstrap admission returned an undefined outcome."),
        };
    }

    private static ManagedFirstInstallationLaunchIssue MapBootstrapAdmissionFailure(
        ImmutableBootstrapAdmissionResult admission)
    {
        return admission.ExitIssue != ImmutableBootstrapExitIssue.None
            ? ManagedFirstInstallationLaunchFailure.MapBootstrapExitIssue(admission.ExitIssue)
            : admission.Outcome switch
            {
                ImmutableBootstrapAdmissionOutcome.Busy => ManagedFirstInstallationLaunchIssue.Busy,
                ImmutableBootstrapAdmissionOutcome.RecoveryRequired =>
                    ManagedFirstInstallationLaunchIssue.RecoveryRequired,
                ImmutableBootstrapAdmissionOutcome.LaunchFailed =>
                    ManagedFirstInstallationLaunchIssue.LaunchFailed,
                ImmutableBootstrapAdmissionOutcome.HealthUnavailable =>
                    ManagedFirstInstallationLaunchIssue.HealthUnavailable,
                ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed =>
                    ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed,
                ImmutableBootstrapAdmissionOutcome.Admitted => throw new InvalidOperationException(
                    "An admitted Bootstrap was mapped as an admission failure."),
                _ => throw new InvalidOperationException(
                    "Bootstrap admission returned an undefined outcome."),
            };
    }

    private static ManagedFirstInstallationLaunchIssue MapBootstrapCompletionFailure(
        ImmutableBootstrapCompletionResult completion)
    {
        return completion.ExitIssue != ImmutableBootstrapExitIssue.None
            ? ManagedFirstInstallationLaunchFailure.MapBootstrapExitIssue(completion.ExitIssue)
            : completion.Outcome switch
            {
                ImmutableBootstrapCompletionOutcome.RolledBack =>
                    ManagedFirstInstallationLaunchIssue.RolledBack,
                ImmutableBootstrapCompletionOutcome.Failed => throw new InvalidOperationException(
                    "A valid failed Bootstrap completion requires one typed exit issue."),
                ImmutableBootstrapCompletionOutcome.Unavailable =>
                    ManagedFirstInstallationLaunchIssue.Unavailable,
                ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed =>
                    ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed,
                ImmutableBootstrapCompletionOutcome.Ready => throw new InvalidOperationException(
                    "A READY Bootstrap was mapped as a completion failure."),
                _ => throw new InvalidOperationException(
                    "Bootstrap completion returned an undefined outcome."),
            };
    }

    private ImmutableBootstrapWaitBudget RemainingBudget(
        long started,
        TimeSpan operationCutoff,
        TimeSpan cleanupBudget)
    {
        TimeSpan elapsed = _timeProvider.GetElapsedTime(started);
        TimeSpan operationRemaining = elapsed >= operationCutoff
            ? TimeSpan.Zero
            : operationCutoff - elapsed;
        TimeSpan totalCutoff = operationCutoff + cleanupBudget;
        TimeSpan totalRemaining = elapsed >= totalCutoff
            ? TimeSpan.Zero
            : totalCutoff - elapsed;
        return new(operationRemaining, totalRemaining);
    }

    private static ManagedFirstInstallationOutcome MapDestinationAdmissionIssue(
        ManagedFirstInstallationMaterializationIssue issue)
    {
        return issue switch
        {
            ManagedFirstInstallationMaterializationIssue.InvalidDestination =>
                ManagedFirstInstallationOutcome.InvalidDestination,
            ManagedFirstInstallationMaterializationIssue.PermissionDenied =>
                ManagedFirstInstallationOutcome.PermissionDenied,
            ManagedFirstInstallationMaterializationIssue.StateUnavailable =>
                ManagedFirstInstallationOutcome.StateUnavailable,
            ManagedFirstInstallationMaterializationIssue.RecoveryRequired =>
                ManagedFirstInstallationOutcome.RecoveryRequired,
            ManagedFirstInstallationMaterializationIssue.None =>
                throw new InvalidOperationException("Successful destination admission was mapped as failure."),
            ManagedFirstInstallationMaterializationIssue.SourceUnavailable or
            ManagedFirstInstallationMaterializationIssue.SourceChanged or
            ManagedFirstInstallationMaterializationIssue.PromotionFailed =>
                throw new InvalidOperationException(
                    "Destination admission returned an issue outside its closed contract."),
            _ => throw new InvalidOperationException(
                "Destination admission returned an undefined issue."),
        };
    }

    private static ManagedFirstInstallationOutcome MapNonMissingState(
        VersionManagerStateLoadResult state)
    {
        return state.Issue == VersionManagerStateLoadIssue.Unavailable
            ? ManagedFirstInstallationOutcome.StateUnavailable
            : ManagedFirstInstallationOutcome.RecoveryRequired;
    }

    private static ManagedFirstInstallationOutcome MapPayloadIssue(
        ManagedDistributionPayloadIssue issue)
    {
        return issue switch
        {
            ManagedDistributionPayloadIssue.Unavailable =>
                ManagedFirstInstallationOutcome.PayloadUnavailable,
            ManagedDistributionPayloadIssue.Invalid =>
                ManagedFirstInstallationOutcome.PayloadInvalid,
            ManagedDistributionPayloadIssue.Changed =>
                ManagedFirstInstallationOutcome.SourceChanged,
            ManagedDistributionPayloadIssue.None =>
                ManagedFirstInstallationOutcome.PayloadInvalid,
            _ => throw new InvalidOperationException("Payload source returned an undefined issue."),
        };
    }

    private static ManagedFirstInstallationOutcome MapRootStatus(
        ManagedInstallationRootStatus status)
    {
        return status switch
        {
            ManagedInstallationRootStatus.InvalidDestination =>
                ManagedFirstInstallationOutcome.InvalidDestination,
            ManagedInstallationRootStatus.PermissionDenied =>
                ManagedFirstInstallationOutcome.PermissionDenied,
            ManagedInstallationRootStatus.Unavailable =>
                ManagedFirstInstallationOutcome.StateUnavailable,
            ManagedInstallationRootStatus.Present or
            ManagedInstallationRootStatus.Residue =>
                ManagedFirstInstallationOutcome.RecoveryRequired,
            ManagedInstallationRootStatus.Absent =>
                ManagedFirstInstallationOutcome.RecoveryRequired,
            _ => throw new InvalidOperationException("Root probe returned an undefined status."),
        };
    }

    private static ManagedFirstInstallationPlanResult PlanFailure(
        ManagedFirstInstallationOutcome outcome)
    {
        return new(null, outcome);
    }

    private static ManagedFirstInstallationLaunchIssue MapTransactionFailure(
        ManagedFirstInstallationTransactionIssue issue)
    {
        return issue switch
        {
            ManagedFirstInstallationTransactionIssue.RecoveryRequired =>
                ManagedFirstInstallationLaunchIssue.RecoveryRequired,
            ManagedFirstInstallationTransactionIssue.StateUnavailable =>
                ManagedFirstInstallationLaunchIssue.StateUnavailable,
            ManagedFirstInstallationTransactionIssue.None => throw new InvalidOperationException(
                "A successful transaction was mapped as a launch-terminal failure."),
            _ => throw new InvalidOperationException("Transaction returned an undefined issue."),
        };
    }

    private static ManagedFirstInstallationResult Result(
        ManagedFirstInstallationPlan plan,
        ManagedFirstInstallationOutcome outcome,
        ManagedFirstInstallationLaunchFailure? launchFailure = null)
    {
        return new(outcome, plan.ManagedRoot, plan.Candidate.Package.Version, launchFailure);
    }

    private static ManagedFirstInstallationResult LaunchFailure(
        ManagedFirstInstallationPlan plan,
        ManagedFirstInstallationOutcome outcome,
        ManagedFirstInstallationLaunchStage stage,
        ManagedFirstInstallationLaunchIssue issue,
        int? exitCode = null)
    {
        return stage == ManagedFirstInstallationLaunchStage.None ||
            issue == ManagedFirstInstallationLaunchIssue.None
                ? throw new InvalidOperationException("A launch failure requires an exact stage and issue.")
                : Result(plan, outcome, new(stage, issue, exitCode));
    }

    private static bool TryNormalizeRoot(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
        {
            return false;
        }
        try
        {
            normalized = ManagedRootPathIdentity.Normalize(value);
            return true;
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private sealed class NonThrowingProgress(
        IProgress<ManagedFirstInstallationProgress> inner)
        : IProgress<ManagedFirstInstallationProgress>
    {
        private readonly IProgress<ManagedFirstInstallationProgress> _inner = inner ??
            throw new ArgumentNullException(nameof(inner));

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Presentation progress is explicitly non-authoritative and cannot change installation.")]
        public void Report(ManagedFirstInstallationProgress value)
        {
            try
            {
                _inner.Report(value);
            }
            catch (Exception)
            {
            }
        }
    }
}
