namespace NvtFwCombiner.Contracts.VersionManagement;

/// <summary>Versioned configured-folder catalog transport.</summary>
public sealed record UpdateCatalogDocument(
    int SchemaVersion,
    string? Product,
    string? RuntimeIdentifier,
    IReadOnlyList<UpdateCatalogVersionDocument?>? Versions);

/// <summary>One package entry in a configured-folder update catalog.</summary>
public sealed record UpdateCatalogVersionDocument(
    string? Version,
    string? PublishedAt,
    string? PackagePath,
    long PackageSize,
    string? PackageSha256,
    string? ReleaseManifestSha256,
    string? ReleaseNotes);

/// <summary>Versioned Catalog v2 transport with required per-entry notification policy.</summary>
public sealed record UpdateCatalogV2Document(
    int SchemaVersion,
    string? Product,
    string? RuntimeIdentifier,
    IReadOnlyList<UpdateCatalogV2VersionDocument?>? Versions);

/// <summary>One package entry in a Catalog v2 publication.</summary>
public sealed record UpdateCatalogV2VersionDocument(
    string? Version,
    string? PublishedAt,
    string? PackagePath,
    long PackageSize,
    string? PackageSha256,
    string? ReleaseManifestSha256,
    string? ReleaseNotes,
    string? NotificationPolicy);
