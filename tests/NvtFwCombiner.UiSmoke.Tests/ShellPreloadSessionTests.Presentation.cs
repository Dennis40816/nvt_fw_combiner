using System.Collections.Specialized;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellPreloadSessionTests
{
    /// <summary>The required catalog stage keeps typed progress and completes only after UI publication.</summary>
    [Fact]
    public async Task RequiredCatalogStagePublishesOneTypedAttempt()
    {
        CapabilityCatalogReloadResult success = await LoadSuccessfulResultAsync();
        using ShellPreloadSession session = CreateSession();
        var events = new List<string>();
        ((INotifyCollectionChanged)session.Stages).CollectionChanged += (_, _) =>
        {
            ShellPreloadAttemptSnapshot? attempt = session.CatalogStage.CurrentAttempt;
            events.Add($"{attempt?.Identity.AttemptNumber}:{attempt?.State}:{attempt?.Progress:0.00}");
        };

        CapabilityCatalogReloadResult result = await session.RunCatalogAsync(
            new ScriptedLoader(
                new CanonicalCapabilityCatalogLoadUpdate(0, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(0.5, Result: null),
                new CanonicalCapabilityCatalogLoadUpdate(1, success)),
            _ =>
            {
                events.Add("apply");
                return ValueTask.CompletedTask;
            },
            retry: false,
            TestContext.Current.CancellationToken);

        Assert.Same(success, result);
        Assert.True(session.Generation > 0);
        ShellPreloadAttemptSnapshot attempt = Assert.IsType<ShellPreloadAttemptSnapshot>(
            session.CatalogStage.CurrentAttempt);
        Assert.Equal(1, attempt.Identity.AttemptNumber);
        Assert.Equal(ShellPreloadStageState.Succeeded, attempt.State);
        Assert.Equal(1, attempt.Progress);
        Assert.Null(session.CatalogStage.PreviousAttempt);
        Assert.Equal("apply", events[^2]);
        Assert.EndsWith(":Succeeded:1.00", events[^1], StringComparison.Ordinal);

        using ShellPreloadSession observerFailure = CreateSession(
            static _ => throw new InvalidOperationException("presentation observer failed"));
        CapabilityCatalogReloadResult isolated = await observerFailure.RunCatalogAsync(
            new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(1, success)),
            static _ => ValueTask.CompletedTask,
            retry: false,
            TestContext.Current.CancellationToken);
        Assert.Same(success, isolated);
        Assert.Equal(ShellPreloadStageState.Succeeded, observerFailure.CatalogStage.State);

        ShellPreloadStageState delivered = ShellPreloadStageState.Pending;
        using ShellPreloadSession upstreamObserverFailures = CreateSession(stage => delivered = stage.State);
        ((INotifyCollectionChanged)upstreamObserverFailures.Stages).CollectionChanged +=
            static (_, _) => throw new InvalidOperationException("collection observer failed");
        upstreamObserverFailures.PropertyChanged +=
            static (_, _) => throw new InvalidOperationException("status observer failed");
        CapabilityCatalogReloadResult deliveredDespiteFailures = await upstreamObserverFailures.RunCatalogAsync(
            new ScriptedLoader(new CanonicalCapabilityCatalogLoadUpdate(1, success)),
            static _ => ValueTask.CompletedTask,
            retry: false,
            TestContext.Current.CancellationToken);
        Assert.Same(success, deliveredDespiteFailures);
        Assert.Equal(ShellPreloadStageState.Succeeded, upstreamObserverFailures.CatalogStage.State);
        Assert.Equal(ShellPreloadStageState.Succeeded, delivered);

        var throwingLoadingState = new ForegroundLoadingState();
        throwingLoadingState.PropertyChanged +=
            static (_, _) => throw new InvalidOperationException("loading observer failed");
        bool shellEnabled = false;
        bool focusRestored = false;
        MainWindow.CommitRequiredStagePresentation(
            succeeded: true,
            shellWasEnabled: false,
            enabled => shellEnabled = enabled,
            () => focusRestored = true,
            () => MainWindow.ApplyPreloadStage(
                session,
                throwingLoadingState,
                Text,
                session.CatalogStage));
        Assert.True(shellEnabled);
        Assert.True(focusRestored);

        PresentationHostServices services = PresentationTestHost.CreateServices("catalog-observer-test");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(services, ShellLanguage.English);
        viewModel.WorkflowSession.PropertyChanged +=
            static (_, _) => throw new InvalidOperationException("catalog observer failed");
        using ShellPreloadSession catalogObserverFailure = CreateSession();
        CapabilityCatalogReloadResult publishedDespiteObserver = await catalogObserverFailure.RunCatalogAsync(
            services.CanonicalCatalogLoader,
            _ =>
            {
                viewModel.PublishCanonicalCatalogState();
                return ValueTask.CompletedTask;
            },
            retry: false,
            TestContext.Current.CancellationToken);
        Assert.True(publishedDespiteObserver.Succeeded);
        Assert.True(viewModel.WorkflowSession.IsCanonicalCatalogReady);
        Assert.Equal(ShellPreloadStageState.Succeeded, catalogObserverFailure.CatalogStage.State);
    }
}
