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
