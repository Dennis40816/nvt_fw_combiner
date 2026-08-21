using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests fail-closed admission of untrusted update-catalog metadata.</summary>
public sealed class UpdateCatalogValidatorTests
{
    private const string PackageHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ManifestHash =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    /// <summary>Valid entries are normalized and ordered without changing their identity.</summary>
    [Fact]
    public void ValidCatalogPublishesCanonicalDescendingVersionsAndNewestUpdate()
    {
        UpdateCatalogDocument document = Catalog(
            Version("0.10.5", "2026-08-19T00:00:00Z"),
            Version("1.0.0", "2026-08-21T00:00:00Z"),
            Version("0.10.6", "2026-08-20T00:00:00Z"));

        UpdateCatalogValidationResult result = UpdateCatalogValidator.Validate(document);

        Assert.True(result.IsValid);
        UpdateCatalogSnapshot snapshot = Assert.IsType<UpdateCatalogSnapshot>(result.Snapshot);
        Assert.Equal(["1.0.0", "0.10.6", "0.10.5"], snapshot.Versions.Select(item => item.Version.ToString()));
        Assert.Equal(
            "1.0.0",
            snapshot.FindNewestNewerThan(ManagedAppVersion.Parse("0.10.5"))?.Version.ToString());
        Assert.Null(snapshot.FindNewestNewerThan(ManagedAppVersion.Parse("1.0.0")));
    }

    /// <summary>Moving the configured source folder does not change a package identity.</summary>
    [Fact]
    public void PackageIdentityDoesNotIncludeConfiguredSourcePath()
    {
        UpdateCatalogValidationResult result = UpdateCatalogValidator.Validate(
            Catalog(Version("0.10.6", "2026-08-21T00:00:00Z")));

        UpdateCatalogVersionSnapshot version = Assert.Single(Assert.IsType<UpdateCatalogSnapshot>(result.Snapshot).Versions);

        Assert.Equal("packages/NvtFwCombiner-v0.10.6-win-x64.zip", version.PackagePath.Value);
        Assert.DoesNotContain("C:", version.Identity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", version.Identity, StringComparison.Ordinal);
        Assert.Contains(PackageHash, version.Identity, StringComparison.Ordinal);
    }

    /// <summary>Every invalid catalog shape fails closed with a stable issue.</summary>
    [Fact]
    public void InvalidCatalogFailsClosedWithStableIssue()
    {
        foreach ((UpdateCatalogDocument document, UpdateCatalogIssueCode expectedIssue) in InvalidCatalogs())
        {
            UpdateCatalogValidationResult result = UpdateCatalogValidator.Validate(document);

            Assert.False(result.IsValid);
            Assert.Null(result.Snapshot);
            Assert.Contains(result.Issues, issue => issue.Code == expectedIssue);
        }
    }

    /// <summary>Every forbidden relative-path shape is rejected before filesystem access.</summary>
    [Theory]
    [MemberData(nameof(UnsafePackagePaths))]
    public void UnsafePackagePathShapesFailClosed(string packagePath)
    {
        UpdateCatalogVersionDocument version = Version("0.10.6", "2026-08-21T00:00:00Z") with
        {
            PackagePath = packagePath,
        };

        UpdateCatalogValidationResult result = UpdateCatalogValidator.Validate(Catalog(version));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == UpdateCatalogIssueCode.UnsafePackagePath);
    }

    /// <summary>A null package path is rejected without dereference or filesystem access.</summary>
    [Fact]
    public void NullPackagePathFailsClosed()
    {
        UpdateCatalogVersionDocument version = Version("0.10.6", "2026-08-21T00:00:00Z") with
        {
            PackagePath = null,
        };

        UpdateCatalogValidationResult result = UpdateCatalogValidator.Validate(Catalog(version));

        Assert.Contains(result.Issues, issue => issue.Code == UpdateCatalogIssueCode.UnsafePackagePath);
    }

    /// <summary>Windows device names never become a catalog-owned filesystem identity.</summary>
    [Theory]
    [InlineData("packages/CON/update.zip")]
    [InlineData("packages/nul.zip")]
    [InlineData("packages/COM1.bin/update.zip")]
    [InlineData("packages/LPT9/update.zip")]
    [InlineData("packages/CONIN$/update.zip")]
    public void WindowsDeviceNamesFailClosed(string packagePath)
    {
        UpdateCatalogVersionDocument version = Version("0.10.6", "2026-08-21T00:00:00Z") with
        {
            PackagePath = packagePath,
        };

        UpdateCatalogValidationResult result = UpdateCatalogValidator.Validate(Catalog(version));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == UpdateCatalogIssueCode.UnsafePackagePath);
    }

    /// <summary>The catalog entry-count ceiling fails the complete catalog without partial publication.</summary>
    [Fact]
    public void TooManyVersionsFailClosedWithoutPartialCatalog()
    {
        UpdateCatalogVersionDocument[] versions = [.. Enumerable.Range(0, UpdateCatalogValidator.MaximumVersionCount + 1)
            .Select(index => Version($"1.0.{index}", "2026-08-21T00:00:00Z"))];

        UpdateCatalogValidationResult result = UpdateCatalogValidator.Validate(Catalog(versions));

        Assert.False(result.IsValid);
        Assert.Null(result.Snapshot);
        Assert.Contains(result.Issues, issue => issue.Code == UpdateCatalogIssueCode.TooManyVersions);
    }

    /// <summary>Forbidden package path values that exercise every bounded path clause.</summary>
    public static TheoryData<string> UnsafePackagePaths =>
    [
        string.Empty,
        " ",
        new string('a', 513) + ".zip",
        "/absolute.zip",
        @"packages\file.zip",
        "packages:file.zip",
        "packages/\u0001.zip",
        "packages/file.bin",
        "packages//file.zip",
        "packages/./file.zip",
        "packages/../file.zip",
        "packages/name /file.zip",
        "packages/name./file.zip",
    ];

    private static IEnumerable<(UpdateCatalogDocument Document, UpdateCatalogIssueCode Issue)> InvalidCatalogs()
    {
        UpdateCatalogVersionDocument valid = Version("0.10.6", "2026-08-21T00:00:00Z");
        yield return (new(2, "NVT FW Combiner", "win-x64", [valid]), UpdateCatalogIssueCode.InvalidSchemaVersion);
        yield return (new(1, "Other", "win-x64", [valid]), UpdateCatalogIssueCode.InvalidProduct);
        yield return (new(1, "NVT FW Combiner", "linux-x64", [valid]), UpdateCatalogIssueCode.InvalidRuntimeIdentifier);
        yield return (Catalog(valid with { Version = "0.10.6-beta.1" }), UpdateCatalogIssueCode.InvalidVersion);
        yield return (Catalog(valid with { PublishedAt = "2026-08-21T08:00:00+08:00" }), UpdateCatalogIssueCode.InvalidPublishedAt);
        yield return (Catalog(valid with { PackagePath = "../escape.zip" }), UpdateCatalogIssueCode.UnsafePackagePath);
        yield return (Catalog(valid with { PackagePath = "C:/escape.zip" }), UpdateCatalogIssueCode.UnsafePackagePath);
        yield return (Catalog(valid with { PackageSize = 0 }), UpdateCatalogIssueCode.InvalidPackageSize);
        yield return (Catalog(valid with { PackageSize = 80_000_001 }), UpdateCatalogIssueCode.InvalidPackageSize);
        yield return (Catalog(valid with { PackageSha256 = "ABC" }), UpdateCatalogIssueCode.InvalidSha256);
        yield return (Catalog(valid with { ReleaseManifestSha256 = "xyz" }), UpdateCatalogIssueCode.InvalidSha256);
        yield return (Catalog(valid with { ReleaseNotes = new string('x', 65_537) }), UpdateCatalogIssueCode.ReleaseNotesTooLarge);
        yield return (Catalog(valid, valid), UpdateCatalogIssueCode.DuplicateVersion);
        yield return (new(1, "NVT FW Combiner", "win-x64", []), UpdateCatalogIssueCode.EmptyVersions);
        yield return (new(1, "NVT FW Combiner", "win-x64", [null]), UpdateCatalogIssueCode.InvalidVersion);
    }

    private static UpdateCatalogDocument Catalog(params UpdateCatalogVersionDocument[] versions)
    {
        return new(1, "NVT FW Combiner", "win-x64", versions);
    }

    private static UpdateCatalogVersionDocument Version(string version, string publishedAt)
    {
        return new(
            version,
            publishedAt,
            $"packages/NvtFwCombiner-v{version}-win-x64.zip",
            42,
            PackageHash,
            ManifestHash,
            $"Release {version}");
    }
}
