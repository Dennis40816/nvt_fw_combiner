namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Captures manifest and entry snapshots from one closed bundle source.</summary>
internal interface IProfileBundleSnapshotSource
{
    ProfileBundleFileSnapshot ReadManifest(int maximumBytes);

    ProfileBundleEntrySnapshotCollection CaptureEntries(
        ProfileBundleManifest manifest,
        ProfileBundleEntrySnapshotLimits limits);
}

/// <summary>Filesystem-backed source for one bundle root and manifest path.</summary>
internal sealed class DirectoryProfileBundleSnapshotSource : IProfileBundleSnapshotSource
{
    private readonly string _bundleRoot;
    private readonly string _manifestPath;

    internal DirectoryProfileBundleSnapshotSource(string bundleRoot, string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        _bundleRoot = bundleRoot;
        _manifestPath = manifestPath;
    }

    public ProfileBundleFileSnapshot ReadManifest(int maximumBytes)
    {
        return ProfileBundleFileSnapshot.ReadManifest(_bundleRoot, _manifestPath, maximumBytes);
    }

    public ProfileBundleEntrySnapshotCollection CaptureEntries(
        ProfileBundleManifest manifest,
        ProfileBundleEntrySnapshotLimits limits)
    {
        return ProfileBundleEntrySnapshotCollection.Capture(_bundleRoot, _manifestPath, manifest, limits);
    }
}
