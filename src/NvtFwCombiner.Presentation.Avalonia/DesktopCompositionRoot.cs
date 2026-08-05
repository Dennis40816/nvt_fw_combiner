using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>One immutable desktop dependency graph created at the executable boundary.</summary>
internal sealed class PresentationHostServices
{
    internal PresentationHostServices(
        PresentationCompositionServices composition,
        IFileRevealService fileReveal,
        ICanonicalSupportMatrixQuery supportMatrix,
        ISystemInformationService systemInformation,
        ISystemDiagnosticsExporter systemDiagnosticsExporter,
        Action<CancellationToken> warmCanonicalCapabilities)
    {
        Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        FileReveal = fileReveal ?? throw new ArgumentNullException(nameof(fileReveal));
        SupportMatrix = supportMatrix ?? throw new ArgumentNullException(nameof(supportMatrix));
        SystemInformation = systemInformation ?? throw new ArgumentNullException(nameof(systemInformation));
        SystemDiagnosticsExporter = systemDiagnosticsExporter ??
            throw new ArgumentNullException(nameof(systemDiagnosticsExporter));
        WarmCanonicalCapabilities = warmCanonicalCapabilities ??
            throw new ArgumentNullException(nameof(warmCanonicalCapabilities));
    }

    internal PresentationCompositionServices Composition { get; }

    internal IFileRevealService FileReveal { get; }

    internal ICanonicalSupportMatrixQuery SupportMatrix { get; }

    internal ISystemInformationService SystemInformation { get; }

    internal ISystemDiagnosticsExporter SystemDiagnosticsExporter { get; }

    internal Action<CancellationToken> WarmCanonicalCapabilities { get; }
}

internal static class DesktopCompositionRoot
{
    internal static PresentationHostServices Create(string appVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        return new PresentationHostServices(
            new PresentationCompositionServices(
                WorkbenchHostServices.CompositionCapabilityExperience,
                WorkbenchHostServices.CompositionAuthoringExperience,
                WorkbenchHostServices.CompositionAuthoringSession,
                WorkbenchHostServices.CompositionMemoryPresentation,
                WorkbenchHostServices.FirmwareInspectionExperience,
                WorkbenchHostServices.CompositionOutputNaming,
                WorkbenchHostServices.AbMergeDeliveryPlanning,
                WorkbenchHostServices.CompositionExecution),
            WorkbenchHostServices.CreateFileRevealService(),
            WorkbenchHostServices.CanonicalSupportMatrixQuery,
            WorkbenchHostServices.CreateSystemInformationService(appVersion),
            WorkbenchHostServices.CreateSystemDiagnosticsExporter(),
            WorkbenchHostServices.WarmCanonicalCapabilities);
    }
}
