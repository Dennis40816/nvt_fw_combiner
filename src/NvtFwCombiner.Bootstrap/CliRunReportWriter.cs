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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        string fullPath = Path.GetFullPath(reportPath);
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
}
