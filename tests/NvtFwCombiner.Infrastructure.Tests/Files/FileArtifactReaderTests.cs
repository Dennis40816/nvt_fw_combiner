using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests host file artifact reads under configured roots.</summary>
public sealed class FileArtifactReaderTests
{
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

    /// <summary>Reads one artifact while proving it is distinct from a protected artifact.</summary>
    [Fact]
    public async Task ReadDistinctAsyncReadsDifferentPhysicalFiles()
    {
        using var workspace = TempWorkspace.Create();
        string artifactPath = workspace.Write("input.bin", [1, 2, 3]);
        string protectedPath = workspace.Write("base.bin", [4, 5, 6]);
        var reader = new FileArtifactReader([workspace.Root]);

        ReadOnlyMemory<byte> bytes = await reader.ReadDistinctAsync(
            artifactPath,
            protectedPath,
            CancellationToken.None);

        Assert.Equal([1, 2, 3], bytes.ToArray());
    }

    /// <summary>The protected snapshot read rejects two paths that resolve to one physical file.</summary>
    [Fact]
    public async Task ReadDistinctAsyncRejectsSamePhysicalFile()
    {
        using var workspace = TempWorkspace.Create();
        string artifactPath = workspace.Write("input.bin", [1, 2, 3]);
        var reader = new FileArtifactReader([workspace.Root]);

        _ = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await reader.ReadDistinctAsync(
                artifactPath,
                artifactPath,
                CancellationToken.None));
    }
}
