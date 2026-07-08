using System.Globalization;
using NvtFwCombiner.Application.FlashMaps;
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

        CompositionProfileDefinition profile = BuiltInReplaceProfiles.CreateDpPerspectiveDpReplaceProfile(
            icId,
            context!.Capacity);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            throw new InvalidOperationException(FormatIssues(compile.Issues));
        }

        CompositionPlan plan = compile.Plan!;
        return await RunCompiledCompositionAsync(
            "ui-replace",
            profile,
            plan,
            context.Bindings,
            context.BasePath,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: context.Selection,
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
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
                new WorkbenchMemoryMapRow(
                    FormatDisplayRange(DpPerspectiveCatalog.CustomerInfoPreserveRange),
                    "DP replacement",
                    "Restore",
                    "Base customer info",
                    $"Copy customer information at {FormatDisplayRange(DpPerspectiveCatalog.CustomerInfoPreserveRange)} from the base firmware after DP replacement."),
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

    private static List<WorkbenchReplaceInputSlot> GetDpReplaceInputSlots(string icId)
    {
        List<WorkbenchReplaceInputSlot> slots =
        [
            new(
                "replace-dp",
                "DP replacement BIN",
                IsDpPerspectiveIc(icId)
                    ? $"Replacement DP is padded to the selected base BIN length ({FormatSupportedDpPerspectiveBaseLengths()}); original TP range is restored from base."
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
