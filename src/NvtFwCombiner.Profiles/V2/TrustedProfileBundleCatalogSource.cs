namespace NvtFwCombiner.Profiles.V2;

/// <summary>Exact immutable identity of a canonical JSON document supplied by the trusted Bootstrap seam.</summary>
internal sealed record TrustedProfileBundleCatalogEntryIdentity(
    string EntryId,
    string Path,
    string SchemaId,
    string ContentHash);
