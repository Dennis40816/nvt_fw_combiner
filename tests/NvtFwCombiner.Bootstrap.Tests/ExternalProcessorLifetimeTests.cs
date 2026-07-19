using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <inheritdoc/>
public sealed class ExternalProcessorLifetimeTests
{
    /// <summary>The public desktop prewarm is process-scoped and reusable without a composition run.</summary>
    [Fact]
    public async Task ReplaceRuntimePrewarmCompletesAndIsReusable()
    {
        await WorkbenchRuntimePrewarmer.PrewarmAsync(TestContext.Current.CancellationToken);
        await WorkbenchRuntimePrewarmer.PrewarmAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Prewarm returns immediately while initialization runs outside the calling thread.</summary>
    [Fact]
    public async Task PrewarmRunsInitializationInBackground()
    {
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        var expected = new StubExternalProcessor();
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            entered.Set();
            return !release.Wait(TimeSpan.FromSeconds(2))
                ? throw new TimeoutException("The test did not release background initialization.")
                : (IExternalProcessor)expected;
        });

        Task<IExternalProcessor?> prewarm = lifetime.PrewarmAsync(TestContext.Current.CancellationToken);

        Assert.True(entered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.False(prewarm.IsCompleted);
        release.Set();
        Assert.Same(expected, await prewarm);
    }

    /// <summary>A Build racing background prewarm shares the same published processor environment.</summary>
    [Fact]
    public async Task PrewarmAndForegroundCallerPublishOneEnvironment()
    {
        int factoryCallCount = 0;
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        var expected = new StubExternalProcessor();
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            _ = Interlocked.Increment(ref factoryCallCount);
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
            return expected;
        });

        Task<IExternalProcessor?> prewarm = lifetime.PrewarmAsync(TestContext.Current.CancellationToken);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Task<IExternalProcessor?> foreground = Task.Run(
            lifetime.GetOrCreateOrNull,
            TestContext.Current.CancellationToken);

        release.Set();
        IExternalProcessor?[] results = await Task.WhenAll(prewarm, foreground);

        Assert.Equal(1, factoryCallCount);
        Assert.All(results, result => Assert.Same(expected, result));
    }

    /// <summary>Publishes one processor environment to concurrent callers for the process lifetime.</summary>
    [Fact]
    public async Task GetOrCreatePublishesOneEnvironmentToConcurrentCallers()
    {
        int factoryCallCount = 0;
        var expected = new StubExternalProcessor();
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            _ = Interlocked.Increment(ref factoryCallCount);
            return expected;
        });
        using var start = new ManualResetEventSlim(initialState: false);
        Task<IExternalProcessor?>[] callers = [
            .. Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                start.Wait();
                return lifetime.GetOrCreateOrNull();
            })),
        ];

        start.Set();
        IExternalProcessor?[] results = await Task.WhenAll(callers);

        Assert.Equal(1, factoryCallCount);
        Assert.All(results, result => Assert.Same(expected, result));
    }

    /// <summary>Caches an unavailable layout so each run does not repeat the same discovery scan.</summary>
    [Fact]
    public void GetOrCreateCachesUnavailableEnvironmentUntilRestart()
    {
        int factoryCallCount = 0;
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            factoryCallCount++;
            return null;
        });

        Assert.Null(lifetime.GetOrCreateOrNull());
        Assert.Null(lifetime.GetOrCreateOrNull());
        Assert.Equal(1, factoryCallCount);
    }

    /// <summary>Caches a fail-closed initialization exception without retrying mutable manifest state mid-process.</summary>
    [Fact]
    public void GetOrCreateCachesInvalidEnvironmentFailureUntilRestart()
    {
        int factoryCallCount = 0;
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            factoryCallCount++;
            throw new InvalidDataException("invalid external processor environment");
        });

        _ = Assert.Throws<InvalidDataException>(lifetime.GetOrCreateOrNull);
        _ = Assert.Throws<InvalidDataException>(lifetime.GetOrCreateOrNull);
        Assert.Equal(1, factoryCallCount);
    }

    /// <summary>A background initialization failure remains the foreground fail-closed result.</summary>
    [Fact]
    public async Task PrewarmCachesInvalidEnvironmentFailureUntilRestart()
    {
        int factoryCallCount = 0;
        var lifetime = new ExternalProcessorLifetime(() =>
        {
            factoryCallCount++;
            throw new InvalidDataException("invalid external processor environment");
        });

        _ = await Assert.ThrowsAsync<InvalidDataException>(
            () => lifetime.PrewarmAsync(TestContext.Current.CancellationToken));
        _ = Assert.Throws<InvalidDataException>(lifetime.GetOrCreateOrNull);
        Assert.Equal(1, factoryCallCount);
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
