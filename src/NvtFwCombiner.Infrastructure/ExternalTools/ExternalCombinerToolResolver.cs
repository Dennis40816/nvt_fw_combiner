using System.Security.Cryptography;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal sealed class ExternalCombinerToolResolver
{
    private readonly ExternalCombinerToolRegistry _registry;
    private readonly string _toolRoot;

    internal ExternalCombinerToolResolver(ExternalCombinerToolRegistry registry, string toolRoot)
    {
        _registry = registry;
        _toolRoot = Path.GetFullPath(toolRoot);
    }

    internal bool TryResolve(
        string toolBindingId,
        out ExternalCombinerToolManifest? manifest,
        out string? executablePath,
        out CompositionIssue? issue)
    {
        executablePath = null;
        try
        {
            manifest = _registry.Resolve(toolBindingId);
        }
        catch (KeyNotFoundException)
        {
            manifest = null;
            issue = new CompositionIssue(
                "external-tool.binding.unknown",
                $"External combiner tool binding '{toolBindingId}' is not registered.");
            return false;
        }

        issue = null;
        if (!IsSafePathSegment(manifest.ToolId) || !IsSafePathSegment(manifest.ToolVersion))
        {
            issue = new CompositionIssue(
                "external-tool.binding.path-unsafe",
                "External combiner tool binding contains unsafe path segments.");
            return false;
        }

        string resolvedPath = Path.GetFullPath(Path.Combine(
            _toolRoot,
            manifest.ToolId,
            manifest.ToolVersion,
            manifest.ExecutableName));
        if (!IsInsideDirectory(_toolRoot, resolvedPath))
        {
            issue = new CompositionIssue(
                "external-tool.executable.path-escape",
                "External combiner executable path escapes the approved tool root.");
            return false;
        }

        if (!File.Exists(resolvedPath))
        {
            issue = new CompositionIssue(
                "external-tool.executable.missing",
                "External combiner executable was not found.");
            return false;
        }

        string actualSha256 = GetLowerSha256(resolvedPath);
        if (!string.Equals(actualSha256, manifest.Sha256, StringComparison.Ordinal))
        {
            issue = new CompositionIssue(
                "external-tool.executable-sha.mismatch",
                "External combiner executable SHA-256 does not match its manifest.");
            return false;
        }

        executablePath = resolvedPath;
        return true;
    }

    internal static bool IsInsideDirectory(string root, string candidate)
    {
        string relative = Path.GetRelativePath(root, candidate);
        return relative != "." &&
               !relative.StartsWith("..", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }

    private static bool IsSafePathSegment(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value != "." &&
               value != ".." &&
               value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;
    }

    private static string GetLowerSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
