namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Single Application owner for genuinely-absent first installation.</summary>
public sealed class ManagedFirstInstallationExperience
{
    /// <summary>Bounded wait for the canonical cross-process state writer.</summary>
    public static readonly TimeSpan WriterLeaseTimeout = TimeSpan.FromSeconds(5);

    private readonly IFreshInstallationCandidateSource _candidateSource;
    private readonly TimeSpan _admissionOperationCutoff;
    private readonly TimeSpan _completionOperationCutoff;
    private readonly IImmutableBootstrapHandoff _handoff;
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
        IImmutableBootstrapHandoff handoff,
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
        _admissionOperationCutoff = admissionOperationCutoff ??
            ManagedLauncherEntryCoordinator.DefaultAdmissionOperationCutoff;
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (cancellationToken.IsCancellationRequested)
        {
            return Result(plan, ManagedFirstInstallationOutcome.Cancelled);
        }
        try
        {
            return await InstallAndLaunchCoreAsync(plan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(plan, ManagedFirstInstallationOutcome.Cancelled);
        }
    }

    private async ValueTask<ManagedFirstInstallationResult> InstallAndLaunchCoreAsync(
        ManagedFirstInstallationPlan plan,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(plan.StatePathIdentity, _statePathIdentity, StringComparison.Ordinal))
        {
            return Result(plan, ManagedFirstInstallationOutcome.RecoveryRequired);
        }

        IManagedPromotedFirstInstallation? promoted = null;
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

                ManagedFirstInstallationTransactionIssue recorded = await promoted
                    .RecordBootstrapLaunchAsync(cancellationToken).ConfigureAwait(false);
                if (recorded != ManagedFirstInstallationTransactionIssue.None)
                {
                    return Result(plan, MapTransactionIssue(recorded));
                }
            }

            long admissionStarted = _timeProvider.GetTimestamp();
            using var admissionDeadline = new CancellationTokenSource(
                _admissionOperationCutoff,
                _timeProvider);
            using var admissionLinked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                admissionDeadline.Token);
            ImmutableBootstrapStartResult started;
            try
            {
                started = await _handoff.StartAsync(
                    plan.ManagedRoot,
                    plan.Payload.Bootstrap,
                    admissionLinked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                admissionDeadline.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                return Result(
                    plan,
                    ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
            }
            ImmutableBootstrapCompletionResult completion;
            IImmutableBootstrapLaunch? launch = started.Launch;
            try
            {
                if (!started.HasValidShape)
                {
                    return Result(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
                }
                if (!started.IsStarted)
                {
                    return Result(
                        plan,
                        cancellationToken.IsCancellationRequested
                            ? ManagedFirstInstallationOutcome.InstalledButLaunchFailed
                            : MapBootstrapStartIssue(started.Issue));
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
                            admissionLinked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    admissionDeadline.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested)
                {
                    return Result(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
                }
                if (admission.Outcome == ImmutableBootstrapAdmissionOutcome.TerminationUnconfirmed)
                {
                    return Result(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
                }
                if (_timeProvider.GetElapsedTime(admissionStarted) >= _admissionOperationCutoff)
                {
                    return Result(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
                }
                if (admission.Outcome != ImmutableBootstrapAdmissionOutcome.Admitted)
                {
                    return Result(
                        plan,
                        cancellationToken.IsCancellationRequested
                            ? ManagedFirstInstallationOutcome.InstalledButLaunchFailed
                            : MapBootstrapAdmissionIssue(admission.Outcome));
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
                    return Result(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
                }
                if (completion.Outcome == ImmutableBootstrapCompletionOutcome.TerminationUnconfirmed)
                {
                    return Result(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
                }
                if (_timeProvider.GetElapsedTime(completionStarted) >= _completionOperationCutoff ||
                    completion.Outcome != ImmutableBootstrapCompletionOutcome.Ready)
                {
                    return Result(
                        plan,
                        ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
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
                    return Result(
                        plan,
                        finalizeLease.Issue == VersionManagerWriteLeaseIssue.Busy
                            ? ManagedFirstInstallationOutcome.RecoveryRequired
                            : ManagedFirstInstallationOutcome.StateUnavailable);
                }

                VersionManagerStateLoadResult bound = await _stateStore
                    .LoadAsync(finalizationDeadline.Token).ConfigureAwait(false);
                if (!bound.IsSuccess ||
                    !ManagedVersionSeedPolicy.IsCanonicalBoundFirstRunState(
                        bound.State!,
                        plan.ManagedRoot,
                        promoted.Admission))
                {
                    return Result(
                        plan,
                        bound.Issue is VersionManagerStateLoadIssue.Invalid or
                            VersionManagerStateLoadIssue.Missing or
                            VersionManagerStateLoadIssue.ManagedRootMismatch
                                ? ManagedFirstInstallationOutcome.RecoveryRequired
                                : ManagedFirstInstallationOutcome.StateUnavailable);
                }

                ManagedFirstInstallationTransactionIssue completed = await promoted
                    .CompleteAsync(finalizationDeadline.Token).ConfigureAwait(false);
                return Result(
                    plan,
                    completed == ManagedFirstInstallationTransactionIssue.None
                        ? ManagedFirstInstallationOutcome.Completed
                        : MapTransactionIssue(completed));
            }
            catch (OperationCanceledException) when (finalizationDeadline.IsCancellationRequested)
            {
                return Result(plan, ManagedFirstInstallationOutcome.RecoveryRequired);
            }
        }
        catch (OperationCanceledException) when (
            promoted is not null && cancellationToken.IsCancellationRequested)
        {
            return Result(
                plan,
                ManagedFirstInstallationOutcome.InstalledButLaunchFailed);
        }
        finally
        {
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

    private static ManagedFirstInstallationOutcome MapTransactionIssue(
        ManagedFirstInstallationTransactionIssue issue)
    {
        return issue == ManagedFirstInstallationTransactionIssue.RecoveryRequired
            ? ManagedFirstInstallationOutcome.RecoveryRequired
            : ManagedFirstInstallationOutcome.StateUnavailable;
    }

    private static ManagedFirstInstallationPlanResult PlanFailure(
        ManagedFirstInstallationOutcome outcome)
    {
        return new(null, outcome);
    }

    private static ManagedFirstInstallationResult Result(
        ManagedFirstInstallationPlan plan,
        ManagedFirstInstallationOutcome outcome)
    {
        return new(outcome, plan.ManagedRoot, plan.Candidate.Package.Version);
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
}
