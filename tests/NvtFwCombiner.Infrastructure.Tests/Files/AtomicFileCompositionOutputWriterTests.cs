using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests atomic output commitment under configured roots.</summary>
public sealed class AtomicFileCompositionOutputWriterTests
{
    /// <summary>A legal maximum-length destination is not made illegal by its temporary filename.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitMaximumLengthFileNamePreservesBytesAndCleansStaging(bool overwrite)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string fileName = new string('a', 251) + ".bin";
        string destination = Path.Combine(workspace.Root, fileName);
        if (overwrite)
        {
            await File.WriteAllBytesAsync(destination, [9], TestContext.Current.CancellationToken);
        }
        AtomicFileCompositionOutputWriter writer = new(workspace.Root, overwrite);

        CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
            fileName, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        Assert.Equal(destination, receipt.OutputId);
        Assert.Equal(fileName, receipt.OutputFileName);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        Assert.Equal([destination], Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Cancellation and refusal preserve a long existing destination and remove temporary output.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedLongNameCommitPreservesExistingOutput(bool cancel)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string fileName = new string('a', 251) + ".bin";
        string destination = Path.Combine(workspace.Root, fileName);
        await File.WriteAllBytesAsync(destination, [9], TestContext.Current.CancellationToken);
        AtomicFileCompositionOutputWriter writer = new(workspace.Root, overwrite: cancel);
        using CancellationTokenSource cancellation = new();
        if (cancel) { cancellation.Cancel(); }

        if (cancel)
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await writer.CommitAsync(fileName, new byte[] { 1 }, cancellation.Token));
        }
        else
        {
            _ = await Assert.ThrowsAsync<IOException>(async () =>
                await writer.CommitAsync(fileName, new byte[] { 1 }, cancellation.Token));
        }

        Assert.Equal([9], await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        Assert.Equal([destination], Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Verifies successful output is promoted into the configured directory.</summary>
    [Fact]
    public async Task CommitAsyncWritesOutputUnderConfiguredRoot()
    {
        using var workspace = TempWorkspace.Create();
        var writer = new AtomicFileCompositionOutputWriter(workspace.Root);

        CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
            "output.bin",
            new byte[] { 1, 2, 3 },
            CancellationToken.None);

        Assert.Equal(Path.Combine(workspace.Root, "output.bin"), receipt.OutputId);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(receipt.OutputId, CancellationToken.None));
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

        CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
            "output.bin",
            new byte[] { 1 },
            CancellationToken.None);

        Assert.Equal(existingPath, receipt.OutputId);
        Assert.Equal([1], await File.ReadAllBytesAsync(existingPath, CancellationToken.None));
    }
}
