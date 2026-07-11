using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class CompositionProfileCompiler
{
    private static void ValidateExplicitMappingProcessorRequirement(
        CompositionProfileDefinition profile,
        ExplicitMapping mapping,
        ProfileRegion targetRegion,
        List<CompositionIssue> issues)
    {
        ProfileRegion? touchedTpRegion = FindTouchedTpClassifiedRegion(
            profile,
            mapping.TargetSpaceId,
            mapping.TargetRange);
        if (IsGeneralReplaceProfile(profile) && touchedTpRegion is not null)
        {
            if (!HasExternalProcessorAfterMapping(profile, mapping))
            {
                issues.Add(new CompositionIssue(
                    "profile.explicit-mapping.tp-processor-required",
                    $"Explicit mapping '{mapping.MappingId}' touches TP region '{touchedTpRegion.RegionId}'; General Replace must run an approved Combiner CRC/header refresh after the mapping.",
                    mapping.MappingId));
            }
        }

        if (targetRegion.ProcessorDependencyIds.Count > 0)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.processor-dependency",
                $"Explicit mapping '{mapping.MappingId}' targets region '{targetRegion.RegionId}' with processor dependencies.",
                mapping.MappingId));
        }
    }

    private static ProfileRegion? FindTouchedTpClassifiedRegion(
        CompositionProfileDefinition profile,
        string targetSpaceId,
        ByteRange targetRange)
    {
        return profile.Regions.FirstOrDefault(region =>
            string.Equals(region.AddressSpaceId, targetSpaceId, StringComparison.Ordinal) &&
            region.Range.Overlaps(targetRange) &&
            IsTpClassifiedRegion(region));
    }

    private static bool IsTpClassifiedRegion(ProfileRegion region)
    {
        return region.ClassificationTags.Any(tag =>
            string.Equals(tag, TpClassificationTag, StringComparison.Ordinal) ||
            tag.StartsWith(TpClassificationTagPrefix, StringComparison.Ordinal));
    }

    private static bool HasExternalProcessorAfterMapping(
        CompositionProfileDefinition profile,
        ExplicitMapping mapping)
    {
        return profile.Operations.Any(operation =>
            operation.Kind == CompositionOperationKind.RunExternalProcessor &&
            operation.Sequence > mapping.Sequence &&
            string.Equals(operation.TargetSpaceId, mapping.TargetSpaceId, StringComparison.Ordinal) &&
            operation.TargetRange.Contains(mapping.TargetRange) &&
            operation.ExternalProcessorInvocation is { } invocation &&
            invocation.AllowedReadRanges.Any(range => range.Contains(mapping.TargetRange)) &&
            invocation.AllowedWriteRanges.Count > 0);
    }
}
