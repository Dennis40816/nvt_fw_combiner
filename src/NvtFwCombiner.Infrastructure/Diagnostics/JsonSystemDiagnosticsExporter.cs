using System.Text.Json;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Diagnostics;

/// <summary>Atomically exports only the allowlisted System Information bundle.</summary>
public sealed class JsonSystemDiagnosticsExporter : ISystemDiagnosticsExporter
{
    /// <inheritdoc />
    public async ValueTask ExportAsync(
        SystemDiagnosticsBundle bundle,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        string fullPath = Path.GetFullPath(destinationPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                "The diagnostics destination directory does not exist.");
        }

        byte[] document = JsonSerializer.SerializeToUtf8Bytes(
            bundle,
            SystemDiagnosticsJsonContext.Default.SystemDiagnosticsBundle);
        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 65_536,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(document, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }
}
