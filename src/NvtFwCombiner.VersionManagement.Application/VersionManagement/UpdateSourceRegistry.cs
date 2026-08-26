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

/// <summary>One normalized absolute update-source root admitted by the registry adapter.</summary>
public sealed record UpdateSourceRegistryEntry
{
    internal UpdateSourceRegistryEntry(string sourceRoot, UpdateSourceRegistryEntryStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        SourceRoot = sourceRoot;
        Status = Enum.IsDefined(status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(status));
    }

    /// <summary>Gets the normalized absolute local or UNC root.</summary>
    public string SourceRoot { get; }

    /// <summary>Gets the declared selection role.</summary>
    public UpdateSourceRegistryEntryStatus Status { get; }
}

/// <summary>One complete immutable fixed-registry publication.</summary>
public sealed class UpdateSourceRegistrySnapshot
{
    internal UpdateSourceRegistrySnapshot(
        long revision,
        string contentDigest,
        IReadOnlyList<UpdateSourceRegistryEntry> entries)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        ArgumentNullException.ThrowIfNull(entries);
        if (!IsLowerSha256(contentDigest) ||
            entries.Count is < 1 or > 16 ||
            entries.Count(entry => entry.Status == UpdateSourceRegistryEntryStatus.Latest) != 1 ||
            entries.Select(entry => entry.SourceRoot).Distinct(PathComparer).Count() != entries.Count)
        {
            throw new ArgumentException("Update-source registry snapshot is inconsistent.", nameof(entries));
        }
        Revision = revision;
        ContentDigest = contentDigest;
        Entries = [.. entries];
    }

    /// <summary>Gets the strictly increasing administrative revision.</summary>
    public long Revision { get; }

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
    /// <summary>The locator or a traversed path is unsafe.</summary>
    UnsafeLocator,
    /// <summary>The raw registry exceeds 64 KiB.</summary>
    RegistryTooLarge,
    /// <summary>The schema, revision, entries, or normalized paths are invalid.</summary>
    InvalidManifest,
    /// <summary>The file changed during its stable read.</summary>
    UnstableRead,
}

/// <summary>Fail-closed result from one fixed-registry read.</summary>
public sealed record UpdateSourceRegistryLoadResult(
    UpdateSourceRegistrySnapshot? Snapshot,
    UpdateSourceRegistryLoadIssue Issue)
{
    /// <summary>Gets whether one complete registry publication was admitted.</summary>
    public bool IsSuccess => Snapshot is not null && Issue == UpdateSourceRegistryLoadIssue.None;
}

/// <summary>Reads one fixed registry without choosing or persisting an effective source.</summary>
public interface IUpdateSourceRegistry
{
    /// <summary>Loads one complete strict registry publication.</summary>
    ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(CancellationToken cancellationToken);
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

/// <summary>Logical mutation fence shared with launcher self-update state.</summary>
public interface IVersionManagementMutationFence
{
    /// <summary>Returns whether one application-state mutation may commit now.</summary>
    ValueTask<bool> CanMutateAsync(CancellationToken cancellationToken);
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

    /// <summary>Gets the normalized automatic source root.</summary>
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
        IReadOnlyList<VersionEnvironmentSelfTestAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        if (!Enum.IsDefined(registryIssue) || attempts.Count > 16 ||
            registryIssue == UpdateSourceRegistryLoadIssue.None != (attempts.Count > 0))
        {
            throw new ArgumentException("Environment self-test result is inconsistent.");
        }
        RegistryIssue = registryIssue;
        Attempts = [.. attempts];
    }

    /// <summary>Gets the fixed-registry load result.</summary>
    public UpdateSourceRegistryLoadIssue RegistryIssue { get; }

    /// <summary>Gets latest then available attempts, never deprecated.</summary>
    public IReadOnlyList<VersionEnvironmentSelfTestAttempt> Attempts { get; }

    /// <summary>Gets whether the registry loaded and at least one automatic source fully verified.</summary>
    public bool IsSuccess => RegistryIssue == UpdateSourceRegistryLoadIssue.None &&
        Attempts.Any(static attempt => attempt.IsVerified);
}

internal sealed class AllowVersionManagementMutationFence : IVersionManagementMutationFence
{
    internal static AllowVersionManagementMutationFence Instance { get; } = new();

    private AllowVersionManagementMutationFence()
    {
    }

    public ValueTask<bool> CanMutateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(true);
    }
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
