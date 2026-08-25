using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class PresentationTestHost
{
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
            static authoring => authoring,
            useRetainedDpReplacePolicy: false);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            services,
            language);
        return PublishCanonicalCatalog(services, viewModel);
    }

    internal static async Task<MainWindowViewModel> CreateViewModelAsync(
        CancellationToken cancellationToken,
        ShellLanguage language = ShellLanguage.English)
    {
        PresentationHostServices services = CreateServices(
            ApplicationVersionProvider.InformationalVersion);
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

    internal static PresentationHostServices CreateServices(
        string applicationVersion,
        Application.VersionManagement.IVersionManagementExperience versionManagement,
        Application.VersionManagement.IStableLauncherHandoff? stableLauncherHandoff = null)
    {
        ArgumentNullException.ThrowIfNull(versionManagement);
        var externalEnvironment = new ExternalProcessorEnvironmentLoader(ExternalEnvironment.Value);
        CompositionHostServices host = CompositionHostServices.Create(
            externalEnvironment,
            RetainedDpReplaceRegressionPolicy.Load);
        return new PresentationHostServices(
            new PresentationCompositionServices(
                host.CompositionCapabilityExperience,
                host.StandardMergeAuthoring,
                host.AbMergeAuthoring,
                host.DpReplaceAuthoring,
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
            versionManagement,
            new Application.VersionManagement.ManagedApplicationStartupCoordinator(
                Application.VersionManagement.ManagedAppVersion.Parse(applicationVersion),
                new UnmanagedApplicationReadySignal(),
                versionManagement),
            stableLauncherHandoff);
    }

    private static PresentationHostServices CreateServices(
        string applicationVersion,
        Func<IGeneralAuthoring, IGeneralAuthoring> generalAuthoringDecorator,
        bool useRetainedDpReplacePolicy = true)
    {
        var externalEnvironment = new ExternalProcessorEnvironmentLoader(ExternalEnvironment.Value);
        CompositionHostServices host = useRetainedDpReplacePolicy
            ? CompositionHostServices.Create(
                externalEnvironment,
                RetainedDpReplaceRegressionPolicy.Load)
            : CompositionHostServices.Create(externalEnvironment);
        return new PresentationHostServices(
            new PresentationCompositionServices(
                host.CompositionCapabilityExperience,
                host.StandardMergeAuthoring,
                host.AbMergeAuthoring,
                host.DpReplaceAuthoring,
                generalAuthoringDecorator(host.GeneralAuthoring),
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
            host.LocalFiles);
    }

    private static ExternalProcessorRuntimeEnvironment LoadExternalEnvironment()
    {
        var loader = new ExternalProcessorEnvironmentLoader();
        Assert.True(((IExternalProcessorEnvironmentLoader)loader)
            .LoadToCompletionAsync(null, CancellationToken.None)
            .GetAwaiter().GetResult().Succeeded);
        ExternalProcessorEnvironmentLease lease = loader.AcquireCurrent();
        return new(lease.Processor, lease.ReadinessProvider, loader.Current.ManifestCount);
    }

    internal static Application.HexEditor.IRawBinaryEditorFileSessionFactory
        CreateRawBinaryEditorFileSessionFactory()
    {
        return CompositionHostServices.Create().RawBinaryEditorFileSessions;
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
