using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static List<OperationRunSummary> CreateCtrlRamPlanningOperations(
        string icId,
        IcNumberSelection selection,
        IReadOnlyList<TpCtrlRamPostbuildSource> sources,
        IReadOnlyDictionary<string, string> slotPaths,
        bool runnablePreview,
        LegacyCombinerPostbuildProfile? postbuildProfile = null)
    {
        OperationRunStatus status = runnablePreview ? OperationRunStatus.Succeeded : OperationRunStatus.Skipped;
        List<OperationRunSummary> operations = [];
        long capacity = Math.Max(
            1,
            TpFlashMapCatalog.GetRegions(icId, selection, postbuildProfile).Max(region => region.Range.EndExclusive));
        int sequence = 100;
        foreach (TpFlashMapRegion region in sources.SelectMany(source => source.Regions).DistinctBy(region => region.RegionId, StringComparer.Ordinal).OrderBy(region => region.Range.Start))
        {
            operations.Add(new OperationRunSummary(
                $"split-base-{region.RegionId}",
                sequence,
                CompositionOperationKind.CopyRange,
                status,
                CompositionAddressSpaceIds.ReferenceBase,
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
        }

        foreach (TpCtrlRamPostbuildSource source in sources.Where(source =>
                     slotPaths.ContainsKey(CtrlRamSlotId(source.SourceId))))
        {
            string slotId = CtrlRamSlotId(source.SourceId);
            long sourceLength = slotPaths.TryGetValue(slotId, out string? sourcePath) && File.Exists(sourcePath)
                ? Math.Min(new FileInfo(sourcePath).Length, source.RequiredLength)
                : source.RequiredLength;
            foreach (LegacyCombinerBlockArgument block in source.Blocks)
            {
                TpFlashMapRegion region = source.Regions.Single(region => region.Range.Overlaps(block.FirmwareRange));
                long availableLength = sourceLength - block.SourceOffset;
                long effectiveLength = availableLength > 0
                    ? Math.Min(block.FirmwareRange.Length, availableLength)
                    : block.FirmwareRange.Length;
                operations.Add(new OperationRunSummary(
                    $"stage-{block.BlockId}",
                    sequence,
                    CompositionOperationKind.ReplaceRange,
                    status,
                    slotId,
                    new ByteRange(block.SourceOffset, effectiveLength),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(block.FirmwareRange.Start, effectiveLength),
                    OverlapPolicy.ReplaceExisting,
                    null,
                    null,
                    [],
                    [],
                    $"Stage up to {block.FirmwareRange.Length} bytes from {source.SourceFileName} offset 0x{block.SourceOffset:X} for {region.DisplayName}; short sources stop at EOF and oversized tails are unused."));
                sequence += 10;
            }
        }

        if (postbuildProfile is null &&
            !IcMetadataFacade.TryGetDefaultPostbuildProfile(icId, out postbuildProfile))
        {
            return operations;
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile!, selection);
        string firmwarePath = Path.Combine("output", postbuildProfile!.FirmwareFileName);
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
                CompositionAddressSpaceIds.OutputImage,
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
}
