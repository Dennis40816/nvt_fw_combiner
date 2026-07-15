namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Closed kinds admitted by one schema-validated profile bundle manifest.</summary>
internal enum ProfileBundleEntryKind
{
    Schema,
    FirmwareFamily,
    CompositionProfile,
    EvidenceManifest,
    SavedCompositionRule,
}

/// <summary>One immutable allowlisted file declaration before file and trust verification.</summary>
internal sealed record ProfileBundleEntry
{
    internal ProfileBundleEntry(
        string entryId,
        ProfileBundleEntryKind kind,
        string path,
        string schemaId,
        string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown bundle entry kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        EntryId = entryId;
        Kind = kind;
        Path = path;
        SchemaId = schemaId;
        ContentHash = contentHash;
    }

    internal string EntryId { get; }

    internal ProfileBundleEntryKind Kind { get; }

    internal string Path { get; }

    internal string SchemaId { get; }

    internal string ContentHash { get; }
}

/// <summary>Immutable manifest semantics before bundle-root and entry trust verification.</summary>
internal sealed class ProfileBundleManifest
{
    private readonly ProfileBundleEntry[] _entries;

    internal ProfileBundleManifest(
        string bundleId,
        string bundleVersion,
        string contentHash,
        string trustAnchorBindingId,
        IEnumerable<ProfileBundleEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustAnchorBindingId);
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [.. entries];
        if (_entries.Length == 0 || _entries.Any(static entry => entry is null))
        {
            throw new ArgumentException("Bundle manifests require non-null entries.", nameof(entries));
        }

        Array.Sort(_entries, static (left, right) => StringComparer.Ordinal.Compare(left.EntryId, right.EntryId));
        BundleId = bundleId;
        BundleVersion = bundleVersion;
        ContentHash = contentHash;
        TrustAnchorBindingId = trustAnchorBindingId;
        Entries = Array.AsReadOnly(_entries);
    }

    internal string BundleId { get; }

    internal string BundleVersion { get; }

    internal string ContentHash { get; }

    internal string TrustAnchorBindingId { get; }

    internal IReadOnlyList<ProfileBundleEntry> Entries { get; }
}
