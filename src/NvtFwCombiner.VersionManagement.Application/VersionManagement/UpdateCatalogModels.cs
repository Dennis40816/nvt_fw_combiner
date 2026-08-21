namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Stable configured-folder catalog validation issue.</summary>
public enum UpdateCatalogIssueCode
{
    /// <summary>The catalog schema version is unsupported.</summary>
    InvalidSchemaVersion,
    /// <summary>The catalog names another product.</summary>
    InvalidProduct,
    /// <summary>The catalog targets another runtime.</summary>
    InvalidRuntimeIdentifier,
    /// <summary>The catalog contains no versions.</summary>
    EmptyVersions,
    /// <summary>The catalog exceeds the bounded entry count.</summary>
    TooManyVersions,
    /// <summary>An entry has a non-canonical version.</summary>
    InvalidVersion,
    /// <summary>The same version appears more than once.</summary>
    DuplicateVersion,
    /// <summary>An entry has an invalid UTC publication timestamp.</summary>
    InvalidPublishedAt,
    /// <summary>An entry has an unsafe or non-canonical relative package path.</summary>
    UnsafePackagePath,
    /// <summary>An entry has an invalid or excessive package size.</summary>
    InvalidPackageSize,
    /// <summary>An entry has a non-canonical SHA-256 digest.</summary>
    InvalidSha256,
    /// <summary>An entry exceeds the bounded release-note size.</summary>
    ReleaseNotesTooLarge,
}

/// <summary>One stable catalog issue with an optional entry identity.</summary>
public sealed record UpdateCatalogIssue(UpdateCatalogIssueCode Code, string? Version = null);

/// <summary>Canonical safe catalog-relative package path.</summary>
public readonly record struct UpdateCatalogPackagePath(string Value);

/// <summary>Immutable admitted package metadata; configured source path is intentionally absent.</summary>
public sealed class UpdateCatalogVersionSnapshot
{
    internal UpdateCatalogVersionSnapshot(
        ManagedAppVersion version,
        DateTimeOffset publishedAt,
        UpdateCatalogPackagePath packagePath,
        long packageSize,
        string packageSha256,
        string releaseManifestSha256,
        string releaseNotes)
    {
        Version = version;
        PublishedAt = publishedAt;
        PackagePath = packagePath;
        PackageSize = packageSize;
        PackageSha256 = packageSha256;
        ReleaseManifestSha256 = releaseManifestSha256;
        ReleaseNotes = releaseNotes;
        Identity = string.Join(
            '|',
            Version,
            PackagePath.Value,
            PackageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PackageSha256,
            ReleaseManifestSha256);
    }

    /// <summary>Gets the package version.</summary>
    public ManagedAppVersion Version { get; }

    /// <summary>Gets the normalized UTC publication timestamp.</summary>
    public DateTimeOffset PublishedAt { get; }

    /// <summary>Gets the catalog-relative package path.</summary>
    public UpdateCatalogPackagePath PackagePath { get; }

    /// <summary>Gets the declared package length in bytes.</summary>
    public long PackageSize { get; }

    /// <summary>Gets the lowercase package SHA-256 digest.</summary>
    public string PackageSha256 { get; }

    /// <summary>Gets the lowercase inner release-manifest SHA-256 digest.</summary>
    public string ReleaseManifestSha256 { get; }

    /// <summary>Gets the bounded release notes.</summary>
    public string ReleaseNotes { get; }

    /// <summary>Gets the location-independent catalog-entry identity.</summary>
    public string Identity { get; }
}

/// <summary>Fully validated immutable catalog publication.</summary>
public sealed class UpdateCatalogSnapshot
{
    internal UpdateCatalogSnapshot(IReadOnlyList<UpdateCatalogVersionSnapshot> versions)
    {
        Versions = versions;
    }

    /// <summary>Gets admitted entries ordered newest first.</summary>
    public IReadOnlyList<UpdateCatalogVersionSnapshot> Versions { get; }

    /// <summary>Finds the newest catalog version strictly newer than the supplied version.</summary>
    /// <param name="current">The active application version.</param>
    /// <returns>The newest newer entry, or <see langword="null"/>.</returns>
    public UpdateCatalogVersionSnapshot? FindNewestNewerThan(ManagedAppVersion current)
    {
        return Versions.FirstOrDefault(version => version.Version > current);
    }
}

/// <summary>Fail-closed validation result: a snapshot or issues, never both.</summary>
public sealed class UpdateCatalogValidationResult
{
    private UpdateCatalogValidationResult(
        UpdateCatalogSnapshot? snapshot,
        IReadOnlyList<UpdateCatalogIssue> issues)
    {
        Snapshot = snapshot;
        Issues = issues;
    }

    /// <summary>Gets whether validation admitted the complete catalog.</summary>
    public bool IsValid => Snapshot is not null;

    /// <summary>Gets the immutable catalog when validation succeeds.</summary>
    public UpdateCatalogSnapshot? Snapshot { get; }

    /// <summary>Gets stable issues when validation fails.</summary>
    public IReadOnlyList<UpdateCatalogIssue> Issues { get; }

    internal static UpdateCatalogValidationResult Success(UpdateCatalogSnapshot snapshot)
    {
        return new(snapshot, []);
    }

    internal static UpdateCatalogValidationResult Failure(IReadOnlyList<UpdateCatalogIssue> issues)
    {
        return new(null, issues);
    }
}
