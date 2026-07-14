namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Projects profile-bundle manifest entries onto the shared closed-root inventory verifier.</summary>
internal static class ProfileBundleInventoryVerifier
{
    internal static void VerifyClosedInventory(
        string bundleRoot,
        string manifestPath,
        ProfileBundleManifest manifest,
        int maximumDirectoryCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(manifest);

        ClosedContentRootInventoryVerifier.VerifyClosedInventory(
            bundleRoot,
            manifestPath,
            [.. manifest.Entries.Select(static entry => entry.Path)],
            maximumDirectoryCount);
    }
}
