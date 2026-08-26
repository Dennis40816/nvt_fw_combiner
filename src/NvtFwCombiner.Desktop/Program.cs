using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        (string? managedRoot, string? statePath, string[] remaining) = ParseManagedHostOptions(args);
        using IInheritedManagedProcessLifetimeCapture lifetime =
            CompositionHostServices.CaptureInheritedManagedProcessLifetime(
                statePath,
                managedRoot is not null || statePath is not null);
        return lifetime.Outcome == InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext
            ? 22
            : DesktopApplication.Run(
                () => CreatePresentationHostServices(managedRoot, statePath),
                CompositionHostServices.CreateLocalFileStore(),
                remaining);
    }

    private static PresentationHostServices CreatePresentationHostServices(
        string? managedRoot,
        string? statePath)
    {
        var host = CompositionHostServices.Create();
        ManagedAppVersion appVersion = ManagedAppVersion.Parse(DesktopApplication.InformationalVersion);
        IVersionManagementExperience versionManagement =
            CompositionHostServices.CreateVersionManagementExperience(
                appVersion.ToString(),
                managedRoot,
                statePath);
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
            versionManagement,
            CompositionHostServices.CreateManagedApplicationStartupCoordinator(
                appVersion.ToString(),
                versionManagement),
            CompositionHostServices.CreateStableLauncherHandoff(managedRoot, statePath));
    }

    private static (string? ManagedRoot, string? StatePath, string[] Remaining) ParseManagedHostOptions(
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? managedRoot = null;
        string? statePath = null;
        var remaining = new List<string>();
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option is not ("--managed-root" or "--state-path"))
            {
                remaining.Add(option);
                continue;
            }
            string value = index + 1 < args.Length
                ? args[++index]
                : throw new ArgumentException("Managed host option is missing its value.", nameof(args));
            if (option == "--managed-root")
            {
                managedRoot = Path.GetFullPath(value);
            }
            else
            {
                statePath = Path.GetFullPath(value);
            }
        }
        return (managedRoot, statePath, [.. remaining]);
    }
}
