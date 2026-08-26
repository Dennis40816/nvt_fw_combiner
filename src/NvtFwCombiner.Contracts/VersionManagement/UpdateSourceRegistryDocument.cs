namespace NvtFwCombiner.Contracts.VersionManagement;

/// <summary>Fixed update-source registry v1 transport.</summary>
public sealed record UpdateSourceRegistryDocument(
    int SchemaVersion,
    long Revision,
    IReadOnlyList<UpdateSourceRegistryEntryDocument?>? Entries);

/// <summary>One explicitly classified absolute update-source root.</summary>
public sealed record UpdateSourceRegistryEntryDocument(
    string? Status,
    string? Path);
