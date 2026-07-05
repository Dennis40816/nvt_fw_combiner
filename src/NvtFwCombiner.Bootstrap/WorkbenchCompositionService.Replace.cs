using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunNt51950DpReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        if (!TryGetNt51950DpReplaceCapacity(
                icId,
                number,
                slotPaths,
                build,
                out long capacity,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        CompositionProfileDefinition profile = CreateNt51950DpReplaceProfile(icId, capacity);
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
        string[] inputRoots = [
            .. bindings
                .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        (string outputDirectory, string outputFileName) = ResolveOutputTarget(
            bindings[0].ArtifactId,
            build,
            outputPath,
            profile.DefaultOutputFileName);
        FileArtifactReader reader = new(inputRoots);
        AtomicFileCompositionOutputWriter? writer = build
            ? new AtomicFileCompositionOutputWriter(outputDirectory, overwrite: true)
            : null;
        CompositionRunService service = new(reader, new SystemClock(), writer);
        CompositionRunRequest request = new(
            $"ui-replace-{(build ? "build" : "preview")}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}",
            ToRunProfile(profile),
            compile.Plan!,
            bindings,
            outputFileName,
            icNumberSelection: ToIcNumberSelection(number));

        CompositionRunResult result;
        if (!build)
        {
            result = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            CompositionRunResult preview = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
            result = preview.Status == CompositionExecutionStatus.Succeeded
                ? await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), cancellationToken)
                    .ConfigureAwait(false)
                : preview;
        }

        return ToWorkbenchRunResult(result);
    }

    private static IReadOnlyList<WorkbenchMemoryMapRow> CreateDpReplaceRows(
        string icId,
        IReadOnlyList<TpFlashMapRegion> regions,
        long? dpBaseLength)
    {
        return IsNt51950Or51(icId)
            ?
            [
                new WorkbenchMemoryMapRow(
                    FormatNt51950DpReplaceContainerLabel(dpBaseLength),
                    "Base flash",
                    "Replace",
                    "DP replacement",
                    DescribeNt51950DpReplaceContainer(dpBaseLength)),
                new WorkbenchMemoryMapRow(
                    FormatDisplayRange(Nt51950TpRestoreRange),
                    "DP replacement",
                    "Restore",
                    "Base TP",
                    $"Copy original TP FW at {FormatDisplayRange(Nt51950TpRestoreRange)} from the base firmware after DP replacement."),
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
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<TpFlashMapRegion> postbuildMappedRegions)
    {
        return
        [
            .. CreatePreserveRows(regions),
            .. postbuildMappedRegions
                .OrderBy(region => region.Range.Start)
                .Select(region => new WorkbenchMemoryMapRow(
                    FormatDisplayRange(region.Range),
                    "Base flash",
                    "Replace + CRC",
                    region.PostbuildFileName ?? "CtrlRAM BIN",
                    $"{region.DisplayName} at {FormatDisplayRange(region.Range)} can use its own replacement BIN; the report shows the CRC/header refresh command.")),
        ];
    }

    private static bool TryGetNt51950DpReplaceCapacity(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        out long capacity,
        out WorkbenchRunResult? failure)
    {
        capacity = 0;
        failure = null;
        if (!slotPaths.TryGetValue("replace-base", out string? basePath) ||
            string.IsNullOrWhiteSpace(basePath))
        {
            failure = CreatePlanningRunResult(
                icId,
                number,
                "DP",
                slotPaths,
                build,
                "ui.input.missing",
                "Base flash BIN is required before NT51950/NT51951 DP Replace can determine the DP base length.");
            return false;
        }

        string fullPath = Path.GetFullPath(basePath);
        if (!File.Exists(fullPath))
        {
            failure = CreatePlanningRunResult(
                icId,
                number,
                "DP",
                slotPaths,
                build,
                "input.artifact.read-failed",
                "Base flash BIN path does not exist.");
            return false;
        }

        long baseLength = new FileInfo(fullPath).Length;
        if (!IsSupportedNt51950DpBaseLength(baseLength))
        {
            failure = CreatePlanningRunResult(
                icId,
                number,
                "DP",
                slotPaths,
                build,
                "input.address-space.length-mismatch",
                $"{icId} DP Replace base flash BIN length must be one of {FormatSupportedNt51950DpBaseLengths()} (actual {FormatHexLength(baseLength)}).",
                baseLength);
            return false;
        }

        capacity = baseLength;
        return true;
    }

    private static CompositionProfileDefinition CreateNt51950DpReplaceProfile(string icId, long capacity)
    {
        var fullContainer = new ByteRange(0, capacity);
        string normalizedIc = icId.ToLowerInvariant();
        return new CompositionProfileDefinition(
            $"{normalizedIc}-dp-replace-dp-perspective",
            "0.5.0",
            icId,
            "dp-replace",
            CompositionKind.Replace,
            "dp-replace",
            $"{normalizedIc}-dp-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            [
                new AddressSpace("reference-base", capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("dp-replacement", capacity, AddressSpaceMutability.Immutable, inputPaddingByte: 0x00),
                new AddressSpace("output-image", capacity, AddressSpaceMutability.Mutable),
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
                    $"Replace the NT51950/NT51951 DP Perspective container using the selected base length {FormatHexLength(capacity)}."),
                CompositionOperation.CopyRange(
                    "restore-base-tp",
                    200,
                    "reference-base",
                    Nt51950TpRestoreRange,
                    "output-image",
                    Nt51950TpRestoreRange,
                    OverlapPolicy.ReplaceExisting,
                    $"Restore original TP FW at {FormatDisplayRange(Nt51950TpRestoreRange)} from the base firmware after DP replacement."),
            ],
            [
                new ProfileRegion(
                    "dp-perspective-container",
                    "output-image",
                    fullContainer,
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp", "tp-restore"]),
            ],
            [
                new RegionAccessRule(
                    "dp-perspective-container",
                    RegionAccessKind.Parts,
                    "NT51950/NT51951 DP Replace first copies replacement DP, then restores the original TP range."),
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
        string issueMessage,
        long? dpBaseLength = null)
    {
        return CreateReplaceReportRunResult(
            icId,
            replaceMode,
            slotPaths,
            build,
            CreateReplacePlanningOperations(icId, number, replaceMode, dpBaseLength),
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
        string replaceMode,
        long? dpBaseLength = null)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        OperationRunStatus status = OperationRunStatus.Skipped;

        if (replaceMode == "DP" && IsNt51950Or51(icId))
        {
            if (dpBaseLength is not long capacity || !IsSupportedNt51950DpBaseLength(capacity))
            {
                return [];
            }

            var fullContainer = new ByteRange(0, capacity);
            return
            [
                new OperationRunSummary(
                    "replace-dp-container",
                    100,
                    CompositionOperationKind.ReplaceRange,
                    status,
                    "dp-replacement",
                    fullContainer,
                    "output-image",
                    fullContainer,
                    OverlapPolicy.Reject,
                    null,
                    null,
                    [],
                    [],
                    "Replace the DP perspective container using the selected base length."),
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
            ];
        }

        return
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
                IsNt51950Or51(icId)
                    ? $"Replacement DP is padded to the selected base BIN length ({FormatSupportedNt51950DpBaseLengths()}); original TP range is restored from base."
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

    private static IReadOnlyList<WorkbenchReplaceInputSlot> GetCtrlRamReplaceInputSlots(
        string icId,
        string number,
        string? basePath)
    {
        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            icId,
            basePath,
            out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        return
        [
            .. TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, ToIcNumberSelection(number), postbuildProfile)
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

    private static bool IsSupportedNt51950DpBaseLength(long? length)
    {
        return length is long value && Nt51950SupportedDpBaseLengths.Contains(value);
    }

    private static string FormatSupportedNt51950DpBaseLengths()
    {
        return string.Join(" / ", Nt51950SupportedDpBaseLengths.Select(FormatHexLength));
    }

    private static string FormatHexLength(long length)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{length:X}");
    }

    private static string FormatNt51950DpReplaceContainerLabel(long? length)
    {
        return length is not long value
            ? $"Base BIN length: {FormatSupportedNt51950DpBaseLengths()}"
            : IsSupportedNt51950DpBaseLength(value)
            ? FormatDisplayRange(new ByteRange(0, value))
            : $"Unsupported base BIN length {FormatHexLength(value)}";
    }

    private static string DescribeNt51950DpReplaceContainer(long? length)
    {
        return length is not long value
            ? $"NT51950/NT51951 DP Replace uses the selected base BIN length; supported lengths are {FormatSupportedNt51950DpBaseLengths()}."
            : IsSupportedNt51950DpBaseLength(value)
            ? $"Replacement DP initializes the selected base length {FormatHexLength(value)}; shorter files are padded by profile policy."
            : $"This base BIN length is not approved for NT51950/NT51951 DP Replace; use {FormatSupportedNt51950DpBaseLengths()}.";
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
