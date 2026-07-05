using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchCtrlRamReplaceAsync(
        string action,
        string profileSelector,
        ParsedOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryResolveWorkbenchCtrlRamIc(profileSelector, out string? icId))
        {
            return await UnknownReplaceProfileAsync("ctrlram-replace", profileSelector, error).ConfigureAwait(false);
        }

        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath))
        {
            return UsageError;
        }

        if (!TryCreateWorkbenchCtrlRamSlotPaths(
                icId,
                icNumber,
                basePath,
                options,
                error,
                out Dictionary<string, string>? slotPaths))
        {
            return UsageError;
        }

        InputArtifactBinding[] bindings = CreateWorkbenchBindings(slotPaths);
        OutputTarget outputTarget = ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            WorkbenchCompositionService.GetReplaceDefaultOutputFileName(icId, "CtrlRAM"));
        string? outputPath = action == "build" ? outputTarget.FullPath : null;
        if (action == "build")
        {
            EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
            if (!options.Flags.Contains("--overwrite") && File.Exists(outputTarget.FullPath))
            {
                await error.WriteLineAsync(
                        $"error: output file already exists: {outputTarget.FullPath}; pass --overwrite to replace it.")
                    .ConfigureAwait(false);
                return SoftwareError;
            }
        }

        EnsureReportDoesNotAliasProtectedPaths(options, bindings, outputTarget, action == "build");

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunReplaceAsync(icId, icNumber, "CtrlRAM", slotPaths, action == "build", cancellationToken, outputPath)
            .ConfigureAwait(false);
        await WriteWorkbenchReportFileIfRequestedAsync(
                result,
                options,
                bindings,
                action == "build" ? outputTarget.FullPath : null,
                output,
                cancellationToken)
            .ConfigureAwait(false);
        await PrintWorkbenchRunResultAsync(result, icId, output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static bool TryResolveWorkbenchCtrlRamIc(
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        icId = WorkbenchCompositionService.GetSupportedIcIds().FirstOrDefault(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetIcNumber(candidate), normalized, StringComparison.OrdinalIgnoreCase));
        return icId is not null;
    }

    private static bool TryCreateWorkbenchCtrlRamSlotPaths(
        string icId,
        string icNumber,
        string basePath,
        ParsedOptions options,
        TextWriter error,
        [NotNullWhen(true)] out Dictionary<string, string>? slotPaths)
    {
        slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["replace-base"] = Path.GetFullPath(basePath),
        };
        List<string> ctrlRamValues = options.GetValues("--ctrlram");
        if (ctrlRamValues.Count == 0)
        {
            error.WriteLine("error: at least one --ctrlram <slot-id=path> value is required for real IC CtrlRAM Replace");
            slotPaths = null;
            return false;
        }

        Dictionary<string, WorkbenchReplaceInputSlot> slotsByToken = CreateCtrlRamSlotLookup(icId, icNumber, basePath);
        if (slotsByToken.Count == 0)
        {
            error.WriteLine($"error: no CtrlRAM replacement slots are available for {icId} / {icNumber}");
            slotPaths = null;
            return false;
        }

        foreach (string value in ctrlRamValues)
        {
            int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                error.WriteLine(
                    "error: real IC CtrlRAM Replace expects --ctrlram <slot-id=path>; example: --ctrlram replace-ctrlram-vn-master=C:\\path\\vn.bin");
                slotPaths = null;
                return false;
            }

            string token = value[..separatorIndex].Trim();
            string path = value[(separatorIndex + 1)..].Trim();
            if (!slotsByToken.TryGetValue(token, out WorkbenchReplaceInputSlot? slot))
            {
                error.WriteLine($"error: unknown CtrlRAM slot '{token}' for {icId} / {icNumber}");
                error.WriteLine($"available slots: {FormatAvailableSlotIds(slotsByToken)}");
                slotPaths = null;
                return false;
            }

            if (!slotPaths.TryAdd(slot.SlotId, Path.GetFullPath(path)))
            {
                error.WriteLine($"error: duplicate CtrlRAM slot '{slot.SlotId}'");
                slotPaths = null;
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, WorkbenchReplaceInputSlot> CreateCtrlRamSlotLookup(
        string icId,
        string icNumber,
        string basePath)
    {
        Dictionary<string, WorkbenchReplaceInputSlot> slotsByToken = new(StringComparer.OrdinalIgnoreCase);
        foreach (WorkbenchReplaceInputSlot slot in WorkbenchCompositionService.GetReplaceInputSlots(
                     icId,
                     icNumber,
                     "CtrlRAM",
                     basePath))
        {
            slotsByToken[slot.SlotId] = slot;
            if (!string.IsNullOrWhiteSpace(slot.RegionId))
            {
                slotsByToken[slot.RegionId] = slot;
            }
        }

        return slotsByToken;
    }

    private static string FormatAvailableSlotIds(Dictionary<string, WorkbenchReplaceInputSlot> slotsByToken)
    {
        return string.Join(
            ", ",
            slotsByToken.Values
                .Select(slot => slot.SlotId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
    }

    private static InputArtifactBinding[] CreateWorkbenchBindings(IReadOnlyDictionary<string, string> slotPaths)
    {
        return [
            .. slotPaths
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new InputArtifactBinding(
                    pair.Key == "replace-base" ? "reference-base" : pair.Key,
                    pair.Key,
                    pair.Value)),
        ];
    }

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
        TextWriter output,
        TextWriter error)
    {
        await output.WriteLineAsync($"Status: {result.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {result.ProfileId} ({icId})").ConfigureAwait(false);
        await output.WriteLineAsync("Experience: ctrlram-replace").ConfigureAwait(false);
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
