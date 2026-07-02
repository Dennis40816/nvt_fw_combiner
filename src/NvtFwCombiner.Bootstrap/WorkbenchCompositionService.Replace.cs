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
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

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
                    $"{region.DisplayName} at {FormatDisplayRange(region.Range)} can receive its own replacement BIN before combiner.exe postbuild refreshes CRC/header.")),
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

    private static WorkbenchRunResult CreateCtrlRamPlanningRunResult(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection);
        if (regions.Count == 0)
        {
            return CreatePlanningRunResult(
                icId,
                number,
                "CtrlRAM",
                slotPaths,
                build,
                "replace.ctrlram.no-mapped-region",
                $"No postbuild-mapped CtrlRAM region is available for {icId} / {number}.");
        }

        bool hasRegionInput = regions.Any(region => slotPaths.ContainsKey(CtrlRamSlotId(region.RegionId)));
        List<CompositionIssue> issues = [];
        if (!slotPaths.ContainsKey("replace-base"))
        {
            issues.Add(new CompositionIssue(
                "ui.input.missing",
                "Base flash BIN is required before CtrlRAM Replace preview/build can run.",
                "replace-base"));
        }

        if (!hasRegionInput)
        {
            issues.Add(new CompositionIssue(
                "replace.ctrlram.no-region-input",
                "Select at least one CtrlRAM replacement BIN.",
                "ctrlram-replace"));
        }

        if (build)
        {
            issues.Add(new CompositionIssue(
                "replace.ctrlram.production-output-gated",
                "CtrlRAM Replace build is held until owner-approved postbuild write ranges and golden outputs are available.",
                "postbuild"));
        }

        return CreateReplaceReportRunResult(
            icId,
            "CtrlRAM",
            slotPaths,
            build,
            CreateCtrlRamPlanningOperations(icId, selection, regions, slotPaths, issues.Count == 0),
            issues,
            outputFileName: GetReplaceDefaultOutputFileName(icId, "CtrlRAM"),
            succeeded: !build && issues.Count == 0);
    }

    private static List<OperationRunSummary> CreateCtrlRamPlanningOperations(
        string icId,
        IcNumberSelection selection,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyDictionary<string, string> slotPaths,
        bool runnablePreview)
    {
        OperationRunStatus status = runnablePreview ? OperationRunStatus.Succeeded : OperationRunStatus.Skipped;
        List<OperationRunSummary> operations = [];
        long capacity = Math.Max(1, TpFlashMapCatalog.GetRegions(icId, selection).Max(region => region.Range.EndExclusive));
        int sequence = 100;
        foreach (TpFlashMapRegion region in regions.OrderBy(region => region.Range.Start))
        {
            string slotId = CtrlRamSlotId(region.RegionId);
            operations.Add(new OperationRunSummary(
                $"split-base-{region.RegionId}",
                sequence,
                CompositionOperationKind.CopyRange,
                status,
                "reference-base",
                region.Range,
                region.PostbuildFileName ?? $"staged-{region.RegionId}",
                new ByteRange(0, region.Range.Length),
                OverlapPolicy.ReplaceExisting,
                null,
                null,
                [],
                [],
                $"Split original {region.DisplayName} from base flash for postbuild staging."));
            sequence += 10;

            if (slotPaths.ContainsKey(slotId))
            {
                operations.Add(new OperationRunSummary(
                    $"replace-{region.RegionId}",
                    sequence,
                    CompositionOperationKind.ReplaceRange,
                    status,
                    slotId,
                    new ByteRange(0, region.Range.Length),
                    "output-image",
                    region.Range,
                    OverlapPolicy.ReplaceExisting,
                    null,
                    null,
                    [],
                    [],
                    $"Replace {region.DisplayName} at {FormatDisplayRange(region.Range)} with the selected BIN; oversized inputs are expected to truncate only by profile policy."));
                sequence += 10;
            }
        }

        if (LegacyCombinerPostbuildCatalog.All.FirstOrDefault(profile =>
                string.Equals(profile.IcId, icId, StringComparison.Ordinal)) is not { } postbuildProfile)
        {
            return operations;
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection);
        string firmwarePath = Path.Combine("output", postbuildProfile.FirmwareFileName);
        string binDirectory = "BIN";
        foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
        {
            IReadOnlyList<string> args = LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(
                command,
                firmwarePath,
                binDirectory);
            operations.Add(new OperationRunSummary(
                $"postbuild-{command.CommandId}",
                sequence,
                CompositionOperationKind.RunExternalProcessor,
                status,
                null,
                null,
                "output-image",
                new ByteRange(0, capacity),
                OverlapPolicy.ReplaceExisting,
                postbuildProfile.ProcessorId,
                postbuildProfile.ToolBindingId,
                [new ByteRange(0, capacity)],
                [new ByteRange(0, capacity)],
                $"Generated {plan.Branch} Combiner command: Combiner.exe {string.Join(' ', args)}."));
            sequence += 10;
        }

        return operations;
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
                    $"TP {FormatDisplayRange(region.Range)} -> {region.PostbuildFileName ?? "postbuild BIN"}",
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
