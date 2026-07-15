namespace NvtFwCombiner.Contracts.Bundles;

/// <summary>DTO for one schema-validated profile-bundle-v1 manifest.</summary>
public sealed record ProfileBundleDocument(
    string SchemaVersion,
    string BundleId,
    string BundleVersion,
    string HashAlgorithm,
    string ContentHash,
    string TrustAnchorBindingId,
    IReadOnlyList<ProfileBundleEntryDocument> Entries);

/// <summary>DTO for one closed allowlisted bundle entry.</summary>
public sealed record ProfileBundleEntryDocument(
    string EntryId,
    string Kind,
    string Path,
    string SchemaId,
    string ContentHash);
