using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunBuiltInV2DpReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        if (!TryCreateBuiltInV2DpReplaceRunContext(
                icId,
                number,
                slotPaths,
                build,
                out BuiltInV2DpReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        if (!TryCompileBuiltInV2DpReplace(
                icId,
                context!.Capacity,
                out CompiledComposition? compiledComposition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            return CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.ReplaceDpProfilePending,
                $"No supported V2 DP Replace profile is registered for {icId}.");
        }

        if (compiledComposition is null)
        {
            CompositionIssue issue = issues.Count > 0
                ? issues[0]
                : new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 DP Replace profile for {icId} did not produce an executable composition.");
            return CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                issue.Code,
                issue.Message);
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
        IReadOnlyList<TpFlashMapRegion> regions)
    {
        return
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

    private static string FormatHexLength(long length)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{length:X}");
    }

    private static bool IsLdRegion(TpFlashMapRegion region)
    {
        return region.RegionId.Contains("ld", StringComparison.OrdinalIgnoreCase) ||
            region.DisplayName.Contains("LDC", StringComparison.OrdinalIgnoreCase);
    }
}
