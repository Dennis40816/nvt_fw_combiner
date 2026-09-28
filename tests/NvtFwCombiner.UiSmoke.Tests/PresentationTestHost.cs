using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class PresentationTestHost
{
    internal static async Task<MainWindowViewModel> CreateConfiguredFormatViewModelAsync(TempWorkspace workspace)
    {
        PresentationHostServices services = await CreateConfiguredFormatServicesAsync(
            workspace, ApplicationVersionProvider.InformationalVersion);
        return PublishCanonicalCatalog(services, ShellViewModelFactory.Create(services, ShellLanguage.English));
    }

    internal static async Task<PresentationHostServices> CreateConfiguredFormatServicesAsync(
        TempWorkspace workspace, string version,
        Func<IAbMergeAuthoring, IAbMergeAuthoring>? abMergeAuthoringDecorator = null)
    {
        CompositionHostServices host = CompositionHostServices.Create(
            new ExternalProcessorEnvironmentLoader(ExternalEnvironment.Value),
            NvtFwCombiner.Infrastructure.Capabilities.BuiltInCanonicalCapabilityPolicy.Load,
            workspace.PathFor("local-state"), configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(
            TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(),
            TestContext.Current.CancellationToken)).Succeeded);
        return CreateServices(version, host, static authoring => authoring,
            abMergeAuthoring: abMergeAuthoringDecorator?.Invoke(host.AbMergeAuthoring));
    }

    private static readonly Lazy<ExternalProcessorRuntimeEnvironment> ExternalEnvironment =
        new(LoadExternalEnvironment);

    internal static MainWindowViewModel CreateViewModel(
        ShellLanguage language = ShellLanguage.English)
    {
        PresentationHostServices services = CreateServices(
            ApplicationVersionProvider.InformationalVersion);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(services, language);
        return PublishCanonicalCatalog(services, viewModel);
    }

    internal static MainWindowViewModel CreateProductViewModel(
        ShellLanguage language = ShellLanguage.English)
    {
        PresentationHostServices services = CreateServices(
            ApplicationVersionProvider.InformationalVersion,
            static authoring => authoring);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            services,
            language);
        return PublishCanonicalCatalog(services, viewModel);
    }

    internal static async Task<MainWindowViewModel> CreateViewModelAsync(
        CancellationToken cancellationToken,
        ShellLanguage language = ShellLanguage.English)
    {
        // The legacy lazy external-tool loader waits synchronously; never initialize it on the UI dispatcher.
        PresentationHostServices services = await Task.Run(
            () => CreateServices(ApplicationVersionProvider.InformationalVersion), cancellationToken);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(services, language);
        return await PublishCanonicalCatalogAsync(
                services,
                viewModel,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static MainWindowViewModel CreateViewModel(
        Func<IGeneralAuthoring, IGeneralAuthoring> generalAuthoringDecorator,
        ShellLanguage language = ShellLanguage.English)
    {
        ArgumentNullException.ThrowIfNull(generalAuthoringDecorator);
        PresentationHostServices services = CreateServices(
            ApplicationVersionProvider.InformationalVersion,
            generalAuthoringDecorator);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(services, language);
        return PublishCanonicalCatalog(services, viewModel);
    }

    internal static MainWindowViewModel PublishCanonicalCatalog(
        PresentationHostServices services,
        MainWindowViewModel viewModel)
    {
        CapabilityCatalogReloadResult reload =
            LoadCanonicalCatalogAsync(
                services.CanonicalCatalogLoader,
                CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(reload.Succeeded);
        viewModel.PublishCanonicalCatalogState();
        return viewModel;
    }

    private static async Task<MainWindowViewModel> PublishCanonicalCatalogAsync(
        PresentationHostServices services,
        MainWindowViewModel viewModel,
        CancellationToken cancellationToken)
    {
        CapabilityCatalogReloadResult reload = await LoadCanonicalCatalogAsync(
                services.CanonicalCatalogLoader,
                cancellationToken)
            .ConfigureAwait(false);
        Assert.True(reload.Succeeded);
        viewModel.PublishCanonicalCatalogState();
        return viewModel;
    }

    internal static async Task<CapabilityCatalogReloadResult> LoadCanonicalCatalogAsync(
        ICanonicalCapabilityCatalogLoader loader,
        CancellationToken cancellationToken)
    {
        CapabilityCatalogReloadResult? result = null;
        await foreach (CanonicalCapabilityCatalogLoadUpdate update in
                       loader.LoadAsync(cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            result = update.Result ?? result;
        }

        return result ?? throw new InvalidOperationException(
            "Canonical catalog loading completed without a terminal result.");
    }

    internal static PresentationHostServices CreateServices(string applicationVersion)
    {
        return CreateServices(applicationVersion, static authoring => authoring);
    }

    /// <summary>Creates a shell host whose local-state IO goes through <paramref name="localFiles"/>.</summary>
    internal static async Task<(PresentationHostServices Services, string LocalStateDirectory)> CreateServicesAsync(
        ILocalFileStore localFiles)
    {
        ArgumentNullException.ThrowIfNull(localFiles);
        string directory = IsolatedLocalState.CreateDirectory("ui-shell");
        // The legacy lazy external-tool loader waits synchronously; never initialize it on the UI dispatcher.
        PresentationHostServices services = await Task.Run(
            () => CreateServices(ApplicationVersionProvider.InformationalVersion, static authoring => authoring, directory),
            TestContext.Current.CancellationToken);
        return (new PresentationHostServices(services.Composition, services.FileReveal, services.SupportMatrix,
            services.SystemInformation, services.SystemDiagnosticsExporter, services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader, services.ExternalEnvironmentLoader, localFiles,
            services.LocalStateDirectory), directory);
    }

    internal static PresentationHostServices CreateServicesWithCatalogPolicy(
        string applicationVersion,
        Func<CanonicalCapabilityPolicySnapshot> loadPolicy)
    {
        ArgumentNullException.ThrowIfNull(loadPolicy);
        var externalEnvironment = new ExternalProcessorEnvironmentLoader(ExternalEnvironment.Value);
        CompositionHostServices host = CompositionHostServices.Create(
            externalEnvironment,
            loadPolicy,
            IsolatedLocalState.CreateDirectory("ui-host"));
        return CreateServices(
            applicationVersion,
            host,
            static authoring => authoring);
    }

    internal static PresentationHostServices CreateServices(
        string applicationVersion,
        Application.VersionManagement.IVersionManagementExperience versionManagement,
        Application.VersionManagement.IStableLauncherHandoff? stableLauncherHandoff = null)
    {
        ArgumentNullException.ThrowIfNull(versionManagement);
        var externalEnvironment = new ExternalProcessorEnvironmentLoader(ExternalEnvironment.Value);
        CompositionHostServices host = CompositionHostServices.Create(
            externalEnvironment,
            NvtFwCombiner.Infrastructure.Capabilities.BuiltInCanonicalCapabilityPolicy.Load,
            IsolatedLocalState.CreateDirectory("ui-host"));
        return new PresentationHostServices(
            new PresentationCompositionServices(
                host.CompositionCapabilityExperience,
                host.StandardMergeAuthoring,
                host.AbMergeAuthoring,
                host.GeneralAuthoring,
                host.CtrlRamAuthoring,
                host.FirmwareInspectionExperience,
                host.CompositionOutputNaming,
                host.CompositionExecution),
            CompositionHostServices.CreateFileRevealService(),
            host.CanonicalSupportMatrixQuery,
            host.CreateSystemInformationService(applicationVersion),
            CompositionHostServices.CreateSystemDiagnosticsExporter(),
            host.RawBinaryEditorFileSessions,
            host.CanonicalCatalogLoader,
            host.ExternalEnvironmentLoader,
            host.LocalFiles,
            host.LocalStateDirectory,
            versionManagement,
            new Application.VersionManagement.ManagedApplicationStartupCoordinator(
                Application.VersionManagement.ManagedAppVersion.Parse(applicationVersion),
                new UnmanagedApplicationReadySignal(),
                versionManagement),
            stableLauncherHandoff);
    }

    internal static PresentationHostServices CreateServices(
        string applicationVersion,
        Func<IGeneralAuthoring, IGeneralAuthoring> generalAuthoringDecorator,
        string? localStateDirectory = null)
    {
        var externalEnvironment = new ExternalProcessorEnvironmentLoader(ExternalEnvironment.Value);
        CompositionHostServices host = CompositionHostServices.Create(
            externalEnvironment,
            localStateDirectory ?? IsolatedLocalState.CreateDirectory("ui-host"));
        return CreateServices(applicationVersion, host, generalAuthoringDecorator);
    }

    internal static PresentationHostServices CreateServices(
        string applicationVersion,
        CompositionHostServices host,
        Func<IGeneralAuthoring, IGeneralAuthoring> generalAuthoringDecorator,
        IAbMergeAuthoring? abMergeAuthoring = null,
        ICompositionExecution? execution = null,
        ICompositionOutputNaming? outputNaming = null,
        ICtrlRamAuthoring? ctrlRamAuthoring = null)
    {
        return new PresentationHostServices(
            new PresentationCompositionServices(
                host.CompositionCapabilityExperience,
                host.StandardMergeAuthoring,
                abMergeAuthoring ?? host.AbMergeAuthoring,
                generalAuthoringDecorator(host.GeneralAuthoring),
                ctrlRamAuthoring ?? host.CtrlRamAuthoring,
                host.FirmwareInspectionExperience,
                outputNaming ?? host.CompositionOutputNaming,
                execution ?? host.CompositionExecution),
            CompositionHostServices.CreateFileRevealService(),
            host.CanonicalSupportMatrixQuery,
            host.CreateSystemInformationService(applicationVersion),
            CompositionHostServices.CreateSystemDiagnosticsExporter(),
            host.RawBinaryEditorFileSessions,
            host.CanonicalCatalogLoader,
            host.ExternalEnvironmentLoader,
            host.LocalFiles,
            host.LocalStateDirectory,
            versionManagement: null,
            managedApplicationStartup: null,
            stableLauncherHandoff: null,
            eventBufferFormatConfigurationSessionFactory: host.GetEventBufferFormatConfigurationAsync);
    }

    private static ExternalProcessorRuntimeEnvironment LoadExternalEnvironment()
    {
        var loader = new ExternalProcessorEnvironmentLoader(
            RepositoryPaths.FromRepositoryRoot("external-tools"));
        // The first caller can be a headless test or a UiThreadTestContext. Loading on the thread pool
        // keeps this blocking wait from deadlocking on that caller's single-threaded context.
        Assert.True(Task.Run(() => ((IExternalProcessorEnvironmentLoader)loader)
                .LoadToCompletionAsync(null, CancellationToken.None))
            .GetAwaiter().GetResult().Succeeded);
        ExternalProcessorEnvironmentLease lease = loader.AcquireCurrent();
        return new(lease.Processor, lease.ReadinessProvider, loader.Current.ManifestCount);
    }

    internal static Application.HexEditor.IRawBinaryEditorFileSessionFactory
        CreateRawBinaryEditorFileSessionFactory()
    {
        return CompositionHostServices.Create(IsolatedLocalState.CreateDirectory()).RawBinaryEditorFileSessions;
    }

    private sealed class UnmanagedApplicationReadySignal
        : Application.VersionManagement.IApplicationReadySignal
    {
        public ValueTask<Application.VersionManagement.ApplicationReadySignalOutcome> ReportReadyAsync(
            Application.VersionManagement.ManagedAppVersion version,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                Application.VersionManagement.ApplicationReadySignalOutcome.NotInherited);
        }
    }
}
