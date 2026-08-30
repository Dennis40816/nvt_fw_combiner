using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>
/// Exact filesystem evidence and mutation adapter for one managed Setup transaction.
/// It never selects a recovery action or acquires a writer lease.
/// </summary>
public sealed class FileSystemManagedSetupRecoveryExecution :
    IManagedSetupRecoveryEvidenceProbe,
    IManagedSetupRecoveryExecutionPort
{
    private readonly IInstalledLauncherRepository _launcherRepository;
    private readonly ILauncherBootstrapStateStore _launcherStateStore;
    private readonly IManagedVersionRepository _repository;
    private readonly JsonVersionManagerStateStore _applicationStateStore;
    private readonly Action<int>? _afterDelete;
    private readonly Action<long>? _afterHashedFile;
    private readonly string _launcherStatePath;
    private readonly string _statePath;

    /// <summary>Creates an adapter bound to the canonical Application state path.</summary>
    public FileSystemManagedSetupRecoveryExecution(string statePath)
        : this(
            statePath,
            new FileSystemManagedVersionRepository(),
            new FileSystemInstalledLauncherRepository(),
            new JsonLauncherBootstrapStateStore(statePath),
            afterDelete: null,
            afterHashedFile: null)
    {
    }

    internal FileSystemManagedSetupRecoveryExecution(
        string statePath,
        IManagedVersionRepository repository,
        IInstalledLauncherRepository launcherRepository,
        ILauncherBootstrapStateStore launcherStateStore,
        Action<int>? afterDelete = null,
        Action<long>? afterHashedFile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        if (!Path.IsPathFullyQualified(statePath))
        {
            throw new ArgumentException("Recovery state path must be absolute.", nameof(statePath));
        }
        _statePath = Path.GetFullPath(statePath);
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _launcherRepository = launcherRepository ??
            throw new ArgumentNullException(nameof(launcherRepository));
        _launcherStateStore = launcherStateStore ??
            throw new ArgumentNullException(nameof(launcherStateStore));
        _applicationStateStore = new(_statePath);
        _launcherStatePath = JsonLauncherBootstrapStateStore.DerivePath(_statePath);
        _afterDelete = afterDelete;
        _afterHashedFile = afterHashedFile;
    }

    /// <inheritdoc />
    public async ValueTask<ManagedSetupRecoveryEvidenceObservation> ObserveAsync(
        ManagedSetupRecoveryTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ManagedPathSafety.PathComparer.Equals(transaction.StatePathIdentity, _statePath) ||
            !ManagedAppVersion.TryParse(transaction.Candidate.Version, out ManagedAppVersion version) ||
            !HasAdmittedExecutableSizes(transaction.Payload))
        {
            return Failure(ManagedSetupRecoveryEvidenceIssue.Invalid);
        }

        ManagedVersionAdmission admission;
        string treeRoot;
        try
        {
            admission = new(
                version,
                transaction.Candidate.EntryIdentity,
                transaction.Candidate.ReleaseManifestSha256);
            treeRoot = transaction.Phase == ManagedSetupRecoveryPhase.Staging
                ? Path.Combine(
                    FileSystemManagedInstallationRootProbe.GetStagingContainerPath(
                        transaction.ManagedRootIdentity),
                    transaction.TransactionId)
                : Path.GetFullPath(transaction.ManagedRootIdentity);
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or NotSupportedException)
        {
            return Failure(ManagedSetupRecoveryEvidenceIssue.Invalid);
        }

        StateFileObservation<RawFileEvidence> marker = await ObserveRawFileAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(
                transaction.ManagedRootIdentity),
            cancellationToken).ConfigureAwait(false);
        if (marker.Issue is { } markerIssue)
        {
            return Failure(markerIssue);
        }

        StateFileObservation<VersionManagerStateLoadResult> appState =
            await ObserveStateFileAsync(
                _statePath,
                _applicationStateStore.LoadAsync,
                static result => result.Issue == VersionManagerStateLoadIssue.Missing,
                cancellationToken).ConfigureAwait(false);
        if (appState.Issue is { } appIssue)
        {
            return Failure(appIssue);
        }
        StateFileObservation<LauncherBootstrapStateLoadResult> launcherObservation =
            await ObserveStateFileAsync(
                _launcherStatePath,
                _launcherStateStore.LoadAsync,
                static result => result.Issue == LauncherBootstrapStateLoadIssue.Missing,
                cancellationToken).ConfigureAwait(false);
        if (launcherObservation.Issue is { } launcherIssue)
        {
            return Failure(launcherIssue);
        }
        LauncherBootstrapStateLoadResult launcherState = launcherObservation.Value!;
        if (launcherState.Issue is LauncherBootstrapStateLoadIssue.Invalid)
        {
            return Failure(ManagedSetupRecoveryEvidenceIssue.Invalid);
        }
        if (launcherState.Issue is LauncherBootstrapStateLoadIssue.Unavailable)
        {
            return Failure(ManagedSetupRecoveryEvidenceIssue.StateUnavailable);
        }

        bool missingStatePair = appState.Value!.Issue == VersionManagerStateLoadIssue.Missing &&
            launcherState.Issue == LauncherBootstrapStateLoadIssue.Missing;
        if (missingStatePair)
        {
            PrefixObservation prefix = await ObserveMarkerDerivedPrefixAsync(
                transaction,
                admission,
                version,
                treeRoot,
                cancellationToken).ConfigureAwait(false);
            if (prefix.Issue is { } prefixIssue)
            {
                return Failure(prefixIssue);
            }
            if (prefix.IsExact)
            {
                return CreateEvidence(
                    transaction,
                    admission,
                    installedLauncher: null,
                    launcherState,
                    appState,
                    launcherObservation,
                    marker,
                    treeRoot,
                    prefix.Snapshot,
                    prefix.TreeInventory!,
                    prefix.StagingInventory!,
                    prefix.StagingDigest!,
                    RecoveryTreeLimits(transaction.Payload));
            }
        }

        WindowsStableTreeLimits limits;
        try
        {
            limits = FileSystemManagedFirstInstallationRootMaterializer.CreateSetupTreeLimits(
                treeRoot,
                transaction.Payload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(ManagedSetupRecoveryEvidenceIssue.StateUnavailable);
        }
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireImmutableTree(
            treeRoot,
            treeLimits: limits,
            cancellationToken: cancellationToken);
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            return Failure(MapEvidenceIssue(acquired.Issue));
        }

        using WindowsStablePathCustody custody = acquired.Custody!;
        try
        {
            if (!custody.RevalidateClosedTree() ||
                !custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot))
            {
                return Failure(ManagedSetupRecoveryEvidenceIssue.SourceChanged);
            }
            bool closedRoot = await new ManagedSetupClosedRootVerifier(_repository).VerifyAsync(
                    treeRoot,
                    transaction.Payload,
                    admission,
                    cancellationToken).ConfigureAwait(false);
            if (!closedRoot && missingStatePair)
            {
                string authorityManifest = string.Join(
                    '/',
                    FileSystemManagedVersionRepository.VersionsDirectoryName,
                    version.ToString(),
                    "RELEASE-MANIFEST.json");
                bool prefix = snapshot!.Files.ContainsKey(authorityManifest) &&
                    await VerifyRetainedManifestPrefixAsync(
                        custody,
                        snapshot,
                        transaction,
                        admission,
                        version,
                        authorityManifest,
                        cancellationToken).ConfigureAwait(false);
                if (!prefix)
                {
                    return Failure(ManagedSetupRecoveryEvidenceIssue.SourceChanged);
                }
                StagingObservation prefixStaging = await ObserveStagingResidueAsync(
                    transaction,
                    snapshot,
                    cancellationToken).ConfigureAwait(false);
                string prefixInventory = await SecureSnapshotProofAsync(
                    custody,
                    snapshot,
                    cancellationToken).ConfigureAwait(false);
                return prefixStaging.Issue is { } prefixIssue
                    ? Failure(prefixIssue)
                    : CreateEvidence(
                        transaction,
                        admission,
                        installedLauncher: null,
                        launcherState,
                        appState,
                        launcherObservation,
                        marker,
                        treeRoot,
                        snapshot,
                        prefixInventory,
                        prefixStaging.Inventory!,
                        prefixStaging.Digest!,
                        limits);
            }
            if (!closedRoot)
            {
                return Failure(ManagedSetupRecoveryEvidenceIssue.SourceChanged);
            }
            InstalledLauncherResult installed = await _launcherRepository.VerifyAsync(
                treeRoot,
                admission,
                cancellationToken).ConfigureAwait(false);
            if (!installed.IsVerified)
            {
                return Failure(MapEvidenceIssue(installed.Issue));
            }
            if (!custody.RevalidateClosedTree())
            {
                return Failure(ManagedSetupRecoveryEvidenceIssue.SourceChanged);
            }

            StagingObservation staging = await ObserveStagingResidueAsync(
                transaction,
                snapshot!,
                cancellationToken).ConfigureAwait(false);
            string treeInventory = await SecureSnapshotProofAsync(
                custody,
                snapshot!,
                cancellationToken).ConfigureAwait(false);
            return staging.Issue is { } stagingIssue
                ? Failure(stagingIssue)
                : CreateEvidence(
                    transaction,
                    admission,
                    installed.Identity!,
                    launcherState,
                    appState,
                launcherObservation,
                    marker,
                    treeRoot,
                    snapshot!,
                    treeInventory,
                    staging.Inventory!,
                    staging.Digest!,
                    limits);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(ManagedSetupRecoveryEvidenceIssue.PermissionDenied);
        }
        catch (Exception exception) when (exception is
            IOException or InvalidDataException or InvalidOperationException)
        {
            return Failure(ManagedSetupRecoveryEvidenceIssue.StateUnavailable);
        }
    }

    private ManagedSetupRecoveryEvidenceObservation CreateEvidence(
        ManagedSetupRecoveryTransaction transaction,
        ManagedVersionAdmission admission,
        ManagedLauncherIdentity? installedLauncher,
        LauncherBootstrapStateLoadResult launcherState,
        StateFileObservation<VersionManagerStateLoadResult> appState,
        StateFileObservation<LauncherBootstrapStateLoadResult> launcherObservation,
        StateFileObservation<RawFileEvidence> marker,
        string treeRoot,
        WindowsStableOwnedTreeSnapshot? treeSnapshot,
        string treeInventory,
        string stagingInventory,
        string stagingDigest,
        WindowsStableTreeLimits limits)
    {
        string treeDigest = treeSnapshot is null ? "absent" : ProofDigest(treeInventory);
        var token = new FileSystemRecoveryToken(
            _statePath,
            transaction.ManagedRootIdentity,
            transaction.TransactionId,
            transaction.Phase,
            admission.Version.ToString(),
            TransactionDigest(transaction),
            treeRoot,
            treeDigest,
            treeInventory,
            treeSnapshot is null ? string.Empty : TreeSteps(treeSnapshot, admission.Version),
            stagingInventory,
            stagingDigest,
            limits.MaximumFiles,
            limits.MaximumDirectories,
            limits.MaximumBytes,
            marker.IdentityDigest!,
            appState.IdentityDigest!,
            launcherObservation.IdentityDigest!,
            LauncherStateDigest(launcherState),
            LauncherDigest(installedLauncher));
        return new ManagedSetupRecoveryEvidenceObservation(
            admission,
            installedLauncher,
            launcherState,
            token);
    }

    private static async ValueTask<PrefixObservation> ObserveMarkerDerivedPrefixAsync(
        ManagedSetupRecoveryTransaction transaction,
        ManagedVersionAdmission admission,
        ManagedAppVersion version,
        string treeRoot,
        CancellationToken cancellationToken)
    {
        if (transaction.Phase == ManagedSetupRecoveryPhase.Staging)
        {
            WindowsStableCustodyResult finalRoot =
                WindowsStablePathCustody.TryAcquireImmutableTreeOrExactMissing(
                    transaction.ManagedRootIdentity,
                    new WindowsStableTreeLimits(0, 1, 0),
                    cancellationToken);
            finalRoot.Custody?.Dispose();
            if (!finalRoot.IsExactChildMissing)
            {
                return new(
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    finalRoot.Issue == WindowsStableCustodyIssue.AccessDenied
                        ? ManagedSetupRecoveryEvidenceIssue.PermissionDenied
                        : ManagedSetupRecoveryEvidenceIssue.Invalid);
            }
            StagingObservation staging = await ObserveStagingContainerAsync(
                transaction.ManagedRootIdentity,
                expectedChild: null,
                transaction.TransactionId,
                RecoveryTreeLimits(transaction.Payload),
                cancellationToken).ConfigureAwait(false);
            return staging.Issue is ManagedSetupRecoveryEvidenceIssue.Invalid
                ? new(false, null, null, null, null, null, Issue: null)
                : staging.Issue is { } issue
                    ? new(false, null, null, null, null, null, issue)
                : staging.IsEmptyOrAbsent
                    ? new(
                        true,
                        null,
                        "absent",
                        string.Empty,
                        staging.Inventory,
                        staging.Digest,
                        Issue: null)
                    : new(false, null, null, null, null, null, Issue: null);
        }

        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireImmutableTreeOrExactMissing(
            treeRoot,
            new WindowsStableTreeLimits(0, 3, 0),
            cancellationToken);
        if (acquired.IsExactChildMissing)
        {
            StagingObservation staging = await ObserveStagingContainerAsync(
                transaction.ManagedRootIdentity,
                expectedChild: null,
                transaction.TransactionId,
                RecoveryTreeLimits(transaction.Payload),
                cancellationToken).ConfigureAwait(false);
            return staging.Issue is { } issue
                ? new(false, null, null, null, null, null, issue)
                : staging.IsEmptyOrAbsent
                    ? new(
                        true,
                        null,
                        "absent",
                        string.Empty,
                        staging.Inventory,
                        staging.Digest,
                        Issue: null)
                    : new(false, null, null, null, null, null, ManagedSetupRecoveryEvidenceIssue.Invalid);
        }
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            if (acquired.Issue != WindowsStableCustodyIssue.Unavailable)
            {
                return new(false, null, null, null, null, null, MapEvidenceIssue(acquired.Issue));
            }
            var broadLimits = new WindowsStableTreeLimits(
                checked(FileSystemManagedVersionRepository.MaximumInstalledFiles + 3),
                checked(FileSystemManagedVersionRepository.MaximumInstalledDirectories + 3),
                checked(
                    FileSystemManagedVersionRepository.MaximumInstalledBytes +
                    transaction.Payload.LauncherSize +
                    transaction.Payload.BootstrapSize +
                    (64 * 1024)));
            WindowsStableCustodyResult broad = WindowsStablePathCustody.TryAcquireImmutableTree(
                treeRoot,
                treeLimits: broadLimits,
                cancellationToken: cancellationToken);
            if (!broad.IsAcquired)
            {
                broad.Custody?.Dispose();
                return new(false, null, null, null, null, null, MapEvidenceIssue(broad.Issue));
            }
            using WindowsStablePathCustody broadCustody = broad.Custody!;
            if (!broadCustody.RevalidateClosedTree() ||
                !broadCustody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? broadSnapshot))
            {
                return new(false, null, null, null, null, null, ManagedSetupRecoveryEvidenceIssue.SourceChanged);
            }
            string authorityManifest = string.Join(
                '/',
                FileSystemManagedVersionRepository.VersionsDirectoryName,
                version.ToString(),
                "RELEASE-MANIFEST.json");
            if (!broadSnapshot!.Files.ContainsKey(authorityManifest))
            {
                return new(false, null, null, null, null, null, ManagedSetupRecoveryEvidenceIssue.Invalid);
            }
            if (HasCompleteTopLevel(broadSnapshot))
            {
                return new(false, null, null, null, null, null, Issue: null);
            }
            bool reachable = await VerifyRetainedManifestPrefixAsync(
                broadCustody,
                broadSnapshot,
                transaction,
                admission,
                version,
                authorityManifest,
                cancellationToken).ConfigureAwait(false);
            if (!reachable)
            {
                return new(false, null, null, null, null, null, ManagedSetupRecoveryEvidenceIssue.Invalid);
            }
            string broadInventory = await SecureSnapshotProofAsync(
                broadCustody,
                broadSnapshot,
                cancellationToken).ConfigureAwait(false);
            StagingObservation prefixStaging = await ObserveStagingContainerAsync(
                transaction.ManagedRootIdentity,
                transaction.Phase == ManagedSetupRecoveryPhase.Staging ? broadSnapshot : null,
                transaction.TransactionId,
                RecoveryTreeLimits(transaction.Payload),
                cancellationToken).ConfigureAwait(false);
            return prefixStaging.Issue is { } prefixStagingIssue
                ? new(false, null, null, null, null, null, prefixStagingIssue)
                : new(
                    true,
                    broadSnapshot,
                    ProofDigest(broadInventory),
                    broadInventory,
                    prefixStaging.Inventory,
                    prefixStaging.Digest,
                    Issue: null);
        }

        using WindowsStablePathCustody custody = acquired.Custody!;
        if (!custody.RevalidateClosedTree() ||
            !custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot))
        {
            return new(false, null, null, null, null, null, ManagedSetupRecoveryEvidenceIssue.SourceChanged);
        }
        string versionPath = string.Join('/',
            FileSystemManagedVersionRepository.VersionsDirectoryName,
            version.ToString());
        HashSet<string> remainingDirectories =
            snapshot!.Directories.Keys.ToHashSet(ManagedPathSafety.PathComparer);
        bool exactSkeleton = snapshot.Files.Count == 0 &&
            (remainingDirectories.SetEquals(
                [string.Empty, FileSystemManagedVersionRepository.VersionsDirectoryName, versionPath]) ||
             remainingDirectories.SetEquals(
                [string.Empty, FileSystemManagedVersionRepository.VersionsDirectoryName]) ||
             remainingDirectories.SetEquals([string.Empty]));
        if (!exactSkeleton)
        {
            return new(false, null, null, null, null, null, ManagedSetupRecoveryEvidenceIssue.Invalid);
        }
        StagingObservation residue = await ObserveStagingContainerAsync(
            transaction.ManagedRootIdentity,
            expectedChild: null,
            transaction.TransactionId,
            RecoveryTreeLimits(transaction.Payload),
            cancellationToken).ConfigureAwait(false);
        string terminalInventory = await SecureSnapshotProofAsync(
            custody,
            snapshot,
            cancellationToken).ConfigureAwait(false);
        return residue.Issue is { } residueIssue
            ? new(false, null, null, null, null, null, residueIssue)
            : residue.IsEmptyOrAbsent
                ? new(
                    true,
                    snapshot,
                    ProofDigest(terminalInventory),
                    terminalInventory,
                    residue.Inventory,
                    residue.Digest,
                    Issue: null)
                : new(false, null, null, null, null, null, ManagedSetupRecoveryEvidenceIssue.Invalid);
    }

    private static ValueTask<StagingObservation> ObserveStagingResidueAsync(
        ManagedSetupRecoveryTransaction transaction,
        WindowsStableOwnedTreeSnapshot treeSnapshot,
        CancellationToken cancellationToken)
    {
        return ObserveStagingContainerAsync(
            transaction.ManagedRootIdentity,
            transaction.Phase == ManagedSetupRecoveryPhase.Staging ? treeSnapshot : null,
            transaction.TransactionId,
            RecoveryTreeLimits(transaction.Payload),
            cancellationToken);
    }

    private static async ValueTask<StagingObservation> ObserveStagingContainerAsync(
        string managedRoot,
        WindowsStableOwnedTreeSnapshot? expectedChild,
        string transactionId,
        WindowsStableTreeLimits treeLimits,
        CancellationToken cancellationToken)
    {
        string container = FileSystemManagedInstallationRootProbe.GetStagingContainerPath(managedRoot);
        var limits = new WindowsStableTreeLimits(
            checked(treeLimits.MaximumFiles + 1),
            checked(treeLimits.MaximumDirectories + 1),
            treeLimits.MaximumBytes);
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireImmutableTreeOrExactMissing(
            container,
            limits,
            cancellationToken);
        if (acquired.IsExactChildMissing)
        {
            return expectedChild is null
                ? new(true, string.Empty, "absent", Issue: null)
                : new(false, null, null, ManagedSetupRecoveryEvidenceIssue.SourceChanged);
        }
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            return new(false, null, null, MapEvidenceIssue(acquired.Issue));
        }

        using WindowsStablePathCustody custody = acquired.Custody!;
        if (!custody.RevalidateClosedTree() ||
            !custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot))
        {
            return new(false, null, null, ManagedSetupRecoveryEvidenceIssue.SourceChanged);
        }
        bool matches = expectedChild is null
            ? snapshot!.Files.Count == 0 &&
                snapshot.Directories.Keys.SequenceEqual([string.Empty], StringComparer.Ordinal)
            : ContainerContainsOnlyExpectedChild(snapshot!, expectedChild, transactionId);
        if (!matches)
        {
            return new(false, null, null, ManagedSetupRecoveryEvidenceIssue.Invalid);
        }
        string proof = await SecureSnapshotProofAsync(
            custody,
            snapshot!,
            cancellationToken).ConfigureAwait(false);
        return custody.RevalidateClosedTree()
            ? new(expectedChild is null, proof, ProofDigest(proof), Issue: null)
            : new(false, null, null, ManagedSetupRecoveryEvidenceIssue.SourceChanged);
    }

    private static bool ContainerContainsOnlyExpectedChild(
        WindowsStableOwnedTreeSnapshot container,
        WindowsStableOwnedTreeSnapshot child,
        string transactionId)
    {
        string Prefix(string relative)
        {
            return string.IsNullOrEmpty(relative)
                ? transactionId
                : string.Concat(transactionId, "/", relative);
        }
        var expectedFiles = child.Files.ToDictionary(
            pair => Prefix(pair.Key),
            static pair => pair.Value,
            ManagedPathSafety.PathComparer);
        var expectedDirectories = child.Directories.ToDictionary(
            pair => Prefix(pair.Key),
            static pair => pair.Value,
            ManagedPathSafety.PathComparer);
        return container.Files.Count == expectedFiles.Count &&
            container.Directories.Count == expectedDirectories.Count + 1 &&
            container.Directories.ContainsKey(string.Empty) &&
            expectedFiles.All(pair =>
                container.Files.TryGetValue(pair.Key, out WindowsStablePathIdentity identity) &&
                identity == pair.Value) &&
            expectedDirectories.All(pair =>
                container.Directories.TryGetValue(pair.Key, out WindowsStablePathIdentity identity) &&
                identity == pair.Value);
    }

    private static bool HasCompleteTopLevel(WindowsStableOwnedTreeSnapshot snapshot)
    {
        return snapshot.Files.ContainsKey(
                FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName) &&
            snapshot.Files.ContainsKey(FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName) &&
            snapshot.Files.ContainsKey(FileSystemManagedFirstInstallationRootMaterializer.SeedFileName) &&
            snapshot.Directories.ContainsKey(FileSystemManagedVersionRepository.VersionsDirectoryName);
    }

    private static async ValueTask<bool> VerifyRetainedManifestPrefixAsync(
        WindowsStablePathCustody custody,
        WindowsStableOwnedTreeSnapshot snapshot,
        ManagedSetupRecoveryTransaction transaction,
        ManagedVersionAdmission admission,
        ManagedAppVersion version,
        string authorityManifest,
        CancellationToken cancellationToken)
    {
        byte[] manifestBytes;
        using (FileStream stream = custody.OpenReadOnlyFile(authorityManifest))
        {
            if (stream.Length is < 1 or > FileSystemManagedVersionRepository.MaximumManifestBytes)
            {
                return false;
            }
            manifestBytes = new byte[stream.Length];
            await stream.ReadExactlyAsync(manifestBytes, cancellationToken).ConfigureAwait(false);
        }
        if (!string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
                admission.ReleaseManifestSha256,
                StringComparison.Ordinal))
        {
            return false;
        }
        if (!ManagedPackageVerifier.TryReadCanonicalManifest(
                manifestBytes,
                version,
                archivePaths: null,
                out ReleaseManifestDocument? manifest))
        {
            return false;
        }

        string versionRoot = string.Join(
            '/',
            FileSystemManagedVersionRepository.VersionsDirectoryName,
            version.ToString());
        var expectedFiles = new HashSet<string>(ManagedPathSafety.PathComparer)
        {
            FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName,
            FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
            FileSystemManagedFirstInstallationRootMaterializer.SeedFileName,
            authorityManifest,
            string.Concat(versionRoot, "/SHA256SUMS.txt"),
            string.Concat(versionRoot, "/", FileSystemManagedVersionRepository.AdmissionFileName),
        };
        foreach (ReleaseManifestFileDocument file in manifest.Files!)
        {
            if (!ManagedPathSafety.IsSafeRelativePayloadPath(file.Path))
            {
                return false;
            }
            _ = expectedFiles.Add(string.Concat(versionRoot, "/", file.Path.Replace('\\', '/')));
        }
        var expectedDirectories = new HashSet<string>(ManagedPathSafety.PathComparer)
        {
            string.Empty,
            FileSystemManagedVersionRepository.VersionsDirectoryName,
            versionRoot,
        };
        foreach (string file in expectedFiles.Where(path => path.StartsWith(
                     string.Concat(versionRoot, "/"),
                     StringComparison.OrdinalIgnoreCase)))
        {
            string? parent = Path.GetDirectoryName(file.Replace('/', Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(parent))
            {
                string normalized = parent.Replace('\\', '/');
                _ = expectedDirectories.Add(normalized);
                if (ManagedPathSafety.PathComparer.Equals(normalized, versionRoot))
                {
                    break;
                }
                parent = Path.GetDirectoryName(parent);
            }
        }
        string[] expectedSteps = BuildTreeSteps(expectedFiles, expectedDirectories, version);
        string[] actualSteps = SplitLines(TreeSteps(snapshot, version));
        if (actualSteps.Length > expectedSteps.Length ||
            !expectedSteps.AsSpan(expectedSteps.Length - actualSteps.Length)
                .SequenceEqual(actualSteps))
        {
            return false;
        }

        bool verified = await VerifySurvivingPrefixFilesAsync(
            custody,
            snapshot,
            transaction,
            admission,
            manifest,
            manifestBytes,
            versionRoot,
            cancellationToken).ConfigureAwait(false);
        return verified && custody.RevalidateClosedTree();
    }

    private static async ValueTask<bool> VerifySurvivingPrefixFilesAsync(
        WindowsStablePathCustody custody,
        WindowsStableOwnedTreeSnapshot snapshot,
        ManagedSetupRecoveryTransaction transaction,
        ManagedVersionAdmission admission,
        ReleaseManifestDocument manifest,
        byte[] manifestBytes,
        string versionRoot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Files.ContainsKey(
                FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName) &&
            !await MatchesHeldAsync(
                custody,
                FileSystemManagedFirstInstallationRootMaterializer.DistributionLauncherFileName,
                transaction.Payload.LauncherSize,
                transaction.Payload.LauncherSha256,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }
        if (snapshot.Files.ContainsKey(FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName) &&
            !await MatchesHeldAsync(
                custody,
                FileSystemManagedFirstInstallationRootMaterializer.BootstrapFileName,
                transaction.Payload.BootstrapSize,
                transaction.Payload.BootstrapSha256,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }
        string seedName = FileSystemManagedFirstInstallationRootMaterializer.SeedFileName;
        if (snapshot.Files.ContainsKey(seedName))
        {
            VersionManagerStateLoadResult seed = await new JsonVersionManagerStateStore(
                Path.Combine(custody.RootPath, seedName),
                allowUnboundSeedTemplate: true).LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!seed.IsSuccess || seed.State is null ||
                !ManagedVersionSeedPolicy.IsCanonicalFirstRunSeed(seed.State) ||
                seed.State.Admissions is not [var only] || only != admission)
            {
                return false;
            }
        }
        foreach (ReleaseManifestFileDocument file in manifest.Files!)
        {
            string relative = string.Concat(versionRoot, "/", file.Path.Replace('\\', '/'));
            if (snapshot.Files.ContainsKey(relative) &&
                !await MatchesHeldAsync(
                    custody,
                    relative,
                    file.Size,
                    file.Sha256,
                    cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }
        string admissionPath = string.Concat(
            versionRoot,
            "/",
            FileSystemManagedVersionRepository.AdmissionFileName);
        if (snapshot.Files.ContainsKey(admissionPath) &&
            await FileSystemManagedVersionRepository.ReadAdmissionAsync(
                Path.Combine(custody.RootPath, admissionPath.Replace('/', Path.DirectorySeparatorChar)),
                cancellationToken).ConfigureAwait(false) != admission)
        {
            return false;
        }
        string checksumPath = string.Concat(versionRoot, "/SHA256SUMS.txt");
        return !snapshot.Files.ContainsKey(checksumPath) ||
            await VerifyHeldChecksumAsync(
                custody,
                checksumPath,
                manifestBytes,
                manifest.Files,
                cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> MatchesHeldAsync(
        WindowsStablePathCustody custody,
        string relative,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        using FileStream stream = custody.OpenReadOnlyFile(relative);
        return stream.Length == expectedSize && string.Equals(
            Convert.ToHexStringLower(
                await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)),
            expectedSha256,
            StringComparison.Ordinal);
    }

    private static async ValueTask<bool> VerifyHeldChecksumAsync(
        WindowsStablePathCustody custody,
        string relative,
        byte[] manifestBytes,
        IReadOnlyList<ReleaseManifestFileDocument> files,
        CancellationToken cancellationToken)
    {
        using FileStream stream = custody.OpenReadOnlyFile(relative);
        if (stream.Length is < 1 or > FileSystemManagedVersionRepository.MaximumManifestBytes)
        {
            return false;
        }
        byte[] bytes = new byte[stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return ManagedPackageVerifier.VerifyChecksumDocument(bytes, manifestBytes, files);
    }

    /// <inheritdoc />
    public async ValueTask<ManagedSetupRecoveryExecutionPortOutcome> ExecuteAsync(
        ManagedSetupRecoveryExecutionRequest request,
        VersionManagerWriteLeaseResult writerLease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(writerLease);
        var progress = new RecoveryMutationProgress();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!writerLease.HoldsStatePath(_statePath))
            {
                return ManagedSetupRecoveryExecutionPortOutcome.StateUnavailable;
            }
            if (request.ExecutionToken is not FileSystemRecoveryToken token ||
                !ManagedPathSafety.PathComparer.Equals(token.StatePath, _statePath))
            {
                return ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
            }

            bool rollback = request.Action ==
                ManagedSetupRecoveryAction.RemoveIncompleteInstallation;
            ManagedSetupRecoveryExecutionPortOutcome? preflight = rollback
                ? await RevalidateStaticSnapshotAsync(
                    token,
                    token.ApplicationStateIdentity,
                    token.LauncherStateIdentity,
                    cancellationToken,
                    validateTreeAndStaging: false).ConfigureAwait(false)
                : await RevalidateTokenAsync(token, cancellationToken).ConfigureAwait(false);
            if (preflight is not null)
            {
                return preflight.Value;
            }

            WindowsStablePathCustody? rollbackTreeCustody = null;
            try
            {
                if (rollback)
                {
                    PreparedRollbackTree prepared = await PrepareRollbackTreeAsync(
                        token,
                        cancellationToken).ConfigureAwait(false);
                    if (prepared.Issue is not null)
                    {
                        return prepared.Issue.Value;
                    }
                    rollbackTreeCustody = prepared.Custody;
                    ManagedSetupRecoveryExecutionPortOutcome? launcherDelete =
                        DeleteExactFileIfPresent(
                            _launcherStatePath,
                            token.LauncherStateIdentity,
                            progress,
                            cancellationToken);
                    if (launcherDelete is not null)
                    {
                        return launcherDelete.Value;
                    }
                    ManagedSetupRecoveryExecutionPortOutcome? afterLauncher =
                        await RevalidateStaticSnapshotAsync(
                            token,
                            token.ApplicationStateIdentity,
                            "missing",
                            cancellationToken,
                            validateTreeAndStaging: false).ConfigureAwait(false);
                    if (afterLauncher is not null)
                    {
                        return afterLauncher.Value;
                    }
                    ManagedSetupRecoveryExecutionPortOutcome? appDelete = DeleteExactFileIfPresent(
                        _statePath,
                        token.ApplicationStateIdentity,
                        progress,
                        cancellationToken);
                    if (appDelete is not null)
                    {
                        return appDelete.Value;
                    }
                    ManagedSetupRecoveryExecutionPortOutcome? afterApplication =
                        await RevalidateStaticSnapshotAsync(
                            token,
                            "missing",
                            "missing",
                            cancellationToken,
                            validateTreeAndStaging: false).ConfigureAwait(false);
                    if (afterApplication is not null)
                    {
                        return afterApplication.Value;
                    }
                    ManagedSetupRecoveryExecutionPortOutcome? treeDelete = await DeleteTreeAsync(
                        token,
                        rollbackTreeCustody,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                    if (treeDelete is not null)
                    {
                        return treeDelete.Value;
                    }
                    rollbackTreeCustody?.Dispose();
                    rollbackTreeCustody = null;
                }

                ManagedSetupRecoveryExecutionPortOutcome? stagingDelete =
                    await DeleteEmptyStagingAsync(
                        token,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                if (stagingDelete is not null)
                {
                    return stagingDelete.Value;
                }
                ManagedSetupRecoveryExecutionPortOutcome? postcondition =
                    await RevalidateFinalPostconditionAsync(
                        token,
                        request.Action,
                        cancellationToken).ConfigureAwait(false);
                if (postcondition is not null)
                {
                    return postcondition.Value;
                }
                ManagedSetupRecoveryExecutionPortOutcome? markerDelete = DeleteExactFileIfPresent(
                    FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(token.ManagedRoot),
                    token.MarkerIdentity,
                    progress,
                    cancellationToken,
                    missingIsComplete: false);
                if (markerDelete is not null)
                {
                    return markerDelete.Value;
                }
                ManagedSetupRecoveryExecutionPortOutcome? terminal =
                    await RevalidateActionPostconditionAsync(
                        token,
                        request.Action,
                        CancellationToken.None).ConfigureAwait(false);
                return terminal is not null ||
                    !WindowsStablePathCustody.TryObserveExactMissingFile(
                        FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(
                            token.ManagedRoot),
                        CancellationToken.None)
                    ? terminal ?? ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired
                    : ManagedSetupRecoveryExecutionPortOutcome.Completed;
            }
            finally
            {
                rollbackTreeCustody?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            return progress.Mutated
                ? ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired
                : ManagedSetupRecoveryExecutionPortOutcome.Cancelled;
        }
        catch (UnauthorizedAccessException)
        {
            return ManagedSetupRecoveryExecutionPortOutcome.PermissionDenied;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return progress.Mutated
                ? ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired
                : ManagedSetupRecoveryExecutionPortOutcome.StateUnavailable;
        }
    }

    private async ValueTask<ManagedSetupRecoveryExecutionPortOutcome?>
        RevalidateFinalPostconditionAsync(
            FileSystemRecoveryToken token,
            ManagedSetupRecoveryAction action,
            CancellationToken cancellationToken)
    {
        ManagedSetupRecoveryExecutionPortOutcome? actionPostcondition =
            await RevalidateActionPostconditionAsync(
                token,
                action,
                cancellationToken).ConfigureAwait(false);
        if (actionPostcondition is not null)
        {
            return actionPostcondition.Value;
        }
        StateFileObservation<RawFileEvidence> marker = await ObserveRawFileAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(token.ManagedRoot),
            cancellationToken).ConfigureAwait(false);
        return marker.Issue is null &&
            string.Equals(marker.IdentityDigest, token.MarkerIdentity, StringComparison.Ordinal)
                ? null
                : ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
    }

    private async ValueTask<ManagedSetupRecoveryExecutionPortOutcome?>
        RevalidateActionPostconditionAsync(
            FileSystemRecoveryToken token,
            ManagedSetupRecoveryAction action,
            CancellationToken cancellationToken)
    {
        string expectedApplication = action ==
            ManagedSetupRecoveryAction.RemoveIncompleteInstallation
                ? "missing"
                : token.ApplicationStateIdentity;
        string expectedLauncher = action ==
            ManagedSetupRecoveryAction.RemoveIncompleteInstallation
                ? "missing"
                : token.LauncherStateIdentity;
        if (!string.Equals(
                ObserveRawOrMissingIdentity(_statePath, cancellationToken),
                expectedApplication,
                StringComparison.Ordinal) ||
            !string.Equals(
                ObserveRawOrMissingIdentity(_launcherStatePath, cancellationToken),
                expectedLauncher,
                StringComparison.Ordinal))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
        }

        StagingObservation staging = await ObserveStagingContainerAsync(
            token.ManagedRoot,
            expectedChild: null,
            token.TransactionId,
            RecoveryTreeLimits(token),
            cancellationToken).ConfigureAwait(false);
        if (staging.Issue is not null ||
            !string.IsNullOrEmpty(staging.Inventory) ||
            !string.Equals(staging.Digest, "absent", StringComparison.Ordinal))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
        }
        if (action == ManagedSetupRecoveryAction.RemoveIncompleteInstallation &&
            token.Phase == ManagedSetupRecoveryPhase.Staging)
        {
            WindowsStableCustodyResult finalRoot =
                WindowsStablePathCustody.TryAcquireImmutableTreeOrExactMissing(
                    token.ManagedRoot,
                    new WindowsStableTreeLimits(0, 1, 0),
                    cancellationToken);
            finalRoot.Custody?.Dispose();
            if (!finalRoot.IsExactChildMissing)
            {
                return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
            }
        }
        else
        {
            ExecutionTreeObservation tree = await ObserveExactTreeAsync(
                token.TreeRoot,
                RecoveryTreeLimits(token),
                cancellationToken).ConfigureAwait(false);
            if (tree.Issue is not null)
            {
                return tree.Issue;
            }
            bool treeMatches = action == ManagedSetupRecoveryAction.RemoveIncompleteInstallation
                ? tree.Snapshot is null && string.Equals(tree.Digest, "absent", StringComparison.Ordinal)
                : string.Equals(tree.Inventory, token.TreeInventory, StringComparison.Ordinal) &&
                    string.Equals(tree.Digest, token.TreeSha256, StringComparison.Ordinal);
            if (!treeMatches)
            {
                return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
            }
        }
        return null;
    }

    private static ManagedSetupRecoveryEvidenceObservation Failure(
        ManagedSetupRecoveryEvidenceIssue issue)
    {
        return new(issue);
    }

    private async ValueTask<ManagedSetupRecoveryExecutionPortOutcome?> RevalidateTokenAsync(
        FileSystemRecoveryToken token,
        CancellationToken cancellationToken)
    {
        return await RevalidateStaticSnapshotAsync(
            token,
            token.ApplicationStateIdentity,
            token.LauncherStateIdentity,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ManagedSetupRecoveryExecutionPortOutcome?> RevalidateStaticSnapshotAsync(
        FileSystemRecoveryToken token,
        string expectedApplicationState,
        string expectedLauncherState,
        CancellationToken cancellationToken,
        bool validateTreeAndStaging = true)
    {
        string markerPath = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(
            token.ManagedRoot);
        StateFileObservation<RawFileEvidence> marker = await ObserveRawFileAsync(
            markerPath,
            cancellationToken).ConfigureAwait(false);
        if (marker.Issue is { } markerIssue)
        {
            return MapExecutionIssue(markerIssue);
        }
        if (!string.Equals(marker.IdentityDigest, token.MarkerIdentity, StringComparison.Ordinal))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
        }
        if (!string.Equals(
                ObserveRawOrMissingIdentity(_statePath, cancellationToken),
                expectedApplicationState,
                StringComparison.Ordinal) ||
            !string.Equals(
                ObserveRawOrMissingIdentity(_launcherStatePath, cancellationToken),
                expectedLauncherState,
                StringComparison.Ordinal))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
        }
        if (!validateTreeAndStaging)
        {
            return token.Phase == ManagedSetupRecoveryPhase.Staging &&
                !IsExactFinalRootMissing(token, cancellationToken)
                ? ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired
                : null;
        }

        bool absentEarlyStaging = token.Phase == ManagedSetupRecoveryPhase.Staging &&
            string.IsNullOrEmpty(token.TreeInventory);
        if (token.Phase == ManagedSetupRecoveryPhase.Staging &&
            !IsExactFinalRootMissing(token, cancellationToken))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
        }
        ExecutionTreeObservation tree = absentEarlyStaging
            ? new(null, string.Empty, "absent", Issue: null)
            : await ObserveExactTreeAsync(
                token.TreeRoot,
                RecoveryTreeLimits(token),
                cancellationToken).ConfigureAwait(false);
        if (tree.Issue is not null)
        {
            return tree.Issue;
        }
        if (!string.Equals(tree.Inventory, token.TreeInventory, StringComparison.Ordinal))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
        }
        if (!string.Equals(tree.Digest, token.TreeSha256, StringComparison.Ordinal))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
        }

        StagingObservation staging = await ObserveStagingContainerAsync(
            token.ManagedRoot,
            token.Phase == ManagedSetupRecoveryPhase.Staging ? tree.Snapshot : null,
            token.TransactionId,
            RecoveryTreeLimits(token),
            cancellationToken).ConfigureAwait(false);
        return staging.Issue is not null ||
            !string.Equals(staging.Inventory, token.StagingInventory, StringComparison.Ordinal) ||
            !string.Equals(staging.Digest, token.StagingSha256, StringComparison.Ordinal)
                ? ManagedSetupRecoveryExecutionPortOutcome.SourceChanged
                : null;
    }

    private async ValueTask<PreparedRollbackTree> PrepareRollbackTreeAsync(
        FileSystemRecoveryToken token,
        CancellationToken cancellationToken)
    {
        bool absentTree = string.IsNullOrEmpty(token.TreeInventory) &&
            string.Equals(token.TreeSha256, "absent", StringComparison.Ordinal);
        WindowsStablePathCustody? custody = null;
        WindowsStableOwnedTreeSnapshot? snapshot = null;
        if (absentTree)
        {
            if (token.Phase != ManagedSetupRecoveryPhase.Staging)
            {
                WindowsStableCustodyResult missing =
                    WindowsStablePathCustody.TryAcquireImmutableTreeOrExactMissing(
                        token.TreeRoot,
                        new WindowsStableTreeLimits(0, 0, 0),
                        cancellationToken);
                missing.Custody?.Dispose();
                if (!missing.IsExactChildMissing)
                {
                    return new(null, MapExecutionIssue(missing.Issue));
                }
            }
        }
        else
        {
            WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireDeleteCapableTree(
                token.TreeRoot,
                RecoveryTreeLimits(token),
                cancellationToken);
            if (!acquired.IsAcquired)
            {
                acquired.Custody?.Dispose();
                return new(null, MapExecutionIssue(acquired.Issue));
            }
            custody = acquired.Custody!;
            if (!custody.TryCreateOwnedSnapshot(out snapshot))
            {
                custody.Dispose();
                return new(null, ManagedSetupRecoveryExecutionPortOutcome.SourceChanged);
            }
            string proof = await SecureSnapshotProofAsync(
                custody,
                snapshot!,
                cancellationToken,
                _afterHashedFile).ConfigureAwait(false);
            if (!string.Equals(proof, token.TreeInventory, StringComparison.Ordinal) ||
                !string.Equals(ProofDigest(proof), token.TreeSha256, StringComparison.Ordinal) ||
                !string.Equals(
                    TreeSteps(snapshot!, ManagedAppVersion.Parse(token.Version)),
                    token.TreeSteps,
                    StringComparison.Ordinal))
            {
                custody.Dispose();
                return new(null, ManagedSetupRecoveryExecutionPortOutcome.SourceChanged);
            }
        }

        StagingObservation staging = await ObserveStagingContainerAsync(
            token.ManagedRoot,
            token.Phase == ManagedSetupRecoveryPhase.Staging ? snapshot : null,
            token.TransactionId,
            RecoveryTreeLimits(token),
            cancellationToken).ConfigureAwait(false);
        if (staging.Issue is not null ||
            !string.Equals(staging.Inventory, token.StagingInventory, StringComparison.Ordinal) ||
            !string.Equals(staging.Digest, token.StagingSha256, StringComparison.Ordinal))
        {
            custody?.Dispose();
            return new(null, staging.Issue is null
                ? ManagedSetupRecoveryExecutionPortOutcome.SourceChanged
                : MapExecutionIssue(staging.Issue.Value));
        }
        return new(custody, Issue: null);
    }

    private async ValueTask<ManagedSetupRecoveryExecutionPortOutcome?> DeleteTreeAsync(
        FileSystemRecoveryToken token,
        WindowsStablePathCustody? custody,
        RecoveryMutationProgress progress,
        CancellationToken cancellationToken)
    {
        string[] steps = SplitLines(token.TreeSteps);
        if (steps.Length == 0)
        {
            return string.IsNullOrEmpty(token.TreeInventory) &&
                string.Equals(token.TreeSha256, "absent", StringComparison.Ordinal)
                ? null
                : ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired;
        }
        if (custody is null)
        {
            return ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired;
        }
        foreach (string step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ManagedSetupRecoveryExecutionPortOutcome? progressIssue =
                await RevalidateRollbackTreeStepAsync(
                    token,
                    treeCustodyHeld: true,
                    cancellationToken).ConfigureAwait(false);
            if (progressIssue is not null)
            {
                return progressIssue.Value;
            }
            string relative = step.Length == 2 ? string.Empty : step[2..];
            WindowsStableCustodyIssue deleted = custody.TryDeleteHeldEntry(
                relative,
                directory: step[0] == 'D');
            if (deleted != WindowsStableCustodyIssue.None)
            {
                return MapExecutionIssue(deleted);
            }
            progress.Mutated = true;
            _afterDelete?.Invoke(++progress.DeleteCount);
            cancellationToken.ThrowIfCancellationRequested();
        }
        WindowsStableCustodyResult remaining =
            WindowsStablePathCustody.TryAcquireImmutableTreeOrExactMissing(
                token.TreeRoot,
                new WindowsStableTreeLimits(0, 0, 0),
                cancellationToken);
        remaining.Custody?.Dispose();
        return remaining.IsExactChildMissing
            ? null
            : ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired;
    }

    private async ValueTask<ManagedSetupRecoveryExecutionPortOutcome?>
        RevalidateRollbackTreeStepAsync(
            FileSystemRecoveryToken token,
            bool treeCustodyHeld,
            CancellationToken cancellationToken)
    {
        if (!string.Equals(
                ObserveRawOrMissingIdentity(_statePath, cancellationToken),
                "missing",
                StringComparison.Ordinal) ||
            !string.Equals(
                ObserveRawOrMissingIdentity(_launcherStatePath, cancellationToken),
                "missing",
                StringComparison.Ordinal))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
        }
        if (token.Phase == ManagedSetupRecoveryPhase.Staging &&
            !IsExactFinalRootMissing(token, cancellationToken))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
        }
        StateFileObservation<RawFileEvidence> marker = await ObserveRawFileAsync(
            FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(token.ManagedRoot),
            cancellationToken).ConfigureAwait(false);
        if (marker.Issue is not null ||
            !string.Equals(marker.IdentityDigest, token.MarkerIdentity, StringComparison.Ordinal))
        {
            return ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
        }
        if (treeCustodyHeld)
        {
            return null;
        }
        StagingObservation staging = await ObserveStagingContainerAsync(
            token.ManagedRoot,
            expectedChild: null,
            token.TransactionId,
            RecoveryTreeLimits(token),
            cancellationToken).ConfigureAwait(false);
        if (staging.Issue is not null)
        {
            return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
        }
        string? expectedStaging = token.StagingInventory;
        return expectedStaging is not null &&
            string.Equals(staging.Inventory, expectedStaging, StringComparison.Ordinal) &&
            string.Equals(staging.Digest, ProofOrAbsentDigest(expectedStaging), StringComparison.Ordinal)
                ? null
                : ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
    }

    private static string? ExpectedStagingInventory(
        FileSystemRecoveryToken token,
        string treeInventory)
    {
        Dictionary<string, string> original = SplitLines(token.StagingInventory)
            .ToDictionary(ProofKey, static line => line, StringComparer.Ordinal);
        if (!original.TryGetValue("D|", out string? containerRoot))
        {
            return null;
        }
        var expected = new List<string> { containerRoot };
        foreach (string treeLine in SplitLines(treeInventory))
        {
            string key = ProofKey(treeLine);
            string path = key.Length == 2 ? token.TransactionId :
                string.Concat(token.TransactionId, "/", key[2..]);
            string stagingKey = string.Concat(key[..2], path);
            if (!original.TryGetValue(stagingKey, out string? line))
            {
                return null;
            }
            expected.Add(line);
        }
        expected.Sort(StringComparer.Ordinal);
        return string.Join('\n', expected);
    }

    private static string ProofKey(string line)
    {
        int delimiter = line.IndexOf('|', 2);
        return delimiter < 0 ? line : line[..delimiter];
    }

    private static string ProofOrAbsentDigest(string proof)
    {
        return string.IsNullOrEmpty(proof) ? "absent" : ProofDigest(proof);
    }

    private static bool IsExactFinalRootMissing(
        FileSystemRecoveryToken token,
        CancellationToken cancellationToken)
    {
        WindowsStableCustodyResult finalRoot =
            WindowsStablePathCustody.TryAcquireImmutableTreeOrExactMissing(
                token.ManagedRoot,
                new WindowsStableTreeLimits(0, 1, 0),
                cancellationToken);
        finalRoot.Custody?.Dispose();
        return finalRoot.IsExactChildMissing;
    }

    private async ValueTask<ManagedSetupRecoveryExecutionPortOutcome?> DeleteEmptyStagingAsync(
        FileSystemRecoveryToken token,
        RecoveryMutationProgress progress,
        CancellationToken cancellationToken)
    {
        StagingObservation observed = await ObserveStagingContainerAsync(
            token.ManagedRoot,
            expectedChild: null,
            token.TransactionId,
            RecoveryTreeLimits(token),
            cancellationToken).ConfigureAwait(false);
        if (observed.Issue is not null)
        {
            return ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired;
        }
        if (string.Equals(observed.Digest, "absent", StringComparison.Ordinal))
        {
            return null;
        }
        string container = FileSystemManagedInstallationRootProbe.GetStagingContainerPath(
            token.ManagedRoot);
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireDeleteCapableTree(
            container,
            new WindowsStableTreeLimits(0, 1, 0),
            cancellationToken);
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            return MapExecutionIssue(acquired.Issue);
        }
        WindowsStableCustodyIssue deleted;
        using (WindowsStablePathCustody custody = acquired.Custody!)
        {
            deleted = custody.TryDeleteHeldEntry(string.Empty, directory: true);
        }
        if (deleted != WindowsStableCustodyIssue.None)
        {
            return MapExecutionIssue(deleted);
        }
        progress.Mutated = true;
        _afterDelete?.Invoke(++progress.DeleteCount);
        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    private ManagedSetupRecoveryExecutionPortOutcome? DeleteExactFileIfPresent(
        string path,
        string expectedIdentity,
        RecoveryMutationProgress progress,
        CancellationToken cancellationToken,
        bool missingIsComplete = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(expectedIdentity, "missing", StringComparison.Ordinal))
        {
            return string.Equals(
                    ObserveRawOrMissingIdentity(path, cancellationToken),
                    "missing",
                    StringComparison.Ordinal)
                ? null
                : ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
        }
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireDeleteCapableFile(
            path,
            cancellationToken);
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            return acquired.IsExactChildMissing && missingIsComplete
                ? null
                : ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
        }
        WindowsStableCustodyIssue deleted;
        using (WindowsStablePathCustody custody = acquired.Custody!)
        {
            using FileStream stream = custody.OpenReadOnlyFile(Path.GetFileName(path));
            string digest = Convert.ToHexStringLower(SHA256.HashData(stream));
            if (!custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot) ||
                !string.Equals(
                    string.Concat(TreeDigest(snapshot!), ":", digest),
                    expectedIdentity,
                    StringComparison.Ordinal))
            {
                return ManagedSetupRecoveryExecutionPortOutcome.SourceChanged;
            }
            deleted = custody.TryDeleteHeldEntry(
                Path.GetFileName(path),
                directory: false,
                revalidateTopology: false);
        }
        if (deleted != WindowsStableCustodyIssue.None)
        {
            return MapExecutionIssue(deleted);
        }
        progress.Mutated = true;
        if (!missingIsComplete)
        {
            _afterDelete?.Invoke(++progress.DeleteCount);
            return WindowsStablePathCustody.TryObserveExactMissingFile(path, CancellationToken.None)
                ? null
                : ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired;
        }
        _afterDelete?.Invoke(++progress.DeleteCount);
        cancellationToken.ThrowIfCancellationRequested();
        return WindowsStablePathCustody.TryObserveExactMissingFile(path, cancellationToken)
            ? null
            : ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired;
    }

    private static async ValueTask<ExecutionTreeObservation> ObserveExactTreeAsync(
        string path,
        WindowsStableTreeLimits limits,
        CancellationToken cancellationToken)
    {
        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryAcquireImmutableTreeOrExactMissing(
                path,
                limits,
                cancellationToken);
        if (acquired.IsExactChildMissing)
        {
            return new(null, string.Empty, "absent", Issue: null);
        }
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            return new(null, null, null, MapExecutionIssue(acquired.Issue));
        }
        using WindowsStablePathCustody custody = acquired.Custody!;
        if (!custody.RevalidateClosedTree() ||
            !custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot))
        {
            return new(
                null,
                null,
                null,
                ManagedSetupRecoveryExecutionPortOutcome.SourceChanged);
        }
        string inventory = await SecureSnapshotProofAsync(
            custody,
            snapshot!,
            cancellationToken).ConfigureAwait(false);
        return custody.RevalidateClosedTree()
            ? new(snapshot, inventory, ProofDigest(inventory), Issue: null)
            : new(null, null, null, ManagedSetupRecoveryExecutionPortOutcome.SourceChanged);
    }

    private static string ObserveRawOrMissingIdentity(
        string path,
        CancellationToken cancellationToken)
    {
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireFile(
            path,
            cancellationToken: cancellationToken);
        if (acquired.IsExactChildMissing)
        {
            return "missing";
        }
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            return "unavailable";
        }
        using WindowsStablePathCustody custody = acquired.Custody!;
        using FileStream stream = custody.OpenReadOnlyFile(Path.GetFileName(path));
        string digest = Convert.ToHexStringLower(SHA256.HashData(stream));
        return custody.RevalidateClosedTree() &&
            custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot)
                ? string.Concat(TreeDigest(snapshot!), ":", digest)
                : "changed";
    }

    private static WindowsStableTreeLimits RecoveryTreeLimits(
        ManagedSetupRecoveryPayloadIdentity payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new(
            checked(FileSystemManagedVersionRepository.MaximumInstalledFiles + 3),
            checked(FileSystemManagedVersionRepository.MaximumInstalledDirectories + 3),
            checked(
                FileSystemManagedVersionRepository.MaximumInstalledBytes +
                payload.LauncherSize +
                payload.BootstrapSize +
                (1024 * 1024)));
    }

    private static bool HasAdmittedExecutableSizes(ManagedSetupRecoveryPayloadIdentity payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        const long maximum = ManagedImmutableBootstrapIdentity.MaximumExecutableBytes;
        return payload.LauncherSize is > 0 and <= maximum &&
            payload.BootstrapSize is > 0 and <= maximum;
    }

    private static WindowsStableTreeLimits RecoveryTreeLimits(FileSystemRecoveryToken token)
    {
        return new(token.MaximumFiles, token.MaximumDirectories, token.MaximumBytes);
    }

    private static string[] SplitLines(string value)
    {
        return string.IsNullOrEmpty(value)
            ? []
            : value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static ManagedSetupRecoveryExecutionPortOutcome MapExecutionIssue(
        WindowsStableCustodyIssue issue)
    {
        return issue switch
        {
            WindowsStableCustodyIssue.AccessDenied =>
                ManagedSetupRecoveryExecutionPortOutcome.PermissionDenied,
            WindowsStableCustodyIssue.Changed =>
                ManagedSetupRecoveryExecutionPortOutcome.SourceChanged,
            WindowsStableCustodyIssue.InvalidPath or WindowsStableCustodyIssue.ReparsePoint =>
                ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired,
            WindowsStableCustodyIssue.Contended or WindowsStableCustodyIssue.Unavailable =>
                ManagedSetupRecoveryExecutionPortOutcome.StateUnavailable,
            WindowsStableCustodyIssue.None => throw new InvalidOperationException(
                "Successful custody cannot map to a recovery failure."),
            _ => throw new InvalidOperationException("Stable custody returned an undefined issue."),
        };
    }

    private static ManagedSetupRecoveryExecutionPortOutcome MapExecutionIssue(
        ManagedSetupRecoveryEvidenceIssue issue)
    {
        return issue switch
        {
            ManagedSetupRecoveryEvidenceIssue.PermissionDenied =>
                ManagedSetupRecoveryExecutionPortOutcome.PermissionDenied,
            ManagedSetupRecoveryEvidenceIssue.StateUnavailable =>
                ManagedSetupRecoveryExecutionPortOutcome.StateUnavailable,
            ManagedSetupRecoveryEvidenceIssue.SourceChanged =>
                ManagedSetupRecoveryExecutionPortOutcome.SourceChanged,
            ManagedSetupRecoveryEvidenceIssue.Invalid =>
                ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired,
            ManagedSetupRecoveryEvidenceIssue.None => throw new InvalidOperationException(
                "Successful recovery evidence cannot map to an execution failure."),
            _ => throw new InvalidOperationException("Recovery evidence returned an undefined issue."),
        };
    }

    private static ManagedSetupRecoveryEvidenceIssue MapEvidenceIssue(
        WindowsStableCustodyIssue issue)
    {
        return issue switch
        {
            WindowsStableCustodyIssue.AccessDenied =>
                ManagedSetupRecoveryEvidenceIssue.PermissionDenied,
            WindowsStableCustodyIssue.Changed => ManagedSetupRecoveryEvidenceIssue.SourceChanged,
            WindowsStableCustodyIssue.InvalidPath or WindowsStableCustodyIssue.ReparsePoint =>
                ManagedSetupRecoveryEvidenceIssue.Invalid,
            WindowsStableCustodyIssue.Contended or WindowsStableCustodyIssue.Unavailable =>
                ManagedSetupRecoveryEvidenceIssue.StateUnavailable,
            WindowsStableCustodyIssue.None => throw new InvalidOperationException(
                "Successful custody omitted its owner."),
            _ => throw new InvalidOperationException("Stable custody returned an undefined issue."),
        };
    }

    private static ManagedSetupRecoveryEvidenceIssue MapEvidenceIssue(InstalledLauncherIssue issue)
    {
        return issue switch
        {
            InstalledLauncherIssue.Unavailable => ManagedSetupRecoveryEvidenceIssue.StateUnavailable,
            InstalledLauncherIssue.Tampered or InstalledLauncherIssue.InvalidManifest =>
                ManagedSetupRecoveryEvidenceIssue.SourceChanged,
            InstalledLauncherIssue.ProtocolMismatch or InstalledLauncherIssue.UnsafePath =>
                ManagedSetupRecoveryEvidenceIssue.Invalid,
            InstalledLauncherIssue.None => throw new InvalidOperationException(
                "Verified Launcher omitted its identity."),
            _ => throw new InvalidOperationException("Launcher verification returned an undefined issue."),
        };
    }

    private static string TransactionDigest(ManagedSetupRecoveryTransaction transaction)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, transaction.TransactionId);
        Append(hash, transaction.ManagedRootIdentity);
        Append(hash, transaction.StatePathIdentity);
        Append(hash, transaction.Phase.ToString());
        foreach (string path in transaction.OwnedPaths)
        {
            Append(hash, path);
        }
        Append(hash, transaction.Payload.LauncherSize);
        Append(hash, transaction.Payload.LauncherSha256);
        Append(hash, transaction.Payload.DescriptorSize);
        Append(hash, transaction.Payload.DescriptorSha256);
        Append(hash, transaction.Payload.BootstrapFileName);
        Append(hash, transaction.Payload.BootstrapSize);
        Append(hash, transaction.Payload.BootstrapSha256);
        Append(hash, transaction.Candidate.RegistryRevision);
        Append(hash, transaction.Candidate.RegistryDigest);
        Append(hash, transaction.Candidate.CatalogSchemaVersion);
        Append(hash, transaction.Candidate.CatalogLatestVersion);
        Append(hash, transaction.Candidate.CatalogDigest);
        Append(hash, transaction.Candidate.CatalogPath);
        Append(hash, transaction.Candidate.RegistryId);
        Append(hash, transaction.Candidate.SourceRoot);
        Append(hash, transaction.Candidate.SourceStatus);
        Append(hash, transaction.Candidate.Version);
        Append(hash, transaction.Candidate.PackagePath);
        Append(hash, transaction.Candidate.PackageSize);
        Append(hash, transaction.Candidate.PackageSha256);
        Append(hash, transaction.Candidate.ReleaseManifestSha256);
        Append(hash, transaction.Candidate.EntryIdentity);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string TreeDigest(WindowsStableOwnedTreeSnapshot snapshot)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string path, WindowsStablePathIdentity identity) in snapshot.Files
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            Append(hash, "F");
            Append(hash, path);
            Append(hash, identity.VolumeSerialNumber);
            Append(hash, identity.FileIdLow);
            Append(hash, identity.FileIdHigh);
        }
        foreach ((string path, WindowsStablePathIdentity identity) in snapshot.Directories
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            Append(hash, "D");
            Append(hash, path);
            Append(hash, identity.VolumeSerialNumber);
            Append(hash, identity.FileIdLow);
            Append(hash, identity.FileIdHigh);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static async ValueTask<string> SecureSnapshotProofAsync(
        WindowsStablePathCustody custody,
        WindowsStableOwnedTreeSnapshot snapshot,
        CancellationToken cancellationToken,
        Action<long>? afterHashedFile = null)
    {
        var lines = new List<string>(snapshot.Files.Count + snapshot.Directories.Count);
        foreach ((string path, WindowsStablePathIdentity identity) in snapshot.Files
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            using FileStream stream = custody.OpenReadOnlyFile(path);
            stream.Position = 0;
            long length = stream.Length;
            string sha256 = Convert.ToHexStringLower(
                await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            afterHashedFile?.Invoke(length);
            lines.Add(string.Join('|',
                "F",
                path,
                identity.VolumeSerialNumber.ToString("x16", CultureInfo.InvariantCulture),
                identity.FileIdLow.ToString("x16", CultureInfo.InvariantCulture),
                identity.FileIdHigh.ToString("x16", CultureInfo.InvariantCulture),
                length.ToString(CultureInfo.InvariantCulture),
                sha256));
        }
        lines.AddRange(snapshot.Directories.Select(static pair =>
            ProofLine('D', pair.Key, pair.Value)));
        lines.Sort(StringComparer.Ordinal);
        return string.Join('\n', lines);
    }

    private static string ProofDigest(string proof)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(proof)));
    }

    private static string ProofLine(
        char kind,
        string path,
        WindowsStablePathIdentity identity)
    {
        return string.Join('|',
            kind,
            path,
            identity.VolumeSerialNumber.ToString("x16", CultureInfo.InvariantCulture),
            identity.FileIdLow.ToString("x16", CultureInfo.InvariantCulture),
            identity.FileIdHigh.ToString("x16", CultureInfo.InvariantCulture));
    }

    private static string TreeSteps(
        WindowsStableOwnedTreeSnapshot snapshot,
        ManagedAppVersion version)
    {
        return string.Join('\n', BuildTreeSteps(snapshot.Files.Keys, snapshot.Directories.Keys, version));
    }

    private static string[] BuildTreeSteps(
        IEnumerable<string> files,
        IEnumerable<string> directories,
        ManagedAppVersion version)
    {
        string[] filePaths = [.. files];
        string[] directoryPaths = [.. directories];
        string versionRoot = string.Join(
            '/',
            FileSystemManagedVersionRepository.VersionsDirectoryName,
            version.ToString());
        string manifest = string.Concat(versionRoot, "/RELEASE-MANIFEST.json");
        string[] fixedSkeleton =
        [
            versionRoot,
            FileSystemManagedVersionRepository.VersionsDirectoryName,
            string.Empty,
        ];
        IEnumerable<string> ordinaryFiles = filePaths
            .Where(path => !string.Equals(path, manifest, StringComparison.Ordinal))
            .OrderByDescending(PathDepth)
            .ThenBy(static path => path, StringComparer.Ordinal)
            .Select(static path => string.Concat("F|", path));
        IEnumerable<string> nonterminalDirectories = directoryPaths
            .Except(fixedSkeleton, ManagedPathSafety.PathComparer)
            .OrderByDescending(PathDepth)
            .ThenBy(static path => path, StringComparer.Ordinal)
            .Select(static path => string.Concat("D|", path));
        IEnumerable<string> manifestStep = filePaths.Contains(manifest, ManagedPathSafety.PathComparer)
            ? [string.Concat("F|", manifest)]
            : [];
        IEnumerable<string> skeletonSteps = fixedSkeleton
            .Where(path => directoryPaths.Contains(path, ManagedPathSafety.PathComparer))
            .Select(static path => string.Concat("D|", path));
        return [.. ordinaryFiles
            .Concat(nonterminalDirectories)
            .Concat(manifestStep)
            .Concat(skeletonSteps)];
    }

    private static int PathDepth(string path)
    {
        return string.IsNullOrEmpty(path) ? 0 : path.Count(static value => value == '/') + 1;
    }

    private static string LauncherStateDigest(LauncherBootstrapStateLoadResult result)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, result.Issue.ToString());
        if (result.State is { } state)
        {
            Append(hash, state.ManagedRootIdentity);
            Append(hash, state.Active);
            Append(hash, state.LastKnownGood);
            Append(hash, state.Pending?.Candidate);
            Append(hash, state.Pending?.PreviousActive);
            Append(hash, state.Pending?.PreviousLastKnownGood);
            Append(hash, state.Pending?.Phase.ToString() ?? string.Empty);
            Append(hash, state.Failed);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string LauncherDigest(ManagedLauncherIdentity? launcher)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, launcher);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static async ValueTask<StateFileObservation<T>> ObserveStateFileAsync<T>(
        string path,
        Func<CancellationToken, ValueTask<T>> load,
        Func<T, bool> isMissing,
        CancellationToken cancellationToken)
        where T : class
    {
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireFile(
            path,
            cancellationToken: cancellationToken);
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            if (!acquired.IsExactChildMissing)
            {
                return new(null, null, MapEvidenceIssue(acquired.Issue));
            }
            T missing = await load(cancellationToken).ConfigureAwait(false);
            return isMissing(missing)
                ? new(missing, "missing", Issue: null)
                : new(null, null, ManagedSetupRecoveryEvidenceIssue.SourceChanged);
        }

        using WindowsStablePathCustody custody = acquired.Custody!;
        T value = await load(cancellationToken).ConfigureAwait(false);
        if (isMissing(value) ||
            !custody.RevalidateClosedTree() ||
            !custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot))
        {
            return new(null, null, ManagedSetupRecoveryEvidenceIssue.SourceChanged);
        }
        using FileStream stream = custody.OpenReadOnlyFile(Path.GetFileName(path));
        string bytesDigest = Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        return new(
            value,
            string.Concat(TreeDigest(snapshot!), ":", bytesDigest),
            Issue: null);
    }

    private static async ValueTask<StateFileObservation<RawFileEvidence>> ObserveRawFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireFile(
            path,
            cancellationToken: cancellationToken);
        if (!acquired.IsAcquired)
        {
            acquired.Custody?.Dispose();
            return new(
                null,
                null,
                acquired.IsExactChildMissing
                    ? ManagedSetupRecoveryEvidenceIssue.SourceChanged
                    : MapEvidenceIssue(acquired.Issue));
        }
        using WindowsStablePathCustody custody = acquired.Custody!;
        using FileStream stream = custody.OpenReadOnlyFile(Path.GetFileName(path));
        string digest = Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        return custody.RevalidateClosedTree() &&
            custody.TryCreateOwnedSnapshot(out WindowsStableOwnedTreeSnapshot? snapshot)
                ? new(new(digest), string.Concat(TreeDigest(snapshot!), ":", digest), Issue: null)
                : new(null, null, ManagedSetupRecoveryEvidenceIssue.SourceChanged);
    }

    private static void Append(IncrementalHash hash, ManagedLauncherIdentity? launcher)
    {
        if (launcher is null)
        {
            Append(hash, string.Empty);
            return;
        }
        Append(hash, launcher.OwnerAppVersion.ToString());
        Append(hash, launcher.OwnerAdmissionIdentity);
        Append(hash, launcher.OwnerReleaseManifestSha256);
        Append(hash, launcher.LauncherVersion.ToString());
        Append(hash, launcher.ProtocolVersion);
        Append(hash, launcher.ExecutableRelativePath);
        Append(hash, launcher.Size);
        Append(hash, launcher.Sha256);
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static void Append(IncrementalHash hash, long value)
    {
        Append(hash, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Append(IncrementalHash hash, int value)
    {
        Append(hash, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Append(IncrementalHash hash, ulong value)
    {
        Append(hash, value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed record FileSystemRecoveryToken(
        string StatePath,
        string ManagedRoot,
        string TransactionId,
        ManagedSetupRecoveryPhase Phase,
        string Version,
        string TransactionSha256,
        string TreeRoot,
        string TreeSha256,
        string TreeInventory,
        string TreeSteps,
        string StagingInventory,
        string StagingSha256,
        int MaximumFiles,
        int MaximumDirectories,
        long MaximumBytes,
        string MarkerIdentity,
        string ApplicationStateIdentity,
        string LauncherStateIdentity,
        string LauncherStateSha256,
        string LauncherSha256) : ManagedSetupRecoveryExecutionToken;

    private sealed record PrefixObservation(
        bool IsExact,
        WindowsStableOwnedTreeSnapshot? Snapshot,
        string? TreeDigest,
        string? TreeInventory,
        string? StagingInventory,
        string? StagingDigest,
        ManagedSetupRecoveryEvidenceIssue? Issue);

    private sealed record StagingObservation(
        bool IsEmptyOrAbsent,
        string? Inventory,
        string? Digest,
        ManagedSetupRecoveryEvidenceIssue? Issue);

    private sealed record StateFileObservation<T>(
        T? Value,
        string? IdentityDigest,
        ManagedSetupRecoveryEvidenceIssue? Issue)
        where T : class;

    private sealed record RawFileEvidence(string Sha256);

    private sealed record ExecutionTreeObservation(
        WindowsStableOwnedTreeSnapshot? Snapshot,
        string? Inventory,
        string? Digest,
        ManagedSetupRecoveryExecutionPortOutcome? Issue);

    private sealed record PreparedRollbackTree(
        WindowsStablePathCustody? Custody,
        ManagedSetupRecoveryExecutionPortOutcome? Issue);

    private sealed class RecoveryMutationProgress
    {
        internal bool Mutated { get; set; }
        internal int DeleteCount { get; set; }
    }
}
