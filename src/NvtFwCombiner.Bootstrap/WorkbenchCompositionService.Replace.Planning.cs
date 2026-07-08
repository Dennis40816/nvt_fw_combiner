using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
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

    private static IReadOnlyList<OperationRunSummary> CreateReplacePlanningOperations(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null)
    {
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        OperationRunStatus status = OperationRunStatus.Skipped;

        if (replaceMode == WorkbenchReplaceModes.Dp && IsDpPerspectiveIc(icId))
        {
            if (dpBaseLength is not long capacity || !IsSupportedDpPerspectiveBaseLength(capacity))
            {
                return [];
            }

            var fullContainer = new ByteRange(0, capacity);
            ByteRange tpRestoreRange = DpPerspectiveCatalog.TpOverlayRange;
            ByteRange customerInfoPreserveRange = DpPerspectiveCatalog.CustomerInfoPreserveRange;
            return
            [
                new OperationRunSummary(
                    DpPerspectiveCatalog.ReplaceDpContainerOperationId,
                    DpPerspectiveCatalog.ReplaceDpContainerSequence,
                    CompositionOperationKind.ReplaceRange,
                    status,
                    CompositionAddressSpaceIds.DpReplacement,
                    fullContainer,
                    CompositionAddressSpaceIds.OutputImage,
                    fullContainer,
                    OverlapPolicy.Reject,
                    null,
                    null,
                    [],
                    [],
                    "Replace the DP perspective container using the selected base length."),
                new OperationRunSummary(
                    DpPerspectiveCatalog.RestoreBaseTpOperationId,
                    DpPerspectiveCatalog.RestoreBaseTpSequence,
                    CompositionOperationKind.CopyRange,
                    status,
                    CompositionAddressSpaceIds.ReferenceBase,
                    tpRestoreRange,
                    CompositionAddressSpaceIds.OutputImage,
                    tpRestoreRange,
                    OverlapPolicy.ReplaceExisting,
                    null,
                    null,
                    [],
                    [],
                    "Restore original TP FW from the base firmware."),
                new OperationRunSummary(
                    DpPerspectiveCatalog.RestoreBaseCustomerInfoOperationId,
                    DpPerspectiveCatalog.RestoreBaseCustomerInfoSequence,
                    CompositionOperationKind.CopyRange,
                    status,
                    CompositionAddressSpaceIds.ReferenceBase,
                    customerInfoPreserveRange,
                    CompositionAddressSpaceIds.OutputImage,
                    customerInfoPreserveRange,
                    OverlapPolicy.ReplaceExisting,
                    null,
                    null,
                    [],
                    [],
                    "Restore customer information from the base firmware."),
            ];
        }

        return
        [
            .. GetDpReplaceRegions(icId, regions).Select((region, index) => new OperationRunSummary(
                $"replace-{region.RegionId}",
                100 + (index * 10),
                CompositionOperationKind.ReplaceRange,
                status,
                GetDpReplaceSourceAddressSpaceId(icId, region),
                new ByteRange(0, region.Range.Length),
                CompositionAddressSpaceIds.OutputImage,
                region.Range,
                OverlapPolicy.ReplaceExisting,
                null,
                null,
                [],
                [],
                $"{region.DisplayName} awaits per-IC DP Replace source mapping evidence.")),
        ];
    }

    private static string GetDpReplaceSourceAddressSpaceId(string icId, TpFlashMapRegion region)
    {
        return DpReplaceAuthoringCatalog.TryGetAdditionalPayload(icId, region.RegionId, out DpReplaceAdditionalPayloadRule? rule)
            ? rule.AddressSpaceId
            : CompositionAddressSpaceIds.DpReplacement;
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
}
