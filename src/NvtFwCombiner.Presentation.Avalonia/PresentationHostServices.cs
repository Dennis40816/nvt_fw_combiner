using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>One immutable desktop dependency graph created at the executable boundary.</summary>
public sealed class PresentationHostServices
{
    /// <summary>Creates the immutable Application and platform services consumed by Presentation.</summary>
    public PresentationHostServices(
        PresentationCompositionServices composition,
        IFileRevealService fileReveal,
        ICanonicalSupportMatrixQuery supportMatrix,
        ISystemInformationService systemInformation,
        ISystemDiagnosticsExporter systemDiagnosticsExporter,
        IRawBinaryEditorFileSessionFactory rawBinaryEditorFileSessions,
        Action<CancellationToken> warmCanonicalCapabilities)
    {
        Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        FileReveal = fileReveal ?? throw new ArgumentNullException(nameof(fileReveal));
        SupportMatrix = supportMatrix ?? throw new ArgumentNullException(nameof(supportMatrix));
        SystemInformation = systemInformation ?? throw new ArgumentNullException(nameof(systemInformation));
        SystemDiagnosticsExporter = systemDiagnosticsExporter ??
            throw new ArgumentNullException(nameof(systemDiagnosticsExporter));
        RawBinaryEditorFileSessions = rawBinaryEditorFileSessions ??
            throw new ArgumentNullException(nameof(rawBinaryEditorFileSessions));
        WarmCanonicalCapabilities = warmCanonicalCapabilities ??
            throw new ArgumentNullException(nameof(warmCanonicalCapabilities));
    }

    internal PresentationCompositionServices Composition { get; }

    internal IFileRevealService FileReveal { get; }

    internal ICanonicalSupportMatrixQuery SupportMatrix { get; }

    internal ISystemInformationService SystemInformation { get; }

    internal ISystemDiagnosticsExporter SystemDiagnosticsExporter { get; }

    internal IRawBinaryEditorFileSessionFactory RawBinaryEditorFileSessions { get; }

    internal Action<CancellationToken> WarmCanonicalCapabilities { get; }
}
