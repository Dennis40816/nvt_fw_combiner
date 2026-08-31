using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Stable issue from read-only fresh-install candidate admission.</summary>
public enum FreshInstallationCandidateIssue
{
    /// <summary>One complete immutable candidate was admitted.</summary>
    None,
    /// <summary>No fixed production Registry is configured.</summary>
    RegistryNotConfigured,
    /// <summary>The Registry, Catalog, or package could not be observed completely.</summary>
    SourceUnavailable,
    /// <summary>The Registry or Catalog publication failed strict authority checks.</summary>
    SourceRejected,
    /// <summary>No automatic Registry entry supplied a completely verified package.</summary>
    CandidateUnavailable,
    /// <summary>The captured Registry/Catalog/package identity changed during exact revalidation.</summary>
    SourceChanged,
}

/// <summary>Closed value identity for one Registry/Catalog/package publication.</summary>
public sealed record FreshInstallationCandidateIdentity
{
    /// <summary>Creates one completely validated publication identity.</summary>
    public FreshInstallationCandidateIdentity(
        string registryId,
        long registryRevision,
        string registryDigest,
        int catalogSchemaVersion,
        ManagedAppVersion catalogLatestVersion,
        string catalogDigest,
        string catalogPath,
        string sourceRoot,
        UpdateSourceRegistryEntryStatus sourceStatus,
        string packagePath,
        long packageSize,
        string packageSha256,
        string releaseManifestSha256)
    {
        if (string.IsNullOrWhiteSpace(registryId) || registryId.Length > 128 ||
            registryRevision <= 0 ||
            !UpdateSourceRegistrySnapshot.IsLowerSha256(registryDigest) ||
            catalogSchemaVersion <= 0 ||
            !UpdateSourceRegistrySnapshot.IsLowerSha256(catalogDigest) ||
            !IsExactCatalogPath(catalogPath, sourceRoot) ||
            !Enum.IsDefined(sourceStatus) ||
            !ManagedRelativePathRules.IsSafeFilePath(packagePath) ||
            packageSize <= 0 ||
            !UpdateSourceRegistrySnapshot.IsLowerSha256(packageSha256) ||
            !UpdateSourceRegistrySnapshot.IsLowerSha256(releaseManifestSha256))
        {
            throw new ArgumentException("Fresh installation candidate identity is invalid.");
        }

        RegistryId = registryId;
        RegistryRevision = registryRevision;
        RegistryDigest = registryDigest;
        CatalogSchemaVersion = catalogSchemaVersion;
        CatalogLatestVersion = catalogLatestVersion;
        CatalogDigest = catalogDigest;
        CatalogPath = catalogPath;
        SourceRoot = sourceRoot;
        SourceStatus = sourceStatus;
        PackagePath = packagePath;
        PackageSize = packageSize;
        PackageSha256 = packageSha256;
        ReleaseManifestSha256 = releaseManifestSha256;
    }

    /// <summary>Gets the Registry authority id.</summary>
    public string RegistryId { get; }
    /// <summary>Gets the monotonic Registry revision.</summary>
    public long RegistryRevision { get; }
    /// <summary>Gets the Registry content digest.</summary>
    public string RegistryDigest { get; }
    /// <summary>Gets the asserted Catalog schema version.</summary>
    public int CatalogSchemaVersion { get; }
    /// <summary>Gets the asserted latest Catalog version.</summary>
    public ManagedAppVersion CatalogLatestVersion { get; }
    /// <summary>Gets the asserted Catalog digest.</summary>
    public string CatalogDigest { get; }
    /// <summary>Gets the exact Catalog file path.</summary>
    public string CatalogPath { get; }
    /// <summary>Gets the exact source root.</summary>
    public string SourceRoot { get; }
    /// <summary>Gets the Registry source status.</summary>
    public UpdateSourceRegistryEntryStatus SourceStatus { get; }
    /// <summary>Gets the exact Catalog-relative package path.</summary>
    public string PackagePath { get; }
    /// <summary>Gets the exact package length.</summary>
    public long PackageSize { get; }
    /// <summary>Gets the exact package digest.</summary>
    public string PackageSha256 { get; }
    /// <summary>Gets the exact inner release-manifest digest.</summary>
    public string ReleaseManifestSha256 { get; }
    private static bool IsExactCatalogPath(string? catalogPath, string? sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(catalogPath) || string.IsNullOrWhiteSpace(sourceRoot) ||
            !Path.IsPathFullyQualified(catalogPath) || !Path.IsPathFullyQualified(sourceRoot))
        {
            return false;
        }
        try
        {
            string catalog = Path.GetFullPath(catalogPath);
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot));
            string? parent = Path.GetDirectoryName(catalog);
            return parent is not null && string.Equals(
                Path.TrimEndingDirectorySeparator(parent),
                root,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

}

/// <summary>Immutable fresh-install candidate plus its already admitted package values.</summary>
public sealed record FreshInstallationCandidate
{
    /// <summary>Creates one cross-checked immutable fresh-install token.</summary>
    public FreshInstallationCandidate(
        FreshInstallationCandidateIdentity identity,
        UpdateCatalogVersionSnapshot package,
        VerifiedUpdateCandidate verifiedCandidate)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(verifiedCandidate);
        if (identity.CatalogLatestVersion != package.Version ||
            !string.Equals(identity.PackagePath, package.PackagePath.Value, StringComparison.Ordinal) ||
            identity.PackageSize != package.PackageSize ||
            !string.Equals(identity.PackageSha256, package.PackageSha256, StringComparison.Ordinal) ||
            !string.Equals(
                identity.ReleaseManifestSha256,
                package.ReleaseManifestSha256,
                StringComparison.Ordinal) ||
            verifiedCandidate.Version != package.Version ||
            !string.Equals(
                verifiedCandidate.AdmissionIdentity,
                package.Identity,
                StringComparison.Ordinal) ||
            !string.Equals(verifiedCandidate.ReleaseNotes, package.ReleaseNotes, StringComparison.Ordinal))
        {
            throw new ArgumentException("Fresh installation candidate values disagree.");
        }
        Identity = identity;
        Package = package;
    }

    /// <summary>Gets the closed publication identity.</summary>
    public FreshInstallationCandidateIdentity Identity { get; }
    /// <summary>Gets the admitted Catalog package.</summary>
    public UpdateCatalogVersionSnapshot Package { get; }
}

/// <summary>Read-only fresh-install admission result.</summary>
public sealed record FreshInstallationCandidateResult(
    FreshInstallationCandidate? Candidate,
    FreshInstallationCandidateIssue Issue)
{
    /// <summary>Gets whether one complete immutable candidate was admitted.</summary>
    public bool IsSuccess => Candidate is not null && Issue == FreshInstallationCandidateIssue.None;
}

/// <summary>Narrow existing-owner seam used only after genuine Setup eligibility.</summary>
public interface IFreshInstallationCandidateSource
{
    /// <summary>Selects one complete latest compatible Registry/Catalog/package candidate.</summary>
    ValueTask<FreshInstallationCandidateResult> InspectFreshInstallationAsync(
        CancellationToken cancellationToken);

    /// <summary>Revalidates the exact captured authority without selecting another candidate.</summary>
    ValueTask<FreshInstallationCandidateResult> ReverifyFreshInstallationAsync(
        FreshInstallationCandidate expected,
        CancellationToken cancellationToken);
}

/// <summary>Stable issue from inspecting or capturing the distribution Launcher payload.</summary>
public enum ManagedDistributionPayloadIssue
{
    /// <summary>The exact closed payload identity or capture is available.</summary>
    None,
    /// <summary>The running distribution payload could not be observed completely.</summary>
    Unavailable,
    /// <summary>The descriptor, Launcher, or embedded Bootstrap failed strict admission.</summary>
    Invalid,
    /// <summary>The exact payload changed after the user reviewed the install plan.</summary>
    Changed,
}

/// <summary>Closed identity of the running Launcher, descriptor, and embedded Bootstrap.</summary>
public sealed record ManagedDistributionPayloadIdentity
{
    /// <summary>Creates one validated distribution payload identity.</summary>
    public ManagedDistributionPayloadIdentity(
        ManagedAppVersion launcherVersion,
        string sourceCommit,
        long launcherSize,
        string launcherSha256,
        long descriptorSize,
        string descriptorSha256,
        ManagedImmutableBootstrapIdentity bootstrap)
    {
        if (!IsLowerHex(sourceCommit, 40) ||
            launcherSize <= 0 ||
            !IsLowerHex(launcherSha256, 64) ||
            descriptorSize is <= 0 or > 65_536 ||
            !IsLowerHex(descriptorSha256, 64))
        {
            throw new ArgumentException("Distribution Launcher payload identity is invalid.");
        }
        LauncherVersion = launcherVersion;
        SourceCommit = sourceCommit;
        LauncherSize = launcherSize;
        LauncherSha256 = launcherSha256;
        DescriptorSize = descriptorSize;
        DescriptorSha256 = descriptorSha256;
        Bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
    }

    /// <summary>Gets the distribution Launcher version.</summary>
    public ManagedAppVersion LauncherVersion { get; }
    /// <summary>Gets the exact release source commit.</summary>
    public string SourceCommit { get; }
    /// <summary>Gets the exact running Launcher length.</summary>
    public long LauncherSize { get; }
    /// <summary>Gets the exact running Launcher digest.</summary>
    public string LauncherSha256 { get; }
    /// <summary>Gets the exact embedded descriptor length.</summary>
    public long DescriptorSize { get; }
    /// <summary>Gets the exact embedded descriptor digest.</summary>
    public string DescriptorSha256 { get; }
    /// <summary>Gets the exact descriptor-bound Root Bootstrap identity.</summary>
    public ManagedImmutableBootstrapIdentity Bootstrap { get; }

    private static bool IsLowerHex(string? value, int length)
    {
        return value?.Length == length &&
            value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}

/// <summary>Stable read-only payload inspection result.</summary>
public sealed record ManagedDistributionPayloadInspectionResult(
    ManagedDistributionPayloadIdentity? Identity,
    ManagedDistributionPayloadIssue Issue)
{
    /// <summary>Gets whether one complete payload identity was admitted.</summary>
    public bool IsSuccess => Identity is not null && Issue == ManagedDistributionPayloadIssue.None;
}

/// <summary>
/// Lightweight entry admission projected without reading or hashing Bootstrap content.
/// </summary>
public sealed record ManagedDistributionPayloadEntryAdmissionResult(
    ManagedAppVersion LauncherVersion,
    ManagedImmutableBootstrapIdentity? Bootstrap,
    ManagedDistributionPayloadIssue Issue)
{
    /// <summary>Gets whether the descriptor and Bootstrap resource metadata were admitted.</summary>
    public bool IsSuccess => Bootstrap is not null && Issue == ManagedDistributionPayloadIssue.None;
}

/// <summary>Opaque stable custody over the exact bytes copied into a fresh installation.</summary>
public interface IManagedDistributionPayloadCapture : IDisposable
{
    /// <summary>Gets the exact captured identity.</summary>
    ManagedDistributionPayloadIdentity Identity { get; }
}

/// <summary>Stable exact payload-capture result.</summary>
public sealed record ManagedDistributionPayloadCaptureResult(
    IManagedDistributionPayloadCapture? Capture,
    ManagedDistributionPayloadIssue Issue)
{
    /// <summary>Gets whether the exact expected payload is held for materialization.</summary>
    public bool IsSuccess => Capture is not null && Issue == ManagedDistributionPayloadIssue.None;
}

/// <summary>Infrastructure seam for inspecting and stably capturing the running payload.</summary>
public interface IManagedDistributionPayloadSource
{
    /// <summary>
    /// Admits bounded descriptor and Bootstrap metadata for local entry without reading content.
    /// </summary>
    ValueTask<ManagedDistributionPayloadEntryAdmissionResult> AdmitEntryAsync(
        CancellationToken cancellationToken);

    /// <summary>Inspects the closed payload without retaining installation bytes.</summary>
    ValueTask<ManagedDistributionPayloadInspectionResult> InspectAsync(
        CancellationToken cancellationToken);

    /// <summary>Captures only the exact previously inspected payload through stable custody.</summary>
    ValueTask<ManagedDistributionPayloadCaptureResult> CaptureExactAsync(
        ManagedDistributionPayloadIdentity expected,
        CancellationToken cancellationToken);
}

/// <summary>Stable issue from atomically materializing one fresh managed root.</summary>
public enum ManagedFirstInstallationMaterializationIssue
{
    /// <summary>The exact closed root was atomically promoted.</summary>
    None,
    /// <summary>The destination is not an admitted local non-reparse root.</summary>
    InvalidDestination,
    /// <summary>The current user cannot materialize the selected root.</summary>
    PermissionDenied,
    /// <summary>The exact source package could not be observed completely.</summary>
    SourceUnavailable,
    /// <summary>The captured package no longer matches its admitted identity.</summary>
    SourceChanged,
    /// <summary>Staging or same-volume promotion did not complete.</summary>
    PromotionFailed,
    /// <summary>Existing or residual root facts require the recovery owner.</summary>
    RecoveryRequired,
    /// <summary>The complete transaction fact could not be persisted or verified.</summary>
    StateUnavailable,
}

/// <summary>Stable marker-operation result for one already promoted installation.</summary>
public enum ManagedFirstInstallationTransactionIssue
{
    /// <summary>The exact transaction phase was durably advanced or completed.</summary>
    None,
    /// <summary>The marker/root facts differ and require recovery.</summary>
    RecoveryRequired,
    /// <summary>The exact transaction fact could not be observed or persisted.</summary>
    StateUnavailable,
}

/// <summary>Opaque promoted-root transaction retained until Bootstrap READY finalization.</summary>
public interface IManagedPromotedFirstInstallation : IDisposable
{
    /// <summary>Gets the exact promoted managed root.</summary>
    string ManagedRoot { get; }
    /// <summary>Gets the exact admitted seed version.</summary>
    ManagedVersionAdmission Admission { get; }
    /// <summary>Records the irreversible pre-process phase under the state writer lease.</summary>
    ValueTask<ManagedFirstInstallationTransactionIssue> RecordBootstrapLaunchAsync(
        CancellationToken cancellationToken);
    /// <summary>
    /// Consumes the one post-record launch opportunity and returns independently owned custody
    /// for the exact promoted Bootstrap without reacquiring custody from its managed path.
    /// </summary>
    ValueTask<ManagedExecutableLaunchLeaseResult> AcquireBootstrapLaunchLeaseAsync(
        ManagedImmutableBootstrapIdentity expectedIdentity,
        CancellationToken cancellationToken);
    /// <summary>Removes only the exact marker after durable bound READY was proved.</summary>
    ValueTask<ManagedFirstInstallationTransactionIssue> CompleteAsync(
        CancellationToken cancellationToken);
}

/// <summary>Typed result from one atomic first-install root materialization.</summary>
public sealed record ManagedFirstInstallationMaterializationResult(
    IManagedPromotedFirstInstallation? Installation,
    ManagedFirstInstallationMaterializationIssue Issue)
{
    /// <summary>Gets whether one complete promoted root and exact marker are available.</summary>
    public bool IsSuccess =>
        Installation is not null && Issue == ManagedFirstInstallationMaterializationIssue.None;
}

/// <summary>Closed stage projected by the single first-install execution owner.</summary>
public enum ManagedFirstInstallationProgressStage
{
    /// <summary>The exact Registry, Catalog, and package token is being revalidated.</summary>
    RevalidatingSource,
    /// <summary>The admitted package bytes are being read and hashed from the update source.</summary>
    ReadingPackage,
    /// <summary>The admitted archive members are being verified without mutation.</summary>
    VerifyingPackage,
    /// <summary>The verified archive members are being copied into the held staging tree.</summary>
    InstallingPackage,
    /// <summary>The staged installed payload bytes are being verified.</summary>
    VerifyingInstallation,
    /// <summary>The verified root transaction is being finalized.</summary>
    FinalizingInstallation,
    /// <summary>The immutable Bootstrap and installed application are starting.</summary>
    StartingApplication,
}

/// <summary>
/// One immutable current first-install progress fact. Determinate work is stage-local and comes
/// from the authoritative operation; unrelated stages are never aggregated into an estimate.
/// </summary>
public readonly record struct ManagedFirstInstallationProgress
{
    /// <summary>Creates one validated determinate or indeterminate progress snapshot.</summary>
    public ManagedFirstInstallationProgress(
        ManagedFirstInstallationProgressStage stage,
        long completedWork,
        long? totalWork)
    {
        if (!Enum.IsDefined(stage) ||
            completedWork < 0 ||
            totalWork is <= 0 ||
            (totalWork is null && completedWork != 0) ||
            (totalWork is { } total && completedWork > total))
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedWork),
                "First-install progress requires a defined stage and valid completed/total work.");
        }

        Stage = stage;
        CompletedWork = completedWork;
        TotalWork = totalWork;
    }

    /// <summary>Gets the current closed stage.</summary>
    public ManagedFirstInstallationProgressStage Stage { get; }

    /// <summary>Gets completed bytes/items within the current stage.</summary>
    public long CompletedWork { get; }

    /// <summary>Gets total bytes/items when the authoritative operation knows that total.</summary>
    public long? TotalWork { get; }

    /// <summary>Gets one stage-local integer percentage, or null when the total is unknown.</summary>
    public int? Percent => TotalWork is { } total
        ? decimal.ToInt32(decimal.Floor(CompletedWork * 100m / total))
        : null;

    /// <summary>Creates one truthful unknown-total stage.</summary>
    public static ManagedFirstInstallationProgress Indeterminate(
        ManagedFirstInstallationProgressStage stage)
    {
        return new(stage, 0, null);
    }
}

/// <summary>Infrastructure-owned whole-root transaction that reuses the package repository.</summary>
public interface IManagedFirstInstallationRootMaterializer
{
    /// <summary>
    /// Proves that the exact destination parent is a writable local non-reparse directory.
    /// This operation leaves no persistent filesystem entry.
    /// </summary>
    ValueTask<ManagedFirstInstallationMaterializationIssue> AdmitDestinationAsync(
        string managedRoot,
        CancellationToken cancellationToken);

    /// <summary>Stages, verifies, and atomically promotes one exact fresh root.</summary>
    ValueTask<ManagedFirstInstallationMaterializationResult> MaterializeAsync(
        string managedRoot,
        string statePathIdentity,
        IManagedDistributionPayloadCapture payload,
        FreshInstallationCandidate candidate,
        VersionManagerState seed,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages, verifies, and atomically promotes one exact fresh root while projecting only
    /// authoritative progress from that same operation.
    /// </summary>
    ValueTask<ManagedFirstInstallationMaterializationResult> MaterializeAsync(
        string managedRoot,
        string statePathIdentity,
        IManagedDistributionPayloadCapture payload,
        FreshInstallationCandidate candidate,
        VersionManagerState seed,
        IProgress<ManagedFirstInstallationProgress>? progress,
        CancellationToken cancellationToken)
    {
        return MaterializeAsync(
            managedRoot,
            statePathIdentity,
            payload,
            candidate,
            seed,
            cancellationToken);
    }
}

/// <summary>Stable presentation outcome for first-install planning and execution.</summary>
public enum ManagedFirstInstallationOutcome
{
    /// <summary>One immutable plan is ready for user confirmation.</summary>
    ReadyToInstall,
    /// <summary>The confirmed immutable plan is being installed.</summary>
    Installing,
    /// <summary>The exact root completed Bootstrap READY and marker finalization.</summary>
    Completed,
    /// <summary>The distribution payload is absent or could not be read.</summary>
    PayloadUnavailable,
    /// <summary>The distribution payload failed strict admission.</summary>
    PayloadInvalid,
    /// <summary>The Registry, Catalog, or package cannot be observed completely.</summary>
    SourceUnavailable,
    /// <summary>The observed Registry or Catalog publication is invalid.</summary>
    SourceRejected,
    /// <summary>No compatible fully admitted package is available.</summary>
    CandidateUnavailable,
    /// <summary>The exact payload or source changed after planning.</summary>
    SourceChanged,
    /// <summary>The selected destination is not admitted.</summary>
    InvalidDestination,
    /// <summary>The selected destination cannot be inspected or written by the current user.</summary>
    PermissionDenied,
    /// <summary>Another process owns the version-state writer.</summary>
    Busy,
    /// <summary>Existing or residual facts require the recovery owner.</summary>
    RecoveryRequired,
    /// <summary>The root was promoted but Bootstrap could not complete first READY.</summary>
    InstalledButLaunchFailed,
    /// <summary>A complete state or transaction fact could not be observed or persisted.</summary>
    StateUnavailable,
    /// <summary>The caller cancelled the current planning or installation attempt.</summary>
    Cancelled,
}

/// <summary>Immutable user-reviewed first-install plan.</summary>
public sealed class ManagedFirstInstallationPlan
{
    internal ManagedFirstInstallationPlan(
        string managedRoot,
        string statePathIdentity,
        ManagedDistributionPayloadIdentity payload,
        FreshInstallationCandidate candidate)
    {
        ManagedRoot = managedRoot;
        StatePathIdentity = statePathIdentity;
        Payload = payload;
        Candidate = candidate;
    }

    /// <summary>Gets the exact normalized destination root.</summary>
    public string ManagedRoot { get; }
    /// <summary>Gets the exact canonical state-path identity.</summary>
    public string StatePathIdentity { get; }
    /// <summary>Gets the exact reviewed distribution payload.</summary>
    public ManagedDistributionPayloadIdentity Payload { get; }
    /// <summary>Gets the exact reviewed Registry/Catalog/package candidate.</summary>
    public FreshInstallationCandidate Candidate { get; }
}

/// <summary>Typed first-install planning result.</summary>
public sealed record ManagedFirstInstallationPlanResult(
    ManagedFirstInstallationPlan? Plan,
    ManagedFirstInstallationOutcome Outcome)
{
    /// <summary>Gets whether one exact plan is ready for confirmation.</summary>
    public bool IsReady => Plan is not null && Outcome == ManagedFirstInstallationOutcome.ReadyToInstall;
}

/// <summary>Exact post-promotion stage that prevented first application READY.</summary>
public enum ManagedFirstInstallationLaunchStage
{
    /// <summary>No post-promotion launch failure was observed.</summary>
    None,
    /// <summary>The immutable Root Bootstrap process could not be started safely.</summary>
    BootstrapStart,
    /// <summary>Root Bootstrap did not admit the exact version Launcher.</summary>
    LauncherAdmission,
    /// <summary>The admitted Launcher did not complete selected-application READY.</summary>
    ApplicationReady,
    /// <summary>
    /// Root promotion completed, but the Bootstrap-launch transaction was not yet durably recorded
    /// or its durable record operation was still in progress.
    /// </summary>
    PostPromotion,
}

/// <summary>Safe typed reason retained for one post-promotion launch failure.</summary>
public enum ManagedFirstInstallationLaunchIssue
{
    /// <summary>No launch issue was observed.</summary>
    None,
    /// <summary>The bounded operation did not complete in time.</summary>
    TimedOut,
    /// <summary>A process result violated its typed custody/result contract.</summary>
    InvalidReceipt,
    /// <summary>Another process owns the required launch transaction.</summary>
    Busy,
    /// <summary>The immutable Bootstrap identity failed verification.</summary>
    Damaged,
    /// <summary>The exact process could not be started.</summary>
    StartFailed,
    /// <summary>The required process result could not be observed safely.</summary>
    Unavailable,
    /// <summary>The installed state requires the recovery owner.</summary>
    RecoveryRequired,
    /// <summary>Root Bootstrap could not start the exact version Launcher.</summary>
    LaunchFailed,
    /// <summary>The pre-launch health result could not be observed safely.</summary>
    HealthUnavailable,
    /// <summary>The Bootstrap state is invalid.</summary>
    InvalidState,
    /// <summary>The Bootstrap state is bound to a different managed root.</summary>
    ManagedRootMismatch,
    /// <summary>An application mutation transaction is still pending.</summary>
    MutationPending,
    /// <summary>The installed version Launcher failed immutable verification.</summary>
    DamagedLauncher,
    /// <summary>The version Launcher protocol is incompatible with Bootstrap.</summary>
    ProtocolMismatch,
    /// <summary>No admitted last-known-good rollback target is available.</summary>
    RollbackUnavailable,
    /// <summary>The version state changed during the launch transaction.</summary>
    StateChanged,
    /// <summary>The version state could not be read or persisted.</summary>
    StateUnavailable,
    /// <summary>The immutable Bootstrap received invalid launch arguments.</summary>
    InvalidArguments,
    /// <summary>The immutable Bootstrap rejected an internal invariant.</summary>
    InvariantViolation,
    /// <summary>The immutable Bootstrap inherited an incomplete or invalid process context.</summary>
    InvalidInheritedContext,
    /// <summary>The parent did not authorize Bootstrap through the inherited start gate.</summary>
    StartNotAuthorized,
    /// <summary>The immutable Bootstrap returned an undefined terminal failure.</summary>
    UndefinedFailure,
    /// <summary>The immutable Bootstrap returned an unrecognized exit code.</summary>
    UnknownExit,
    /// <summary>The launched process tree could not be proven terminated.</summary>
    TerminationUnconfirmed,
    /// <summary>Only the last-known-good rollback, not the selected version, reported READY.</summary>
    RolledBack,
    /// <summary>The caller cancelled after the managed root was promoted.</summary>
    Cancelled,
}

/// <summary>Safe diagnostic retained from the exact Bootstrap/Launcher process boundary.</summary>
public sealed record ManagedFirstInstallationLaunchFailure(
    ManagedFirstInstallationLaunchStage Stage,
    ManagedFirstInstallationLaunchIssue Issue,
    int? ExitCode = null)
{
    /// <summary>Gets the exact non-empty post-promotion failure stage.</summary>
    public ManagedFirstInstallationLaunchStage Stage { get; init; } =
        !Enum.IsDefined(Stage) || Stage == ManagedFirstInstallationLaunchStage.None
            ? throw new ArgumentOutOfRangeException(nameof(Stage))
            : Stage;

    /// <summary>Gets the single authoritative path-free failure reason.</summary>
    public ManagedFirstInstallationLaunchIssue Issue { get; init; } =
        !Enum.IsDefined(Issue) || Issue == ManagedFirstInstallationLaunchIssue.None
            ? throw new ArgumentOutOfRangeException(nameof(Issue))
            : Issue;

    /// <summary>Gets the optional exit code only when it agrees with the typed failure reason.</summary>
    public int? ExitCode { get; init; } =
        HasValidFailureShape(Stage, Issue, ExitCode)
            ? ExitCode
            : throw new ArgumentException(
                "Bootstrap boundary, issue, and exit code contradict one another.",
                nameof(ExitCode));

    /// <summary>Gets whether the current record still contains one non-contradictory failure.</summary>
    public bool HasValidShape =>
        Enum.IsDefined(Stage) &&
        Enum.IsDefined(Issue) &&
        Stage != ManagedFirstInstallationLaunchStage.None &&
        Issue != ManagedFirstInstallationLaunchIssue.None &&
        HasValidFailureShape(Stage, Issue, ExitCode);

    internal bool IsCompatibleWithOutcome(ManagedFirstInstallationOutcome outcome)
    {
        if (!HasValidShape)
        {
            return false;
        }
        bool recoveryRequired =
            (Stage == ManagedFirstInstallationLaunchStage.PostPromotion && Issue is
                ManagedFirstInstallationLaunchIssue.RecoveryRequired or
                ManagedFirstInstallationLaunchIssue.StateUnavailable) ||
            (Stage == ManagedFirstInstallationLaunchStage.BootstrapStart &&
                Issue == ManagedFirstInstallationLaunchIssue.Damaged) ||
            (Stage == ManagedFirstInstallationLaunchStage.LauncherAdmission &&
                Issue is ManagedFirstInstallationLaunchIssue.RecoveryRequired or
                    ManagedFirstInstallationLaunchIssue.InvalidState or
                    ManagedFirstInstallationLaunchIssue.ManagedRootMismatch or
                    ManagedFirstInstallationLaunchIssue.MutationPending or
                    ManagedFirstInstallationLaunchIssue.DamagedLauncher or
                    ManagedFirstInstallationLaunchIssue.ProtocolMismatch);
        ManagedFirstInstallationOutcome expected = recoveryRequired
            ? ManagedFirstInstallationOutcome.RecoveryRequired
            : ManagedFirstInstallationOutcome.InstalledButLaunchFailed;
        return outcome == expected;
    }

    internal static ManagedFirstInstallationLaunchIssue MapBootstrapExitIssue(
        ImmutableBootstrapExitIssue issue)
    {
        return issue switch
        {
            ImmutableBootstrapExitIssue.Busy => ManagedFirstInstallationLaunchIssue.Busy,
            ImmutableBootstrapExitIssue.InvalidState => ManagedFirstInstallationLaunchIssue.InvalidState,
            ImmutableBootstrapExitIssue.ManagedRootMismatch =>
                ManagedFirstInstallationLaunchIssue.ManagedRootMismatch,
            ImmutableBootstrapExitIssue.MutationPending =>
                ManagedFirstInstallationLaunchIssue.MutationPending,
            ImmutableBootstrapExitIssue.DamagedLauncher =>
                ManagedFirstInstallationLaunchIssue.DamagedLauncher,
            ImmutableBootstrapExitIssue.ProtocolMismatch =>
                ManagedFirstInstallationLaunchIssue.ProtocolMismatch,
            ImmutableBootstrapExitIssue.StartFailed => ManagedFirstInstallationLaunchIssue.StartFailed,
            ImmutableBootstrapExitIssue.RollbackUnavailable =>
                ManagedFirstInstallationLaunchIssue.RollbackUnavailable,
            ImmutableBootstrapExitIssue.StateChanged => ManagedFirstInstallationLaunchIssue.StateChanged,
            ImmutableBootstrapExitIssue.StateUnavailable =>
                ManagedFirstInstallationLaunchIssue.StateUnavailable,
            ImmutableBootstrapExitIssue.TerminationUnconfirmed =>
                ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed,
            ImmutableBootstrapExitIssue.InvalidArguments =>
                ManagedFirstInstallationLaunchIssue.InvalidArguments,
            ImmutableBootstrapExitIssue.InvariantViolation =>
                ManagedFirstInstallationLaunchIssue.InvariantViolation,
            ImmutableBootstrapExitIssue.InvalidInheritedContext =>
                ManagedFirstInstallationLaunchIssue.InvalidInheritedContext,
            ImmutableBootstrapExitIssue.StartNotAuthorized =>
                ManagedFirstInstallationLaunchIssue.StartNotAuthorized,
            ImmutableBootstrapExitIssue.UndefinedFailure =>
                ManagedFirstInstallationLaunchIssue.UndefinedFailure,
            ImmutableBootstrapExitIssue.Unknown => ManagedFirstInstallationLaunchIssue.UnknownExit,
            ImmutableBootstrapExitIssue.None => throw new ArgumentOutOfRangeException(
                nameof(issue), issue, "A successful Bootstrap exit has no launch failure."),
            _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, "Issue is undefined."),
        };
    }

    private static bool HasValidFailureShape(
        ManagedFirstInstallationLaunchStage stage,
        ManagedFirstInstallationLaunchIssue issue,
        int? exitCode)
    {
        bool stageAllowsIssue = stage switch
        {
            ManagedFirstInstallationLaunchStage.PostPromotion =>
                exitCode is null && issue is
                    ManagedFirstInstallationLaunchIssue.RecoveryRequired or
                    ManagedFirstInstallationLaunchIssue.StateUnavailable or
                    ManagedFirstInstallationLaunchIssue.Cancelled,
            ManagedFirstInstallationLaunchStage.BootstrapStart =>
                exitCode is null && issue is
                    ManagedFirstInstallationLaunchIssue.TimedOut or
                    ManagedFirstInstallationLaunchIssue.InvalidReceipt or
                    ManagedFirstInstallationLaunchIssue.Busy or
                    ManagedFirstInstallationLaunchIssue.Damaged or
                    ManagedFirstInstallationLaunchIssue.StartFailed or
                    ManagedFirstInstallationLaunchIssue.Unavailable or
                    ManagedFirstInstallationLaunchIssue.Cancelled,
            ManagedFirstInstallationLaunchStage.LauncherAdmission => issue is
                ManagedFirstInstallationLaunchIssue.TimedOut or
                ManagedFirstInstallationLaunchIssue.InvalidReceipt or
                ManagedFirstInstallationLaunchIssue.Busy or
                ManagedFirstInstallationLaunchIssue.HealthUnavailable or
                ManagedFirstInstallationLaunchIssue.InvalidState or
                ManagedFirstInstallationLaunchIssue.ManagedRootMismatch or
                ManagedFirstInstallationLaunchIssue.MutationPending or
                ManagedFirstInstallationLaunchIssue.DamagedLauncher or
                ManagedFirstInstallationLaunchIssue.ProtocolMismatch or
                ManagedFirstInstallationLaunchIssue.StartFailed or
                ManagedFirstInstallationLaunchIssue.RollbackUnavailable or
                ManagedFirstInstallationLaunchIssue.StateChanged or
                ManagedFirstInstallationLaunchIssue.StateUnavailable or
                ManagedFirstInstallationLaunchIssue.InvalidArguments or
                ManagedFirstInstallationLaunchIssue.InvariantViolation or
                ManagedFirstInstallationLaunchIssue.InvalidInheritedContext or
                ManagedFirstInstallationLaunchIssue.StartNotAuthorized or
                ManagedFirstInstallationLaunchIssue.UndefinedFailure or
                ManagedFirstInstallationLaunchIssue.UnknownExit or
                ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed or
                ManagedFirstInstallationLaunchIssue.Cancelled,
            ManagedFirstInstallationLaunchStage.ApplicationReady => issue is
                ManagedFirstInstallationLaunchIssue.TimedOut or
                ManagedFirstInstallationLaunchIssue.InvalidReceipt or
                ManagedFirstInstallationLaunchIssue.Busy or
                ManagedFirstInstallationLaunchIssue.Unavailable or
                ManagedFirstInstallationLaunchIssue.InvalidState or
                ManagedFirstInstallationLaunchIssue.ManagedRootMismatch or
                ManagedFirstInstallationLaunchIssue.MutationPending or
                ManagedFirstInstallationLaunchIssue.DamagedLauncher or
                ManagedFirstInstallationLaunchIssue.ProtocolMismatch or
                ManagedFirstInstallationLaunchIssue.StartFailed or
                ManagedFirstInstallationLaunchIssue.RollbackUnavailable or
                ManagedFirstInstallationLaunchIssue.StateChanged or
                ManagedFirstInstallationLaunchIssue.StateUnavailable or
                ManagedFirstInstallationLaunchIssue.InvalidArguments or
                ManagedFirstInstallationLaunchIssue.InvariantViolation or
                ManagedFirstInstallationLaunchIssue.InvalidInheritedContext or
                ManagedFirstInstallationLaunchIssue.StartNotAuthorized or
                ManagedFirstInstallationLaunchIssue.UndefinedFailure or
                ManagedFirstInstallationLaunchIssue.UnknownExit or
                ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed or
                ManagedFirstInstallationLaunchIssue.RolledBack or
                ManagedFirstInstallationLaunchIssue.Cancelled,
            ManagedFirstInstallationLaunchStage.None => false,
            _ => false,
        };
        bool hasExpectedExitPresence = stage switch
        {
            ManagedFirstInstallationLaunchStage.PostPromotion or
            ManagedFirstInstallationLaunchStage.BootstrapStart => exitCode is null,
            ManagedFirstInstallationLaunchStage.LauncherAdmission when issue is
                ManagedFirstInstallationLaunchIssue.StartFailed or
                ManagedFirstInstallationLaunchIssue.InvalidReceipt or
                ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed => true,
            ManagedFirstInstallationLaunchStage.LauncherAdmission when issue is
                ManagedFirstInstallationLaunchIssue.TimedOut or
                ManagedFirstInstallationLaunchIssue.HealthUnavailable or
                ManagedFirstInstallationLaunchIssue.Cancelled => exitCode is null,
            ManagedFirstInstallationLaunchStage.LauncherAdmission => exitCode is not null,
            ManagedFirstInstallationLaunchStage.ApplicationReady when
                issue is ManagedFirstInstallationLaunchIssue.InvalidReceipt or
                    ManagedFirstInstallationLaunchIssue.TerminationUnconfirmed => true,
            ManagedFirstInstallationLaunchStage.ApplicationReady when issue is
                ManagedFirstInstallationLaunchIssue.TimedOut or
                ManagedFirstInstallationLaunchIssue.Unavailable or
                ManagedFirstInstallationLaunchIssue.Cancelled => exitCode is null,
            ManagedFirstInstallationLaunchStage.ApplicationReady => exitCode is not null,
            ManagedFirstInstallationLaunchStage.None => false,
            _ => false,
        };
        return stageAllowsIssue &&
            hasExpectedExitPresence &&
            (exitCode is null || issue == ManagedFirstInstallationLaunchIssue.InvalidReceipt ||
                (exitCode == ImmutableBootstrapExitCodeCodec.RolledBack
                ? issue == ManagedFirstInstallationLaunchIssue.RolledBack
                : exitCode != ImmutableBootstrapExitCodeCodec.Ready &&
                    issue == MapBootstrapExitIssue(
                        ImmutableBootstrapExitCodeCodec.DecodeFailure(exitCode.Value))));
    }
}

/// <summary>Typed first-install execution result.</summary>
public sealed record ManagedFirstInstallationResult(
    ManagedFirstInstallationOutcome Outcome,
    string ManagedRoot,
    ManagedAppVersion Version,
    ManagedFirstInstallationLaunchFailure? LaunchFailure = null)
{
    /// <summary>Gets the defined execution outcome.</summary>
    public ManagedFirstInstallationOutcome Outcome { get; init; } =
        Enum.IsDefined(Outcome)
            ? Outcome
            : throw new ArgumentOutOfRangeException(nameof(Outcome));

    /// <summary>Gets the exact post-promotion failure, when this result is launch-terminal.</summary>
    public ManagedFirstInstallationLaunchFailure? LaunchFailure { get; init; } =
        IsValidLaunchFailureShape(Outcome, LaunchFailure)
            ? LaunchFailure
            : throw new ArgumentException(
                "First-install result carries an invalid launch-failure shape.",
                nameof(LaunchFailure));

    /// <summary>Gets whether this is one complete terminal presentation result.</summary>
    public bool HasValidShape =>
        Enum.IsDefined(Outcome) &&
        Outcome is not ManagedFirstInstallationOutcome.ReadyToInstall and
            not ManagedFirstInstallationOutcome.Installing &&
        (LaunchFailure is not { } failure
            ? Outcome != ManagedFirstInstallationOutcome.InstalledButLaunchFailed
            : (Outcome is ManagedFirstInstallationOutcome.InstalledButLaunchFailed or
                ManagedFirstInstallationOutcome.RecoveryRequired) &&
                failure.IsCompatibleWithOutcome(Outcome));

    /// <summary>Gets whether only the separate recovery owner may continue this terminal result.</summary>
    public bool IsRecoveryOwned =>
        HasValidShape && Outcome is
            ManagedFirstInstallationOutcome.RecoveryRequired or
            ManagedFirstInstallationOutcome.InstalledButLaunchFailed;

    private static bool IsValidLaunchFailureShape(
        ManagedFirstInstallationOutcome outcome,
        ManagedFirstInstallationLaunchFailure? failure)
    {
        return Enum.IsDefined(outcome) &&
            (outcome == ManagedFirstInstallationOutcome.InstalledButLaunchFailed
                ? failure is not null && failure.IsCompatibleWithOutcome(outcome)
                : failure is null ||
                    (outcome == ManagedFirstInstallationOutcome.RecoveryRequired &&
                        failure.IsCompatibleWithOutcome(outcome)));
    }
}
