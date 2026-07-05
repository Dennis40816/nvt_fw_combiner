using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
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

        CompositionProfileDefinition profile = BuiltInReplaceProfiles.CreateNt51950FamilyDpReplaceProfile(icId, capacity);
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

        return await RunCompiledCompositionAsync(
            "ui-replace",
            profile,
            compile.Plan!,
            bindings,
            bindings[0].ArtifactId,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: ToIcNumberSelection(number),
            cancellationToken).ConfigureAwait(false);
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
                    FormatDisplayRange(BuiltInReplaceProfiles.Nt51950FamilyTpRestoreRange),
                    "DP replacement",
                    "Restore",
                    "Base TP",
                    $"Copy original TP FW at {FormatDisplayRange(BuiltInReplaceProfiles.Nt51950FamilyTpRestoreRange)} from the base firmware after DP replacement."),
                new WorkbenchMemoryMapRow(
                    FormatDisplayRange(BuiltInReplaceProfiles.Nt51950FamilyCustomerInfoPreserveRange),
                    "DP replacement",
                    "Restore",
                    "Base customer info",
                    $"Copy customer information at {FormatDisplayRange(BuiltInReplaceProfiles.Nt51950FamilyCustomerInfoPreserveRange)} from the base firmware after DP replacement."),
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
        return length is long value && BuiltInReplaceProfiles.IsSupportedNt51950FamilyDpBaseLength(value);
    }

    private static string FormatSupportedNt51950DpBaseLengths()
    {
        return string.Join(" / ", BuiltInReplaceProfiles.Nt51950FamilySupportedDpBaseLengths.Select(FormatHexLength));
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
        return BuiltInReplaceProfiles.IsNt51950FamilyDpReplaceIc(icId);
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
