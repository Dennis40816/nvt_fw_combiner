using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Cli;

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
        IEnumerable<ProtectedPathGuard.ProtectedPath> protectedPaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        ArgumentNullException.ThrowIfNull(protectedPaths);

        string fullPath = Path.GetFullPath(reportPath);
        ProtectedPathGuard.EnsureDoesNotAlias(
            fullPath,
            "Report path",
            protectedPaths,
            nameof(reportPath));

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
