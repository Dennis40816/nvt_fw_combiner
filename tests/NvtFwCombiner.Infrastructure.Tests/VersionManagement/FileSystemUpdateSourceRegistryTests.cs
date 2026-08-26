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

    /// <summary>A valid registry preserves policy order and publishes normalized paths.</summary>
    [Fact]
    public async Task ValidRegistryPreservesAvailableOrderAndNormalizesPaths()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        string latest = workspace.PathFor("latest");
        string first = workspace.PathFor("first");
        string second = workspace.PathFor("second");
        await WriteAsync(path, new(
            1,
            12,
            [
                new("latest", latest),
                new("available", first),
                new("deprecated", workspace.PathFor("old")),
                new("available", second),
            ]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Snapshot!.Revision);
        Assert.Matches("^[0-9a-f]{64}$", result.Snapshot.ContentDigest);
        Assert.Equal(
            [Path.GetFullPath(latest), Path.GetFullPath(first), Path.GetFullPath(workspace.PathFor("old")), Path.GetFullPath(second)],
            result.Snapshot.Entries.Select(entry => entry.SourceRoot));
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
        string root = workspace.PathFor("same");
        await WriteAsync(path, new(
            1,
            1,
            [new("latest", root), new("available", root + Path.DirectorySeparatorChar)]));

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
        await WriteAsync(path, new(
            1,
            1,
            [.. Enumerable.Range(0, FileSystemUpdateSourceRegistry.MaximumEntries + 1)
                .Select(index => new UpdateSourceRegistryEntryDocument(
                    index == 0 ? "latest" : "available",
                    workspace.PathFor($"source-{index}")))]));
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
        await WriteAsync(target, new(
            1,
            1,
            [new("latest", workspace.PathFor("source"))]));
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
        string source = workspace.PathFor("source");
        await File.WriteAllTextAsync(
            path,
            $$"""{"schemaVersion":1,"revision":1,"entries":[{"status":"latest","path":"{{source.Replace("\\", "\\\\", StringComparison.Ordinal)}}"}]}""",
            TestContext.Current.CancellationToken);
        UpdateSourceRegistryLoadResult compact = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            path,
            $$"""
            {
              "schemaVersion": 1,
              "revision": 1,
              "entries": [{ "status": "latest", "path": "{{source.Replace("\\", "\\\\", StringComparison.Ordinal)}}" }]
            }
            """,
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
            new(1, 1, [new("latest", workspace.PathFor("source"))]));
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
        await WriteAsync(path, new(1, 1, [new("latest", sourceRoot)]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.Issue);
    }

    /// <summary>An existing writer prevents publication of an unstable read.</summary>
    [Fact]
    public async Task ExistingWriterHandlePreventsStableReadPublication()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor(FileSystemUpdateSourceRegistry.RegistryFileName);
        await WriteAsync(path, new(1, 1, [new("latest", workspace.PathFor("source"))]));
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
        await WriteAsync(registryPath, new(
            1,
            2,
            [
                new("latest", latest),
                new("available", fallback),
            ]));

        UpdateSourceRegistryLoadResult result = await new FileSystemUpdateSourceRegistry(registryPath)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([latest, fallback], result.Snapshot!.Entries.Select(entry => entry.SourceRoot));
        Assert.DoesNotContain(
            result.Snapshot.Entries,
            entry => entry.SourceRoot.StartsWith(original, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteAsync(string path, UpdateSourceRegistryDocument document)
    {
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(document, SerializerOptions),
            TestContext.Current.CancellationToken);
    }
}
