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
}
