using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Exercises the shared exclusive staging ownership boundary.</summary>
public sealed class ExternalStagingDirectoryTests
{
    /// <summary>Contending acquisitions have exactly one owner and do not remove its data.</summary>
    [Fact]
    public async Task ContendingAcquisitionsHaveOneOwner()
    {
        using var workspace = TempWorkspace.Create("staging-ownership");
        string path = Path.Combine(workspace.Root, "run");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ExternalStagingDirectory?>[] attempts = [.. Enumerable.Range(0, 16).Select(async _ =>
        {
            await start.Task;
            return ExternalStagingDirectory.TryAcquire(path);
        })];
        start.SetResult();
        ExternalStagingDirectory?[] leases = await Task.WhenAll(attempts);
        try
        {
            _ = Assert.Single(leases, lease => lease is not null);
            string sentinel = Path.Combine(path, "sentinel");
            await File.WriteAllTextAsync(sentinel, "owned", TestContext.Current.CancellationToken);
            Assert.Null(ExternalStagingDirectory.TryAcquire(path));
            Assert.Equal("owned", await File.ReadAllTextAsync(sentinel, TestContext.Current.CancellationToken));
        }
        finally
        {
            foreach (ExternalStagingDirectory? lease in leases)
            {
                lease?.Dispose();
            }
        }

        Assert.False(Directory.Exists(path));
    }

    /// <summary>Repeated disposal cannot clean a subsequently acquired directory.</summary>
    [Fact]
    public void DisposedOwnerCannotDeleteNextOwner()
    {
        using var workspace = TempWorkspace.Create("staging-ownership");
        string path = Path.Combine(workspace.Root, "run");
        using ExternalStagingDirectory? first = ExternalStagingDirectory.TryAcquire(path);
        Assert.NotNull(first);
        first.Dispose();
        using ExternalStagingDirectory? second = ExternalStagingDirectory.TryAcquire(path);
        Assert.NotNull(second);
        first.Dispose();
        Assert.True(Directory.Exists(path));
    }

    /// <summary>An existing file is not directory ownership and remains untouched.</summary>
    [Fact]
    public void ExistingFileIsPreserved()
    {
        using var workspace = TempWorkspace.Create("staging-ownership");
        string path = Path.Combine(workspace.Root, "run");
        File.WriteAllText(path, "existing");
        Assert.Null(ExternalStagingDirectory.TryAcquire(path));
        Assert.Equal("existing", File.ReadAllText(path));
    }
}
