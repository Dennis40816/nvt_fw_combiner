using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildPlanner
{
    private static void AddNtBasedHeaderIntegrityRanges(
        LegacyCombinerPostbuildCommand command,
        long capacity,
        List<LegacyCombinerPostbuildWriteRange> ranges)
    {
        long[] crcWordOffsets = command.ModeArgument switch
        {
            "NT51930BASED_NORMAL_MODE" or "NT51932BASED_NORMAL_MODE" => [0x100, 0x118],
            "NT51950BASED_NORMAL_MODE" => [0x11C, 0x130],
            _ => [],
        };
        if (crcWordOffsets.Length == 0)
        {
            return;
        }

        foreach (LegacyCombinerBlockArgument block in command.Blocks.Where(IsHeaderCopyBlock))
        {
            foreach (long crcWordOffset in crcWordOffsets)
            {
                if (crcWordOffset + 4 > block.FirmwareRange.Length &&
                    !IsNt51930LegacyHeaderCopy(command, block))
                {
                    continue;
                }

                AddIfWithin(
                    ranges,
                    capacity,
                    new ByteRange(block.SourceOffset + crcWordOffset, 4),
                    TpHeaderSectionIds.FlashHeaderCrc);
            }
        }
    }

    private static bool IsNt51930LegacyHeaderCopy(
        LegacyCombinerPostbuildCommand command,
        LegacyCombinerBlockArgument block)
    {
        return command.ModeArgument == "NT51930BASED_NORMAL_MODE" &&
            block.SourceOffset == 0x7000 &&
            block.FirmwareRange.Length == 0x100;
    }

    private static void AddNt51927BasedCrcOnlyIntegrityRanges(
        LegacyCombinerPostbuildBranch branch,
        long capacity,
        List<LegacyCombinerPostbuildWriteRange> ranges)
    {
        AddIfWithin(ranges, capacity, new ByteRange(0x23C, 4), TpHeaderSectionIds.FlashHeaderCrc);
        AddIfWithin(ranges, capacity, new ByteRange(0x24C, 4), TpHeaderSectionIds.FlashHeaderCrc);
        AddIfWithin(ranges, capacity, new ByteRange(0x26C, 4), TpHeaderSectionIds.FlashHeaderCrc);
        AddIfWithin(ranges, capacity, new ByteRange(0x27C, 4), TpHeaderSectionIds.FlashHeaderCrc);
        if (branch is LegacyCombinerPostbuildBranch.Cascade or LegacyCombinerPostbuildBranch.ThreeChip)
        {
            AddIfWithin(ranges, capacity, new ByteRange(0x22C, 4), TpHeaderSectionIds.FlashHeaderCrc);
            AddIfWithin(ranges, capacity, new ByteRange(0x29C, 4), TpHeaderSectionIds.FlashHeaderCrc);
            AddIfWithin(ranges, capacity, new ByteRange(0x2AC, 4), TpHeaderSectionIds.FlashHeaderCrc);
        }
    }

    private static bool IsHeaderCopyBlock(LegacyCombinerBlockArgument block)
    {
        return block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage &&
            block.BlockId.Contains("header-copy", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPostbuildBlockSectionId(LegacyCombinerBlockArgument block)
    {
        return TpHeaderCatalog.ResolvePostbuildBlockSectionId(
            block.BlockId,
            block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile);
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
