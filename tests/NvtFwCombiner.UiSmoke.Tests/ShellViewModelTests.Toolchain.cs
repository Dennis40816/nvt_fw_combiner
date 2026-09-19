using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Contracts.Configuration;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Observable Toolchain configuration editing and cross-page draft protection.</summary>
public sealed class ToolchainSettingsTests
{
    /// <summary>A real published session remains generation-current when the already loaded page opens.</summary>
    [Fact]
    public async Task OpeningPagePreservesRealSessionEnvironmentGeneration()
    {
        using var session = new ToolchainRuntimeConfigurationSession(new MemoryToolchainStorage(), new BundledCandidateInspector());
        _ = await session.ReloadAsync(TestContext.Current.CancellationToken);
        var loader = new ExternalProcessorEnvironmentLoader(
            (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(new ExternalProcessorRuntimeEnvironment(
                    null, new EmptyReadiness(), 0, null, session.Current.Generation));
            }, session);
        Assert.True((await ReadEnvironmentAsync(loader)).Succeeded);
        long lease = loader.AcquireCurrent().Generation;
        MainWindowViewModel vm = CreateToolchainViewModel(session);

        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;

        Assert.True(loader.IsCurrent(lease));
        Assert.Equal(1, session.Current.Generation);
    }

    /// <summary>Opening an already loaded Config page does not publish a new generation or invalidate Build.</summary>
    [Fact]
    public async Task OpeningLoadedToolchainPageDoesNotReloadSession()
    {
        var session = new ToolchainUiSession();
        long generation = session.Current.Generation;
        MainWindowViewModel vm = CreateToolchainViewModel(session);

        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;

        Assert.Equal(0, session.ReloadCount);
        Assert.Equal(generation, session.Current.Generation);
    }

    /// <summary>Rejected detections remain explanatory text, never blank selectable rows.</summary>
    [Fact]
    public async Task RejectedDetectionDoesNotRenderBlankCandidate()
    {
        var session = new ToolchainUiSession { RejectInspection = true };
        MainWindowViewModel vm = CreateToolchainViewModel(session);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;

        await vm.Settings.DetectToolchainCommand.ExecuteAsync(null);

        Assert.Empty(vm.Settings.ToolchainCandidates);
        Assert.Contains("untrusted", vm.Settings.ToolchainOperationStatus, StringComparison.Ordinal);
        Assert.Contains("Microsoft trust unavailable", vm.Settings.ToolchainOperationStatus, StringComparison.Ordinal);
    }

    /// <summary>Detection only observes; explicit selection and Save own the transaction.</summary>
    [Fact]
    public async Task DetectionDoesNotSelectAndSavePublishesOnlyExplicitVerifiedDraft()
    {
        var session = new ToolchainUiSession();
        MainWindowViewModel vm = CreateToolchainViewModel(session);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        Assert.True(vm.Settings.IsBundledToolchainSelected);
        Assert.Equal(vm.Text.ToolchainIncludedLabel, vm.Settings.ToolchainVerification);
        await vm.Settings.DetectToolchainCommand.ExecuteAsync(null);
        Assert.True(vm.Settings.IsBundledToolchainSelected);
        Assert.False(vm.Settings.HasToolchainUnsavedChanges);
        Assert.Equal(0, session.SaveCount);
        vm.Settings.SelectToolchainCandidateCommand.Execute(session.Verified);
        Assert.True(vm.Settings.IsUserToolchainSelected);
        Assert.True(vm.Settings.CanSaveToolchain);
        Assert.Equal("14.44.35211.0", vm.Settings.ToolchainVersion);
        await vm.Settings.SaveToolchainCommand.ExecuteAsync(null);
        Assert.Equal(new(ToolchainRuntimeSource.User, session.Verified.Identity!.Path, session.Verified.Identity.Sha256), session.LastSaved);
        Assert.False(vm.Settings.HasToolchainUnsavedChanges);
        await vm.Settings.SelectBundledToolchainCommand.ExecuteAsync(null);
        Assert.True(vm.Settings.HasToolchainUnsavedChanges);
        Assert.Equal(ToolchainRuntimeSource.User, session.Current.Selection!.Source);
        await vm.Settings.DiscardToolchainChangesCommand.ExecuteAsync(null);
        Assert.True(vm.Settings.IsUserToolchainSelected);
        Assert.False(vm.Settings.HasToolchainUnsavedChanges);
    }

    /// <summary>Saving a new Toolchain generation republishes the external environment before readiness refresh.</summary>
    [Fact]
    public async Task SaveReloadsExternalEnvironmentForPublishedToolchainGeneration()
    {
        var session = new ToolchainUiSession();
        int loadCount = 0;
        var loader = new ExternalProcessorEnvironmentLoader(
            (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                loadCount++;
                return ValueTask.FromResult(new ExternalProcessorRuntimeEnvironment(
                    null,
                    new EmptyReadiness(),
                    0,
                    null,
                    session.Current.Generation));
            },
            session);
        Assert.True((await ReadEnvironmentAsync(loader)).Succeeded);
        MainWindowViewModel vm = CreateToolchainViewModel(session, externalEnvironmentLoader: loader);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;

        await vm.Settings.DetectToolchainCommand.ExecuteAsync(null);
        vm.Settings.SelectToolchainCandidateCommand.Execute(session.Verified);
        vm.Merge.PropertyChanged += static (_, _) =>
            throw new InvalidOperationException("merge observer failed");
        await vm.Settings.SaveToolchainCommand.ExecuteAsync(null);

        Assert.Equal(2, loadCount);
        Assert.Equal(1, session.SaveCount);
        Assert.Equal(2, session.Current.Generation);
        Assert.NotEqual(0, loader.AcquireCurrent().Generation);
        Assert.Empty(vm.Settings.ToolchainOperationStatus);
    }

    /// <summary>A superseding explicit refresh owns the terminal Save readiness result without a third load.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveWaitsForSupersedingExplicitEnvironmentRefresh(bool newerRefreshFails)
    {
        var session = new ToolchainUiSession();
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var explicitEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseExplicit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int loadCount = 0;
        var loader = new ExternalProcessorEnvironmentLoader(
            async (_, cancellationToken) =>
            {
                int attempt = Interlocked.Increment(ref loadCount);
                if (attempt == 2)
                {
                    saveEntered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                if (attempt == 3)
                {
                    explicitEntered.SetResult();
                    await releaseExplicit.Task.WaitAsync(cancellationToken);
                    if (newerRefreshFails)
                    {
                        throw new InvalidDataException("newer refresh failed");
                    }
                }
                return new(
                    null,
                    new EmptyReadiness(),
                    0,
                    null,
                    session.Current.Generation);
            },
            session);
        Assert.True((await ReadEnvironmentAsync(loader)).Succeeded);
        MainWindowViewModel vm = CreateToolchainViewModel(session, externalEnvironmentLoader: loader);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        await vm.Settings.DetectToolchainCommand.ExecuteAsync(null);
        vm.Settings.SelectToolchainCandidateCommand.Execute(session.Verified);

        Task save = vm.Settings.SaveToolchainCommand.ExecuteAsync(null);
        await saveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Task refresh = vm.MessageCenter.RefreshCommand.ExecuteAsync(null);
        await explicitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(save.IsCompleted);

        releaseExplicit.SetResult();
        await Task.WhenAll(save, refresh);

        Assert.Equal(3, loadCount);
        if (newerRefreshFails)
        {
            Assert.Equal(vm.Text.ToolchainRefreshFailedLabel, vm.Settings.ToolchainOperationStatus);
            Assert.Equal(ExternalProcessorEnvironmentState.Unavailable, loader.Current.State);
        }
        else
        {
            Assert.Empty(vm.Settings.ToolchainOperationStatus);
            Assert.Equal(ExternalProcessorEnvironmentState.Current, loader.Current.State);
        }
    }

    /// <summary>A lone stale candidate cannot adopt the prior generation or trigger a hidden retry.</summary>
    [Fact]
    public async Task SaveRejectsSupersededReloadWithoutNewerPublication()
    {
        var session = new ToolchainUiSession();
        int loadCount = 0;
        var loader = new ExternalProcessorEnvironmentLoader(
            (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                loadCount++;
                return ValueTask.FromResult(new ExternalProcessorRuntimeEnvironment(
                    null,
                    new EmptyReadiness(),
                    0,
                    null,
                    ToolchainGeneration: 1));
            },
            session);
        Assert.True((await ReadEnvironmentAsync(loader)).Succeeded);
        MainWindowViewModel vm = CreateToolchainViewModel(session, externalEnvironmentLoader: loader);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        await vm.Settings.DetectToolchainCommand.ExecuteAsync(null);
        vm.Settings.SelectToolchainCandidateCommand.Execute(session.Verified);

        await vm.Settings.SaveToolchainCommand.ExecuteAsync(null);

        Assert.Equal(2, loadCount);
        Assert.Equal(vm.Text.ToolchainRefreshFailedLabel, vm.Settings.ToolchainOperationStatus);
        Assert.Equal(ExternalProcessorEnvironmentState.NotLoaded, loader.Current.State);
    }

    /// <summary>An invalid user choice stays visible and cannot silently select Bundled.</summary>
    [Fact]
    public async Task RejectedPathRemainsSelectedAndCannotSaveOrFallBack()
    {
        var session = new ToolchainUiSession { RejectInspection = true };
        MainWindowViewModel vm = CreateToolchainViewModel(session);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        await vm.Settings.InspectToolchainPathAsync("C:/runtime/bad.dll");
        Assert.True(vm.Settings.IsUserToolchainSelected);
        Assert.True(vm.Settings.HasToolchainIssues);
        Assert.False(vm.Settings.CanSaveToolchain);
        Assert.Equal(vm.Text.ToolchainFailedLabel, vm.Settings.ToolchainVerification);
        Assert.Equal(ToolchainRuntimeSource.Bundled, session.Current.Selection!.Source);
        Assert.Equal(0, session.SaveCount);
    }

    /// <summary>Persistence failure retains editable intent and protects it when closing.</summary>
    [Fact]
    public async Task FailedSaveRetainsPublicationAndDraftAndCloseRequiresExplicitDiscard()
    {
        var session = new ToolchainUiSession { FailSave = true };
        MainWindowViewModel vm = CreateToolchainViewModel(session);
        vm.OpenSettingsCommand.Execute(null);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        await vm.Settings.InspectToolchainPathAsync(session.Verified.Identity!.Path);
        await vm.Settings.SaveToolchainCommand.ExecuteAsync(null);
        Assert.True(vm.Settings.HasToolchainUnsavedChanges);
        Assert.Equal(ToolchainRuntimeSource.Bundled, session.Current.Selection!.Source);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Preferences);
        vm.CloseSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsModalOpen);
        Assert.True(vm.Settings.IsToolchainSelected);
        Assert.True(vm.Settings.IsToolchainCloseConfirmationOpen);
        vm.Settings.CancelToolchainCloseCommand.Execute(null);
        Assert.True(vm.Settings.HasToolchainUnsavedChanges);
        vm.CloseSettingsCommand.Execute(null);
        await vm.Settings.ConfirmToolchainCloseCommand.ExecuteAsync(null);
        Assert.False(vm.IsSettingsModalOpen);
        Assert.True(vm.Settings.IsBundledToolchainSelected);
    }

    /// <summary>Missing bundled metadata cannot produce a successful verification label.</summary>
    [Fact]
    public async Task BundledReadFailureIsNotPresentedAsIncludedOrVerified()
    {
        var session = new ToolchainUiSession { BundledMissing = true };
        MainWindowViewModel vm = CreateToolchainViewModel(session);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        Assert.True(vm.Settings.IsBundledToolchainSelected);
        Assert.Equal(vm.Text.ToolchainFailedLabel, vm.Settings.ToolchainVerification);
        Assert.Contains("missing", vm.Settings.ToolchainIssueText, StringComparison.Ordinal);
    }

    /// <summary>A publication race rejects metadata from an older selection generation.</summary>
    [Fact]
    public async Task SupersededGenerationCannotPublishCandidateEvidence()
    {
        var session = new ToolchainUiSession();
        MainWindowViewModel vm = CreateToolchainViewModel(session);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        session.PendingInspection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task inspection = vm.Settings.InspectToolchainPathAsync(session.Verified.Identity!.Path);
        session.Current = new(2, ToolchainRuntimeConfigurationStatus.Current, new(ToolchainRuntimeSource.Bundled), new(ToolchainRuntimeSource.Bundled), []);
        session.PendingInspection.SetResult(session.Verified);
        await inspection;
        Assert.False(vm.Settings.CanSaveToolchain);
        Assert.Equal(vm.Text.ToolchainUnknownLabel, vm.Settings.ToolchainVerification);
    }

    /// <summary>Closing invalidates pending metadata before another explicit draft selection.</summary>
    [Fact]
    public async Task ClosedInspectionCannotOverwriteANewerBundledDraft()
    {
        var session = new ToolchainUiSession();
        MainWindowViewModel vm = CreateToolchainViewModel(session);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        session.PendingInspection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task inspection = vm.Settings.InspectToolchainPathAsync(session.Verified.Identity!.Path);
        vm.Settings.InvalidateToolchainOperations();
        await vm.Settings.SelectBundledToolchainCommand.ExecuteAsync(null);
        session.PendingInspection.SetResult(session.Verified);
        await inspection;
        Assert.True(vm.Settings.IsBundledToolchainSelected);
        Assert.False(vm.Settings.IsToolchainBusy);
        Assert.Equal(vm.Text.ToolchainIncludedLabel, vm.Settings.ToolchainVerification);
        Assert.Equal(0, session.SaveCount);
    }

    /// <summary>Both Config drafts require their own explicit discard before Settings closes.</summary>
    [Fact]
    public async Task ClosingTwoDirtyConfigPagesDoesNotDiscardTheSecondDraftImplicitly()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), null,
            configurationPath: workspace.PathFor("config.json"));
        IEventBufferFormatConfigurationSession formats = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        var runtime = new ToolchainUiSession();
        MainWindowViewModel vm = CreateToolchainViewModel(runtime, formatSession: formats);
        vm.OpenSettingsCommand.Execute(null);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await vm.Settings.EventBufferFormatLoadTask;
        Assert.Single(vm.Settings.EventBufferFormatRows).AliasName = "Unpublished";
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Toolchain);
        await vm.Settings.ToolchainLoadTask;
        await vm.Settings.InspectToolchainPathAsync(runtime.Verified.Identity!.Path);
        vm.CloseSettingsCommand.Execute(null);
        Assert.True(vm.Settings.IsEventBufferFormatCloseConfirmationOpen);
        vm.Settings.ConfirmEventBufferFormatCloseCommand.Execute(null);
        Assert.True(vm.IsSettingsModalOpen);
        Assert.True(vm.Settings.IsToolchainCloseConfirmationOpen);
        Assert.True(vm.Settings.HasToolchainUnsavedChanges);
        Assert.Equal(0, runtime.SaveCount);
        await vm.Settings.ConfirmToolchainCloseCommand.ExecuteAsync(null);
        Assert.False(vm.IsSettingsModalOpen);
        Assert.False(vm.Settings.HasToolchainUnsavedChanges);
        Assert.False(vm.Settings.HasEventBufferFormatUnsavedChanges);
    }

    internal static MainWindowViewModel CreateToolchainViewModel(IToolchainRuntimeConfigurationSession session, ShellLanguage language = ShellLanguage.English,
        IEventBufferFormatConfigurationSession? formatSession = null,
        IExternalProcessorEnvironmentLoader? externalEnvironmentLoader = null)
    {
        PresentationHostServices original = PresentationTestHost.CreateServices(ApplicationVersionProvider.InformationalVersion);
        var services = new PresentationHostServices(original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter, original.RawBinaryEditorFileSessions,
            original.CanonicalCatalogLoader, externalEnvironmentLoader ?? original.ExternalEnvironmentLoader, original.LocalFiles,
            versionManagement: null, managedApplicationStartup: null, stableLauncherHandoff: null,
            eventBufferFormatConfigurationSessionFactory: formatSession is null ? null : _ => Task.FromResult(formatSession),
            toolchainRuntimeConfigurationSessionFactory: _ => Task.FromResult<IToolchainRuntimeConfigurationSession>(session));
        return PresentationTestHost.PublishCanonicalCatalog(services, ShellViewModelFactory.Create(services, language));
    }

    private static async Task<ExternalProcessorEnvironmentLoadResult> ReadEnvironmentAsync(ExternalProcessorEnvironmentLoader loader)
    {
        ExternalProcessorEnvironmentLoadResult? result = null;
        await foreach (ExternalProcessorEnvironmentLoadUpdate update in loader.LoadAsync(TestContext.Current.CancellationToken))
        {
            result = update.Result ?? result;
        }
        return Assert.IsType<ExternalProcessorEnvironmentLoadResult>(result);
    }

    private sealed class MemoryToolchainStorage : IToolchainRuntimeConfigurationStorage
    {
        public ValueTask<ToolchainRuntimeConfigurationDocument?> ReadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<ToolchainRuntimeConfigurationDocument?>(null);
        }
        public ValueTask WriteAsync(ToolchainRuntimeConfigurationDocument document, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BundledCandidateInspector : IToolchainRuntimeCandidateInspector
    {
        public ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new ToolchainRuntimeCandidateInspection(null, ToolchainRuntimeCandidateVerification.Unknown, []));
        }
        public ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IReadOnlyList<ToolchainRuntimeCandidateInspection>>([]);
        }
    }

    private sealed class EmptyReadiness : IRuntimeDependencyReadinessProvider
    {
        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request, long generation, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}

internal sealed class ToolchainUiSession : IToolchainRuntimeConfigurationSession
{
    public ToolchainRuntimeConfigurationSnapshot Current { get; set; } = new(1, ToolchainRuntimeConfigurationStatus.Current,
        new(ToolchainRuntimeSource.Bundled), new(ToolchainRuntimeSource.Bundled), []);
    internal ToolchainRuntimeCandidateInspection Verified { get; } = new(new("C:/runtime/vcruntime140.dll", new('a', 64), "14.44.35211.0", "x64"), ToolchainRuntimeCandidateVerification.Verified, []);
    internal bool RejectInspection { get; set; }
    internal bool FailSave { get; set; }
    internal bool BundledMissing { get; set; }
    internal int SaveCount { get; private set; }
    internal int ReloadCount { get; private set; }
    internal ToolchainRuntimeSelection? LastSaved { get; private set; }
    internal TaskCompletionSource<ToolchainRuntimeCandidateInspection>? PendingInspection { get; set; }
    public ValueTask<ToolchainRuntimeConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken)
    {
        ReloadCount++;
        return ValueTask.FromResult(new ToolchainRuntimeConfigurationOperationResult(Current, true, Current.Issues));
    }
    public ValueTask<ToolchainRuntimeConfigurationOperationResult> SaveAsync(ToolchainRuntimeSelection selection, CancellationToken cancellationToken)
    {
        SaveCount++;
        LastSaved = selection;
        if (FailSave) { return ValueTask.FromResult(new ToolchainRuntimeConfigurationOperationResult(Current, false, [new("save-failed", "Write failed")])); }
        Current = new(Current.Generation + 1, ToolchainRuntimeConfigurationStatus.Current, selection, selection, []);
        return ValueTask.FromResult(new ToolchainRuntimeConfigurationOperationResult(Current, true, []));
    }
    public ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken)
    {
        return PendingInspection is not null ? new(PendingInspection.Task) :
            ValueTask.FromResult(RejectInspection ? new(null, ToolchainRuntimeCandidateVerification.Rejected, [new("untrusted", "Microsoft trust unavailable")]) : Verified);
    }
    public ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ToolchainRuntimeCandidateInspection(
            BundledMissing ? null : Verified.Identity, ToolchainRuntimeCandidateVerification.Unknown,
            BundledMissing ? [new("missing", "Bundled runtime is missing")] : []));
    }
    public ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<IReadOnlyList<ToolchainRuntimeCandidateInspection>>(
            RejectInspection
                ? [new(null, ToolchainRuntimeCandidateVerification.Rejected, [new("untrusted", "Microsoft trust unavailable")])]
                : [Verified]);
    }
}
