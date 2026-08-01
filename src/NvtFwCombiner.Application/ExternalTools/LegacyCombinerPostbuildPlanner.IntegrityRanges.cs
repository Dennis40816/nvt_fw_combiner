using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildPlanner
{
    private static void AddNtBasedHeaderIntegrityRanges(
        LegacyCombinerPostbuildCommandPlan plan,
        LegacyCombinerPostbuildCommand command,
        long capacity,
        List<LegacyCombinerPostbuildWriteRange> ranges)
    {
        long[] crcWordOffsets = command.ModeArgument switch
        {
            "NT51932BASED_NORMAL_MODE" => [0x100, 0x118],
            "NT51950BASED_NORMAL_MODE" => [0x11C, 0x130],
            _ => [],
        };
        if (crcWordOffsets.Length == 0)
        {
            return;
        }

        foreach (LegacyCombinerBlockArgument block in command.Blocks.Where(static block =>
                     block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage &&
                     PostbuildWriteSectionSemantics.IsHeaderSection(block.SectionId)))
        {
            foreach (long crcWordOffset in crcWordOffsets)
            {
                if (crcWordOffset + 4 > block.FirmwareRange.Length)
                {
                    continue;
                }

                AddIfWithin(
                    ranges,
                    capacity,
                    new ByteRange(block.SourceOffset + crcWordOffset, 4),
                    PostbuildWriteSectionIds.FlashHeaderCrc);
            }

            ByteRange? cascadeDlmCrcOffsets = plan.Branch == LegacyCombinerPostbuildBranch.Cascade
                ? command.ModeArgument switch
                {
                    "NT51932BASED_NORMAL_MODE" => new ByteRange(0x128, 0x1C),
                    "NT51950BASED_NORMAL_MODE" => new ByteRange(0x134, 0x4C),
                    _ => null,
                }
                : null;
            if (cascadeDlmCrcOffsets is { } offsets &&
                offsets.EndExclusive <= block.FirmwareRange.Length)
            {
                AddIfWithin(
                    ranges,
                    capacity,
                    new ByteRange(checked(block.SourceOffset + offsets.Start), offsets.Length),
                    PostbuildWriteSectionIds.FlashHeaderCrc);
            }
        }
    }

    private static void AddNt51927BasedCrcOnlyIntegrityRanges(
        LegacyCombinerPostbuildBranch branch,
        long capacity,
        List<LegacyCombinerPostbuildWriteRange> ranges)
    {
        AddIfWithin(ranges, capacity, new ByteRange(0x23C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
        AddIfWithin(ranges, capacity, new ByteRange(0x24C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
        AddIfWithin(ranges, capacity, new ByteRange(0x26C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
        AddIfWithin(ranges, capacity, new ByteRange(0x27C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
        if (branch is LegacyCombinerPostbuildBranch.Cascade or LegacyCombinerPostbuildBranch.ThreeChip)
        {
            AddIfWithin(ranges, capacity, new ByteRange(0x22C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
            AddIfWithin(ranges, capacity, new ByteRange(0x29C, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
            AddIfWithin(ranges, capacity, new ByteRange(0x2AC, 4), PostbuildWriteSectionIds.FlashHeaderCrc);
        }
    }

    private static string GetPostbuildBlockSectionId(LegacyCombinerBlockArgument block)
    {
        return block.SectionId;
    }

    private static void AddIfWithin(
        List<LegacyCombinerPostbuildWriteRange> ranges,
        long capacity,
        ByteRange range,
        string sectionId)
    {
        if (range.EndExclusive <= capacity)
        {
            ranges.Add(new LegacyCombinerPostbuildWriteRange(range, sectionId));
        }
    }
}
