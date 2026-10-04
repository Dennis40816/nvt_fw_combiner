using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests host file artifact reads under configured roots.</summary>
public sealed class FileArtifactReaderTests
{
    /// <summary>Lease source admission preserves root confinement and creates no staging on refusal.</summary>
    [Fact]
    public async Task LeaseRejectsOutsideSourceRootWithoutStaging()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("outside/input.bin", [1]);
        string allowed = Directory.CreateDirectory(workspace.PathFor("inside")).FullName;
        string staging = Directory.CreateDirectory(workspace.PathFor("staging")).FullName;
        var reader = new FileArtifactReader([allowed]);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reader.OpenReadLeaseAsync(path, staging, TestContext.Current.CancellationToken).AsTask());

        Assert.Empty(Directory.GetFileSystemEntries(staging));
        Assert.Equal([1], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    /// <summary>Source file and ancestor links cannot bypass admission for either reader capability.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadersRejectSourceLinks(bool linkParent)
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("real/input.bin", [1]);
        string path;
        if (linkParent)
        {
            string alias = workspace.PathFor("alias");
            _ = Directory.CreateSymbolicLink(alias, Path.GetDirectoryName(source)!);
            path = Path.Combine(alias, "input.bin");
        }
        else
        {
            path = workspace.PathFor("alias.bin");
            _ = File.CreateSymbolicLink(path, source);
        }
        string staging = Directory.CreateDirectory(workspace.PathFor("staging")).FullName;
        var reader = new FileArtifactReader([workspace.Root]);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reader.ReadAsync(path, TestContext.Current.CancellationToken).AsTask());
        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reader.OpenReadLeaseAsync(path, staging, TestContext.Current.CancellationToken).AsTask());

        Assert.Empty(Directory.GetFileSystemEntries(staging));
    }

    /// <summary>The caller's staging root must exist and cannot contain a linked ancestor.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeaseRejectsMissingOrLinkedStagingRoot(bool linked)
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("input.bin", [1]);
        string target = Directory.CreateDirectory(workspace.PathFor("real/child")).FullName;
        string staging = workspace.PathFor("missing");
        if (linked)
        {
            string alias = workspace.PathFor("alias");
            _ = Directory.CreateSymbolicLink(alias, Path.GetDirectoryName(target)!);
            staging = Path.Combine(alias, "child");
        }
        var reader = new FileArtifactReader([workspace.Root]);

        if (linked)
        {
            _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                reader.OpenReadLeaseAsync(path, staging, TestContext.Current.CancellationToken).AsTask());
        }
        else
        {
            _ = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                reader.OpenReadLeaseAsync(path, staging, TestContext.Current.CancellationToken).AsTask());
            Assert.False(Directory.Exists(staging));
        }
        Assert.Empty(Directory.GetFileSystemEntries(target));
    }

    /// <summary>Cancellation before admission neither opens the source nor creates staging.</summary>
    [Fact]
    public async Task CancelledLeaseDoesNotAccessMissingSource()
    {
        using var workspace = TempWorkspace.Create();
        var reader = new FileArtifactReader([workspace.Root]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.OpenReadLeaseAsync(workspace.PathFor("missing.bin"), workspace.Root, cancellation.Token).AsTask());

        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>The additive lease port stages complete content without changing materialized reads.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(131089)]
    public async Task LeaseAndMaterializedReadsPreserveCompleteContent(int length)
    {
        using var workspace = TempWorkspace.Create();
        byte[] expected = [.. Enumerable.Range(0, length).Select(static index => (byte)(index % 251))];
        string path = workspace.Write("input.bin", expected);
        string stagingRoot = Directory.CreateDirectory(workspace.PathFor("staging")).FullName;
        IArtifactReader reader = new FileArtifactReader([workspace.Root]);
        using ArtifactReadLease lease = await reader.OpenReadLeaseAsync(
            path, stagingRoot, TestContext.Current.CancellationToken);
        byte[] actual = new byte[length];

        await lease.ReadExactlyAsync(0, actual, TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
        Assert.Equal(FileStamp.FromBytes(expected), lease.FileStamp);
        Assert.Equal(length, lease.Length);
        Assert.Equal(expected, (await reader.ReadAsync(path, TestContext.Current.CancellationToken)).ToArray());
        _ = Assert.Single(Directory.GetFiles(stagingRoot));
    }

    /// <summary>Verifies a file under an allowed root is read as immutable artifact bytes.</summary>
    [Fact]
    public async Task ReadAsyncReadsFileUnderAllowedRoot()
    {
        using var workspace = TempWorkspace.Create();
        string artifactPath = workspace.Write("input.bin", [1, 2, 3]);
        var reader = new FileArtifactReader([workspace.Root]);

        ReadOnlyMemory<byte> bytes = await reader.ReadAsync(artifactPath, CancellationToken.None);

        Assert.Equal([1, 2, 3], bytes.ToArray());
    }

    /// <summary>Verifies artifact reads fail closed when the path is outside configured roots.</summary>
    [Fact]
    public async Task ReadAsyncRejectsPathOutsideAllowedRoot()
    {
        using var workspace = TempWorkspace.Create();
        string outsideRoot = Path.Combine(workspace.Root, "outside");
        _ = Directory.CreateDirectory(outsideRoot);
        string insideRoot = Path.Combine(workspace.Root, "inside");
        _ = Directory.CreateDirectory(insideRoot);
        string artifactPath = Path.Combine(outsideRoot, "input.bin");
        await File.WriteAllBytesAsync(artifactPath, [1], CancellationToken.None);
        var reader = new FileArtifactReader([insideRoot]);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await reader.ReadAsync(artifactPath, CancellationToken.None));
    }
}
