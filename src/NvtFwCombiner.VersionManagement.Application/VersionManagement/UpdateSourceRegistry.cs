namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Declared automatic-selection role of one registry entry.</summary>
public enum UpdateSourceRegistryEntryStatus
{
    /// <summary>The first and only preferred source.</summary>
    Latest,
    /// <summary>One ordered automatic fallback source.</summary>
    Available,
    /// <summary>A historical source that automatic resolution must never select.</summary>
    Deprecated,
}

/// <summary>One normalized absolute Catalog-file path admitted by the registry adapter.</summary>
public sealed record UpdateSourceRegistryEntry
{
    internal UpdateSourceRegistryEntry(string catalogPath, UpdateSourceRegistryEntryStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        CatalogPath = catalogPath;
        SourceRoot = Path.GetDirectoryName(catalogPath) ??
            throw new ArgumentException("Catalog path has no parent directory.", nameof(catalogPath));
        Status = Enum.IsDefined(status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(status));
    }

    /// <summary>Gets the normalized absolute Catalog JSON file path.</summary>
    public string CatalogPath { get; }

    /// <summary>Gets the Catalog parent used to resolve package paths.</summary>
    public string SourceRoot { get; }

    /// <summary>Gets the declared selection role.</summary>
    public UpdateSourceRegistryEntryStatus Status { get; }
}

/// <summary>Mandatory identity assertions for the Catalog selected by a Registry.</summary>
public sealed record UpdateCatalogPublicationAssertion
{
    internal UpdateCatalogPublicationAssertion(
        string latestVersion,
        int catalogSchemaVersion,
        string catalogSha256)
    {
        LatestVersion = ManagedAppVersion.Parse(latestVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(catalogSchemaVersion);
        if (!UpdateSourceRegistrySnapshot.IsLowerSha256(catalogSha256))
        {
            throw new ArgumentException("Catalog SHA-256 is invalid.", nameof(catalogSha256));
        }
        CatalogSchemaVersion = catalogSchemaVersion;
        CatalogSha256 = catalogSha256;
    }

    /// <summary>Gets the asserted newest application version.</summary>
    public ManagedAppVersion LatestVersion { get; }

    /// <summary>Gets the asserted Catalog wire-schema version.</summary>
    public int CatalogSchemaVersion { get; }

    /// <summary>Gets the lowercase SHA-256 of the exact Catalog bytes.</summary>
    public string CatalogSha256 { get; }
}

/// <summary>One complete immutable fixed-registry publication.</summary>
public sealed class UpdateSourceRegistrySnapshot
{
    /// <summary>The only Registry authority admitted by the 1.x runtime.</summary>
    internal const string ProductionRegistryId = "nvt-fw-combiner-production";

    internal UpdateSourceRegistrySnapshot(
        string registryId,
        long registryRevision,
        DateTimeOffset publishedAtUtc,
        UpdateCatalogPublicationAssertion catalogPublication,
        string contentDigest,
        IReadOnlyList<UpdateSourceRegistryEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(registryRevision);
        ArgumentNullException.ThrowIfNull(catalogPublication);
        ArgumentNullException.ThrowIfNull(entries);
        if (!string.Equals(registryId, ProductionRegistryId, StringComparison.Ordinal) ||
            publishedAtUtc.Offset != TimeSpan.Zero ||
            !IsLowerSha256(contentDigest) ||
            entries.Count is < 1 or > 16 ||
            entries.Count(entry => entry.Status == UpdateSourceRegistryEntryStatus.Latest) != 1 ||
            entries.Select(entry => entry.CatalogPath).Distinct(PathComparer).Count() != entries.Count ||
            entries.Select(entry => entry.SourceRoot).Distinct(PathComparer).Count() != entries.Count)
        {
            throw new ArgumentException("Update-source registry snapshot is inconsistent.", nameof(entries));
        }
        RegistryId = registryId;
        RegistryRevision = registryRevision;
        PublishedAtUtc = publishedAtUtc;
        CatalogPublication = catalogPublication;
        ContentDigest = contentDigest;
        Entries = [.. entries];
    }

    /// <summary>Gets the stable logical Registry identity shared by its replicas.</summary>
    public string RegistryId { get; }

    /// <summary>Gets the strictly increasing administrative revision.</summary>
    public long RegistryRevision { get; }

    /// <summary>Gets audit-only UTC publication time; it never selects a replica.</summary>
    public DateTimeOffset PublishedAtUtc { get; }

    /// <summary>Gets mandatory assertions for the selected Catalog publication.</summary>
    public UpdateCatalogPublicationAssertion CatalogPublication { get; }

    /// <summary>Gets the lowercase SHA-256 of the exact stable registry bytes.</summary>
    public string ContentDigest { get; }

    /// <summary>Gets entries in their declared stable array order.</summary>
    public IReadOnlyList<UpdateSourceRegistryEntry> Entries { get; }

    internal IEnumerable<UpdateSourceRegistryEntry> AutomaticCandidates()
    {
        yield return Entries.Single(entry => entry.Status == UpdateSourceRegistryEntryStatus.Latest);
        foreach (UpdateSourceRegistryEntry entry in Entries)
        {
            if (entry.Status == UpdateSourceRegistryEntryStatus.Available)
            {
                yield return entry;
            }
        }
    }

    internal static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
            value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}

/// <summary>Stable fixed-registry read issue.</summary>
public enum UpdateSourceRegistryLoadIssue
{
    /// <summary>The complete registry was loaded.</summary>
    None,
    /// <summary>No registry locator was configured.</summary>
    NotConfigured,
    /// <summary>The registry file is absent.</summary>
    RegistryMissing,
    /// <summary>The registry could not be read.</summary>
    RegistryUnavailable,
    /// <summary>The current user cannot read the registry.</summary>
    PermissionDenied,
    /// <summary>The remote Registry requires credentials unavailable to this client.</summary>
    AuthenticationRequired,
    /// <summary>The remote Registry did not complete within the bounded deadline.</summary>
    RegistryTimedOut,
    /// <summary>The locator or a traversed path is unsafe.</summary>
    UnsafeLocator,
    /// <summary>The raw registry exceeds 64 KiB.</summary>
    RegistryTooLarge,
    /// <summary>The schema, revision, entries, or normalized paths are invalid.</summary>
    InvalidManifest,
    /// <summary>The file changed during its stable read.</summary>
    UnstableRead,
    /// <summary>Replicas published different exact bytes at the same revision.</summary>
    ReplicaConflict,
}

/// <summary>Fail-closed result from one fixed-registry read.</summary>
public sealed record UpdateSourceRegistryLoadResult(
    UpdateSourceRegistrySnapshot? Snapshot,
    UpdateSourceRegistryLoadIssue Issue,
    IReadOnlyList<UpdateSourceRegistryReplicaObservation>? Replicas = null)
{
    /// <summary>Gets whether one complete registry publication was admitted.</summary>
    public bool IsSuccess => Snapshot is not null && Issue == UpdateSourceRegistryLoadIssue.None;
}

/// <summary>Read-only health of one ordered Registry replica.</summary>
public sealed record UpdateSourceRegistryReplicaObservation(
    int Position,
    UpdateSourceRegistryLoadIssue Issue,
    long? RegistryRevision,
    bool IsSelected);

/// <summary>Reads one fixed registry without choosing or persisting an effective source.</summary>
public interface IUpdateSourceRegistry
{
    /// <summary>Loads one complete strict registry publication.</summary>
    ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>Selects one complete authoritative publication from ordered replicas.</summary>
public sealed class ReplicatedUpdateSourceRegistry : IUpdateSourceRegistry
{
    private static readonly TimeSpan DefaultReplicaTimeout = TimeSpan.FromSeconds(45);
    private readonly IReadOnlyList<ReplicaReadSlot> _replicas;

    /// <summary>Creates an ordered replica set; index zero is the primary.</summary>
    public ReplicatedUpdateSourceRegistry(IReadOnlyList<IUpdateSourceRegistry> replicas)
        : this(replicas, DefaultReplicaTimeout)
    {
    }

    internal ReplicatedUpdateSourceRegistry(
        IReadOnlyList<IUpdateSourceRegistry> replicas,
        TimeSpan replicaTimeout)
    {
        ArgumentNullException.ThrowIfNull(replicas);
        if (replicas.Count is < 1 or > 8 ||
            replicas.Any(static replica => replica is null) ||
            replicaTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Registry replica set is invalid.", nameof(replicas));
        }
        _replicas = [.. replicas.Select(replica => new ReplicaReadSlot(replica, replicaTimeout))];
    }

    /// <inheritdoc />
    public async ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<UpdateSourceRegistryLoadResult>[] reads =
        [.. _replicas.Select(replica => replica.LoadAsync(cancellationToken))];
        UpdateSourceRegistryLoadResult[] results = await Task.WhenAll(reads).ConfigureAwait(false);
        UpdateSourceRegistrySnapshot[] valid =
        [.. results.Where(static result => result.IsSuccess).Select(static result => result.Snapshot!)];
        if (valid.Length == 0)
        {
            return Publish(results, snapshot: null, results[0].Issue);
        }
        if (valid.Select(static snapshot => snapshot.RegistryId)
            .Distinct(StringComparer.Ordinal).Skip(1).Any())
        {
            return Publish(results, snapshot: null, UpdateSourceRegistryLoadIssue.ReplicaConflict);
        }

        long highestRevision = valid.Max(static snapshot => snapshot.RegistryRevision);
        UpdateSourceRegistrySnapshot[] newest =
        [.. valid.Where(snapshot => snapshot.RegistryRevision == highestRevision)];
        return newest.Select(static snapshot => snapshot.ContentDigest)
            .Distinct(StringComparer.Ordinal).Skip(1).Any()
                ? Publish(results, snapshot: null, UpdateSourceRegistryLoadIssue.ReplicaConflict)
                : Publish(results, newest[0], UpdateSourceRegistryLoadIssue.None);
    }

    private sealed class ReplicaReadSlot(
        IUpdateSourceRegistry replica,
        TimeSpan timeout)
    {
        private readonly Lock _sync = new();
        private Task<UpdateSourceRegistryLoadResult>? _read;
        private bool _abandoned;

        internal Task<UpdateSourceRegistryLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task<UpdateSourceRegistryLoadResult> read;
            lock (_sync)
            {
                RetireCompletedUnderLock();
                _read ??= Task.Run(
                    async () => await replica.LoadAsync(CancellationToken.None)
                        .ConfigureAwait(false),
                    CancellationToken.None);
                if (_abandoned)
                {
                    return Task.FromResult(new UpdateSourceRegistryLoadResult(
                        null,
                        UpdateSourceRegistryLoadIssue.RegistryTimedOut));
                }
                read = _read;
            }
            return AwaitCurrentAsync(read, cancellationToken);
        }

        private async Task<UpdateSourceRegistryLoadResult> AwaitCurrentAsync(
            Task<UpdateSourceRegistryLoadResult> read,
            CancellationToken cancellationToken)
        {
            try
            {
                UpdateSourceRegistryLoadResult result = await read.WaitAsync(
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    if (_abandoned && ReferenceEquals(_read, read))
                    {
                        return new(null, UpdateSourceRegistryLoadIssue.RegistryTimedOut);
                    }
                    RetireUnderLock(read);
                }
                return result;
            }
            catch (TimeoutException)
            {
                Abandon(read);
                return new(null, UpdateSourceRegistryLoadIssue.RegistryTimedOut);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                lock (_sync)
                {
                    RetireUnderLock(read);
                }
                return new(null, UpdateSourceRegistryLoadIssue.RegistryUnavailable);
            }
        }

        private void Abandon(Task<UpdateSourceRegistryLoadResult> read)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_read, read) || _abandoned)
                {
                    return;
                }
                _abandoned = true;
                _ = read.ContinueWith(
                    _ => RetireAbandoned(read),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
            }
        }

        private void RetireAbandoned(Task<UpdateSourceRegistryLoadResult> read)
        {
            lock (_sync)
            {
                RetireUnderLock(read);
            }
        }

        private void RetireCompletedUnderLock()
        {
            if (_read is { IsCompleted: true } completed)
            {
                RetireUnderLock(completed);
            }
        }

        private void RetireUnderLock(Task<UpdateSourceRegistryLoadResult> read)
        {
            if (!ReferenceEquals(_read, read) || !read.IsCompleted)
            {
                return;
            }
            _ = read.Exception;
            _read = null;
            _abandoned = false;
        }
    }

    private static UpdateSourceRegistryLoadResult Publish(
        IReadOnlyList<UpdateSourceRegistryLoadResult> results,
        UpdateSourceRegistrySnapshot? snapshot,
        UpdateSourceRegistryLoadIssue issue)
    {
        return new(
            snapshot,
            issue,
            [.. results.Select((result, index) => new UpdateSourceRegistryReplicaObservation(
                index + 1,
                result.Issue,
                result.Snapshot?.RegistryRevision,
                snapshot is not null && ReferenceEquals(result.Snapshot, snapshot))) ]);
    }
}

/// <summary>Visible terminal state of fixed-registry resolution.</summary>
public enum VersionRegistryStatus
{
    /// <summary>No registry adapter is configured.</summary>
    NotConfigured,
    /// <summary>A user-confirmed source remains durably pinned.</summary>
    ManualPin,
    /// <summary>The unique latest source was selected.</summary>
    LatestSelected,
    /// <summary>One ordered available fallback was selected.</summary>
    FallbackSelected,
    /// <summary>The registry could not be read.</summary>
    Unavailable,
    /// <summary>No automatic candidate passed admission.</summary>
    Exhausted,
    /// <summary>Registry authority was rejected before candidate selection.</summary>
    Rejected,
    /// <summary>The prior source is deprecated and no permitted replacement admitted.</summary>
    DeprecatedRetained,
}

/// <summary>Stable Application-owned fixed-registry issue.</summary>
public enum UpdateSourceRegistryIssue
{
    /// <summary>No issue occurred.</summary>
    None,
    /// <summary>No registry locator is configured.</summary>
    NotConfigured,
    /// <summary>The registry file is absent or unavailable.</summary>
    Unavailable,
    /// <summary>The registry exists but cannot be read by the current user.</summary>
    PermissionDenied,
    /// <summary>The remote Registry requires an authenticated Microsoft 365 session.</summary>
    AuthenticationRequired,
    /// <summary>The remote Registry did not complete within its bounded deadline.</summary>
    TimedOut,
    /// <summary>The registry contract or locator is invalid or unsafe.</summary>
    Invalid,
    /// <summary>The observed revision is lower than the last accepted revision.</summary>
    RevisionRollback,
    /// <summary>The same revision was republished with different exact bytes.</summary>
    RevisionConflict,
    /// <summary>Every latest/available candidate failed catalog or package admission.</summary>
    CandidatesExhausted,
    /// <summary>The registry changed before the source commit could be serialized.</summary>
    RegistryChanged,
    /// <summary>The exact state writer or durable state was unavailable.</summary>
    StateUnavailable,
    /// <summary>A newer check superseded this resolution.</summary>
    Superseded,
    /// <summary>The retained prior source is declared deprecated.</summary>
    CurrentSourceDeprecated,
}

/// <summary>One read-only environment self-test attempt for an automatic source.</summary>
public sealed record VersionEnvironmentSelfTestAttempt
{
    /// <summary>Creates one internally consistent latest/available attempt.</summary>
    public VersionEnvironmentSelfTestAttempt(
        string sourceRoot,
        UpdateSourceRegistryEntryStatus status,
        UpdateCatalogLoadIssue catalogIssue,
        ManagedVersionInstallIssue? packageIssue,
        ManagedAppVersion? newestVersion,
        bool isVerified)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        bool catalogPassed = catalogIssue == UpdateCatalogLoadIssue.None;
        bool packagePassed = packageIssue == ManagedVersionInstallIssue.None;
        if (status is not (UpdateSourceRegistryEntryStatus.Latest or
                UpdateSourceRegistryEntryStatus.Available) ||
            !Enum.IsDefined(catalogIssue) ||
            (packageIssue is { } declaredPackageIssue && !Enum.IsDefined(declaredPackageIssue)) ||
            (catalogPassed != (packageIssue is not null && newestVersion is not null)) ||
            (isVerified != (catalogPassed && packagePassed)))
        {
            throw new ArgumentException("Environment self-test attempt is inconsistent.");
        }
        SourceRoot = sourceRoot;
        Status = status;
        CatalogIssue = catalogIssue;
        PackageIssue = packageIssue;
        NewestVersion = newestVersion;
        IsVerified = isVerified;
    }

    /// <summary>Gets the normalized automatic source root. Presentation must not expose it by default.</summary>
    public string SourceRoot { get; }

    /// <summary>Gets whether this was latest or an ordered available source.</summary>
    public UpdateSourceRegistryEntryStatus Status { get; }

    /// <summary>Gets the complete catalog admission outcome.</summary>
    public UpdateCatalogLoadIssue CatalogIssue { get; }

    /// <summary>Gets newest-package admission after catalog success.</summary>
    public ManagedVersionInstallIssue? PackageIssue { get; }

    /// <summary>Gets the newest catalog version when the catalog passed.</summary>
    public ManagedAppVersion? NewestVersion { get; }

    /// <summary>Gets whether the complete catalog and newest package passed.</summary>
    public bool IsVerified { get; }
}

/// <summary>Bounded read-only result for the configured registry environment.</summary>
public sealed class VersionEnvironmentSelfTestResult
{
    /// <summary>Creates one bounded immutable result.</summary>
    public VersionEnvironmentSelfTestResult(
        UpdateSourceRegistryLoadIssue registryIssue,
        IReadOnlyList<VersionEnvironmentSelfTestAttempt> attempts,
        IReadOnlyList<UpdateSourceRegistryReplicaObservation>? replicas = null,
        UpdateSourceRegistryIssue authorityIssue = UpdateSourceRegistryIssue.None,
        long? acceptedRegistryRevision = null)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        if (!Enum.IsDefined(registryIssue) || attempts.Count > 16 ||
            !Enum.IsDefined(authorityIssue) ||
            acceptedRegistryRevision is < 0 ||
            (registryIssue != UpdateSourceRegistryLoadIssue.None &&
                authorityIssue != UpdateSourceRegistryIssue.None) ||
            ((registryIssue == UpdateSourceRegistryLoadIssue.None &&
                authorityIssue == UpdateSourceRegistryIssue.None) != (attempts.Count > 0)))
        {
            throw new ArgumentException("Environment self-test result is inconsistent.");
        }
        RegistryIssue = registryIssue;
        AuthorityIssue = authorityIssue;
        AcceptedRegistryRevision = acceptedRegistryRevision;
        Attempts = [.. attempts];
        Replicas = replicas is null ? [] : [.. replicas];
    }

    /// <summary>Gets the fixed-registry load result.</summary>
    public UpdateSourceRegistryLoadIssue RegistryIssue { get; }

    /// <summary>Gets durable Registry-authority admission after a successful replica read.</summary>
    public UpdateSourceRegistryIssue AuthorityIssue { get; }

    /// <summary>Gets the durable accepted revision used to classify replica freshness.</summary>
    public long? AcceptedRegistryRevision { get; }

    /// <summary>Gets latest then available attempts, never deprecated.</summary>
    public IReadOnlyList<VersionEnvironmentSelfTestAttempt> Attempts { get; }

    /// <summary>Gets ordered primary/backup health when replicated Registry discovery was used.</summary>
    public IReadOnlyList<UpdateSourceRegistryReplicaObservation> Replicas { get; }

    /// <summary>Gets whether the registry loaded and at least one automatic source fully verified.</summary>
    public bool IsSuccess => RegistryIssue == UpdateSourceRegistryLoadIssue.None &&
        AuthorityIssue == UpdateSourceRegistryIssue.None &&
        Attempts.Any(static attempt => attempt.IsVerified);
}

/// <summary>Durable anti-rollback and manual-pin state.</summary>
public sealed record VersionSourceRegistryState
{
    /// <summary>Creates one validated durable registry authority.</summary>
    public VersionSourceRegistryState(
        long acceptedRevision,
        string? acceptedDigest,
        bool isManualPin)
    {
        bool hasNoRevision = acceptedRevision == 0;
        bool hasNoDigest = acceptedDigest is null;
        if (acceptedRevision < 0 ||
            hasNoRevision != hasNoDigest ||
            (hasNoRevision && !isManualPin) ||
            (acceptedDigest is not null && !UpdateSourceRegistrySnapshot.IsLowerSha256(acceptedDigest)))
        {
            throw new ArgumentException("Registry revision and digest are inconsistent.");
        }
        AcceptedRevision = acceptedRevision;
        AcceptedDigest = acceptedDigest;
        IsManualPin = isManualPin;
    }

    /// <summary>Gets the last accepted monotonic registry revision, or zero before first admission.</summary>
    public long AcceptedRevision { get; }

    /// <summary>Gets the digest bound to the accepted revision.</summary>
    public string? AcceptedDigest { get; }

    /// <summary>Gets whether automatic resolution is suspended by a durable manual source.</summary>
    public bool IsManualPin { get; }
}
