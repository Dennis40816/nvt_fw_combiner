using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

public static partial class MemoryLayoutProjector
{
    private static string ResolveLogicalCoverageGroupId(
        FirmwareImageMap? map,
        ByteRange range,
        string? sourceSlotId,
        string segmentId,
        string? retainedCompanionSlotId = null)
    {
        if (retainedCompanionSlotId is not null)
        {
            return $"slot:{retainedCompanionSlotId}";
        }

        if (sourceSlotId is not null)
        {
            return $"slot:{sourceSlotId}";
        }

        if (map is null)
        {
            return $"segment:{segmentId}";
        }

        FirmwareRegion[] containing =
        [
            .. map.Regions.Where(region => region.Range.Contains(range)),
        ];
        long minimumLength = containing.Length > 0
            ? containing.Min(static region => region.Range.Length)
            : throw new InvalidDataException(
                $"Memory range {range} is outside the canonical map.");
        FirmwareRegion[] smallest =
        [
            .. containing.Where(region => region.Range.Length == minimumLength),
        ];
        FirmwareRegion resolved = smallest.Length == 1
            ? smallest[0]
            : throw new InvalidDataException(
                $"Memory range {range} does not resolve one smallest canonical region.");
        return $"region:{resolved.RegionId}";
    }

    private static Dictionary<ProjectionRegion, string> ResolveRetainedCompanionSlots(
        IReadOnlyList<ProjectionRegion> primaryRegions,
        IReadOnlyList<CompositionOperation> plannedOperations,
        string outputSpaceId,
        Dictionary<string, string> slotsBySpace,
        Dictionary<string, AuthoringSlotState> statesById,
        CompositionKind compositionKind)
    {
        return compositionKind != CompositionKind.Replace
            ? []
            : primaryRegions
            .Select(region => (
                Region: region,
                CandidateSlots: plannedOperations
                    .Where(operation => StringComparer.Ordinal.Equals(
                        operation.TargetSpaceId,
                        outputSpaceId))
                    .Where(operation => operation.DeclaredWriteRanges.Any(writeRange =>
                        region.Range.Overlaps(writeRange)))
                    .Where(static operation => operation.SourceSpaceId is not null)
                    .Select(operation => slotsBySpace.TryGetValue(
                        operation.SourceSpaceId!,
                        out string? slotId)
                            ? slotId
                            : null)
                    .Where(slotId => slotId is not null &&
                        statesById.TryGetValue(slotId, out AuthoringSlotState? state) &&
                        IsAdmitted(state))
                    .Distinct(StringComparer.Ordinal)
                    .Take(2)
                    .ToArray()))
            .Where(static candidate => candidate.CandidateSlots.Length == 1)
            .ToDictionary(
                static candidate => candidate.Region,
                static candidate => candidate.CandidateSlots[0]!);
    }
}
