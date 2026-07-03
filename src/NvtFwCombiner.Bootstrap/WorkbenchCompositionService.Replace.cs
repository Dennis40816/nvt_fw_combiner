using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunNt51950DpReplaceAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        CompositionProfileDefinition profile = GetDpReplaceProfile(icId);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            throw new InvalidOperationException(FormatIssues(compile.Issues));
        }

        InputArtifactBinding[] bindings =
        [
            CreateBinding("reference-base", "replace-base", slotPaths),
            CreateBinding("dp-replacement", "replace-dp", slotPaths),
        ];
        return await RunCompiledWorkbenchProfileAsync(
            "ui-replace",
            profile,
            compile.Plan!,
            bindings,
            build,
            outputPath,
            cancellationToken,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])).ConfigureAwait(false);
    }

    private static IReadOnlyList<WorkbenchMemoryMapRow> CreateDpReplaceRows(
        string icId,
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        if (TryGetDpReplaceProfile(icId, out CompositionProfileDefinition? profile))
        {
            ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
            return !compile.IsSuccess
                ?
                [
                    new WorkbenchMemoryMapRow(
                        "Profile",
                        "Profile",
                        "Blocked",
                        "No output",
                        FormatIssues(compile.Issues)),
                ]
                :
                [
                    .. compile.Plan!.OrderedOperations.Select(operation => new WorkbenchMemoryMapRow(
                        FormatDisplayRange(operation.TargetRange),
                        operation.Kind == CompositionOperationKind.ReplaceRange ? "Base flash" : "DP replacement",
                        DpReplaceActionLabel(operation),
                        operation.SourceSpaceId is null ? "No source" : AddressSpaceLabel(operation.SourceSpaceId),
                        $"Sequence {operation.Sequence}: {operation.Reason}")),
                ];
        }

        return
        [
            .. CreatePreserveRows(regions),
            .. GetDpReplaceRegions(icId, regions)
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base flash",
                    "Replace",
                    IsLdRegion(region) ? "LDC BIN" : "DP BIN",
                    $"{region.DisplayName}; source mapping is blocked until per-IC DP Replace evidence is approved.")),
        ];
    }

    private static IReadOnlyList<WorkbenchMemoryMapRow> CreateCtrlRamReplaceRows(
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
        [
            .. CreatePreserveRows(regions),
            .. regions
                .Where(region => region.Kind == TpFlashMapRegionKind.CtrlRam)
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base flash",
                    "Replace + CRC",
                    region.PostbuildFileName ?? "CtrlRAM BIN",
                    $"{region.DisplayName} at {FormatDisplayRange(region.Range)} can use its own replacement BIN; the report shows the CRC/header refresh command.")),
        ];
    }

    private static WorkbenchRunResult CreatePlanningRunResult(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string issueCode,
        string issueMessage)
    {
        return CreateReplaceReportRunResult(
            icId,
            replaceMode,
            slotPaths,
            build,
            CreateReplacePlanningOperations(icId, number, replaceMode),
            [new CompositionIssue(issueCode, issueMessage, replaceMode.ToLowerInvariant())],
            GetReplaceDefaultOutputFileName(icId, replaceMode),
            succeeded: false);
    }

    private static WorkbenchRunResult CreateReplaceReportRunResult(
        string icId,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        bool succeeded)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        string profileId = $"{icId.ToLowerInvariant()}-{replaceMode.ToLowerInvariant()}-replace-workbench";
        var report = new CompositionRunReport(
            $"ui-replace-{replaceMode.ToLowerInvariant()}-{(build ? "build" : "preview")}-{timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}",
            profileId,
            "0.5.0",
            icId,
            $"{replaceMode.ToLowerInvariant()}-replace",
            $"{replaceMode.ToLowerInvariant()}-replace",
            CompositionKind.Replace,
            timestamp,
            timestamp,
            CreateInputSummaries(slotPaths),
            operations,
            [],
            issues,
            new OutputArtifactSummary(outputFileName, 0, EmptySha256, committed: false));
        string reportJson = JsonSerializer.Serialize(report, ReportJsonOptions);
        return new WorkbenchRunResult(
            succeeded,
            succeeded ? "Succeeded" : "Blocked",
            profileId,
            0,
            EmptySha256,
            outputFileName,
            null,
            reportJson);
    }

    private static IReadOnlyList<OperationRunSummary> CreateReplacePlanningOperations(
        string icId,
        string number,
        string replaceMode)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        OperationRunStatus status = OperationRunStatus.Skipped;

        if (replaceMode == "DP" && TryGetDpReplaceProfile(icId, out CompositionProfileDefinition? profile))
        {
            ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
            return compile.IsSuccess
                ?
                [
                    .. compile.Plan!.OrderedOperations.Select(operation => new OperationRunSummary(
                        operation.OperationId,
                        operation.Sequence,
                        operation.Kind,
                        status,
                        operation.SourceSpaceId,
                        operation.SourceRange,
                        operation.TargetSpaceId,
                        operation.TargetRange,
                        operation.OverlapPolicy,
                        operation.ExternalProcessorInvocation?.ProcessorId,
                        operation.ExternalProcessorInvocation?.ToolBindingId,
                        operation.ExternalProcessorInvocation?.AllowedReadRanges ?? [],
                        operation.ExternalProcessorInvocation?.AllowedWriteRanges ?? [],
                        operation.Reason)),
                ]
                : [];
        }

        return replaceMode == "DP"
            ?
        [
            .. GetDpReplaceRegions(icId, regions).Select((region, index) => new OperationRunSummary(
                $"replace-{region.RegionId}",
                100 + (index * 10),
                CompositionOperationKind.ReplaceRange,
                status,
                IsLdRegion(region) ? "ldc-replacement" : "dp-replacement",
                new ByteRange(0, region.Range.Length),
                "output-image",
                region.Range,
                OverlapPolicy.ReplaceExisting,
                null,
                null,
                [],
                [],
                $"{region.DisplayName} awaits per-IC DP Replace source mapping evidence.")),
        ]
            : [];
    }

    private static List<InputArtifactSummary> CreateInputSummaries(
        IReadOnlyDictionary<string, string> slotPaths)
    {
        List<InputArtifactSummary> summaries = [];
        foreach ((string slotId, string path) in slotPaths.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                summaries.Add(new InputArtifactSummary(slotId, Path.GetFileName(path), 0, EmptySha256));
                continue;
            }

            FileInfo file = new(fullPath);
            summaries.Add(new InputArtifactSummary(slotId, Path.GetFileName(fullPath), file.Length, Sha256File(fullPath)));
        }

        return summaries;
    }

    private static List<WorkbenchReplaceInputSlot> GetDpReplaceInputSlots(string icId)
    {
        List<WorkbenchReplaceInputSlot> slots =
        [
            new(
                "replace-dp",
                "DP replacement BIN",
                TryGetDpReplaceProfile(icId, out CompositionProfileDefinition? profile)
                    ? $"Replacement DP container {FormatDisplayRange(new ByteRange(0, GetAddressSpaceLength(profile, "dp-replacement")))}; shorter files are padded before the original TP range is restored."
                    : "Replacement DP payload. Build stays gated until this IC has approved DP Replace mapping evidence.",
                false,
                "dp-replacement",
                "dp"),
        ];
        if (string.Equals(icId, "NT51928", StringComparison.Ordinal))
        {
            slots.Add(new WorkbenchReplaceInputSlot(
                "replace-ldc",
                "LDC replacement BIN",
                "NT51928-only LDC payload under DP Replace.",
                false,
                "ldc-replacement",
                "dp-ldc-51928"));
        }

        return slots;
    }

    private static IReadOnlyList<WorkbenchReplaceInputSlot> GetCtrlRamReplaceInputSlots(string icId, string number)
    {
        return
        [
            .. TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, ToIcNumberSelection(number))
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchReplaceInputSlot(
                    CtrlRamSlotId(region.RegionId),
                    region.DisplayName,
                    $"Replace this area only when needed. TP position {FormatDisplayRange(region.Range)}.",
                    true,
                    CtrlRamSlotId(region.RegionId),
                    region.RegionId)),
        ];
    }

    private static IEnumerable<TpFlashMapRegion> GetDpReplaceRegions(
        string icId,
        IEnumerable<TpFlashMapRegion> regions)
    {
        return regions
            .Where(region => region.Kind == TpFlashMapRegionKind.Dp)
            .Where(region => IsDpRegionVisibleForReplace(icId, region));
    }

    private static bool IsDpRegionVisibleForReplace(string icId, TpFlashMapRegion region)
    {
        return !IsLdRegion(region) || string.Equals(icId, "NT51928", StringComparison.Ordinal);
    }

    private static string CtrlRamSlotId(string regionId)
    {
        return $"replace-ctrlram-{regionId}";
    }

    private static bool IsNt51950Or51(string icId)
    {
        return TryGetDpReplaceProfile(icId, out _);
    }

    private static bool TryGetDpReplaceProfile(
        string icId,
        [NotNullWhen(true)] out CompositionProfileDefinition? profile)
    {
        return DpReplaceProfilesByIc.TryGetValue(icId, out profile);
    }

    private static CompositionProfileDefinition GetDpReplaceProfile(string icId)
    {
        return TryGetDpReplaceProfile(icId, out CompositionProfileDefinition? profile)
            ? profile
            : throw new InvalidOperationException($"DP Replace is not available for '{icId}'.");
    }

    private static long GetAddressSpaceLength(CompositionProfileDefinition profile, string addressSpaceId)
    {
        return profile.AddressSpaces
            .Single(space => string.Equals(space.AddressSpaceId, addressSpaceId, StringComparison.Ordinal))
            .Length;
    }

    private static string DpReplaceActionLabel(CompositionOperation operation)
    {
        return operation.OperationId switch
        {
            "restore-base-tp" => "Restore",
            "restore-base-customer-info" => "Preserve",
            _ => ActionLabel(operation.Kind),
        };
    }

    private static string DpReplaceCoverageLabel(CompositionOperation operation)
    {
        return operation.OperationId switch
        {
            "replace-dp-container" => "Changed DP BIN",
            "restore-base-tp" => "Restored TP",
            "restore-base-customer-info" => "Preserved customer info",
            _ => DpReplaceActionLabel(operation),
        };
    }

    private static IReadOnlyList<WorkbenchMemoryMapRow> CreatePreserveRows(
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
        [
            .. regions
                .Where(IsPreservedRegion)
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base flash",
                    "Preserve",
                    "Base flash",
                    $"{region.DisplayName} is intentionally not written by this workflow.")),
        ];
    }

    private static bool IsLdRegion(TpFlashMapRegion region)
    {
        return region.RegionId.Contains("ld", StringComparison.OrdinalIgnoreCase) ||
            region.DisplayName.Contains("LDC", StringComparison.OrdinalIgnoreCase);
    }

    private static InputArtifactBinding CreateBinding(
        string addressSpaceId,
        string slotId,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return slotPaths.TryGetValue(slotId, out string? path) && !string.IsNullOrWhiteSpace(path)
            ? new InputArtifactBinding(addressSpaceId, slotId, Path.GetFullPath(path))
            : throw new InvalidOperationException($"Input slot '{slotId}' is required.");
    }
}
