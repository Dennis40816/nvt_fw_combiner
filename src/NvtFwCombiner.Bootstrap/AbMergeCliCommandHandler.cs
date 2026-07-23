using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Focused CLI adapter for the owner-approved AB Merge pilot.</summary>
internal static class AbMergeCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    private const int SoftwareError = 70;

    private static readonly Dictionary<string, string> InputOptionsByAddressSpace =
        new(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = "--dp-ab",
            [CompositionAddressSpaceIds.TpAInput] = "--tp-a",
            [CompositionAddressSpaceIds.TpBInput] = "--tp-b",
        };

    internal static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown ab-merge command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        string[] valueOptions = ["--profile", "--dp-ab", "--tp-a", "--tp-b", "--output", "--report"];
        string[] flagOptions = action == "build" ? ["--overwrite"] : [];
        if (!CliOptionParser.TryParse(args[1..], valueOptions, [], flagOptions, error, out ParsedCliOptions options))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryFindProfile(profileSelector, out WorkbenchProfileSummary? profile))
        {
            await error.WriteLineAsync($"error: unknown AB Merge profile '{profileSelector}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!profile.CompileSucceeded)
        {
            _ = AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                profile.IcId,
                out _,
                out IReadOnlyList<CompositionIssue> issues);
            await CliCompositionRunSupport.PrintIssuesAsync(error, issues).ConfigureAwait(false);
            return SoftwareError;
        }

        if (!TryCreateSlotPaths(profile.RequiredInputAddressSpaceIds, options, error, out IReadOnlyDictionary<string, string> slotPaths))
        {
            return UsageError;
        }

        InputArtifactBinding[] bindings =
        [
            .. slotPaths.Select(pair => new InputArtifactBinding(pair.Key, pair.Key, pair.Value)),
        ];
        bool build = action == "build";
        bool hasExplicitOutput = options.Values.ContainsKey("--output");
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            profile.DefaultOutputFileName);
        if (build)
        {
            CliCompositionRunSupport.EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
        }

        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            options.Values.GetValueOrDefault("--report"),
            bindings,
            outputTarget,
            build);

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeForCliAsync(
                profile.IcId,
                slotPaths,
                build,
                build && hasExplicitOutput ? outputTarget.FullPath : null,
                !build && hasExplicitOutput ? outputTarget.FileName : null,
                cancellationToken)
            .ConfigureAwait(false);
        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            options.Values.GetValueOrDefault("--report"),
            bindings,
            new CliOutputTarget(outputTarget.OutputDirectory, result.OutputFileName),
            build);
        bool reportWritten = options.Values.TryGetValue("--report", out string? reportPath);
        if (reportWritten)
        {
            await CliCompositionRunSupport.WriteReportJsonAsync(
                    reportPath!,
                    result.ReportJson,
                    output,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await PrintResultAsync(result, profile.IcId, output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static bool TryCreateSlotPaths(
        IReadOnlyList<string> requiredAddressSpaceIds,
        ParsedCliOptions options,
        TextWriter error,
        out IReadOnlyDictionary<string, string> slotPaths)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> requiredAddressSpaces = [.. requiredAddressSpaceIds];
        foreach (string addressSpaceId in requiredAddressSpaces.Order(StringComparer.Ordinal))
        {
            if (!InputOptionsByAddressSpace.TryGetValue(addressSpaceId, out string? optionName))
            {
                error.WriteLine($"error: AB Merge profile requires unsupported address space '{addressSpaceId}'");
                slotPaths = new Dictionary<string, string>();
                return false;
            }

            if (!options.Values.TryGetValue(optionName, out string? path))
            {
                error.WriteLine($"error: {optionName} is required for address space '{addressSpaceId}'");
                slotPaths = new Dictionary<string, string>();
                return false;
            }

            paths.Add(addressSpaceId, Path.GetFullPath(path));
        }

        foreach ((string addressSpaceId, string optionName) in InputOptionsByAddressSpace)
        {
            if (options.Values.ContainsKey(optionName) && !requiredAddressSpaces.Contains(addressSpaceId))
            {
                error.WriteLine($"error: {optionName} is not used by this profile");
                slotPaths = new Dictionary<string, string>();
                return false;
            }
        }

        slotPaths = paths;
        return true;
    }

    private static bool TryFindProfile(
        string selector,
        [NotNullWhen(true)] out WorkbenchProfileSummary? profile)
    {
        string normalized = selector.Trim();
        profile = WorkbenchCompositionService.GetAbMergeProfileSummaries().FirstOrDefault(candidate =>
            string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                CliCompositionRunSupport.GetIcNumber(candidate.IcId),
                normalized,
                StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }

    private static async Task PrintResultAsync(
        WorkbenchRunResult result,
        string icId,
        TextWriter output,
        TextWriter error)
    {
        await output.WriteLineAsync($"Status: {result.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {result.ProfileId} ({icId})").ConfigureAwait(false);
        await output.WriteLineAsync("Experience: ab-merge").ConfigureAwait(false);
        await output.WriteLineAsync($"Output: {result.OutputFileName}").ConfigureAwait(false);
        await output.WriteLineAsync($"Size: {result.OutputSize.ToString(CultureInfo.InvariantCulture)} bytes").ConfigureAwait(false);
        await output.WriteLineAsync($"SHA256: {result.OutputSha256}").ConfigureAwait(false);
        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        using var report = JsonDocument.Parse(result.ReportJson);
        if (!report.RootElement.TryGetProperty("Issues", out JsonElement issues) ||
            issues.ValueKind != JsonValueKind.Array ||
            issues.GetArrayLength() == 0)
        {
            return;
        }

        await error.WriteLineAsync("Issues:").ConfigureAwait(false);
        foreach (JsonElement issue in issues.EnumerateArray())
        {
            string code = ReadString(issue, "Code", "unknown");
            string operationId = ReadString(issue, "OperationId", string.Empty);
            string message = ReadString(issue, "Message", "No message.");
            string operation = string.IsNullOrWhiteSpace(operationId) ? string.Empty : $" [{operationId}]";
            await error.WriteLineAsync($"  {code}{operation}: {message}").ConfigureAwait(false);
        }
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!
                : fallback;
    }

    private static async Task WriteUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge preview --profile <id|ic> --dp-ab <path> --tp-a <path> --tp-b <path> [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge build --profile <id|ic> --dp-ab <path> --tp-a <path> --tp-b <path> [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
    }
}
