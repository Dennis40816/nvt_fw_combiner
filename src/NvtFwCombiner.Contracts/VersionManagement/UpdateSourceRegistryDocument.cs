namespace NvtFwCombiner.Contracts.VersionManagement;

/// <summary>Fixed update-source registry v1 transport.</summary>
public sealed record UpdateSourceRegistryDocument(
    int SchemaVersion,
    string? RegistryId,
    long RegistryRevision,
    DateTimeOffset PublishedAtUtc,
    UpdateCatalogPublicationDocument? CatalogPublication,
    IReadOnlyList<UpdateSourceRegistryEntryDocument?>? Entries);

/// <summary>Catalog identity assertions carried by the Registry publication.</summary>
public sealed record UpdateCatalogPublicationDocument(
    string? LatestVersion,
    int CatalogSchemaVersion,
    string? CatalogSha256);

/// <summary>One explicitly classified absolute Catalog-file path.</summary>
public sealed record UpdateSourceRegistryEntryDocument(
    string? Status,
    string? CatalogPath);
