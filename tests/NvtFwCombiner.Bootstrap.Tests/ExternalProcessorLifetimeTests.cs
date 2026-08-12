using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <inheritdoc/>
public sealed class ExternalProcessorLifetimeTests
{
    /// <summary>Publishes one processor environment to concurrent callers for the process lifetime.</summary>
    [Fact]
    public async Task GetOrCreatePublishesOneEnvironmentToConcurrentCallers()
    {
        int factoryCallCount = 0;
        var expected = new StubExternalProcessor();
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            _ = Interlocked.Increment(ref factoryCallCount);
            return Environment(expected);
        });
        using var start = new ManualResetEventSlim(initialState: false);
        Task<ExternalProcessorGenerationLease>[] callers = [
            .. Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                start.Wait();
                return lifetime.AcquireCurrent();
            })),
        ];

        start.Set();
        ExternalProcessorGenerationLease[] results = await Task.WhenAll(callers);

        Assert.Equal(1, factoryCallCount);
        Assert.All(results, result =>
        {
            Assert.Same(expected, result.Processor);
            Assert.Same(ReadyProvider.Instance, result.ReadinessProvider);
            Assert.Equal(1, result.Generation);
        });
    }

    /// <summary>An explicit refresh re-probes a previously unavailable environment.</summary>
    [Fact]
    public void RefreshRechecksUnavailableEnvironment()
    {
        int factoryCallCount = 0;
        var expected = new StubExternalProcessor();
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            factoryCallCount++;
            return Environment(factoryCallCount == 1 ? null : expected);
        });

        Assert.Null(lifetime.AcquireCurrent().Processor);
        Assert.Null(lifetime.AcquireCurrent().Processor);
        Assert.Equal(1, factoryCallCount);
        Assert.Equal(1, lifetime.AcquireCurrent().Generation);

        lifetime.Refresh();

        Assert.Same(expected, lifetime.AcquireCurrent().Processor);
        Assert.Equal(2, factoryCallCount);
        Assert.Equal(2, lifetime.AcquireCurrent().Generation);
    }

    /// <summary>An explicit refresh recovers from a cached invalid environment without restarting.</summary>
    [Fact]
    public void RefreshRechecksInvalidEnvironment()
    {
        int factoryCallCount = 0;
        var expected = new StubExternalProcessor();
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            factoryCallCount++;
            return factoryCallCount == 1
                ? throw new InvalidDataException("invalid external processor environment")
                : Environment(expected);
        });

        _ = Assert.Throws<InvalidDataException>(lifetime.AcquireCurrent);
        _ = Assert.Throws<InvalidDataException>(lifetime.AcquireCurrent);
        Assert.Equal(1, factoryCallCount);

        lifetime.Refresh();

        Assert.Same(expected, lifetime.AcquireCurrent().Processor);
        Assert.Equal(2, factoryCallCount);
    }

    /// <summary>Refresh publishes a replacement environment while keeping one construction per generation.</summary>
    [Fact]
    public void RefreshReplacesPreviouslyPublishedProcessor()
    {
        var first = new StubExternalProcessor();
        var second = new StubExternalProcessor();
        int factoryCallCount = 0;
        var lifetime = new ExternalProcessorLifetime(() =>
            Environment(++factoryCallCount == 1 ? first : second));

        Assert.Same(first, lifetime.AcquireCurrent().Processor);
        lifetime.Refresh();

        Assert.Same(second, lifetime.AcquireCurrent().Processor);
        Assert.Same(second, lifetime.AcquireCurrent().Processor);
        Assert.Equal(2, factoryCallCount);
    }

    /// <summary>A caller that captured an old lazy generation retries instead of publishing it after refresh.</summary>
    [Fact]
    public async Task RefreshDuringConstructionReturnsOnlyTheCurrentGeneration()
    {
        var first = new StubExternalProcessor();
        var second = new StubExternalProcessor();
        var firstEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseFirst = new ManualResetEventSlim(initialState: false);
        int factoryCallCount = 0;
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            int call = Interlocked.Increment(ref factoryCallCount);
            if (call == 1)
            {
                firstEntered.SetResult();
                releaseFirst.Wait();
                return Environment(first);
            }

            return Environment(second);
        });

        Task<ExternalProcessorGenerationLease> capturedOldGeneration =
            Task.Run(lifetime.AcquireCurrent);
        await firstEntered.Task;

        lifetime.Refresh();
        ExternalProcessorGenerationLease current = lifetime.AcquireCurrent();
        releaseFirst.Set();
        ExternalProcessorGenerationLease retried = await capturedOldGeneration;

        Assert.Equal(2, current.Generation);
        Assert.Same(second, current.Processor);
        Assert.Equal(2, retried.Generation);
        Assert.Same(second, retried.Processor);
        Assert.True(lifetime.IsCurrent(retried.Generation));
        Assert.Equal(2, factoryCallCount);
    }

    private static ExternalProcessorRuntimeEnvironment Environment(
        IExternalProcessor? processor)
    {
        return new ExternalProcessorRuntimeEnvironment(
            processor,
            ReadyProvider.Instance);
    }

    private sealed class ReadyProvider : IRuntimeDependencyReadinessProvider
    {
        internal static ReadyProvider Instance { get; } = new();

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubExternalProcessor : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
