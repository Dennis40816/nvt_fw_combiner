using System.Globalization;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunDpPerspectiveDpReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        if (!TryCreateDpPerspectiveDpReplaceRunContext(
                icId,
                number,
                slotPaths,
                build,
                out DpPerspectiveDpReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        if (!TryCompileDpPerspectiveDpReplace(
                icId,
                context!.Capacity,
                out CompiledComposition? compiledComposition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            return CreatePlanningRunResult(
                icId,
                number,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.ReplaceDpProfilePending,
                $"No supported V2 DP Replace profile is registered for {icId}.",
                context.Capacity);
        }

        if (compiledComposition is null)
        {
            CompositionIssue issue = issues.Count > 0
                ? issues[0]
                : new CompositionIssue(
                    BuiltInV2CompilationFailed,
                    $"The built-in V2 DP Replace profile for {icId} did not produce an executable composition.");
            return CreatePlanningRunResult(
                icId,
                number,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                issue.Code,
                issue.Message,
                context.Capacity);
        }

        InputArtifactBinding[] bindings =
        [
            CompiledCompositionInputBindingFactory.Create(
                compiledComposition,
                CompositionAddressSpaceIds.ReferenceBase,
                context.BasePath),
            CreateDpReplacementBinding(compiledComposition, context.SlotPaths),
        ];
        return await RunCompiledCompositionAsync(
            DpReplaceRunIdPrefix,
            compiledComposition,
            bindings,
            context.BasePath,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: context.Selection,
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
    }

    private static InputArtifactBinding CreateDpReplacementBinding(
        CompiledComposition compiledComposition,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return slotPaths.TryGetValue(WorkbenchSlotIds.ReplaceDp, out string? path) && !string.IsNullOrWhiteSpace(path)
            ? CompiledCompositionInputBindingFactory.Create(
                compiledComposition,
                CompositionAddressSpaceIds.DpReplacement,
                Path.GetFullPath(path))
            : throw new InvalidOperationException($"Input slot '{WorkbenchSlotIds.ReplaceDp}' is required.");
    }

    private static IReadOnlyList<WorkbenchMemoryMapRow> CreateDpReplaceRows(
        string icId,
        IReadOnlyList<TpFlashMapRegion> regions,
        long? dpBaseLength)
    {
        return IsDpPerspectiveIc(icId)
            ?
            [
                new WorkbenchMemoryMapRow(
                    FormatDpPerspectiveDpReplaceContainerLabel(dpBaseLength),
                    "Base flash",
                    "Replace",
                    "DP replacement",
                    DescribeDpPerspectiveDpReplaceContainer(dpBaseLength)),
                new WorkbenchMemoryMapRow(
                    FormatDisplayRange(DpPerspectiveCatalog.TpOverlayRange),
                    "DP replacement",
                    "Restore",
                    "Base TP",
                    $"Copy original TP FW at {FormatDisplayRange(DpPerspectiveCatalog.TpOverlayRange)} from the base firmware after DP replacement."),
            ]
            :
        [
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

    private static List<WorkbenchReplaceInputSlot> GetDpReplaceInputSlots(string icId)
    {
        List<WorkbenchReplaceInputSlot> slots =
        [
            new(
                WorkbenchSlotIds.ReplaceDp,
                "DP replacement BIN",
                TryGetV2DpReplaceInputDescription(icId, out string v2Description)
                    ? v2Description
                    : IsDpPerspectiveIc(icId)
                    ? $"Replacement DP is padded to the selected base BIN length ({FormatSupportedDpPerspectiveBaseLengths()}); only the original TP range is restored from base."
                    : "Replacement DP payload. Build stays gated until this IC has approved DP Replace mapping evidence.",
                false,
                CompositionAddressSpaceIds.DpReplacement,
                "dp"),
        ];
        foreach (DpReplaceAdditionalPayloadRule rule in DpReplaceAuthoringCatalog.GetAdditionalPayloads(icId))
        {
            slots.Add(new WorkbenchReplaceInputSlot(
                rule.SlotId,
                rule.Title,
                rule.Description,
                false,
                rule.AddressSpaceId,
                rule.RegionId));
        }

        return slots;
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
        return !IsLdRegion(region) || DpReplaceAuthoringCatalog.IsAdditionalPayloadRegion(icId, region.RegionId);
    }

    private static bool IsSupportedDpPerspectiveBaseLength(long? length)
    {
        return length is long value && DpPerspectiveCatalog.IsSupportedContainerLength(value);
    }

    private static string FormatSupportedDpPerspectiveBaseLengths()
    {
        return DpPerspectiveCatalog.FormatSupportedLengths();
    }

    private static string FormatDpPerspectiveIcIds()
    {
        return DpPerspectiveCatalog.FormatSupportedIcIds();
    }

    private static string FormatHexLength(long length)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{length:X}");
    }

    private static string FormatDpPerspectiveDpReplaceContainerLabel(long? length)
    {
        return length is not long value
            ? $"Base BIN length: {FormatSupportedDpPerspectiveBaseLengths()}"
            : IsSupportedDpPerspectiveBaseLength(value)
            ? FormatDisplayRange(new ByteRange(0, value))
            : $"Unsupported base BIN length {FormatHexLength(value)}";
    }

    private static string DescribeDpPerspectiveDpReplaceContainer(long? length)
    {
        return length is not long value
            ? $"{FormatDpPerspectiveIcIds()} DP Replace uses the selected base BIN length; supported lengths are {FormatSupportedDpPerspectiveBaseLengths()}."
            : IsSupportedDpPerspectiveBaseLength(value)
            ? $"Replacement DP initializes the selected base length {FormatHexLength(value)}; shorter files are padded by profile policy."
            : $"This base BIN length is not approved for {FormatDpPerspectiveIcIds()} DP Replace; use {FormatSupportedDpPerspectiveBaseLengths()}.";
    }

    private static bool IsLdRegion(TpFlashMapRegion region)
    {
        return region.RegionId.Contains("ld", StringComparison.OrdinalIgnoreCase) ||
            region.DisplayName.Contains("LDC", StringComparison.OrdinalIgnoreCase);
    }
}
