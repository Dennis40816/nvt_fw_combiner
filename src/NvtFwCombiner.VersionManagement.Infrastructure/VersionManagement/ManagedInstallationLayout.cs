using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Resolves the stable managed root without making its absolute path a package identity.</summary>
public static class ManagedInstallationLayout
{
    /// <summary>Resolves a side-by-side payload base or preserves an unmanaged development base.</summary>
    /// <param name="applicationBaseDirectory">Running payload base directory.</param>
    /// <returns>The stable managed root.</returns>
    public static string ResolveManagedRoot(string applicationBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        var payload = new DirectoryInfo(Path.GetFullPath(applicationBaseDirectory));
        DirectoryInfo? versions = payload.Parent;
        return ManagedAppVersion.TryParse(payload.Name, out _) &&
               versions is not null &&
               string.Equals(
                   versions.Name,
                   FileSystemManagedVersionRepository.VersionsDirectoryName,
                   StringComparison.OrdinalIgnoreCase) &&
               versions.Parent is not null
            ? versions.Parent.FullName
            : payload.FullName;
    }
}
