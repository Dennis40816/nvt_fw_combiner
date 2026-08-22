using System.Globalization;
using System.Text;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Validates configured-folder catalog facts without filesystem or source-path authority.</summary>
public static class UpdateCatalogValidator
{
    /// <summary>The only catalog schema version currently admitted.</summary>
    public const int CurrentSchemaVersion = 1;
    /// <summary>The maximum number of catalog entries processed.</summary>
    public const int MaximumVersionCount = 128;
    /// <summary>The maximum UTF-8 release-note length per entry.</summary>
    public const int MaximumReleaseNotesBytes = 64 * 1024;
    /// <summary>The maximum declared package length in bytes.</summary>
    public const long MaximumPackageBytes = 80_000_000;
    private const string Product = "NVT FW Combiner";
    private const string RuntimeIdentifier = "win-x64";

    /// <summary>Validates and snapshots an untrusted catalog document.</summary>
    /// <param name="document">The deserialized catalog document.</param>
    /// <returns>A fail-closed validation result.</returns>
    public static UpdateCatalogValidationResult Validate(UpdateCatalogDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<UpdateCatalogIssue>();
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            issues.Add(new(UpdateCatalogIssueCode.InvalidSchemaVersion));
        }
        if (!string.Equals(document.Product, Product, StringComparison.Ordinal))
        {
            issues.Add(new(UpdateCatalogIssueCode.InvalidProduct));
        }
        if (!string.Equals(document.RuntimeIdentifier, RuntimeIdentifier, StringComparison.Ordinal))
        {
            issues.Add(new(UpdateCatalogIssueCode.InvalidRuntimeIdentifier));
        }

        IReadOnlyList<UpdateCatalogVersionDocument?> versions = document.Versions ?? [];
        if (versions.Count == 0)
        {
            issues.Add(new(UpdateCatalogIssueCode.EmptyVersions));
        }
        if (versions.Count > MaximumVersionCount)
        {
            issues.Add(new(UpdateCatalogIssueCode.TooManyVersions));
        }

        var admitted = new List<UpdateCatalogVersionSnapshot>(Math.Min(versions.Count, MaximumVersionCount));
        var seen = new HashSet<ManagedAppVersion>();
        foreach (UpdateCatalogVersionDocument? entry in versions.Take(MaximumVersionCount))
        {
            if (entry is null)
            {
                issues.Add(new(UpdateCatalogIssueCode.InvalidVersion));
                continue;
            }
            string? versionText = entry.Version;
            if (!ManagedAppVersion.TryParse(versionText, out ManagedAppVersion version))
            {
                issues.Add(new(UpdateCatalogIssueCode.InvalidVersion, versionText));
                continue;
            }
            if (!seen.Add(version))
            {
                issues.Add(new(UpdateCatalogIssueCode.DuplicateVersion, versionText));
            }

            bool publishedValid = TryParseUtc(entry.PublishedAt, out DateTimeOffset publishedAt);
            if (!publishedValid)
            {
                issues.Add(new(UpdateCatalogIssueCode.InvalidPublishedAt, versionText));
            }

            bool pathValid = TryCreatePackagePath(entry.PackagePath, out UpdateCatalogPackagePath packagePath);
            if (!pathValid)
            {
                issues.Add(new(UpdateCatalogIssueCode.UnsafePackagePath, versionText));
            }

            bool sizeValid = entry.PackageSize is > 0 and <= MaximumPackageBytes;
            if (!sizeValid)
            {
                issues.Add(new(UpdateCatalogIssueCode.InvalidPackageSize, versionText));
            }

            bool packageHashValid = IsLowerSha256(entry.PackageSha256);
            bool manifestHashValid = IsLowerSha256(entry.ReleaseManifestSha256);
            if (!packageHashValid || !manifestHashValid)
            {
                issues.Add(new(UpdateCatalogIssueCode.InvalidSha256, versionText));
            }

            string? releaseNotes = entry.ReleaseNotes;
            bool releaseNotesPresent = releaseNotes is not null;
            bool releaseNotesValid = releaseNotesPresent &&
                                     Encoding.UTF8.GetByteCount(releaseNotes!) <= MaximumReleaseNotesBytes;
            if (!releaseNotesPresent)
            {
                issues.Add(new(UpdateCatalogIssueCode.MissingReleaseNotes, versionText));
            }
            else if (!releaseNotesValid)
            {
                issues.Add(new(UpdateCatalogIssueCode.ReleaseNotesTooLarge, versionText));
            }

            if (publishedValid && pathValid && sizeValid && packageHashValid && manifestHashValid &&
                releaseNotesValid)
            {
                admitted.Add(new(
                    version,
                    publishedAt,
                    packagePath,
                    entry.PackageSize,
                    entry.PackageSha256!,
                    entry.ReleaseManifestSha256!,
                    releaseNotes!));
            }
        }

        if (issues.Count > 0)
        {
            return UpdateCatalogValidationResult.Failure(issues);
        }

        admitted.Sort(static (left, right) => right.Version.CompareTo(left.Version));
        return UpdateCatalogValidationResult.Success(new UpdateCatalogSnapshot([.. admitted]));
    }

    private static bool TryParseUtc(string? value, out DateTimeOffset publishedAt)
    {
        publishedAt = default;
        string[] canonicalUtcFormats =
        [
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        ];
        return value is not null &&
               DateTimeOffset.TryParseExact(
                   value,
                   canonicalUtcFormats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                   out publishedAt) &&
               publishedAt.Offset == TimeSpan.Zero;
    }

    private static bool TryCreatePackagePath(string? value, out UpdateCatalogPackagePath path)
    {
        path = default;
        if (value is null ||
            value.Length < 5 ||
            !ManagedRelativePathRules.IsSafeFilePath(value) ||
            !value.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        path = new(value);
        return true;
    }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}
