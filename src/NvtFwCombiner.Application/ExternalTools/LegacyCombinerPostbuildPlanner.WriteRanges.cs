using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildPlanner
{
    /// <summary>Returns known CRC/header word writes with TP flash-header section identifiers.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildWriteRange> GetKnownIntegrityWriteRangeSections(
        LegacyCombinerPostbuildCommandPlan plan,
        long capacity)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        List<LegacyCombinerPostbuildWriteRange> ranges = [];
        foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
        {
            switch (command.Family)
            {
                case LegacyCombinerCommandFamily.NormalMode when command.ModeArgument is "CRC_Enable" or "CRC32_Enable":
                    AddIfWithin(ranges, capacity, new ByteRange(0x18, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
                    AddIfWithin(ranges, capacity, new ByteRange(0x1C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
                    AddIfWithin(ranges, capacity, new ByteRange(0x3C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
                    AddIfWithin(ranges, capacity, new ByteRange(0x4C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
                    AddIfWithin(ranges, capacity, new ByteRange(0x5C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
                    AddIfWithin(ranges, capacity, new ByteRange(0xFC, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
                    break;
                case LegacyCombinerCommandFamily.NtBasedNormalMode when command.CrcArgument is "CRC8" or "CRC32":
                    AddNtBasedHeaderIntegrityRanges(plan, command, capacity, ranges);
                    break;
                case LegacyCombinerCommandFamily.CrcOnlyMode
                    when command.ModeArgument == "NT51927BASED_GEN_CRC_MODE" && command.CrcArgument == "CRC32":
                    AddNt51927BasedCrcOnlyIntegrityRanges(plan.Branch, capacity, ranges);
                    break;
                case LegacyCombinerCommandFamily.NormalMode:
                case LegacyCombinerCommandFamily.MergeMode:
                case LegacyCombinerCommandFamily.NtBasedNormalMode:
                case LegacyCombinerCommandFamily.CrcOnlyMode:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plan), command.Family, "Unsupported postbuild command family.");
            }
        }

        return NormalizeCandidateWriteRangeSections(ranges, []);
    }

    /// <summary>Returns allowed write ranges with TP flash/header section identifiers for staged-source postbuild.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildWriteRange> GetAllowedWriteRangeSectionsForStagedSources(
        LegacyCombinerPostbuildCommandPlan plan,
        long capacity,
        IEnumerable<ByteRange> allowedStagedTargetRanges,
        IEnumerable<ByteRange> allStagedTargetRanges)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(allowedStagedTargetRanges);
        ArgumentNullException.ThrowIfNull(allStagedTargetRanges);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        ByteRange[] allowedStagedRanges = [.. allowedStagedTargetRanges];
        ByteRange[] stagedRanges = [.. allStagedTargetRanges];
        List<LegacyCombinerPostbuildWriteRange> candidateRanges = [];
        foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
        {
            foreach (LegacyCombinerBlockArgument block in command.Blocks)
            {
                if (block.FirmwareRange.EndExclusive > capacity)
                {
                    continue;
                }

                if (block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile)
                {
                    foreach (ByteRange allowedStagedRange in allowedStagedRanges)
                    {
                        ByteRange? overlap = block.FirmwareRange.Intersect(allowedStagedRange);
                        if (overlap is not null)
                        {
                            candidateRanges.Add(new LegacyCombinerPostbuildWriteRange(
                                overlap.Value,
                                GetPostbuildBlockSectionId(block)));
                        }
                    }

                    continue;
                }

                if (block.SourceOffset != block.FirmwareRange.Start)
                {
                    candidateRanges.Add(new LegacyCombinerPostbuildWriteRange(
                        block.FirmwareRange,
                        GetPostbuildBlockSectionId(block),
                        new ByteRange(block.SourceOffset, block.FirmwareRange.Length)));
                }
            }
        }

        candidateRanges.AddRange(GetKnownIntegrityWriteRangeSections(plan, capacity));
        return NormalizeCandidateWriteRangeSections(candidateRanges, stagedRanges);
    }

    /// <summary>Returns in-place refresh write ranges with TP flash/header section identifiers.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildWriteRange> GetAllowedWriteRangeSectionsForInPlaceRefresh(
        LegacyCombinerPostbuildCommandPlan plan,
        long capacity)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        List<LegacyCombinerPostbuildWriteRange> candidateRanges = [];
        foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
        {
            foreach (LegacyCombinerBlockArgument block in command.Blocks)
            {
                if (block.FirmwareRange.EndExclusive > capacity)
                {
                    continue;
                }

                if (block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage &&
                    block.SourceOffset != block.FirmwareRange.Start)
                {
                    candidateRanges.Add(new LegacyCombinerPostbuildWriteRange(
                        block.FirmwareRange,
                        GetPostbuildBlockSectionId(block),
                        new ByteRange(block.SourceOffset, block.FirmwareRange.Length)));
                }
            }
        }

        candidateRanges.AddRange(GetKnownIntegrityWriteRangeSections(plan, capacity));
        return NormalizeCandidateWriteRangeSections(candidateRanges, []);
    }
}
