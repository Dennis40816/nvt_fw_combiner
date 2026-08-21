using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        return DesktopApplication.Run(
            CreatePresentationHostServices,
            CompositionHostServices.CreateLocalFileStore(),
            args);
    }

    private static PresentationHostServices CreatePresentationHostServices()
    {
        var host = CompositionHostServices.Create();
        ManagedAppVersion appVersion = ManagedAppVersion.Parse(DesktopApplication.InformationalVersion);
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
            host.CreateSystemInformationService(DesktopApplication.InformationalVersion),
            CompositionHostServices.CreateSystemDiagnosticsExporter(),
            host.RawBinaryEditorFileSessions,
            host.CanonicalCatalogLoader,
            host.ExternalEnvironmentLoader,
            host.LocalFiles,
            appVersion,
            CompositionHostServices.CreateVersionManagementExperience(appVersion.ToString()),
            CompositionHostServices.CreateApplicationReadySignal(),
            CompositionHostServices.CreateStableLauncherHandoff());
    }
}
