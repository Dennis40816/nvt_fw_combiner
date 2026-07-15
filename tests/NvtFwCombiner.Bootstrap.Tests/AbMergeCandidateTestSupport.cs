using System.Text.Json;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Materializes non-routed AB candidate bundles for direct test evidence only.</summary>
internal static class AbMergeCandidateTestSupport
{
    internal static TrustedProfileBundleCatalog LoadSourceCandidateCatalog(
        TempWorkspace workspace,
        string bundleDirectory,
        string bundleContentHash)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleContentHash);

        string sourceRoot = RepositoryPaths.FromRepositoryRoot("profiles", "built-in", bundleDirectory);
        string manifestPath = Path.Combine(sourceRoot, "profile-bundle.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        _ = workspace.Write("profile-bundle.json", File.ReadAllBytes(manifestPath));

        foreach (JsonElement entry in manifest.RootElement.GetProperty("entries").EnumerateArray())
        {
            string relativePath = entry.GetProperty("path").GetString()!;
            string sourcePath = StringComparer.Ordinal.Equals(entry.GetProperty("kind").GetString(), "schema")
                ? RepositoryPaths.FromRepositoryRoot(
                    "profiles",
                    "schema-source",
                    "sha256",
                    entry.GetProperty("contentHash").GetString()!,
                    Path.GetFileName(relativePath))
                : Path.Combine(sourceRoot, relativePath);
            _ = workspace.Write(relativePath, File.ReadAllBytes(sourcePath));
        }

        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            workspace.Root,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(bundleContentHash, "built-in-profile-bundle-v2"),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
        return TrustedProfileBundleCatalogProjection.Create(bundle.CreateDocumentProjection());
    }
}
