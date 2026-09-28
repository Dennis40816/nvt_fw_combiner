namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>The existing built-in runtime admission bounds, shared with the build generator.</summary>
internal static class BuiltInProfileBundleAdmissionSettings
{
    internal static ProfileBundleLoadLimits Limits { get; } = new(
        maximumManifestBytes: 16384,
        maximumJsonDepth: 32,
        new ProfileBundleEntrySnapshotLimits(16, 131072, 262144, 8));
}
