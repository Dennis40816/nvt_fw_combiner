using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Concurrency regression coverage for indexed report row projections.</summary>
public sealed class ReportIndexedReadOnlyListsTests
{
    /// <summary>Concurrent readers execute an expensive row factory once and share its published instance.</summary>
    [Fact]
    public async Task MemoizedIndexPublishesOneFactoryResultToConcurrentReaders()
    {
        object expected = new();
        int invocationCount = 0;
        using var factoryEntered = new ManualResetEventSlim(initialState: false);
        using var releaseFactory = new ManualResetEventSlim(initialState: false);
        using var secondReaderStarted = new ManualResetEventSlim(initialState: false);
        var rows = new MemoizedIndexedReadOnlyList<object>(
            1,
            _ =>
            {
                _ = Interlocked.Increment(ref invocationCount);
                factoryEntered.Set();
                releaseFactory.Wait(TestContext.Current.CancellationToken);
                return expected;
            });
        Task<object> firstReader = StartDedicatedReader(() => rows[0]);
        Assert.True(factoryEntered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Thread? secondReaderThread = null;
        Task<object> secondReader = StartDedicatedReader(() =>
        {
            Volatile.Write(ref secondReaderThread, Thread.CurrentThread);
            secondReaderStarted.Set();
            return rows[0];
        });

        try
        {
            Assert.True(secondReaderStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.True(SpinWait.SpinUntil(
                () => Volatile.Read(ref secondReaderThread) is { } thread &&
                    (thread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(5)));
            Assert.Equal(1, Volatile.Read(ref invocationCount));
        }
        finally
        {
            releaseFactory.Set();
        }

        object[] results = await Task.WhenAll(firstReader, secondReader);

        Assert.Equal(1, invocationCount);
        Assert.Equal(1, rows.MaterializedCount);
        Assert.All(results, result => Assert.Same(expected, result));

        static Task<object> StartDedicatedReader(Func<object> reader)
        {
            return Task.Factory.StartNew(
                reader,
                TestContext.Current.CancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }
}
