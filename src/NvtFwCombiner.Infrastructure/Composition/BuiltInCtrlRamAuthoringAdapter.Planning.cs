using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInCtrlRamAuthoringAdapter
{
    internal static List<OperationRunSummary> CreateCtrlRamPlanningOperations(
        string icId,
        IcNumberSelection selection,
        IReadOnlyList<TpCtrlRamPostbuildSource> sources,
        IReadOnlyDictionary<string, string> slotPaths,
        bool runnablePreview,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        LegacyCombinerPostbuildCommandPlan? commandPlan,
        IReadOnlyDictionary<string, byte[]>? selectedInputBytes = null)
    {
        OperationRunStatus status = runnablePreview ? OperationRunStatus.Succeeded : OperationRunStatus.Skipped;
        List<OperationRunSummary> operations = [];
        long capacity = Math.Max(
            1,
            BuiltInTpFlashMapCatalog.GetRegions(
                icId,
                selection,
                commandPlan is null ? null : postbuildProfile).Max(region => region.Range.EndExclusive));
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
                $"Split original {region.DisplayName} from the base firmware BIN for postbuild staging."));
            sequence += 10;
        }

        foreach (TpCtrlRamPostbuildSource source in sources.Where(source =>
                     slotPaths.ContainsKey(DynamicCtrlRamReplacementIds.Create(source.SourceId))))
        {
            string slotId = DynamicCtrlRamReplacementIds.Create(source.SourceId);
            long sourceLength = selectedInputBytes?.TryGetValue(
                    slotId,
                    out byte[]? sourceBytes) == true
                ? Math.Min(sourceBytes.LongLength, source.RequiredLength)
                : slotPaths.TryGetValue(slotId, out string? sourcePath) &&
                    File.Exists(sourcePath)
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

        if (postbuildProfile is null || commandPlan is null)
        {
            return operations;
        }

        string firmwarePath = Path.Combine("output", postbuildProfile.FirmwareFileName);
        string binDirectory = "BIN";
        foreach (LegacyCombinerPostbuildCommand command in commandPlan.Commands)
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
                $"Generated {commandPlan.Branch} Combiner command: Combiner.exe {string.Join(' ', args)}."));
            sequence += 10;
        }

        return operations;
    }
}
