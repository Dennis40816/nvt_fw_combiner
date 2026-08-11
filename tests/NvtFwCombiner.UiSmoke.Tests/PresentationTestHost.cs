using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class PresentationTestHost
{
    internal static MainWindowViewModel CreateViewModel(
        ShellLanguage language = ShellLanguage.English)
    {
        PresentationHostServices services = CreateServices(
            ApplicationVersionProvider.InformationalVersion);
        return ShellViewModelFactory.Create(services, language);
    }

    internal static PresentationHostServices CreateServices(string applicationVersion)
    {
        var host = CompositionHostServices.Create();
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
            host.WarmCanonicalCapabilities);
    }

    internal static Application.HexEditor.IRawBinaryEditorFileSessionFactory
        CreateRawBinaryEditorFileSessionFactory()
    {
        return CompositionHostServices.Create().RawBinaryEditorFileSessions;
    }
}
