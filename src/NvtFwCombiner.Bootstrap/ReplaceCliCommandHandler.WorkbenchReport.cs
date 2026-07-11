using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task WriteWorkbenchReportFileIfRequestedAsync(
        WorkbenchRunResult result,
        ParsedOptions options,
        IReadOnlyList<InputArtifactBinding> bindings,
        string? outputFullPath,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (!options.Values.TryGetValue("--report", out string? reportPath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(reportPath);
        ProtectedPathGuard.EnsureDoesNotAlias(
            fullPath,
            "Report path",
            ProtectedPathGuard.CreateProtectedPaths(bindings, outputFullPath),
            nameof(reportPath));
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, result.ReportJson, cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync($"Report: {fullPath}").ConfigureAwait(false);
    }

    private static async Task PrintWorkbenchRunResultAsync(
        WorkbenchRunResult result,
        string icId,
        string experienceId,
        TextWriter output,
        TextWriter error)
    {
        await output.WriteLineAsync($"Status: {result.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {result.ProfileId} ({icId})").ConfigureAwait(false);
        await output.WriteLineAsync($"Experience: {experienceId}").ConfigureAwait(false);
        await output.WriteLineAsync($"Output: {result.OutputFileName}").ConfigureAwait(false);
        await output.WriteLineAsync($"Size: {result.OutputSize.ToString(CultureInfo.InvariantCulture)} bytes").ConfigureAwait(false);
        await output.WriteLineAsync($"SHA256: {result.OutputSha256}").ConfigureAwait(false);
        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("Mutations", out JsonElement mutations) && mutations.GetArrayLength() > 0)
        {
            await output.WriteLineAsync("Mutations:").ConfigureAwait(false);
            foreach (JsonElement mutation in mutations.EnumerateArray())
            {
                string operationId = mutation.GetProperty("OperationId").GetString() ?? string.Empty;
                string targetSpaceId = mutation.GetProperty("TargetSpaceId").GetString() ?? string.Empty;
                JsonElement range = mutation.GetProperty("TargetRange");
                long changed = mutation.GetProperty("ChangedByteCount").GetInt64();
                await output.WriteLineAsync(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"  {operationId}: {targetSpaceId} {FormatRange(range)} changed={changed}"))
                    .ConfigureAwait(false);
            }
        }

        if (root.TryGetProperty("Issues", out JsonElement issues) && issues.GetArrayLength() > 0)
        {
            await error.WriteLineAsync("Issues:").ConfigureAwait(false);
            foreach (JsonElement issue in issues.EnumerateArray())
            {
                string code = issue.GetProperty("Code").GetString() ?? string.Empty;
                string message = issue.GetProperty("Message").GetString() ?? string.Empty;
                string? operationId = issue.TryGetProperty("OperationId", out JsonElement operation) &&
                    operation.ValueKind == JsonValueKind.String
                        ? operation.GetString()
                        : null;
                await error.WriteLineAsync(
                        string.IsNullOrWhiteSpace(operationId)
                            ? $"  {code}: {message}"
                            : $"  {code} [{operationId}]: {message}")
                    .ConfigureAwait(false);
            }
        }
    }

    private static string FormatRange(JsonElement range)
    {
        long start = range.GetProperty("Start").GetInt64();
        long length = range.GetProperty("Length").GetInt64();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"0x{start:X}-0x{start + length - 1:X} (len 0x{length:X})");
    }
}
