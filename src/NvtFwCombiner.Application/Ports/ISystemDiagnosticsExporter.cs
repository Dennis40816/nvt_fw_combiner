using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Writes a versioned privacy-filtered System Information bundle.</summary>
public interface ISystemDiagnosticsExporter
{
    /// <summary>Writes the supplied immutable bundle to one caller-selected destination.</summary>
    ValueTask ExportAsync(
        SystemDiagnosticsBundle bundle,
        string destinationPath,
        CancellationToken cancellationToken);
}
