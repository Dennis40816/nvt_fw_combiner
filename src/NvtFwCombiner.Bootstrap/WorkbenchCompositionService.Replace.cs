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
        CompositionProfileDefinition profile = CreateNt51950DpReplaceProfile(icId);
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
        return IsNt51950Or51(icId)
            ?
            [
                new WorkbenchMemoryMapRow(
                    FormatDisplayRange(new ByteRange(0, Nt51950DpContainerLength)),
                    "Base flash",
                    "Replace",
                    "DP replacement",
                    "Replacement DP initializes the 0x100000 work container; shorter files are padded by profile policy."),
                new WorkbenchMemoryMapRow(
                    FormatDisplayRange(Nt51950TpRestoreRange),
                    "DP replacement",
                    "Restore",
                    "Base TP",
                    $"Copy original TP FW at {FormatDisplayRange(Nt51950TpRestoreRange)} from the base firmware after DP replacement."),
                new WorkbenchMemoryMapRow(
                    FormatDisplayRange(Nt51950CustomerInfoPreserveRange),
                    "DP replacement",
                    "Preserve",
                    "Base customer info",
                    $"Copy customer information at {FormatDisplayRange(Nt51950CustomerInfoPreserveRange)} from the base firmware after DP replacement."),
            ]
            :
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

    private static CompositionProfileDefinition CreateNt51950DpReplaceProfile(string icId)
    {
        var fullContainer = new ByteRange(0, Nt51950DpContainerLength);
        string normalizedIc = icId.ToLowerInvariant();
        return new CompositionProfileDefinition(
            $"{normalizedIc}-dp-replace-dp-perspective",
            "0.5.0",
            icId,
            "dp-replace",
            CompositionKind.Replace,
            "dp-replace",
            $"{normalizedIc}-dp-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", Nt51950DpContainerLength),
            [
                new AddressSpace("reference-base", Nt51950DpContainerLength, AddressSpaceMutability.Immutable),
                new AddressSpace("dp-replacement", Nt51950DpContainerLength, AddressSpaceMutability.Immutable, inputPaddingByte: 0x00),
                new AddressSpace("output-image", Nt51950DpContainerLength, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-dp-container",
                    100,
                    "dp-replacement",
                    fullContainer,
                    "output-image",
                    fullContainer,
                    OverlapPolicy.Reject,
                    "Replace the NT51950/NT51951 DP Perspective container after padding to 0x100000."),
                CompositionOperation.CopyRange(
                    "restore-base-tp",
                    200,
                    "reference-base",
                    Nt51950TpRestoreRange,
                    "output-image",
                    Nt51950TpRestoreRange,
                    OverlapPolicy.ReplaceExisting,
                    $"Restore original TP FW at {FormatDisplayRange(Nt51950TpRestoreRange)} from the base firmware after DP replacement."),
                CompositionOperation.CopyRange(
                    "restore-base-customer-info",
                    210,
                    "reference-base",
                    Nt51950CustomerInfoPreserveRange,
                    "output-image",
                    Nt51950CustomerInfoPreserveRange,
                    OverlapPolicy.ReplaceExisting,
                    $"Restore customer information at {FormatDisplayRange(Nt51950CustomerInfoPreserveRange)} from the base firmware after DP replacement."),
            ],
            [
                new ProfileRegion(
                    "dp-perspective-container",
                    "output-image",
                    fullContainer,
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp", "tp-restore", "customer-info-preserve"]),
            ],
            [
                new RegionAccessRule(
                    "dp-perspective-container",
                    RegionAccessKind.Parts,
                    "NT51950/NT51951 DP Replace first copies replacement DP, then restores the original TP and customer-info ranges."),
            ],
            IcNumberInputMode.SingleSelector);
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

        return replaceMode == "DP" && IsNt51950Or51(icId)
            ?
            [
                new OperationRunSummary(
                    "replace-dp-container",
                    100,
                    CompositionOperationKind.ReplaceRange,
                    status,
                    "dp-replacement",
                    new ByteRange(0, Nt51950DpContainerLength),
                    "output-image",
                    new ByteRange(0, Nt51950DpContainerLength),
                    OverlapPolicy.Reject,
                    null,
                    null,
                    [],
                    [],
                    "Replace the DP perspective container."),
                new OperationRunSummary(
                    "restore-base-tp",
                    200,
                    CompositionOperationKind.CopyRange,
                    status,
                    "reference-base",
                    Nt51950TpRestoreRange,
                    "output-image",
                    Nt51950TpRestoreRange,
                    OverlapPolicy.ReplaceExisting,
                    null,
                    null,
                    [],
                    [],
                    "Restore original TP FW from the base firmware."),
                new OperationRunSummary(
                    "restore-base-customer-info",
                    210,
                    CompositionOperationKind.CopyRange,
                    status,
                    "reference-base",
                    Nt51950CustomerInfoPreserveRange,
                    "output-image",
                    Nt51950CustomerInfoPreserveRange,
                    OverlapPolicy.ReplaceExisting,
                    null,
                    null,
                    [],
                    [],
                    "Restore customer information from the base firmware."),
            ]
            :
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
        ];
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
                summaries.Add(new InputArtifactSummary(slotId, Path.GetFileName(path), 0, string.Empty));
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
                IsNt51950Or51(icId)
                    ? $"Replacement DP container {FormatDisplayRange(new ByteRange(0, Nt51950DpContainerLength))}; shorter files are padded before the original TP range is restored."
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
        return icId is "NT51950" or "NT51951";
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
