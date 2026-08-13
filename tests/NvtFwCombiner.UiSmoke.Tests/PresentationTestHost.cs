using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

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

    private static PresentationHostServices CreateServices(
        string applicationVersion,
        Func<IGeneralAuthoring, IGeneralAuthoring> generalAuthoringDecorator)
    {
        var externalEnvironment = new ExternalProcessorEnvironmentLoader(ExternalEnvironment.Value);
        var host = CompositionHostServices.Create(externalEnvironment);
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
}
