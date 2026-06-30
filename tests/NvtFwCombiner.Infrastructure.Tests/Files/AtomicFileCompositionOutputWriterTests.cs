using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests atomic output commitment under configured roots.</summary>
public sealed class AtomicFileCompositionOutputWriterTests
{
    /// <summary>Verifies successful output is promoted into the configured directory.</summary>
    [Fact]
    public async Task CommitAsyncWritesOutputUnderConfiguredRoot()
    {
        using var workspace = TempWorkspace.Create();
        var writer = new AtomicFileCompositionOutputWriter(workspace.Root);

        string committedPath = await writer.CommitAsync(
            "output.bin",
            new byte[] { 1, 2, 3 },
            CancellationToken.None);

        Assert.Equal(Path.Combine(workspace.Root, "output.bin"), committedPath);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(committedPath, CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(workspace.Root, "*.tmp"));
    }

    /// <summary>Verifies output file names cannot carry path traversal syntax.</summary>
    [Fact]
    public async Task CommitAsyncRejectsPathSyntaxInFileName()
    {
        using var workspace = TempWorkspace.Create();
        var writer = new AtomicFileCompositionOutputWriter(workspace.Root);

        _ = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.CommitAsync(@"..\escape.bin", new byte[] { 1 }, CancellationToken.None));
    }

    /// <summary>Verifies existing outputs are not overwritten unless explicitly allowed.</summary>
    [Fact]
    public async Task CommitAsyncDoesNotOverwriteByDefault()
    {
        using var workspace = TempWorkspace.Create();
        string existingPath = Path.Combine(workspace.Root, "output.bin");
        await File.WriteAllBytesAsync(existingPath, [9], CancellationToken.None);
        var writer = new AtomicFileCompositionOutputWriter(workspace.Root);

        _ = await Assert.ThrowsAsync<IOException>(async () =>
            await writer.CommitAsync("output.bin", new byte[] { 1 }, CancellationToken.None));

        Assert.Equal([9], await File.ReadAllBytesAsync(existingPath, CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(workspace.Root, "*.tmp"));
    }

    /// <summary>Verifies existing outputs can be replaced when the caller opted in.</summary>
    [Fact]
    public async Task CommitAsyncOverwritesWhenAllowed()
    {
        using var workspace = TempWorkspace.Create();
        string existingPath = Path.Combine(workspace.Root, "output.bin");
        await File.WriteAllBytesAsync(existingPath, [9], CancellationToken.None);
        var writer = new AtomicFileCompositionOutputWriter(workspace.Root, overwrite: true);

        string committedPath = await writer.CommitAsync(
            "output.bin",
            new byte[] { 1 },
            CancellationToken.None);

        Assert.Equal(existingPath, committedPath);
        Assert.Equal([1], await File.ReadAllBytesAsync(existingPath, CancellationToken.None));
    }

    private sealed class TempWorkspace : IDisposable
    {
        private static int s_id;

        private TempWorkspace(string root)
        {
            Root = root;
            _ = Directory.CreateDirectory(root);
        }

        internal string Root { get; }

        internal static TempWorkspace Create()
        {
            int id = Interlocked.Increment(ref s_id);
            string root = Path.Combine(
                Path.GetTempPath(),
                "nfc-output-writer-tests",
                FormattableString.Invariant($"{id:D4}"));
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            return new TempWorkspace(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
