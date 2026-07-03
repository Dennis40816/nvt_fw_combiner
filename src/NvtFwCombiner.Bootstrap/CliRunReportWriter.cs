using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static class CliRunReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static async ValueTask<string> WriteAsync(
        CompositionRunReport report,
        string reportPath,
        IEnumerable<string> protectedInputPaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        ArgumentNullException.ThrowIfNull(protectedInputPaths);

        string fullPath = Path.GetFullPath(reportPath);
        foreach (string protectedInputPath in protectedInputPaths)
        {
            string protectedFullPath = Path.GetFullPath(protectedInputPath);
            if (string.Equals(fullPath, protectedFullPath, PathComparison))
            {
                throw new ArgumentException("Report path must not overwrite an input artifact.", nameof(reportPath));
            }
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, report, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        return fullPath;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
