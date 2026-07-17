using System.Globalization;
using System.Text.Json;

using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private static async Task PrintResultAsync(
        WorkbenchRunResult result,
        string icId,
        TextWriter output,
        TextWriter error,
        bool reportWritten)
    {
        await output.WriteLineAsync($"Status: {result.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {result.ProfileId} ({icId})").ConfigureAwait(false);
        await output.WriteLineAsync("Experience: general-merge").ConfigureAwait(false);
        await output.WriteLineAsync($"Output: {result.OutputFileName}").ConfigureAwait(false);
        await output.WriteLineAsync($"Size: {result.OutputSize.ToString(CultureInfo.InvariantCulture)} bytes").ConfigureAwait(false);
        await output.WriteLineAsync($"SHA256: {result.OutputSha256}").ConfigureAwait(false);
        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        if (result.Succeeded)
        {
            return;
        }

        if (reportWritten)
        {
            await error.WriteLineAsync("General Merge failed; inspect the JSON report for issues.").ConfigureAwait(false);
            return;
        }

        await error.WriteLineAsync("General Merge failed; no JSON report was written. Issues:").ConfigureAwait(false);
        await PrintReportIssuesAsync(result.ReportJson, error).ConfigureAwait(false);
    }

    private static async Task PrintReportIssuesAsync(string reportJson, TextWriter error)
    {
        using var document = JsonDocument.Parse(reportJson);
        if (!document.RootElement.TryGetProperty("Issues", out JsonElement issues) ||
            issues.ValueKind != JsonValueKind.Array ||
            issues.GetArrayLength() == 0)
        {
            await error.WriteLineAsync("  - Unknown issue: no issue rows were recorded.").ConfigureAwait(false);
            return;
        }

        foreach (JsonElement issue in issues.EnumerateArray())
        {
            string code = GetJsonString(issue, "Code", "unknown");
            string source = GetJsonString(issue, "Source", IcWorkflowIds.GeneralMerge);
            string message = GetJsonString(issue, "Message", "No message.");
            await error.WriteLineAsync($"  - {code} [{source}]: {message}").ConfigureAwait(false);
        }
    }

    private static string GetJsonString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!
                : fallback;
    }

}
