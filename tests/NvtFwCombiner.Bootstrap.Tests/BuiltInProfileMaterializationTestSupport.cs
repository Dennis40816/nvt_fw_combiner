using System.Text.Json;
using System.Xml.Linq;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Reconstructs manifest-pinned test bundles from the Bootstrap materialization declaration.</summary>
internal static class BuiltInProfileMaterializationTestSupport
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
            string sourcePath = ResolveManifestEntrySource(bundleDirectory, entry);
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

    internal static string ResolveManifestEntrySource(string bundleDirectory, JsonElement entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        string relativePath = entry.GetProperty("path").GetString()!;
        string contentHash = entry.GetProperty("contentHash").GetString()!;
        if (StringComparer.Ordinal.Equals(entry.GetProperty("kind").GetString(), "schema"))
        {
            return V2StandardMergeGoldenTestSupport.ResolveContractSchemaPath(relativePath, contentHash);
        }

        string builtInRoot = RepositoryPaths.FromRepositoryRoot("profiles", "built-in");
        XElement bundle = XDocument.Load(RepositoryPaths.FromRepositoryRoot(
                "src",
                "NvtFwCombiner.Bootstrap",
                "NvtFwCombiner.Bootstrap.csproj"))
            .Descendants("BuiltInProfileBundle")
            .Single(item => StringComparer.Ordinal.Equals(item.Attribute("Include")?.Value, bundleDirectory));
        string? destination = bundle.Element("CanonicalFirmwareFamilyDestination")?.Value;
        string source = StringComparer.Ordinal.Equals(Normalize(destination), Normalize(relativePath))
            ? bundle.Element("CanonicalFirmwareFamilySource")?.Value
                ?? throw new InvalidOperationException($"Canonical family source is missing for '{bundleDirectory}'.")
            : Path.Combine(bundleDirectory, relativePath);
        return ResolveUnderRoot(builtInRoot, source);
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        string normalizedRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string platformPath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        string resolved = Path.GetFullPath(Path.Combine(root, platformPath));
        return resolved.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            ? resolved
            : throw new InvalidOperationException(
                $"Materialization source escapes the built-in profile root: {relativePath}");
    }

    private static string? Normalize(string? path)
    {
        return path?.Replace('\\', '/');
    }
}
