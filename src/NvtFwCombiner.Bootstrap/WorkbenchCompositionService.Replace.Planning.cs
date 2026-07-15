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
        string issueMessage)
    {
        return CreateReplaceReportRunResult(
            icId,
            replaceMode,
            slotPaths,
            build,
            CreateReplacePlanningOperations(icId, number, replaceMode),
            [new CompositionIssue(issueCode, issueMessage, replaceMode.ToLowerInvariant())],
            GetReplaceDefaultOutputFileName(icId, replaceMode));
    }

    private static IReadOnlyList<OperationRunSummary> CreateReplacePlanningOperations(
        string icId,
        string number,
        string replaceMode)
    {
        if (replaceMode == WorkbenchReplaceModes.Dp && IsBuiltInV2DpReplaceIc(icId))
        {
            return [];
        }

        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetRegions(icId, selection);
        OperationRunStatus status = OperationRunStatus.Skipped;

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
