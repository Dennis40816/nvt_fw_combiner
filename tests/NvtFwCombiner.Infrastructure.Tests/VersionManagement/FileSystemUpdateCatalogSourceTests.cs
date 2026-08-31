using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Tests bounded, strict, location-independent catalog discovery.</summary>
public sealed class FileSystemUpdateCatalogSourceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A Registry may select an exact Catalog filename without rebuilding the App.</summary>
    [Fact]
    public async Task ExactRenamedCatalogPathPublishesItsByteIdentity()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await WriteCatalogAsync(root);
        string original = Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName);
        string renamed = Path.Combine(root, "catalog-publication-42.json");
        File.Move(original, renamed);
        byte[] expectedBytes = await File.ReadAllBytesAsync(
            renamed,
            TestContext.Current.CancellationToken);

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadCatalogAsync(
            renamed,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ContentIdentity!.SchemaVersion);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expectedBytes))
                .ToLowerInvariant(),
            result.ContentIdentity.Sha256);
    }

    /// <summary>A direct source prefers strict v2 while exact-path loading dispatches by schema.</summary>
    [Fact]
    public async Task DirectSourcePrefersPresentValidV2AndPublishesItsSchemaIdentity()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await WriteCatalogAsync(root);
        await WriteCatalogV2Async(root, "manual-only");

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ContentIdentity!.SchemaVersion);
        Assert.Equal(
            UpdateNotificationPolicy.ManualOnly,
            Assert.Single(result.Snapshot!.Versions).NotificationPolicy);
    }

    /// <summary>Direct roots deliberately return to v1 when a previously admitted v2 file is removed.</summary>
    [Fact]
    public async Task DirectSourceUsesV1AfterPreviouslyAdmittedV2IsRemoved()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await WriteCatalogAsync(root);
        await WriteCatalogV2Async(root, "manual-only");
        var source = new FileSystemUpdateCatalogSource();

        UpdateCatalogLoadResult first = await source.LoadAsync(
            root,
            TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(root, FileSystemUpdateCatalogSource.CatalogV2FileName));
        UpdateCatalogLoadResult second = await source.LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, first.ContentIdentity!.SchemaVersion);
        Assert.Equal(1, second.ContentIdentity!.SchemaVersion);
        Assert.Equal(
            UpdateNotificationPolicy.Notify,
            Assert.Single(second.Snapshot!.Versions).NotificationPolicy);
    }

    /// <summary>A present invalid v2 publication fails closed and never falls back to valid v1.</summary>
    [Fact]
    public async Task PresentInvalidV2FailsClosedWithoutV1Fallback()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await WriteCatalogAsync(root);
        await WriteCatalogV2Async(root, "Notify");

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateCatalogLoadIssue.InvalidManifest, result.Issue);
    }

    /// <summary>A v1 document published under the reserved v2 name is invalid and cannot expose v1 fallback.</summary>
    [Fact]
    public async Task V1DocumentAtV2NameFailsClosedWithoutV1Fallback()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await WriteCatalogAsync(root);
        File.Copy(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName),
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogV2FileName));

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateCatalogLoadIssue.InvalidManifest, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>A v2 document published under the reserved v1 name is invalid even when no v2 file exists.</summary>
    [Fact]
    public async Task V2DocumentAtV1NameFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await WriteCatalogV2Async(root, "notify");
        File.Move(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogV2FileName),
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName));

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateCatalogLoadIssue.InvalidManifest, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>Every non-exact v2 policy shape is invalid and cannot expose stale v1 authority.</summary>
    [Theory]
    [MemberData(nameof(InvalidStrictV2Documents))]
    public async Task StrictV2PolicyShapeFailsClosedWithoutV1Fallback(
        string caseId,
        string v2Document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentNullException.ThrowIfNull(v2Document);
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await WriteCatalogAsync(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogV2FileName),
            v2Document,
            TestContext.Current.CancellationToken);

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateCatalogLoadIssue.InvalidManifest, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>Moving identical source content preserves every admitted package identity.</summary>
    [Fact]
    public async Task IdenticalCatalogMovedToAnotherFolderKeepsIdentity()
    {
        using var workspace = TempWorkspace.Create();
        string firstRoot = workspace.PathFor("first");
        string secondRoot = workspace.PathFor("second");
        _ = Directory.CreateDirectory(firstRoot);
        await WriteCatalogAsync(firstRoot);
        Directory.Move(firstRoot, secondRoot);
        var source = new FileSystemUpdateCatalogSource();

        UpdateCatalogLoadResult moved = await source.LoadAsync(
            secondRoot,
            TestContext.Current.CancellationToken);

        Assert.True(moved.IsSuccess);
        UpdateCatalogVersionSnapshot version = Assert.Single(moved.Snapshot!.Versions);
        Assert.DoesNotContain(firstRoot, version.Identity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secondRoot, version.Identity, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Unknown fields fail the complete catalog instead of being ignored.</summary>
    [Fact]
    public async Task UnknownFieldFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName),
            """
            {
              "schemaVersion": 1,
              "product": "NVT FW Combiner",
              "runtimeIdentifier": "win-x64",
              "versions": [],
              "unexpected": true
            }
            """,
            TestContext.Current.CancellationToken);

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateCatalogLoadIssue.InvalidManifest, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>Duplicate keys cannot select a last-wins catalog authority.</summary>
    [Fact]
    public async Task DuplicateCatalogPropertyFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName),
            """
            {
              "schemaVersion": 1,
              "product": "Other",
              "product": "NVT FW Combiner",
              "runtimeIdentifier": "win-x64",
              "versions": []
            }
            """,
            TestContext.Current.CancellationToken);

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCatalogLoadIssue.InvalidManifest, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>A catalog larger than one MiB is rejected before JSON allocation.</summary>
    [Fact]
    public async Task OversizedCatalogFailsBeforeParse()
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName),
            new byte[FileSystemUpdateCatalogSource.MaximumCatalogBytes + 1],
            TestContext.Current.CancellationToken);

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCatalogLoadIssue.CatalogTooLarge, result.Issue);
    }

    /// <summary>Access denial remains distinguishable from transient source unavailability.</summary>
    [Fact]
    public void PermissionFailureHasDedicatedStableIssue()
    {
        Assert.Equal(
            UpdateCatalogLoadIssue.PermissionDenied,
            FileSystemUpdateCatalogSource.ClassifyReadFailure(new UnauthorizedAccessException()));
        Assert.Equal(
            UpdateCatalogLoadIssue.SourceUnavailable,
            FileSystemUpdateCatalogSource.ClassifyReadFailure(new IOException()));
        Assert.Equal(
            UpdateCatalogLoadIssue.SourceMissing,
            FileSystemUpdateCatalogSource.ClassifyReadFailure(new FileNotFoundException()));
        Assert.Equal(
            UpdateCatalogLoadIssue.SourceMissing,
            FileSystemUpdateCatalogSource.ClassifyReadFailure(new DirectoryNotFoundException()));
    }

    /// <summary>An invalid confirmed path is a visible unsafe-source result, not an escaped exception.</summary>
    [Fact]
    public async Task InvalidSourcePathFailsClosed()
    {
        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            "\0",
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCatalogLoadIssue.UnsafeSource, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>A missing folder or missing root catalog is an offline source, not an invalid verified catalog.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSourceOrCatalogReturnsSourceMissing(bool createFolder)
    {
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        if (createFolder)
        {
            _ = Directory.CreateDirectory(root);
        }

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateCatalogLoadIssue.SourceMissing, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>Empty, malformed, and JSON-null documents never publish partial catalog state.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    public async Task InvalidRawCatalogFailsClosed(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("source");
        _ = Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName),
            content,
            TestContext.Current.CancellationToken);

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            root,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateCatalogLoadIssue.InvalidManifest, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>A configured source reparse point is never followed into another tree.</summary>
    [Fact]
    public async Task ReparseSourceRootFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string target = workspace.PathFor("real-source");
        string link = workspace.PathFor("linked-source");
        _ = Directory.CreateDirectory(target);
        await WriteCatalogAsync(target);
        _ = Directory.CreateSymbolicLink(link, target);

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
            link,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateCatalogLoadIssue.UnsafeSource, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>An exact Catalog path cannot cross a reparse ancestor below its apparent parent.</summary>
    [Fact]
    public async Task ExactCatalogPathRejectsIntermediateReparseAncestor()
    {
        using var workspace = TempWorkspace.Create();
        string target = workspace.PathFor("real-source");
        string child = Path.Combine(target, "child");
        string link = workspace.PathFor("linked-source");
        _ = Directory.CreateDirectory(child);
        await WriteCatalogAsync(child);
        _ = Directory.CreateSymbolicLink(link, target);

        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadCatalogAsync(
            Path.Combine(link, "child", FileSystemUpdateCatalogSource.CatalogFileName),
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCatalogLoadIssue.UnsafeSource, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>The Registry-only exact-file API never resolves a relative Catalog path.</summary>
    [Fact]
    public async Task ExactCatalogPathRejectsRelativeLocator()
    {
        UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadCatalogAsync(
            "update-catalog.v1.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCatalogLoadIssue.UnsafeSource, result.Issue);
    }

    /// <summary>The embedded schema and runtime share one version-independent package ceiling.</summary>
    [Theory]
    [InlineData("0.10.6")]
    [InlineData("1.0.2")]
    [InlineData("2.0.0")]
    [InlineData("9.9.9")]
    public async Task PackageSizeCeilingIsVersionIndependent(string version)
    {
        foreach ((long packageSize, bool expectedSuccess) in new[]
                 {
                     (134_217_728L, true),
                     (134_217_729L, false),
                     (0L, false),
                     (-1L, false),
                 })
        {
            using var workspace = TempWorkspace.Create();
            string root = workspace.PathFor("source");
            _ = Directory.CreateDirectory(root);
            var document = new UpdateCatalogDocument(
                1,
                "NVT FW Combiner",
                "win-x64",
                [new(
                    version,
                    "2026-08-26T00:00:00Z",
                    $"packages/NvtFwCombiner-v{version}-win-x64.zip",
                    packageSize,
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                    $"Release {version}")]);
            await File.WriteAllTextAsync(
                Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName),
                JsonSerializer.Serialize(document, JsonOptions),
                TestContext.Current.CancellationToken);

            UpdateCatalogLoadResult result = await new FileSystemUpdateCatalogSource().LoadAsync(
                root,
                TestContext.Current.CancellationToken);

            Assert.Equal(expectedSuccess, result.IsSuccess);
            Assert.Equal(
                expectedSuccess ? UpdateCatalogLoadIssue.None : UpdateCatalogLoadIssue.InvalidManifest,
                result.Issue);
        }
    }

    private static async Task WriteCatalogAsync(string root)
    {
        const string packageHash =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        const string manifestHash =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        var document = new UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [new(
                "0.10.6",
                "2026-08-21T00:00:00Z",
                "packages/NvtFwCombiner-v0.10.6-win-x64.zip",
                42,
                packageHash,
                manifestHash,
                "Release 0.10.6")]);
        await File.WriteAllTextAsync(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogFileName),
            JsonSerializer.Serialize(document, JsonOptions),
            TestContext.Current.CancellationToken);
    }

    private static async Task WriteCatalogV2Async(string root, string policy)
    {
        var document = new UpdateCatalogV2Document(
            2,
            "NVT FW Combiner",
            "win-x64",
            [new(
                "1.0.8",
                "2026-09-01T00:00:00Z",
                "packages/NvtFwCombiner-v1.0.8-win-x64.zip",
                42,
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                "Release 1.0.8",
                policy)]);
        await File.WriteAllTextAsync(
            Path.Combine(root, FileSystemUpdateCatalogSource.CatalogV2FileName),
            JsonSerializer.Serialize(document, JsonOptions),
            TestContext.Current.CancellationToken);
    }

    /// <summary>Malformed v2 documents exercising required, typed, closed policy shape.</summary>
    public static TheoryData<string, string> InvalidStrictV2Documents
    {
        get
        {
            const string prefix =
                """
                {"schemaVersion":2,"product":"NVT FW Combiner","runtimeIdentifier":"win-x64","versions":[{"version":"1.0.8","publishedAt":"2026-09-01T00:00:00Z","packagePath":"packages/NvtFwCombiner-v1.0.8-win-x64.zip","packageSize":42,"packageSha256":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","releaseManifestSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","releaseNotes":"Release 1.0.8"
                """;
            TheoryData<string, string> data = [];
            data.Add("missing-policy", prefix + "}]}");
            data.Add("null-policy", prefix + ",\"notificationPolicy\":null}]}");
            data.Add("numeric-policy", prefix + ",\"notificationPolicy\":2}]}");
            data.Add(
                "unexpected-field",
                prefix + ",\"notificationPolicy\":\"notify\",\"unexpected\":true}]}");
            data.Add(
                "duplicate-policy",
                prefix + ",\"notificationPolicy\":\"manual-only\",\"notificationPolicy\":\"notify\"}]}");
            return data;
        }
    }
}
