using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>One immutable desktop dependency graph created at the executable boundary.</summary>
public sealed class PresentationHostServices
{
    /// <summary>Creates the legacy host graph when managed version services are not available.</summary>
    public PresentationHostServices(
        PresentationCompositionServices composition,
        IFileRevealService fileReveal,
        ICanonicalSupportMatrixQuery supportMatrix,
        ISystemInformationService systemInformation,
        ISystemDiagnosticsExporter systemDiagnosticsExporter,
        IRawBinaryEditorFileSessionFactory rawBinaryEditorFileSessions,
        ICanonicalCapabilityCatalogLoader canonicalCatalogLoader,
        IExternalProcessorEnvironmentLoader externalEnvironmentLoader,
        ILocalFileStore localFiles)
        : this(
            composition,
            fileReveal,
            supportMatrix,
            systemInformation,
            systemDiagnosticsExporter,
            rawBinaryEditorFileSessions,
            canonicalCatalogLoader,
            externalEnvironmentLoader,
            localFiles,
            managedAppVersion: null,
            versionManagement: null,
            applicationReadySignal: null,
            stableLauncherHandoff: null)
    {
    }

    /// <summary>Creates the immutable Application and platform services consumed by Presentation.</summary>
    public PresentationHostServices(
        PresentationCompositionServices composition,
        IFileRevealService fileReveal,
        ICanonicalSupportMatrixQuery supportMatrix,
        ISystemInformationService systemInformation,
        ISystemDiagnosticsExporter systemDiagnosticsExporter,
        IRawBinaryEditorFileSessionFactory rawBinaryEditorFileSessions,
        ICanonicalCapabilityCatalogLoader canonicalCatalogLoader,
        IExternalProcessorEnvironmentLoader externalEnvironmentLoader,
        ILocalFileStore localFiles,
        ManagedAppVersion? managedAppVersion,
        IVersionManagementExperience? versionManagement,
        IApplicationReadySignal? applicationReadySignal,
        IStableLauncherHandoff? stableLauncherHandoff)
    {
        Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        FileReveal = fileReveal ?? throw new ArgumentNullException(nameof(fileReveal));
        SupportMatrix = supportMatrix ?? throw new ArgumentNullException(nameof(supportMatrix));
        SystemInformation = systemInformation ?? throw new ArgumentNullException(nameof(systemInformation));
        SystemDiagnosticsExporter = systemDiagnosticsExporter ??
            throw new ArgumentNullException(nameof(systemDiagnosticsExporter));
        RawBinaryEditorFileSessions = rawBinaryEditorFileSessions ??
            throw new ArgumentNullException(nameof(rawBinaryEditorFileSessions));
        CanonicalCatalogLoader = canonicalCatalogLoader ??
            throw new ArgumentNullException(nameof(canonicalCatalogLoader));
        ExternalEnvironmentLoader = externalEnvironmentLoader ??
            throw new ArgumentNullException(nameof(externalEnvironmentLoader));
        LocalFiles = localFiles ?? throw new ArgumentNullException(nameof(localFiles));
        ManagedAppVersion = managedAppVersion;
        VersionManagement = versionManagement;
        ApplicationReadySignal = applicationReadySignal;
        StableLauncherHandoff = stableLauncherHandoff;
    }

    internal PresentationCompositionServices Composition { get; }

    internal IFileRevealService FileReveal { get; }

    internal ICanonicalSupportMatrixQuery SupportMatrix { get; }

    internal ISystemInformationService SystemInformation { get; }

    internal ISystemDiagnosticsExporter SystemDiagnosticsExporter { get; }

    internal IRawBinaryEditorFileSessionFactory RawBinaryEditorFileSessions { get; }

    internal ICanonicalCapabilityCatalogLoader CanonicalCatalogLoader { get; }

    internal IExternalProcessorEnvironmentLoader ExternalEnvironmentLoader { get; }

    internal ILocalFileStore LocalFiles { get; }

    internal ManagedAppVersion? ManagedAppVersion { get; }

    internal IVersionManagementExperience? VersionManagement { get; }

    internal IApplicationReadySignal? ApplicationReadySignal { get; }

    internal IStableLauncherHandoff? StableLauncherHandoff { get; }
}
