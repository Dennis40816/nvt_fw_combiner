namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Resource limits for one closed capture of manifest-listed bundle entries.</summary>
internal sealed class ProfileBundleEntrySnapshotLimits
{
    internal ProfileBundleEntrySnapshotLimits(
        int maximumEntryCount,
        int maximumEntryBytes,
        int maximumTotalEntryBytes,
        int maximumDirectoryCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntryCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntryBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTotalEntryBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDirectoryCount);
        MaximumEntryCount = maximumEntryCount;
        MaximumEntryBytes = maximumEntryBytes;
        MaximumTotalEntryBytes = maximumTotalEntryBytes;
        MaximumDirectoryCount = maximumDirectoryCount;
    }

    internal int MaximumEntryCount { get; }

    internal int MaximumEntryBytes { get; }

    internal int MaximumTotalEntryBytes { get; }

    internal int MaximumDirectoryCount { get; }
}

/// <summary>One normalized bundle entry paired with its private hash-verified file snapshot.</summary>
internal sealed class ProfileBundleEntrySnapshot
{
    internal ProfileBundleEntrySnapshot(
        ProfileBundleEntry entry,
        ProfileBundleFileSnapshot fileSnapshot)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(fileSnapshot);
        if (!StringComparer.Ordinal.Equals(entry.Path, fileSnapshot.ManifestPath) ||
            !StringComparer.Ordinal.Equals(entry.ContentHash, fileSnapshot.ActualSha256))
        {
            throw new ArgumentException(
                "Bundle entry metadata does not identify the supplied file snapshot.",
                nameof(fileSnapshot));
        }

        Entry = entry;
        FileSnapshot = fileSnapshot;
    }

    internal ProfileBundleEntry Entry { get; }

    internal ProfileBundleFileSnapshot FileSnapshot { get; }
}

/// <summary>Immutable bounded capture of every file listed by one normalized bundle manifest.</summary>
internal sealed class ProfileBundleEntrySnapshotCollection
{
    private ProfileBundleEntrySnapshotCollection(
        ProfileBundleManifest manifest,
        IEnumerable<ProfileBundleEntrySnapshot> entries)
    {
        Manifest = manifest;
        ProfileBundleEntrySnapshot[] snapshot = [.. entries];
        Entries = Array.AsReadOnly(snapshot);
    }

    internal ProfileBundleManifest Manifest { get; }

    internal IReadOnlyList<ProfileBundleEntrySnapshot> Entries { get; }

    internal static ProfileBundleEntrySnapshotCollection Capture(
        string bundleRoot,
        string manifestPath,
        ProfileBundleManifest manifest,
        ProfileBundleEntrySnapshotLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(limits);
        if (manifest.Entries.Count > limits.MaximumEntryCount)
        {
            throw new InvalidDataException(
                $"Bundle entry count exceeds the {limits.MaximumEntryCount}-entry limit.");
        }

        ProfileBundleInventoryVerifier.VerifyClosedInventory(
            bundleRoot,
            manifestPath,
            manifest,
            limits.MaximumDirectoryCount);

        var snapshots = new ProfileBundleEntrySnapshot[manifest.Entries.Count];
        int totalEntryBytes = 0;
        for (int index = 0; index < manifest.Entries.Count; index++)
        {
            ProfileBundleEntry entry = manifest.Entries[index];
            int remainingBytes = limits.MaximumTotalEntryBytes - totalEntryBytes;
            if (remainingBytes <= 0)
            {
                throw new InvalidDataException(
                    $"Bundle entries exceed the {limits.MaximumTotalEntryBytes}-byte aggregate limit.");
            }

            int maximumBytes = Math.Min(limits.MaximumEntryBytes, remainingBytes);
            var fileSnapshot = ProfileBundleFileSnapshot.ReadEntry(
                bundleRoot,
                entry,
                maximumBytes);
            totalEntryBytes = checked(totalEntryBytes + fileSnapshot.Length);
            snapshots[index] = new ProfileBundleEntrySnapshot(entry, fileSnapshot);
        }

        ProfileBundleInventoryVerifier.VerifyClosedInventory(
            bundleRoot,
            manifestPath,
            manifest,
            limits.MaximumDirectoryCount);

        return new ProfileBundleEntrySnapshotCollection(manifest, snapshots);
    }
}
