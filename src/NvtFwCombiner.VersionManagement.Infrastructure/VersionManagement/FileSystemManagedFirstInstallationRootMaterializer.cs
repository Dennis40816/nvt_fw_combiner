using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Stages one closed first installation and promotes it by one same-volume move.</summary>
public sealed partial class FileSystemManagedFirstInstallationRootMaterializer
    : IManagedFirstInstallationRootMaterializer
{
    /// <summary>The immutable user-facing distribution Launcher in the managed root.</summary>
    public const string DistributionLauncherFileName = "NvtFwCombiner.DistributionLauncher.exe";
    /// <summary>The immutable Root Bootstrap in the managed root.</summary>
    public const string BootstrapFileName = "NvtFwCombiner.Bootstrap.exe";
    /// <summary>The unbound canonical seed imported only by Root Bootstrap.</summary>
    public const string SeedFileName = "version-manager.seed.v1.json";

    private const string RootPromotedPhase = ManagedSetupTransactionCodec.RootPromotedPhase;
    private const string BootstrapLaunchRecordedPhase =
        ManagedSetupTransactionCodec.BootstrapLaunchRecordedPhase;

    private readonly Func<string, CancellationToken, ValueTask>? _stagingCustodyAcquired;
    private readonly Func<string, CancellationToken, ValueTask>? _destinationCustodyAcquired;
    private readonly Func<string, CancellationToken, ValueTask>? _closedRootVerified;
    private readonly Action<string>? _beforeRepositoryStagingDelete;
    private readonly Action<string>? _beforeRepositoryStagingCleanup;
    private readonly Action<int, ManagedSetupStagingCleanupState>?
        _repositoryStagingCleanupAttemptObserved;
    private readonly Func<TimeSpan, CancellationToken, ValueTask>?
        _repositoryStagingCleanupDelay;
    private readonly Func<int, int>? _repositoryStagingDeleteOpenStatusOverride;
    private readonly Func<int, int>? _repositoryOwnedDeletionObservationStatusOverride;
    private readonly Action<string>? _afterPackageDirectoryCreated;
    private readonly Action? _afterMarkerTopologyProof;
    private readonly Action<string>? _afterRootPromotion;
    private readonly IWindowsCustodiedManagedVersionRepository _installer;
    private readonly IManagedVersionRepository _repository;

    /// <summary>Creates the whole-root adapter with the canonical Windows-custodied repository.</summary>
    public FileSystemManagedFirstInstallationRootMaterializer()
        : this(new FileSystemManagedVersionRepository(), stagingCustodyAcquired: null)
    {
    }

    internal FileSystemManagedFirstInstallationRootMaterializer(
        IWindowsCustodiedManagedVersionRepository repository,
        Func<string, CancellationToken, ValueTask>? stagingCustodyAcquired = null,
        Func<string, CancellationToken, ValueTask>? destinationCustodyAcquired = null,
        Func<string, CancellationToken, ValueTask>? closedRootVerified = null,
        Action<string>? beforeRepositoryStagingDelete = null,
        Action<string>? afterPackageDirectoryCreated = null,
        Action? afterMarkerTopologyProof = null,
        Action<string>? afterRootPromotion = null,
        Action<string>? beforeRepositoryStagingCleanup = null,
        Action<int, ManagedSetupStagingCleanupState>? repositoryStagingCleanupAttemptObserved = null,
        Func<TimeSpan, CancellationToken, ValueTask>? repositoryStagingCleanupDelay = null,
        Func<int, int>? repositoryStagingDeleteOpenStatusOverride = null,
        Func<int, int>? repositoryOwnedDeletionObservationStatusOverride = null)
    {
        _installer = repository ?? throw new ArgumentNullException(nameof(repository));
        _repository = repository;
        _stagingCustodyAcquired = stagingCustodyAcquired;
        _destinationCustodyAcquired = destinationCustodyAcquired;
        _closedRootVerified = closedRootVerified;
        _beforeRepositoryStagingDelete = beforeRepositoryStagingDelete;
        _beforeRepositoryStagingCleanup = beforeRepositoryStagingCleanup;
        _repositoryStagingCleanupAttemptObserved = repositoryStagingCleanupAttemptObserved;
        _repositoryStagingCleanupDelay = repositoryStagingCleanupDelay;
        _repositoryStagingDeleteOpenStatusOverride = repositoryStagingDeleteOpenStatusOverride;
        _repositoryOwnedDeletionObservationStatusOverride =
            repositoryOwnedDeletionObservationStatusOverride;
        _afterPackageDirectoryCreated = afterPackageDirectoryCreated;
        _afterMarkerTopologyProof = afterMarkerTopologyProof;
        _afterRootPromotion = afterRootPromotion;
    }

    /// <inheritdoc />
    public ValueTask<ManagedFirstInstallationMaterializationIssue> AdmitDestinationAsync(
        string managedRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(WindowsManagedSetupPathCustody.Admit(managedRoot));
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Custody is disposed in finally on failures and transfers to the promoted transaction on success.")]
    public async ValueTask<ManagedFirstInstallationMaterializationResult> MaterializeAsync(
        string managedRoot,
        string statePathIdentity,
        IManagedDistributionPayloadCapture payload,
        FreshInstallationCandidate candidate,
        VersionManagerState seed,
        CancellationToken cancellationToken)
    {
        return await MaterializeAsync(
            managedRoot,
            statePathIdentity,
            payload,
            candidate,
            seed,
            progress: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Custody is disposed in finally on failures and transfers to the promoted transaction on success.")]
    public async ValueTask<ManagedFirstInstallationMaterializationResult> MaterializeAsync(
        string managedRoot,
        string statePathIdentity,
        IManagedDistributionPayloadCapture payload,
        FreshInstallationCandidate candidate,
        VersionManagerState seed,
        IProgress<ManagedFirstInstallationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePathIdentity);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(seed);
        cancellationToken.ThrowIfCancellationRequested();

        ManagedInstallationRootStatus rootAdmission =
            FileSystemManagedInstallationRootProbe.AdmitRoot(managedRoot, out string root);
        if (rootAdmission != ManagedInstallationRootStatus.Absent)
        {
            return Failure(MapObservedRoot(rootAdmission));
        }
        if (!Path.IsPathFullyQualified(statePathIdentity))
        {
            return Failure(ManagedFirstInstallationMaterializationIssue.StateUnavailable);
        }
        if (payload is not IManagedDistributionPayloadContent content ||
            !string.Equals(
                payload.Identity.Bootstrap.FileName,
                BootstrapFileName,
                StringComparison.Ordinal))
        {
            return Failure(ManagedFirstInstallationMaterializationIssue.SourceChanged);
        }

        ManagedVersionAdmission expectedAdmission = new(
            candidate.Package.Version,
            candidate.Package.Identity,
            candidate.Package.ReleaseManifestSha256);
        if (!ManagedVersionSeedPolicy.IsCanonicalFirstRunSeed(seed) ||
            seed.Admissions is not [var only] ||
            only != expectedAdmission)
        {
            return Failure(ManagedFirstInstallationMaterializationIssue.SourceChanged);
        }

        var probe = new FileSystemManagedInstallationRootProbe();
        ManagedInstallationRootObservation initial = await probe.ObserveAsync(
            root,
            cancellationToken).ConfigureAwait(false);
        if (initial.Status != ManagedInstallationRootStatus.Absent)
        {
            return Failure(MapObservedRoot(initial.Status));
        }

        ManagedFirstInstallationMaterializationIssue custodyIssue =
            WindowsManagedSetupPathCustody.TryAcquire(
                root,
                out WindowsManagedSetupPathCustody? custody,
                _repositoryStagingDeleteOpenStatusOverride,
                _repositoryOwnedDeletionObservationStatusOverride);
        if (custodyIssue != ManagedFirstInstallationMaterializationIssue.None)
        {
            return Failure(custodyIssue);
        }
        if (_destinationCustodyAcquired is not null)
        {
            await _destinationCustodyAcquired(root, cancellationToken).ConfigureAwait(false);
        }
        ManagedFirstInstallationMaterializationIssue finalAdmission = custody!.AdmitFreshDestination();
        if (finalAdmission != ManagedFirstInstallationMaterializationIssue.None)
        {
            custody.Dispose();
            return Failure(finalAdmission);
        }

        string markerPath = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        string stagingContainer = FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root);
        string transactionId = Guid.NewGuid().ToString("N");
        string stagingRoot = Path.Combine(stagingContainer, transactionId);
        var marker = ManagedSetupTransactionDocument.Create(
            transactionId,
            root,
            Path.GetFullPath(statePathIdentity),
            stagingRoot,
            payload.Identity,
            candidate,
            ManagedSetupTransactionCodec.StagingPhase);
        FileStream? markerStream = null;
        bool promotionCommitted = false;
        try
        {
            MarkerCreateResult createdMarker = await CreateNewMarkerAsync(
                custody,
                Path.GetFileName(markerPath),
                ManagedSetupTransactionCodec.Serialize(marker),
                cancellationToken).ConfigureAwait(false);
            if (!createdMarker.IsSuccess)
            {
                return Failure(createdMarker.Issue);
            }
            markerStream = createdMarker.Stream!;
            ManagedFirstInstallationMaterializationIssue staging = custody!.CreateStaging(
                stagingContainer,
                stagingRoot);
            if (staging != ManagedFirstInstallationMaterializationIssue.None)
            {
                return Failure(staging);
            }
            if (_stagingCustodyAcquired is not null)
            {
                await _stagingCustodyAcquired(stagingRoot, cancellationToken).ConfigureAwait(false);
            }

            string launcherPath = Path.Combine(stagingRoot, DistributionLauncherFileName);
            string bootstrapPath = Path.Combine(stagingRoot, BootstrapFileName);
            await content.CopyDistributionLauncherAsync(launcherPath, cancellationToken)
                .ConfigureAwait(false);
            await content.CopyBootstrapAsync(bootstrapPath, cancellationToken).ConfigureAwait(false);
            if (!await MatchesAsync(
                    launcherPath,
                    payload.Identity.LauncherSize,
                    payload.Identity.LauncherSha256,
                    cancellationToken).ConfigureAwait(false) ||
                !await MatchesAsync(
                    bootstrapPath,
                    payload.Identity.Bootstrap.Length,
                    payload.Identity.Bootstrap.Sha256,
                    cancellationToken).ConfigureAwait(false))
            {
                return Failure(ManagedFirstInstallationMaterializationIssue.SourceChanged);
            }

            ManagedVersionPayloadMaterializationResult payloadResult;
            using (WindowsStableRelativeWriteRoot writeRoot = custody.OpenPackageWriteRoot())
            {
                payloadResult = await _installer
                    .MaterializeVerifiedPayloadWithinHeldRootAsync(
                        writeRoot,
                        candidate.Identity.SourceRoot,
                        candidate.Package,
                        _afterPackageDirectoryCreated,
                        progress,
                        cancellationToken).ConfigureAwait(false);
            }
            if (!payloadResult.IsVerified || payloadResult.Admission != expectedAdmission)
            {
                return Failure(MapInstallIssue(payloadResult.Issue));
            }

            progress?.Report(ManagedFirstInstallationProgress.Indeterminate(
                ManagedFirstInstallationProgressStage.FinalizingInstallation));

            var seedStore = new JsonVersionManagerStateStore(
                Path.Combine(stagingRoot, SeedFileName),
                allowUnboundSeedTemplate: true);
            await seedStore.SaveAsync(seed, cancellationToken).ConfigureAwait(false);

            string repositoryStaging = Path.Combine(
                stagingRoot,
                FileSystemManagedVersionRepository.StagingDirectoryName);
            _beforeRepositoryStagingCleanup?.Invoke(repositoryStaging);
            if (!await RemoveEmptyRepositoryStagingAsync(
                    custody,
                    repositoryStaging,
                    _beforeRepositoryStagingDelete,
                    _repositoryStagingCleanupAttemptObserved,
                    _repositoryStagingCleanupDelay,
                    cancellationToken).ConfigureAwait(false))
            {
                return Failure(ManagedFirstInstallationMaterializationIssue.RecoveryRequired);
            }
            if (!await VerifyStagedRootAsync(
                    stagingRoot,
                    payload.Identity,
                    expectedAdmission,
                    cancellationToken).ConfigureAwait(false))
            {
                return Failure(ManagedFirstInstallationMaterializationIssue.StateUnavailable);
            }

            if (Directory.Exists(root) || File.Exists(root))
            {
                return Failure(ManagedFirstInstallationMaterializationIssue.RecoveryRequired);
            }
            ManagedFirstInstallationMaterializationIssue promotion = custody.Promote(
                Path.GetFileName(root));
            if (promotion != ManagedFirstInstallationMaterializationIssue.None)
            {
                return Failure(promotion);
            }
            promotionCommitted = true;
            _afterRootPromotion?.Invoke(root);
            WindowsStableTreeLimits setupTreeLimits = CreateSetupTreeLimits(root, payload.Identity);
            ManagedFirstInstallationMaterializationIssue captured = custody.CaptureClosedTree(
                root,
                setupTreeLimits,
                cancellationToken);
            if (captured != ManagedFirstInstallationMaterializationIssue.None ||
                !await VerifyClosedRootAsync(
                    root,
                    payload.Identity,
                    expectedAdmission,
                    cancellationToken).ConfigureAwait(false))
            {
                return Failure(ManagedFirstInstallationMaterializationIssue.RecoveryRequired);
            }
            if (_closedRootVerified is not null)
            {
                await _closedRootVerified(root, cancellationToken).ConfigureAwait(false);
                if (!await VerifyClosedRootAsync(
                        root,
                        payload.Identity,
                        expectedAdmission,
                        cancellationToken).ConfigureAwait(false))
                {
                    return Failure(ManagedFirstInstallationMaterializationIssue.RecoveryRequired);
                }
            }

            ManagedSetupTransactionDocument promotedMarker = marker with
            {
                Phase = ManagedSetupTransactionCodec.RootPromotedPhase,
            };
            await ReplaceExactMarkerAsync(
                    markerStream,
                    marker,
                    promotedMarker,
                    custody.RevalidateClosedTree,
                    _afterMarkerTopologyProof,
                    cancellationToken)
                .ConfigureAwait(false);
            ManagedDistributionPayloadIdentity payloadIdentity = payload.Identity;
            WindowsManagedSetupPathCustody promotedCustody = custody;
            FileStream promotedMarkerStream = markerStream;
            custody = null;
            markerStream = null;
            return new(
                new PromotedInstallation(
                    root,
                    expectedAdmission,
                    payloadIdentity.Bootstrap,
                    promotedMarker,
                    promotedMarkerStream,
                    promotedCustody,
                    cancellation => VerifyClosedRootAsync(
                        root,
                        payloadIdentity,
                        expectedAdmission,
                        cancellation),
                    _afterMarkerTopologyProof),
                ManagedFirstInstallationMaterializationIssue.None);
        }
        catch (OperationCanceledException)
        {
            if (!promotionCommitted)
            {
                throw;
            }
            return Failure(ManagedFirstInstallationMaterializationIssue.RecoveryRequired);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(promotionCommitted
                ? ManagedFirstInstallationMaterializationIssue.RecoveryRequired
                : ManagedFirstInstallationMaterializationIssue.PermissionDenied);
        }
        catch (ManagedSetupPathChangedException)
        {
            return Failure(ManagedFirstInstallationMaterializationIssue.RecoveryRequired);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            return Failure(promotionCommitted
                ? ManagedFirstInstallationMaterializationIssue.RecoveryRequired
                : ManagedFirstInstallationMaterializationIssue.PromotionFailed);
        }
        catch (InvalidOperationException)
        {
            return Failure(promotionCommitted
                ? ManagedFirstInstallationMaterializationIssue.RecoveryRequired
                : ManagedFirstInstallationMaterializationIssue.StateUnavailable);
        }
        finally
        {
            markerStream?.Dispose();
            custody?.Dispose();
        }
    }
}
