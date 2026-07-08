using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    // Conservative deny-by-default header window. Inspected postbuild references use at least
    // a 0x100-byte firmware header copy block, and General Replace has no owner-approved
    // header editing workflow yet.
    private const long GeneralReplaceProtectedHeaderLength = 0x100;
    private const int GeneralReplacePostbuildSequence = 900;

    private static CompositionProfileDefinition CreateGeneralReplaceProfile(
        string icId,
        IcNumberSelection selection,
        long capacity,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        LegacyCombinerPostbuildCommandPlan? commandPlan,
        IReadOnlyList<LegacyCombinerPostbuildWriteRange> postbuildWriteRangeSections)
    {
        string normalizedIc = icId.ToLowerInvariant();
        ByteRange[] postbuildWriteRanges = [.. postbuildWriteRangeSections.Select(section => section.Range)];
        ProfileRegion[] regions = CreateGeneralReplaceRegions(
            icId,
            selection,
            capacity,
            postbuildProfile,
            postbuildWriteRanges);
        RegionAccessRule[] accessRules =
        [
            .. regions.Select(region => new RegionAccessRule(
                region.RegionId,
                region.WritePolicy == RegionWritePolicy.GeneralExplicit
                    ? RegionAccessKind.ExplicitRange
                    : RegionAccessKind.Hidden,
                region.WritePolicy == RegionWritePolicy.GeneralExplicit
                    ? "General Replace explicit mapping range."
                    : "Protected from General Replace.")),
        ];
        CompositionOperation[] operations = commandPlan is null || postbuildProfile is null
            ? []
            :
            [
                CompositionOperation.RunExternalProcessor(
                    $"postbuild-{commandPlan.Branch.ToString().ToLowerInvariant()}",
                    GeneralReplacePostbuildSequence,
                    "output-image",
                    new ByteRange(0, capacity),
                    new ExternalProcessorInvocation(
                        postbuildProfile.ProcessorId,
                        postbuildProfile.ToolBindingId,
                        [new ByteRange(0, capacity)],
                        postbuildWriteRanges,
                        allowedWriteRangeSections: postbuildWriteRangeSections.Select(section =>
                            new ExternalProcessorWriteRangeSection(section.SectionId, section.Range))),
                    OverlapPolicy.ReplaceExisting,
                    $"Run {commandPlan.Branch} legacy Combiner postbuild after TP-touching General Replace mappings. Combiner command: {FormatPostbuildCommandBlock(commandPlan)}."),
            ];

        return new CompositionProfileDefinition(
            $"{normalizedIc}-general-replace-workbench",
            "0.7.0",
            icId,
            "general-replace",
            CompositionKind.Replace,
            "general-replace",
            $"{normalizedIc}-general-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            [
                new AddressSpace("reference-base", capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", capacity, AddressSpaceMutability.Mutable),
            ],
            operations,
            regions,
            accessRules,
            selection.Mode);
    }

    private static ProfileRegion[] CreateGeneralReplaceRegions(
        string icId,
        IcNumberSelection selection,
        long capacity,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        IReadOnlyList<ByteRange> postbuildWriteRanges)
    {
        List<ProfileRegion> regions = [];
        IReadOnlyList<ByteRange> normalizedPostbuildWriteRanges = NormalizeGeneralPostbuildWriteRanges(
            postbuildWriteRanges,
            capacity);
        long protectedHeaderEnd = Math.Min(capacity, GeneralReplaceProtectedHeaderLength);
        if (protectedHeaderEnd > 0)
        {
            AddGeneralReplaceSplitRegion(
                regions,
                "protected-header",
                "output-image",
                new ByteRange(0, protectedHeaderEnd),
                RegionAtomicity.Whole,
                RegionWritePolicy.Forbidden,
                ["header", "protected"],
                normalizedPostbuildWriteRanges);
        }

        foreach (TpFlashMapRegion region in TpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile))
        {
            long start = Math.Max(region.Range.Start, protectedHeaderEnd);
            long end = Math.Min(region.Range.EndExclusive, capacity);
            if (end <= start)
            {
                continue;
            }

            bool explicitRange = region.Kind is TpFlashMapRegionKind.Dp or TpFlashMapRegionKind.CtrlRam;
            AddGeneralReplaceSplitRegion(
                regions,
                region.RegionId,
                "output-image",
                ByteRange.FromStartEndExclusive(start, end),
                explicitRange ? RegionAtomicity.ExplicitMapping : RegionAtomicity.Whole,
                explicitRange ? RegionWritePolicy.GeneralExplicit : RegionWritePolicy.Forbidden,
                CreateGeneralReplaceRegionTags(region),
                normalizedPostbuildWriteRanges);
        }

        if (postbuildProfile is not null)
        {
            foreach ((ByteRange range, int index) in normalizedPostbuildWriteRanges.Select((range, index) => (range, index)))
            {
                regions.Add(new ProfileRegion(
                    FormattableString.Invariant($"postbuild-write-{index:D2}"),
                    "output-image",
                    range,
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    processorDependencyIds: [postbuildProfile.ProcessorId],
                    classificationTags: ["postbuild", "protected"]));
            }
        }

        return [.. regions];
    }

    private static void AddGeneralReplaceSplitRegion(
        List<ProfileRegion> regions,
        string regionId,
        string addressSpaceId,
        ByteRange range,
        RegionAtomicity atomicity,
        RegionWritePolicy writePolicy,
        IReadOnlyList<string> classificationTags,
        IReadOnlyList<ByteRange> postbuildWriteRanges)
    {
        List<ByteRange> remainingSegments = SubtractRanges(range, postbuildWriteRanges);
        bool split = remainingSegments.Count != 1 || remainingSegments[0] != range;
        foreach ((ByteRange segment, int index) in remainingSegments.Select((segment, index) => (segment, index)))
        {
            regions.Add(new ProfileRegion(
                split ? FormattableString.Invariant($"{regionId}-{index:D2}") : regionId,
                addressSpaceId,
                segment,
                atomicity,
                writePolicy,
                classificationTags: classificationTags));
        }
    }

    private static List<ByteRange> SubtractRanges(
        ByteRange source,
        IReadOnlyList<ByteRange> removedRanges)
    {
        ByteRange[] overlaps =
        [
            .. removedRanges
                .Select(source.Intersect)
                .Where(overlap => overlap is not null)
                .Select(overlap => overlap!.Value),
        ];
        if (overlaps.Length == 0)
        {
            return [source];
        }

        SortedSet<long> splitPoints = [source.Start, source.EndExclusive];
        foreach (ByteRange overlap in overlaps)
        {
            _ = splitPoints.Add(overlap.Start);
            _ = splitPoints.Add(overlap.EndExclusive);
        }

        long[] points = [.. splitPoints];
        List<ByteRange> ranges = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var segment = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            if (!overlaps.Any(overlap => overlap.Overlaps(segment)))
            {
                ranges.Add(segment);
            }
        }

        return ranges;
    }

    private static IReadOnlyList<ByteRange> NormalizeGeneralPostbuildWriteRanges(
        IReadOnlyList<ByteRange> postbuildWriteRanges,
        long capacity)
    {
        return
        [
            .. postbuildWriteRanges
                .Where(range => range.Start >= 0 && range.EndExclusive <= capacity)
                .Distinct()
                .OrderBy(range => range.Start)
                .ThenBy(range => range.Length),
        ];
    }

    private static bool GeneralReplaceTouchesTpRegion(
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        return explicitMappings.Any(mapping => regions.Any(region =>
            IsGeneralReplaceTpRegion(region) &&
            region.Range.Overlaps(mapping.TargetRange)));
    }

    private static bool IsGeneralReplaceTpRegion(TpFlashMapRegion region)
    {
        return region.Kind == TpFlashMapRegionKind.CtrlRam ||
            region.Tags.Any(tag =>
                string.Equals(tag, "tp", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("tp-", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> CreateGeneralReplaceRegionTags(TpFlashMapRegion region)
    {
        List<string> tags = region.Kind switch
        {
            TpFlashMapRegionKind.Dp => ["dp"],
            TpFlashMapRegionKind.CtrlRam => ["tp", "tp-ctrlram"],
            TpFlashMapRegionKind.CustomerInfo => ["customer-info", "protected"],
            TpFlashMapRegionKind.ProjectId => ["project-id", "protected"],
            TpFlashMapRegionKind.Other => ["other", "protected"],
            _ => ["unknown", "protected"],
        };
        tags.AddRange(region.Tags);
        return [.. tags.Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
