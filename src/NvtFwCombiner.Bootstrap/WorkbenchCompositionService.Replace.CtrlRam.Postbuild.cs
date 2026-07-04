using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const long LegacyInsertPidOffsetFromHeaderStart = 0x24;

    private static bool RequiresTpWorkAssembly(LegacyCombinerPostbuildProfile postbuildProfile)
    {
        return postbuildProfile.AssemblyKind == LegacyCombinerPostbuildAssemblyKind.RefreshedTpThenStandardMerge;
    }

    private static bool TryCreatePostbuildWorkArea(
        string icId,
        LegacyCombinerPostbuildProfile postbuildProfile,
        LegacyCombinerPostbuildCommandPlan commandPlan,
        IReadOnlyCollection<TpFlashMapRegion> regions,
        long baseLength,
        out CtrlRamPostbuildWorkArea? workArea,
        out CompositionIssue? issue)
    {
        long requiredPostbuildLength = CalculatePostbuildRequiredCapacity(commandPlan, regions);
        if (!RequiresTpWorkAssembly(postbuildProfile))
        {
            if (baseLength < requiredPostbuildLength)
            {
                workArea = null;
                issue = new CompositionIssue(
                    "input.address-space.length-mismatch",
                    $"Base flash BIN is too short for {icId} CtrlRAM postbuild (actual {baseLength} bytes, required at least {requiredPostbuildLength} bytes).",
                    "replace-base");
                return false;
            }

            workArea = new CtrlRamPostbuildWorkArea(baseLength, null);
            issue = null;
            return true;
        }

        if (!TryResolveStandardMergeTpAssemblyRange(icId, out ByteRange tpAssemblyRange, out issue))
        {
            workArea = null;
            return false;
        }

        if (requiredPostbuildLength > tpAssemblyRange.EndExclusive)
        {
            workArea = null;
            issue = new CompositionIssue(
                "replace.ctrlram.postbuild-range-outside-tp",
                $"CtrlRAM postbuild for {icId} writes through 0x{requiredPostbuildLength:X}, outside the Standard Merge TP range {FormatDisplayRange(tpAssemblyRange)}.",
                "postbuild");
            return false;
        }

        if (baseLength < tpAssemblyRange.EndExclusive)
        {
            workArea = null;
            issue = new CompositionIssue(
                "input.address-space.length-mismatch",
                $"Base flash BIN is too short for {icId} CtrlRAM TP work assembly (actual {baseLength} bytes, required at least {tpAssemblyRange.EndExclusive} bytes).",
                "replace-base");
            return false;
        }

        workArea = new CtrlRamPostbuildWorkArea(tpAssemblyRange.EndExclusive, tpAssemblyRange);
        issue = null;
        return true;
    }

    private static CtrlRamPostbuildWorkArea CreatePlanningPostbuildWorkArea(
        string icId,
        LegacyCombinerPostbuildProfile postbuildProfile,
        LegacyCombinerPostbuildCommandPlan commandPlan,
        IReadOnlyCollection<TpFlashMapRegion> regions,
        long fallbackCapacity)
    {
        long requiredPostbuildLength = CalculatePostbuildRequiredCapacity(commandPlan, regions);
        return RequiresTpWorkAssembly(postbuildProfile) &&
            TryResolveStandardMergeTpAssemblyRange(icId, out ByteRange tpAssemblyRange, out _) &&
            requiredPostbuildLength <= tpAssemblyRange.EndExclusive
            ? new CtrlRamPostbuildWorkArea(tpAssemblyRange.EndExclusive, tpAssemblyRange)
            : new CtrlRamPostbuildWorkArea(Math.Max(fallbackCapacity, requiredPostbuildLength), null);
    }

    private static bool TryResolveStandardMergeTpAssemblyRange(
        string icId,
        out ByteRange tpAssemblyRange,
        out CompositionIssue? issue)
    {
        tpAssemblyRange = default;
        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? standardMergeProfile))
        {
            issue = new CompositionIssue(
                "replace.ctrlram.standard-merge-profile-missing",
                $"CtrlRAM Replace TP assembly requires a Standard Merge profile for {icId}.",
                "profile");
            return false;
        }

        CompositionOperation[] tpOperations =
        [
            .. standardMergeProfile.Operations.Where(operation =>
                operation.Kind == CompositionOperationKind.CopyRange &&
                string.Equals(operation.SourceSpaceId, "tp-input", StringComparison.Ordinal) &&
                string.Equals(operation.TargetSpaceId, "output-image", StringComparison.Ordinal)),
        ];
        if (tpOperations.Length != 1)
        {
            issue = new CompositionIssue(
                "replace.ctrlram.standard-merge-tp-range-missing",
                $"CtrlRAM Replace TP assembly requires exactly one Standard Merge TP copy operation for {icId}.",
                "profile");
            return false;
        }

        tpAssemblyRange = tpOperations[0].TargetRange;
        if (tpAssemblyRange.Start != 0)
        {
            issue = new CompositionIssue(
                "replace.ctrlram.tp-work-start-unsupported",
                $"CtrlRAM Replace TP work assembly currently requires a TP range starting at 0x0; {icId} declares {FormatDisplayRange(tpAssemblyRange)}.",
                "profile");
            return false;
        }

        issue = null;
        return true;
    }

    private static long CalculatePostbuildRequiredCapacity(
        LegacyCombinerPostbuildCommandPlan commandPlan,
        IReadOnlyCollection<TpFlashMapRegion> selectedRegions)
    {
        long requiredCapacity = selectedRegions.Count == 0
            ? 1
            : selectedRegions.Max(region => region.Range.EndExclusive);
        foreach (LegacyCombinerPostbuildCommand command in commandPlan.Commands)
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

    private static List<ByteRange> CreatePostbuildAllowedWriteRanges(
        LegacyCombinerPostbuildCommandPlan commandPlan,
        long capacity,
        IReadOnlyList<TpFlashMapRegion> selectedCtrlRamRegions,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions)
    {
        List<ByteRange> candidateRanges = [];
        HashSet<long> protectedPidOffsets = CollectProtectedPidOffsets(commandPlan, capacity);
        foreach (LegacyCombinerPostbuildCommand command in commandPlan.Commands)
        {
            foreach (LegacyCombinerBlockArgument block in command.Blocks)
            {
                if (block.FirmwareRange.EndExclusive > capacity)
                {
                    continue;
                }

                if (block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile)
                {
                    foreach (TpFlashMapRegion selectedRegion in selectedCtrlRamRegions)
                    {
                        ByteRange? overlap = block.FirmwareRange.Intersect(selectedRegion.Range);
                        if (overlap is not null)
                        {
                            candidateRanges.AddRange(SplitProtectedOffsets(overlap.Value, protectedPidOffsets));
                        }
                    }

                    continue;
                }

                if (block.SourceOffset != block.FirmwareRange.Start)
                {
                    candidateRanges.AddRange(SplitProtectedOffsets(block.FirmwareRange, protectedPidOffsets));
                }
            }
        }

        return NormalizeCandidateWriteRanges(candidateRanges, ctrlRamRegions);
    }

    private sealed record CtrlRamPostbuildWorkArea(long Capacity, ByteRange? FinalAssemblyRange)
    {
        internal bool RequiresFinalAssembly => FinalAssemblyRange is not null;
    }

    private static HashSet<long> CollectProtectedPidOffsets(
        LegacyCombinerPostbuildCommandPlan commandPlan,
        long capacity)
    {
        HashSet<long> offsets = [];
        foreach (LegacyCombinerBlockArgument block in commandPlan.Commands.SelectMany(command => command.Blocks))
        {
            if (!IsHeaderPostbuildBlock(block))
            {
                continue;
            }

            long protectedOffset = checked(block.FirmwareRange.Start + LegacyInsertPidOffsetFromHeaderStart);
            if (protectedOffset >= 0 && protectedOffset < capacity)
            {
                _ = offsets.Add(protectedOffset);
            }
        }

        return offsets;
    }

    private static List<ByteRange> SplitProtectedOffsets(
        ByteRange range,
        HashSet<long> protectedOffsets)
    {
        long[] offsets =
        [
            .. protectedOffsets
                .Where(range.Contains)
                .Order(),
        ];
        if (offsets.Length == 0)
        {
            return [range];
        }

        List<ByteRange> ranges = [];
        long cursor = range.Start;
        foreach (long protectedOffset in offsets)
        {
            if (cursor < protectedOffset)
            {
                ranges.Add(ByteRange.FromStartEndExclusive(cursor, protectedOffset));
            }

            cursor = checked(protectedOffset + 1);
        }

        if (cursor < range.EndExclusive)
        {
            ranges.Add(ByteRange.FromStartEndExclusive(cursor, range.EndExclusive));
        }

        return ranges;
    }

    private static bool IsHeaderPostbuildBlock(LegacyCombinerBlockArgument block)
    {
        return block.BlockId.Contains("header", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ByteRange> NormalizeCandidateWriteRanges(
        List<ByteRange> candidateRanges,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions)
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
            foreach (TpFlashMapRegion region in ctrlRamRegions)
            {
                ByteRange? overlap = range.Intersect(region.Range);
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

    private static string FormatPostbuildCommandBlock(LegacyCombinerPostbuildCommandPlan commandPlan)
    {
        string firmwarePath = Path.Combine("output", commandPlan.Profile.FirmwareFileName);
        const string binDirectory = "BIN";
        return string.Join(
            Environment.NewLine,
            commandPlan.Commands.Select(command =>
                $"Combiner.exe {string.Join(' ', LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(command, firmwarePath, binDirectory))}"));
    }
}
