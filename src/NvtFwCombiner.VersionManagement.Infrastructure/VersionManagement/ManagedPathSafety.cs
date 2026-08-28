using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal static class ManagedPathSafety
{
    internal static bool TryNormalizeExactAbsolutePath(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            !Path.IsPathFullyQualified(value) ||
            IsDeviceExtendedOrAlternateStream(value))
        {
            return false;
        }

        try
        {
            normalized = Path.GetFullPath(value);
            return PathComparer.Equals(normalized, value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool HasReparseComponent(string path)
    {
        string? current = path;
        while (!string.IsNullOrEmpty(current))
        {
            if (IsReparsePoint(current))
            {
                return true;
            }
            current = Path.GetDirectoryName(current);
        }
        return false;
    }

    internal static bool TryResolveRelativeFile(string root, string relativePath, out string resolved)
    {
        resolved = string.Empty;
        try
        {
            string fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
            string candidate = Path.GetFullPath(Path.Combine(
                fullRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(candidate) ||
                IsReparsePoint(candidate))
            {
                return false;
            }

            string? current = Path.GetDirectoryName(candidate);
            while (current is not null && !PathEquals(current, fullRoot))
            {
                if (IsReparsePoint(current))
                {
                    return false;
                }
                current = Path.GetDirectoryName(current);
            }
            resolved = candidate;
            return current is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    internal static string GetExactVersionDirectory(string versionsRoot, ManagedAppVersion version)
    {
        string fullRoot = EnsureTrailingSeparator(Path.GetFullPath(versionsRoot));
        string target = Path.GetFullPath(Path.Combine(fullRoot, version.ToString()));
        return target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            ? target
            : throw new InvalidOperationException("Managed version target escaped the versions root.");
    }

    internal static bool IsSafeExistingDirectory(string path)
    {
        return Directory.Exists(path) && !IsReparsePoint(path);
    }

    internal static bool IsSafeOwnedTree(string root)
    {
        if (!IsSafeExistingDirectory(root))
        {
            return false;
        }
        try
        {
            return !Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
                .Any(IsReparsePoint);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool IsReparsePoint(string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    internal static bool IsSafeRelativePayloadPath(string path)
    {
        return ManagedRelativePathRules.IsSafeFilePath(path);
    }

    internal static async ValueTask<byte[]?> ReadBoundedFileAsync(
        string path,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1);
        if (!File.Exists(path) || IsReparsePoint(path))
        {
            return null;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        long admittedLength = stream.Length;
        if (admittedLength is < 1 || admittedLength > maximumBytes)
        {
            return null;
        }
        byte[] bytes = new byte[checked((int)admittedLength)];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return stream.Length == admittedLength && stream.Position == admittedLength
            ? bytes
            : null;
    }

    internal static string ResolvePayloadPath(string root, string relativePath)
    {
        string fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        string path = Path.GetFullPath(Path.Combine(
            fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            ? path
            : throw new InvalidDataException("Payload path escaped the managed root.");
    }

    private static bool PathEquals(string candidate, string rootWithSeparator)
    {
        return string.Equals(
            EnsureTrailingSeparator(Path.GetFullPath(candidate)),
            rootWithSeparator,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }

    private static bool IsDeviceExtendedOrAlternateStream(string path)
    {
        if (path.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            path.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            return true;
        }
        string? root = Path.GetPathRoot(path);
        return root is null || path.AsSpan(root.Length).Contains(':');
    }

    internal static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
