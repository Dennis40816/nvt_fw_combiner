using System.Runtime.CompilerServices;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellPreloadSessionTests
{
    private static async Task<CapabilityCatalogReloadResult> LoadSuccessfulResultAsync()
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("shell-preload-test");
        CapabilityCatalogReloadResult result = await PresentationTestHost.LoadCanonicalCatalogAsync(
            services.CanonicalCatalogLoader,
            TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Snapshot);
        return result;
    }

    private static ShellPreloadSession CreateSession(
        Action<ShellPreloadStageSnapshot>? report = null,
        bool includeStartupReport = false)
    {
        return new ShellPreloadSession(report ?? (static _ => { }), Text, includeStartupReport);
    }

    private static ShellPreloadStageSnapshot Stage(ShellPreloadSession session, string stageId)
    {
        return session.Stage(stageId);
    }

    private static TaskCompletionSource NewSignal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static CapabilityCatalogReloadResult Failure(
        bool retainedLastKnownGood,
        CanonicalCapabilityCatalogSnapshot? snapshot = null)
    {
        return new CapabilityCatalogReloadResult(
            Succeeded: false,
            retainedLastKnownGood,
            snapshot,
            [new CapabilityCatalogIssue(CapabilityCatalogIssueCodes.SourceInvalid, "Invalid catalog.")]);
    }

    private sealed class ScriptedLoader(
        params CanonicalCapabilityCatalogLoadUpdate[] updates) :
        ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (CanonicalCapabilityCatalogLoadUpdate update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return update;
            }
        }
    }

    private sealed class BlockingLoader(TaskCompletionSource entered) :
        ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CanonicalCapabilityCatalogLoadUpdate(0, Result: null);
            _ = entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class UncooperativeLoader(
        TaskCompletionSource entered,
        TaskCompletionSource release) : ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CanonicalCapabilityCatalogLoadUpdate(0, Result: null);
            _ = entered.TrySetResult();
            await release.Task;
            yield return new CanonicalCapabilityCatalogLoadUpdate(0.5, Result: null);
        }
    }

    private sealed class GatedTerminalLoader(
        CapabilityCatalogReloadResult result,
        TaskCompletionSource terminalObserved,
        TaskCompletionSource releaseEndOfStream) :
        ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CanonicalCapabilityCatalogLoadUpdate(0, Result: null);
            yield return new CanonicalCapabilityCatalogLoadUpdate(1, result);
            _ = terminalObserved.TrySetResult();
            await releaseEndOfStream.Task.WaitAsync(cancellationToken);
        }
    }
}
