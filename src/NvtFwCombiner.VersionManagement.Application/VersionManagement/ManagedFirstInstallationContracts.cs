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

/// <summary>Typed first-install execution result.</summary>
public sealed record ManagedFirstInstallationResult(
    ManagedFirstInstallationOutcome Outcome,
    string ManagedRoot,
    ManagedAppVersion Version);
