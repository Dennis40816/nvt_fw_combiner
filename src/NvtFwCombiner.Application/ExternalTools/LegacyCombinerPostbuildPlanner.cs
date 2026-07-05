using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Resolves postbuild branch and command data for legacy combiner transforms.</summary>
public static class LegacyCombinerPostbuildPlanner
{
    /// <summary>Creates a command plan from an IC profile and user-selected IC number context.</summary>
    public static LegacyCombinerPostbuildCommandPlan CreatePlan(
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection? icNumberSelection)
    {
        ArgumentNullException.ThrowIfNull(profile);

        LegacyCombinerPostbuildBranch branch = ResolveBranch(profile, icNumberSelection);
        IReadOnlyList<LegacyCombinerPostbuildCommand> commands = branch switch
        {
            LegacyCombinerPostbuildBranch.SingleChip => profile.SingleCommands,
            LegacyCombinerPostbuildBranch.CascadeExtended => profile.CascadeExtendedCommands ?? profile.CascadeCommands,
            LegacyCombinerPostbuildBranch.TwoChip => profile.TwoChipCommands ?? profile.CascadeCommands,
            LegacyCombinerPostbuildBranch.ThreeChip => profile.ThreeChipCommands ?? profile.CascadeCommands,
            LegacyCombinerPostbuildBranch.Cascade => profile.CascadeCommands,
            _ => throw new ArgumentOutOfRangeException(nameof(icNumberSelection), "Unsupported postbuild branch."),
        };
        return new LegacyCombinerPostbuildCommandPlan(profile, branch, commands);
    }

    /// <summary>Returns staged file block arguments in deterministic order.</summary>
    public static IReadOnlyList<LegacyCombinerBlockArgument> GetStagedFileBlocks(
        LegacyCombinerPostbuildCommandPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return [
            .. plan.Commands
                .SelectMany(command => command.Blocks)
                .Where(block => block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile)
                .OrderBy(block => block.SourceFileName, StringComparer.Ordinal)
                .ThenBy(block => block.SourceOffset)
                .ThenBy(block => block.FirmwareRange.Start),
        ];
    }

    /// <summary>Returns known CRC/header word writes performed by command families outside explicit block targets.</summary>
    public static IReadOnlyList<ByteRange> GetKnownIntegrityWriteRanges(
        LegacyCombinerPostbuildCommandPlan plan,
        long capacity)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        List<ByteRange> ranges = [];
        foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
        {
            switch (command.Family)
            {
                case LegacyCombinerCommandFamily.NormalMode when command.ModeArgument is "CRC_Enable" or "CRC32_Enable":
                    AddIfWithin(ranges, capacity, new ByteRange(0x1C, 4));
                    AddIfWithin(ranges, capacity, new ByteRange(0xFC, 4));
                    break;
                case LegacyCombinerCommandFamily.NtBasedNormalMode when command.CrcArgument is "CRC8" or "CRC32":
                    AddNtBasedHeaderIntegrityRanges(command, capacity, ranges);
                    break;
                case LegacyCombinerCommandFamily.CrcOnlyMode
                    when command.ModeArgument == "NT51927BASED_GEN_CRC_MODE" && command.CrcArgument == "CRC32":
                    AddNt51927BasedCrcOnlyIntegrityRanges(capacity, ranges);
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

        return [
            .. ranges
                .Distinct()
                .OrderBy(range => range.Start)
                .ThenBy(range => range.Length),
        ];
    }

    /// <summary>Calculates the minimum firmware image capacity needed by a postbuild plan.</summary>
    public static long CalculateRequiredCapacity(
        LegacyCombinerPostbuildCommandPlan plan,
        IEnumerable<ByteRange> requiredTargetRanges)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(requiredTargetRanges);

        long requiredCapacity = 1;
        foreach (ByteRange range in requiredTargetRanges)
        {
            requiredCapacity = Math.Max(requiredCapacity, range.EndExclusive);
        }

        foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
        {
            foreach (LegacyCombinerBlockArgument block in command.Blocks)
            {
                requiredCapacity = Math.Max(requiredCapacity, block.FirmwareRange.EndExclusive);
                if (block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage)
                {
                    requiredCapacity = Math.Max(
                        requiredCapacity,
                        checked(block.SourceOffset + block.FirmwareRange.Length));
                }
            }
        }

        return requiredCapacity;
    }

    /// <summary>Returns write ranges allowed when staged BIN sources are pasted back by Combiner.</summary>
    public static IReadOnlyList<ByteRange> GetAllowedWriteRangesForStagedSources(
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
        List<ByteRange> candidateRanges = [];
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
                            candidateRanges.Add(overlap.Value);
                        }
                    }

                    continue;
                }

                if (block.SourceOffset != block.FirmwareRange.Start)
                {
                    candidateRanges.Add(block.FirmwareRange);
                }
            }
        }

        candidateRanges.AddRange(GetKnownIntegrityWriteRanges(plan, capacity));
        return NormalizeCandidateWriteRanges(candidateRanges, stagedRanges);
    }

    /// <summary>Returns write ranges allowed when Combiner only refreshes firmware-owned header/integrity bytes.</summary>
    public static IReadOnlyList<ByteRange> GetAllowedWriteRangesForInPlaceRefresh(
        LegacyCombinerPostbuildCommandPlan plan,
        long capacity)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        List<ByteRange> candidateRanges = [];
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
                    candidateRanges.Add(block.FirmwareRange);
                }
            }
        }

        candidateRanges.AddRange(GetKnownIntegrityWriteRanges(plan, capacity));
        return NormalizeCandidateWriteRanges(candidateRanges, []);
    }

    private static LegacyCombinerPostbuildBranch ResolveBranch(
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection? selection)
    {
        if (selection is null)
        {
            return LegacyCombinerPostbuildBranch.SingleChip;
        }

        string token = LegacyCombinerPostbuildBranchRule.NormalizeToken(selection.Parts[^1]);
        return profile.BranchRules.TryGetValue(token, out LegacyCombinerPostbuildBranch branch)
            ? branch
            : throw new ArgumentException(
                $"IC number selection '{selection.Parts[^1]}' is not supported by postbuild profile '{profile.ProcessorId}'.");
    }

    private static void AddNtBasedHeaderIntegrityRanges(
        LegacyCombinerPostbuildCommand command,
        long capacity,
        List<ByteRange> ranges)
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

                AddIfWithin(ranges, capacity, new ByteRange(block.SourceOffset + crcWordOffset, 4));
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

    private static void AddNt51927BasedCrcOnlyIntegrityRanges(long capacity, List<ByteRange> ranges)
    {
        AddIfWithin(ranges, capacity, new ByteRange(0x23C, 4));
        AddIfWithin(ranges, capacity, new ByteRange(0x24C, 4));
        AddIfWithin(ranges, capacity, new ByteRange(0x26C, 4));
        AddIfWithin(ranges, capacity, new ByteRange(0x27C, 4));
    }

    private static bool IsHeaderCopyBlock(LegacyCombinerBlockArgument block)
    {
        return block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage &&
            block.BlockId.Contains("header-copy", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddIfWithin(List<ByteRange> ranges, long capacity, ByteRange range)
    {
        if (range.EndExclusive <= capacity)
        {
            ranges.Add(range);
        }
    }

    private static IReadOnlyList<ByteRange> NormalizeCandidateWriteRanges(
        List<ByteRange> candidateRanges,
        IReadOnlyList<ByteRange> stagedTargetRanges)
    {
        if (candidateRanges.Count == 0)
        {
            return [];
        }

        SortedSet<long> splitPoints = [];
        foreach (ByteRange range in candidateRanges)
        {
            _ = splitPoints.Add(range.Start);
            _ = splitPoints.Add(range.EndExclusive);
            foreach (ByteRange stagedRange in stagedTargetRanges)
            {
                ByteRange? overlap = range.Intersect(stagedRange);
                if (overlap is not null)
                {
                    _ = splitPoints.Add(overlap.Value.Start);
                    _ = splitPoints.Add(overlap.Value.EndExclusive);
                }
            }
        }

        long[] points = [.. splitPoints];
        List<ByteRange> ranges = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var segment = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            if (candidateRanges.Any(range => range.Contains(segment)))
            {
                ranges.Add(segment);
            }
        }

        return [
            .. ranges
                .Distinct()
                .OrderBy(range => range.Start)
                .ThenBy(range => range.Length),
        ];
    }
}
