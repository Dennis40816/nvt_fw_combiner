using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static class MergeCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    private const int SoftwareError = 70;
    private const string GeneralMergeModeId = "general-merge";

    internal static async Task<int> RunAsync(
        string command,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (command != "general-merge")
        {
            await error.WriteLineAsync($"error: unknown merge command '{command}'").ConfigureAwait(false);
            return UsageError;
        }

        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown general-merge command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryParseOptions(args[1..], action == "build", error, out ParsedOptions options))
        {
            return UsageError;
        }

        if (!RequireOption(options, "--profile", error, out string? profileSelector) ||
            !RequireOption(options, "--size", error, out string? outputLength))
        {
            return UsageError;
        }

        if (!TryResolveIc(profileSelector, out string? icId))
        {
            await error.WriteLineAsync($"error: General Merge profile '{profileSelector}' is not available").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryCreateMappings(options, icId, error, out WorkbenchGeneralMergeMappingInput[]? mappings))
        {
            return UsageError;
        }

        OutputTarget outputTarget = ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            WorkbenchCompositionService.GetGeneralMergeDefaultOutputFileName(icId));
        string? outputPath = action == "build" ? outputTarget.FullPath : null;
        List<ProtectedPathGuard.ProtectedPath> protectedPaths =
        [
            .. mappings.Select(mapping => new ProtectedPathGuard.ProtectedPath(
                Path.GetFullPath(mapping.FilePath),
                $"input mapping '{mapping.MappingId}'")),
        ];
        if (options.Values.TryGetValue("--rule", out string? savedRulePath))
        {
            protectedPaths.Add(new ProtectedPathGuard.ProtectedPath(
                Path.GetFullPath(savedRulePath),
                "saved-rule input"));
        }

        if (action == "build")
        {
            ProtectedPathGuard.EnsureDoesNotAlias(
                outputTarget.FullPath,
                "Output path",
                protectedPaths,
                "--output");
            if (!options.Flags.Contains("--overwrite") && File.Exists(outputTarget.FullPath))
            {
                await error.WriteLineAsync(
                        $"error: output file already exists: {outputTarget.FullPath}; pass --overwrite to replace it.")
                    .ConfigureAwait(false);
                return SoftwareError;
            }
        }

        if (options.Values.TryGetValue("--report", out string? reportPath))
        {
            ProtectedPathGuard.EnsureDoesNotAlias(
                reportPath,
                "Report path",
                action == "build"
                    ? [.. protectedPaths, new ProtectedPathGuard.ProtectedPath(outputTarget.FullPath, "built firmware output")]
                    : protectedPaths,
                "--report");
        }

        WorkbenchRunResult result = await WorkbenchCompositionService.RunGeneralMergeAsync(
                icId,
                outputLength,
                mappings,
                action == "build",
                cancellationToken,
                outputPath,
                overwrite: options.Flags.Contains("--overwrite"))
            .ConfigureAwait(false);
        bool reportWritten = options.Values.TryGetValue("--report", out string? requestedReportPath);
        if (reportWritten)
        {
            await WriteReportAsync(requestedReportPath!, result.ReportJson, output, cancellationToken)
                .ConfigureAwait(false);
        }

        await PrintResultAsync(result, icId, output, error, reportWritten).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static bool TryCreateMappings(
        ParsedOptions options,
        string icId,
        TextWriter error,
        [NotNullWhen(true)] out WorkbenchGeneralMergeMappingInput[]? mappings)
    {
        mappings = null;
        List<string> values = options.GetValues("--mapping");
        bool usesRule = options.Values.TryGetValue("--rule", out string? rulePath);
        if (usesRule)
        {
            if (values.Count > 0)
            {
                error.WriteLine("error: --rule cannot be combined with manual --mapping values");
                return false;
            }

            return TryCreateMappingsFromSavedRule(rulePath!, options.GetValues("--slot"), icId, error, out mappings);
        }

        if (options.GetValues("--slot").Count > 0)
        {
            error.WriteLine("error: --slot can be used only with --rule");
            return false;
        }

        if (values.Count == 0)
        {
            error.WriteLine("error: at least one --mapping <source-start+target-start+length=path> value or --rule <rule.json> is required for General Merge");
            return false;
        }

        List<WorkbenchGeneralMergeMappingInput> items = [];
        for (int index = 0; index < values.Count; index++)
        {
            if (!TryParseMappingValue(values[index], index + 1, error, out WorkbenchGeneralMergeMappingInput? mapping))
            {
                return false;
            }

            items.Add(mapping);
        }

        mappings = [.. items];
        return true;
    }

    private static bool TryCreateMappingsFromSavedRule(
        string rulePath,
        IReadOnlyList<string> slotValues,
        string icId,
        TextWriter error,
        [NotNullWhen(true)] out WorkbenchGeneralMergeMappingInput[]? mappings)
    {
        mappings = null;
        SavedCompositionRuleLoadResult load = SavedCompositionRuleLoader.Load(rulePath);
        if (!load.IsValid)
        {
            PrintSavedRuleIssues(load.Issues, error);
            return false;
        }

        SavedCompositionRule rule = load.Rule!;
        if (!string.Equals(rule.CompositionKind, "merge", StringComparison.Ordinal) ||
            !string.Equals(rule.SourceExperience, GeneralMergeModeId, StringComparison.Ordinal))
        {
            error.WriteLine($"error: saved rule '{rule.RuleId}' is for {rule.CompositionKind} / {rule.SourceExperience}, not merge / {GeneralMergeModeId}");
            return false;
        }

        string profileId = WorkbenchCompositionService.GetGeneralMergeWorkbenchProfileId(icId);
        if (!MatchesCompatibility(rule.Compatibility.IcIds, icId, StringComparer.OrdinalIgnoreCase) ||
            !MatchesCompatibility(rule.Compatibility.ProfileIds, profileId, StringComparer.Ordinal) ||
            !MatchesCompatibility(rule.Compatibility.ModeIds, GeneralMergeModeId, StringComparer.Ordinal))
        {
            error.WriteLine($"error: saved rule '{rule.RuleId}' is not compatible with {icId} / {profileId} / {GeneralMergeModeId}");
            return false;
        }

        if (rule.ProcessorDependencyIds.Count > 0)
        {
            error.WriteLine($"error: saved rule '{rule.RuleId}' requires processors that General Merge saved-rule CLI consumption does not support yet");
            return false;
        }

        if (!TryCreateSlotBindings(slotValues, error, out Dictionary<string, string>? slotsById))
        {
            return false;
        }

        var operationIdsByRowId = rule.OperationFragments
            .SelectMany(fragment => fragment.MappingRowIds.Select(rowId => new KeyValuePair<string, string>(
                rowId,
                fragment.OperationId)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        List<WorkbenchGeneralMergeMappingInput> items = [];
        foreach (SavedRuleMappingRow row in rule.MappingRows)
        {
            if (!string.Equals(row.TargetAddressSpaceId, "output-image", StringComparison.Ordinal))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' targets unsupported address space '{row.TargetAddressSpaceId}'");
                return false;
            }

            if (!string.Equals(row.OverlapPolicy, "reject", StringComparison.Ordinal))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' uses unsupported overlap policy '{row.OverlapPolicy}'");
                return false;
            }

            if (row.SourceRange is not { } sourceRange)
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' has no sourceRange for General Merge");
                return false;
            }

            if (!slotsById.TryGetValue(row.SourceReference, out string? sourcePath))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' requires --slot {row.SourceReference}=<path>");
                return false;
            }

            if (!operationIdsByRowId.TryGetValue(row.RowId, out string? operationId))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' is not linked to a reviewed operation fragment");
                return false;
            }

            items.Add(new WorkbenchGeneralMergeMappingInput(
                operationId,
                sourcePath,
                FormatHex(sourceRange.Start),
                FormatHex(row.TargetRange.Start),
                FormatHex(row.TargetRange.Length),
                row.Alignment,
                row.Reason,
                OperationProvenance.SavedRule(rule.RuleId, rule.RuleVersion)));
        }

        mappings = [.. items];
        return true;
    }

    private static bool TryCreateSlotBindings(
        IReadOnlyList<string> slotValues,
        TextWriter error,
        [NotNullWhen(true)] out Dictionary<string, string>? slotsById)
    {
        slotsById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string value in slotValues)
        {
            int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                error.WriteLine("error: --slot must use <slot-id=path>");
                return false;
            }

            string slotId = value[..separatorIndex].Trim();
            string path = value[(separatorIndex + 1)..].Trim();
            if (slotId.Length == 0 || path.Length == 0)
            {
                error.WriteLine("error: --slot must use non-empty slot id and path");
                return false;
            }

            if (!slotsById.TryAdd(slotId, Path.GetFullPath(path)))
            {
                error.WriteLine($"error: duplicate --slot binding for '{slotId}'");
                return false;
            }
        }

        return true;
    }

    private static bool MatchesCompatibility(
        IReadOnlyList<string> values,
        string candidate,
        IEqualityComparer<string> comparer)
    {
        return values.Count == 0 || values.Contains(candidate, comparer);
    }

    private static void PrintSavedRuleIssues(IReadOnlyList<SavedRuleValidationIssue> issues, TextWriter error)
    {
        error.WriteLine("error: saved rule validation failed");
        foreach (SavedRuleValidationIssue issue in issues)
        {
            error.WriteLine($"  {issue.Code} at {issue.Path}: {issue.Message}");
        }
    }

    private static bool TryParseMappingValue(
        string value,
        int index,
        TextWriter error,
        [NotNullWhen(true)] out WorkbenchGeneralMergeMappingInput? mapping)
    {
        mapping = null;
        int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            error.WriteLine(
                "error: General Merge expects --mapping <source-start+target-start+length=path>; example: --mapping 0x0+0x100+0x20=C:\\path\\source.bin");
            return false;
        }

        string rangeText = value[..separatorIndex].Trim();
        string path = value[(separatorIndex + 1)..].Trim();
        if (path.Length == 0)
        {
            error.WriteLine("error: --mapping path must not be empty");
            return false;
        }

        string[] parts = rangeText.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !TryParseNonNegativeLong(parts[0], out long sourceStart) ||
            !TryParseNonNegativeLong(parts[1], out long targetStart) ||
            !TryParseNonNegativeLong(parts[2], out long length) ||
            length <= 0)
        {
            error.WriteLine("error: --mapping must use non-negative source start, non-negative target start, and positive length");
            return false;
        }

        mapping = new WorkbenchGeneralMergeMappingInput(
            string.Create(CultureInfo.InvariantCulture, $"general-merge-map-{index}"),
            Path.GetFullPath(path),
            FormatHex(sourceStart),
            FormatHex(targetStart),
            FormatHex(length));
        return true;
    }

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
            string source = GetJsonString(issue, "Source", "general-merge");
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

    private static async Task WriteReportAsync(
        string reportPath,
        string reportJson,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(reportPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, reportJson, cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync($"Report: {fullPath}").ConfigureAwait(false);
    }

    private static bool RequireOption(
        ParsedOptions options,
        string option,
        TextWriter error,
        [NotNullWhen(true)] out string? value)
    {
        if (options.Values.TryGetValue(option, out value) && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        error.WriteLine($"error: {option} is required");
        value = null;
        return false;
    }

    private static bool TryResolveIc(string selector, [NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        icId = WorkbenchCompositionService.GetSupportedIcIds().FirstOrDefault(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Replace("NT", string.Empty, StringComparison.Ordinal), normalized, StringComparison.OrdinalIgnoreCase));
        return icId is not null;
    }

    private static OutputTarget ResolveOutputTarget(string? requestedOutput, string defaultFileName)
    {
        string fullPath = string.IsNullOrWhiteSpace(requestedOutput)
            ? Path.GetFullPath(defaultFileName)
            : Path.GetFullPath(requestedOutput);
        string? directory = Path.GetDirectoryName(fullPath);
        string fileName = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("Output path must include a directory and file name.", nameof(requestedOutput))
            : new OutputTarget(fullPath, directory, fileName);
    }

    private static bool TryParseOptions(
        string[] args,
        bool build,
        TextWriter error,
        out ParsedOptions options)
    {
        options = new ParsedOptions(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, List<string>>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
        string[] valueOptions = build
            ? ["--profile", "--size", "--mapping", "--rule", "--slot", "--output", "--report"]
            : ["--profile", "--size", "--mapping", "--rule", "--slot", "--report"];
        string[] repeatableOptions = ["--mapping", "--slot"];
        string[] flags = build ? ["--overwrite"] : [];
        for (int index = 0; index < args.Length; index++)
        {
            string token = args[index];
            if (flags.Contains(token, StringComparer.Ordinal))
            {
                _ = options.Flags.Add(token);
                continue;
            }

            if (!valueOptions.Contains(token, StringComparer.Ordinal))
            {
                error.WriteLine($"error: unknown option '{token}'");
                return false;
            }

            if (index + 1 >= args.Length)
            {
                error.WriteLine($"error: option '{token}' expects a value");
                return false;
            }

            string value = args[++index];
            if (repeatableOptions.Contains(token, StringComparer.Ordinal))
            {
                if (!options.RepeatedValues.TryGetValue(token, out List<string>? values))
                {
                    values = [];
                    options.RepeatedValues[token] = values;
                }

                values.Add(value);
                continue;
            }

            options.Values[token] = value;
        }

        return true;
    }

    private static bool TryParseNonNegativeLong(string text, out long value)
    {
        value = 0;
        string trimmed = text.Trim();
        bool parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        return parsed && value >= 0;
    }

    private static string FormatHex(long value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{value:X}");
    }

    private static Task WriteUsageAsync(TextWriter output)
    {
        return output.WriteLineAsync(
            "usage: nvt_fw_combiner general-merge preview --profile <ic> --size <length> --mapping <source-start+target-start+length=path> [--mapping ...] [--report <path>]\n" +
            "       nvt_fw_combiner general-merge preview --profile <ic> --size <length> --rule <rule.json> --slot <slot-id=path> [--slot ...] [--report <path>]\n" +
            "       nvt_fw_combiner general-merge build --profile <ic> --size <length> --mapping <source-start+target-start+length=path> [--mapping ...] [--output <path>] [--report <path>] [--overwrite]\n" +
            "       nvt_fw_combiner general-merge build --profile <ic> --size <length> --rule <rule.json> --slot <slot-id=path> [--slot ...] [--output <path>] [--report <path>] [--overwrite]");
    }

    private sealed record OutputTarget(string FullPath, string Directory, string FileName);

    private sealed record ParsedOptions(
        Dictionary<string, string> Values,
        Dictionary<string, List<string>> RepeatedValues,
        HashSet<string> Flags)
    {
        public List<string> GetValues(string option)
        {
            return RepeatedValues.TryGetValue(option, out List<string>? values)
                ? values
                : Values.TryGetValue(option, out string? value) ? [value] : [];
        }
    }
}
