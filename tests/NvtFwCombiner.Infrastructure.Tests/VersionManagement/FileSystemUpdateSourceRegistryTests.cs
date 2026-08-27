using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies bounded, stable filesystem registry reads.</summary>
public sealed class FileSystemUpdateSourceRegistryTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions IndentedSerializerOptions =
        new(SerializerOptions) { WriteIndented = true };

    /// <summary>The deployed Registry name is stable across wire-schema revisions.</summary>
    [Fact]
    public void DeployedRegistryFileNameIsVersionIndependent()
    {
        Assert.Equal("update-source-registry.json", FileSystemUpdateSourceRegistry.RegistryFileName);
    }

    /// <summary>The approved Registry shape binds one exact Catalog publication.</summary>
    [Fact]
    public async Task ApprovedRegistryShapePublishesExactCatalogPathAndAssertions()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("update-source-registry.json");
        string catalogPath = workspace.PathFor("renamed-catalog.json");
        string catalogSha256 = new('a', 64);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                registryId = "nvt-fw-combiner-production",
                registryRevision = 12,
                publishedAtUtc = "2026-08-27T00:00:00Z",
                catalogPublication = new
                {
                    latestVersion = "1.0.1",
                    catalogSchemaVersion = 1,
                    catalogSha256,
                },
                entries = new[] { new { status = "latest", catalogPath } },
            }),
            TestContext.Current.CancellationToken);

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Snapshot!.RegistryRevision);
        Assert.Equal("nvt-fw-combiner-production", result.Snapshot.RegistryId);
        Assert.Equal(Path.GetFullPath(catalogPath), Assert.Single(result.Snapshot.Entries).CatalogPath);
        Assert.Equal(catalogSha256, result.Snapshot.CatalogPublication.CatalogSha256);
    }

    /// <summary>A higher revision cannot replace the one declared production Registry authority.</summary>
    [Fact]
    public async Task ForeignRegistryAuthorityFailsBeforeRuntimeAdmission()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        string json = JsonSerializer.Serialize(
            Document(
                999,
                [new("latest", workspace.PathFor("source/update-catalog.v1.json"))]),
            SerializerOptions).Replace(
                "nvt-fw-combiner-production",
                "foreign-registry",
                StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, json, TestContext.Current.CancellationToken);

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>A valid registry preserves policy order and publishes normalized paths.</summary>
    [Fact]
    public async Task ValidRegistryPreservesAvailableOrderAndNormalizesPaths()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        string latest = workspace.PathFor("latest/update-catalog.json");
        string first = workspace.PathFor("first/update-catalog.json");
        string second = workspace.PathFor("second/update-catalog.json");
        await WriteAsync(path, Document(
            12,
            [
                new("latest", latest),
                new("available", first),
                new("deprecated", workspace.PathFor("old/update-catalog.json")),
                new("available", second),
            ]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Snapshot!.RegistryRevision);
        Assert.Matches("^[0-9a-f]{64}$", result.Snapshot.ContentDigest);
        Assert.Equal(
            [Path.GetFullPath(latest), Path.GetFullPath(first), Path.GetFullPath(workspace.PathFor("old/update-catalog.json")), Path.GetFullPath(second)],
            result.Snapshot.Entries.Select(entry => entry.CatalogPath));
        Assert.Equal(
            [UpdateSourceRegistryEntryStatus.Latest, UpdateSourceRegistryEntryStatus.Available, UpdateSourceRegistryEntryStatus.Deprecated, UpdateSourceRegistryEntryStatus.Available],
            result.Snapshot.Entries.Select(entry => entry.Status));
    }

    /// <summary>Unknown, duplicate, missing, relative, or conflicting JSON fails closed.</summary>
    [Theory]
    [InlineData("{\"schemaVersion\":1,\"revision\":1,\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"revision\":1,\"entries\":[{\"status\":\"available\",\"path\":\"C:\\\\a\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"revision\":1,\"entries\":[{\"status\":\"latest\",\"path\":\"C:\\\\a\"},{\"status\":\"latest\",\"path\":\"C:\\\\b\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"revision\":1,\"entries\":[{\"status\":\"latest\",\"path\":\"relative\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"revision\":1,\"entries\":[{\"status\":\"latest\",\"path\":\"C:\\\\a\",\"extra\":true}]}")]
    [InlineData("{\"schemaVersion\":1,\"revision\":1,\"revision\":2,\"entries\":[{\"status\":\"latest\",\"path\":\"C:\\\\a\"}]}")]
    public async Task InvalidShapeFailsClosed(string json)
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        await File.WriteAllTextAsync(path, json, TestContext.Current.CancellationToken);

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>Paths that normalize to the same source cannot be published twice.</summary>
    [Fact]
    public async Task NormalizedDuplicatePathsFailClosed()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        string root = workspace.PathFor("same/update-catalog.json");
        await WriteAsync(path, Document(
            1,
            [new("latest", root), new("available", root)]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.Issue);
    }

    /// <summary>One durable source root cannot carry conflicting Catalog identities.</summary>
    [Fact]
    public async Task DifferentCatalogFilesUnderSameSourceRootFailClosed()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        string first = workspace.PathFor("same/catalog-a.json");
        string second = workspace.PathFor("same/catalog-b.json");
        await WriteAsync(path, Document(
            1,
            [new("latest", first), new("deprecated", second)]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.Issue);
    }

    /// <summary>Entry-count and raw-byte limits are enforced before publication.</summary>
    [Fact]
    public async Task EntryAndByteBoundsFailBeforePublication()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        await WriteAsync(path, Document(
            1,
            [.. Enumerable.Range(0, FileSystemUpdateSourceRegistry.MaximumEntries + 1)
                .Select(index => new UpdateSourceRegistryEntryDocument(
                    index == 0 ? "latest" : "available",
                    workspace.PathFor($"source-{index}/update-catalog.json")))]));
        UpdateSourceRegistryLoadResult tooMany = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, tooMany.Issue);

        await File.WriteAllBytesAsync(
            path,
            new byte[FileSystemUpdateSourceRegistry.MaximumRegistryBytes + 1],
            TestContext.Current.CancellationToken);
        UpdateSourceRegistryLoadResult tooLarge = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTooLarge, tooLarge.Issue);
    }

    /// <summary>The registry file itself cannot be a reparse point.</summary>
    [Fact]
    public async Task RegistryReparsePointIsNeverFollowed()
    {
        using var workspace = TempWorkspace.Create();
        string target = workspace.PathFor("target.json");
        string link = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        await WriteAsync(target, Document(
            1,
            [new("latest", workspace.PathFor("source/update-catalog.json"))]));
        _ = File.CreateSymbolicLink(link, target);

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(link)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.UnsafeLocator, result.Issue);
    }

    /// <summary>The digest binds exact stable bytes rather than semantic JSON equality.</summary>
    [Fact]
    public async Task ContentDigestBindsExactStableBytesNotOnlyJsonMeaning()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        string source = workspace.PathFor("source/update-catalog.json");
        await File.WriteAllTextAsync(
            path,
            CompactJson(source),
            TestContext.Current.CancellationToken);
        UpdateSourceRegistryLoadResult compact = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(
                Document(1, [new("latest", source)]),
                IndentedSerializerOptions),
            TestContext.Current.CancellationToken);
        UpdateSourceRegistryLoadResult formatted = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(compact.IsSuccess);
        Assert.True(formatted.IsSuccess);
        Assert.NotEqual(compact.Snapshot!.ContentDigest, formatted.Snapshot!.ContentDigest);
    }

    /// <summary>Device and extended locators are rejected without probing them.</summary>
    [Theory]
    [InlineData("\\\\.\\C:\\registry.json")]
    [InlineData("\\\\?\\C:\\registry.json")]
    public async Task DeviceLocatorsFailAsUnsafeWithoutFilesystemProbe(string locator)
    {
        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(locator)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.UnsafeLocator, result.Issue);
    }

    /// <summary>Permission and I/O failures retain stable distinct issue codes.</summary>
    [Fact]
    public void PermissionAndIoFailuresHaveStableDistinctIssues()
    {
        Assert.Equal(
            UpdateSourceRegistryLoadIssue.PermissionDenied,
            FileSystemUpdateSourceRegistry.ClassifyReadFailure(new UnauthorizedAccessException()));
        Assert.Equal(
            UpdateSourceRegistryLoadIssue.RegistryUnavailable,
            FileSystemUpdateSourceRegistry.ClassifyReadFailure(new IOException()));
        Assert.Equal(
            UpdateSourceRegistryLoadIssue.RegistryMissing,
            FileSystemUpdateSourceRegistry.ClassifyReadFailure(new FileNotFoundException()));
        Assert.Equal(
            UpdateSourceRegistryLoadIssue.RegistryMissing,
            FileSystemUpdateSourceRegistry.ClassifyReadFailure(new DirectoryNotFoundException()));
    }

    /// <summary>Missing Registry files and parents stay distinct from an unavailable share.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingRegistryFileOrParentReturnsRegistryMissing(bool createParent)
    {
        using var workspace = TempWorkspace.Create();
        string parent = workspace.PathFor("missing-parent");
        if (createParent)
        {
            _ = Directory.CreateDirectory(parent);
        }
        string path = Path.Combine(parent, FileSystemUpdateSourceRegistry.RegistryFileName);

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryMissing, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>A reparse point in any traversed locator component fails closed.</summary>
    [Fact]
    public async Task ReparseInAnyTraversedLocatorComponentFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string target = workspace.PathFor("target");
        string link = workspace.PathFor("link");
        _ = Directory.CreateDirectory(target);
        await WriteAsync(
            Path.Combine(target, FileSystemUpdateSourceRegistry.RegistryFileName),
            Document(1, [new("latest", workspace.PathFor("source/update-catalog.json"))]));
        _ = Directory.CreateSymbolicLink(link, target);

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(
            Path.Combine(link, FileSystemUpdateSourceRegistry.RegistryFileName))
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.UnsafeLocator, result.Issue);
    }

    /// <summary>Device, extended, and alternate-stream source entries are invalid.</summary>
    [Theory]
    [InlineData("\\\\.\\C:\\source")]
    [InlineData("\\\\?\\C:\\source")]
    [InlineData("C:\\source:stream")]
    public async Task DeviceExtendedAndAlternateStreamEntriesFailClosed(string sourceRoot)
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        await WriteAsync(path, Document(1, [new("latest", sourceRoot)]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.Issue);
    }

    /// <summary>Runtime source-root normalization matches the release publisher vectors.</summary>
    [Theory]
    [InlineData("\\\\server\\share\\update-catalog.json", true)]
    [InlineData("\\\\server\\share\\update-catalog.json\\", false)]
    [InlineData("G:\\update-catalog.json", true)]
    [InlineData("G:\\AUTO\\update-catalog.json\\", false)]
    public async Task PublisherAndRuntimeRootNormalizationVectorsStayAligned(
        string sourceRoot,
        bool expectedSuccess)
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        await WriteAsync(path, Document(1, [new("latest", sourceRoot)]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedSuccess, result.IsSuccess);
        if (!expectedSuccess)
        {
            Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.Issue);
        }
    }

    /// <summary>An existing writer prevents publication of an unstable read.</summary>
    [Fact]
    public async Task ExistingWriterHandlePreventsStableReadPublication()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        await WriteAsync(path, Document(1, [new("latest", workspace.PathFor("source/update-catalog.json"))]));
        await using var writer = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryUnavailable, result.Issue);
        Assert.Null(result.Snapshot);
    }

    /// <summary>A moved complete publication is selected only from its newly published absolute root.</summary>
    [Fact]
    public async Task RelocatedPublicationUsesRegistryPathsWithoutACompiledDefaultRoot()
    {
        using var workspace = TempWorkspace.Create();
        string original = workspace.PathFor("original-publication");
        string relocated = workspace.PathFor("relocated-publication");
        _ = Directory.CreateDirectory(Path.Combine(original, "latest", "packages"));
        _ = Directory.CreateDirectory(Path.Combine(original, "fallback", "packages"));
        Directory.Move(original, relocated);
        string registryPath = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        string latest = Path.Combine(relocated, "latest");
        string fallback = Path.Combine(relocated, "fallback");
        await WriteAsync(registryPath, Document(
            2,
            [
                new("latest", Path.Combine(latest, "renamed-catalog.json")),
                new("available", Path.Combine(fallback, "renamed-catalog.json")),
            ]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(registryPath)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([latest, fallback], result.Snapshot!.Entries.Select(entry => entry.SourceRoot));
        Assert.DoesNotContain(
            result.Snapshot.Entries,
            entry => entry.SourceRoot.StartsWith(original, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Two real filesystem replicas select the highest valid Registry revision.</summary>
    [Fact]
    public async Task FileSystemReplicaPairSelectsHighestValidRevision()
    {
        using var workspace = TempWorkspace.Create();
        string primaryPath = workspace.PathFor("primary/update-source-registry.json");
        string backupPath = workspace.PathFor("backup/update-source-registry.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(primaryPath)!);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        string primaryCatalog = workspace.PathFor("catalogs/primary.json");
        string backupCatalog = workspace.PathFor("catalogs/backup.json");
        await WriteAsync(primaryPath, Document(4, [new("latest", primaryCatalog)]));
        await WriteAsync(backupPath, Document(5, [new("latest", backupCatalog)]));
        var registry = new ReplicatedUpdateSourceRegistry(
            [
                new FileSystemUpdateSourceRegistry(primaryPath),
                new FileSystemUpdateSourceRegistry(backupPath),
            ]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Snapshot!.RegistryRevision);
        Assert.Equal(backupCatalog, Assert.Single(result.Snapshot.Entries).CatalogPath);
    }

    /// <summary>A missing primary does not hide a complete valid backup replica.</summary>
    [Fact]
    public async Task MissingPrimaryUsesValidBackupReplica()
    {
        using var workspace = TempWorkspace.Create();
        string backupPath = workspace.PathFor("backup/update-source-registry.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        await WriteAsync(
            backupPath,
            Document(5, [new("latest", workspace.PathFor("catalogs/backup.json"))]));
        var registry = new ReplicatedUpdateSourceRegistry(
            [
                new FileSystemUpdateSourceRegistry(workspace.PathFor("missing.json")),
                new FileSystemUpdateSourceRegistry(backupPath),
            ]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Snapshot!.RegistryRevision);
    }

    /// <summary>Same-revision filesystem replicas with different bytes fail closed.</summary>
    [Fact]
    public async Task SameRevisionDifferentFileSystemReplicaBytesConflict()
    {
        using var workspace = TempWorkspace.Create();
        string primaryPath = workspace.PathFor("primary/update-source-registry.json");
        string backupPath = workspace.PathFor("backup/update-source-registry.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(primaryPath)!);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        await WriteAsync(
            primaryPath,
            Document(5, [new("latest", workspace.PathFor("catalogs/primary.json"))]));
        await WriteAsync(
            backupPath,
            Document(5, [new("latest", workspace.PathFor("catalogs/backup.json"))]));
        var registry = new ReplicatedUpdateSourceRegistry(
            [
                new FileSystemUpdateSourceRegistry(primaryPath),
                new FileSystemUpdateSourceRegistry(backupPath),
            ]);

        UpdateSourceRegistryLoadResult result = await registry.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.ReplicaConflict, result.Issue);
        Assert.Null(result.Snapshot);
    }

    private static async Task WriteAsync(string path, UpdateSourceRegistryDocument document)
    {
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(document, SerializerOptions),
            TestContext.Current.CancellationToken);
    }

    private static UpdateSourceRegistryDocument Document(
        long revision,
        IReadOnlyList<UpdateSourceRegistryEntryDocument?> entries)
    {
        return new(
            1,
            "nvt-fw-combiner-production",
            revision,
            new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            new("1.0.1", 1, new string('a', 64)),
            entries);
    }

    private static string CompactJson(string catalogPath)
    {
        return JsonSerializer.Serialize(Document(1, [new("latest", catalogPath)]), SerializerOptions);
    }
}
