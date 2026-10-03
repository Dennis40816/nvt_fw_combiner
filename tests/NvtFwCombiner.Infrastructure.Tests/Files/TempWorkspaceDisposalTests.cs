using System.Runtime.ExceptionServices;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Verifies workspace deletion completion and bounded Windows lock retries.</summary>
public sealed class TempWorkspaceDisposalTests
{
    /// <summary>An unlocked workspace is gone when disposal returns.</summary>
    [Fact]
    public void DisposalDeletesAnUnlockedWorkspace()
    {
        using var workspace = TempWorkspace.Create();
        _ = workspace.Write("nested/input.bin", [1]);

        workspace.Dispose();

        Assert.False(Directory.Exists(workspace.Root));
    }

    /// <summary>A workspace that has already been deleted needs no cleanup.</summary>
    [Fact]
    public void DisposalSucceedsWhenWorkspaceIsAlreadyAbsent()
    {
        using var workspace = TempWorkspace.Create();
        Directory.Delete(workspace.Root);

        workspace.Dispose();

        Assert.False(Directory.Exists(workspace.Root));
    }

    /// <summary>Disposal retries a real delete failure and completes after its lock is released.</summary>
    [Fact]
    public async Task DisposalCompletesAfterFileLockIsReleased()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create();
        string lockedPath = workspace.Write("locked.bin", [2]);
        using var fileLock = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);
        bool lockedFileWasPresent = false;
        Task disposal = Task.Run(() =>
        {
            int disposalThreadId = Environment.CurrentManagedThreadId;
            bool deleteFailed = false;
            void ReleaseLock(object? _, FirstChanceExceptionEventArgs args)
            {
                if (args.Exception is IOException && Environment.CurrentManagedThreadId == disposalThreadId &&
                    !deleteFailed)
                {
                    // Release on the deleting thread's first failure without waiting for the test thread.
                    deleteFailed = true;
                    lockedFileWasPresent = File.Exists(lockedPath);
                    fileLock.Dispose();
                }
            }

            AppDomain.CurrentDomain.FirstChanceException += ReleaseLock;
            try
            {
                workspace.Dispose();
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= ReleaseLock;
            }

            Assert.True(deleteFailed);
        }, TestContext.Current.CancellationToken);

        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(lockedFileWasPresent);
        Assert.False(Directory.Exists(workspace.Root));
    }

    /// <summary>A persistent lock propagates its delete exception after the bounded retry budget.</summary>
    [Fact]
    public async Task DisposalPropagatesLockFailureWithinRetryBudget()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create();
        string lockedPath = workspace.Write("locked.bin", [1]);
        using var fileLock = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);
        Task disposal = Task.Run(() =>
        {
            _ = Assert.Throws<IOException>(workspace.Dispose);
        }, TestContext.Current.CancellationToken);

        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(workspace.Root));
    }
}
