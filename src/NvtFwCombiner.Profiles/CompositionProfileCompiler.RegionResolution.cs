using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class CompositionProfileCompiler
{
    private static ProfileRegion? ResolveTargetRegionByRange(
        CompositionProfileDefinition profile,
        string targetSpaceId,
        ByteRange targetRange,
        string unresolvedIssueCode,
        string ambiguousIssueCode,
        string evidenceId,
        List<CompositionIssue> issues)
    {
        ProfileRegion[] candidates = [
            .. profile.Regions.Where(region =>
                string.Equals(region.AddressSpaceId, targetSpaceId, StringComparison.Ordinal) &&
                region.Range.Contains(targetRange)),
        ];
        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        if (candidates.Length == 0)
        {
            issues.Add(new CompositionIssue(
                unresolvedIssueCode,
                $"Target range '{targetRange}' is not contained by exactly one profile region.",
                evidenceId));
        }
        else
        {
            issues.Add(new CompositionIssue(
                ambiguousIssueCode,
                $"Target range '{targetRange}' is contained by multiple profile regions.",
                evidenceId));
        }

        return null;
    }

    private static RegionAccessRule? FindAccessRule(CompositionProfileDefinition profile, string regionId)
    {
        return profile.RegionAccessRules.FirstOrDefault(rule =>
            string.Equals(rule.RegionId, regionId, StringComparison.Ordinal));
    }

    private static bool OverlapsProtectedRegion(
        CompositionProfileDefinition profile,
        ProfileRegion targetRegion,
        string targetSpaceId,
        ByteRange targetRange)
    {
        foreach (ProfileRegion region in profile.Regions)
        {
            if (!string.Equals(region.AddressSpaceId, targetSpaceId, StringComparison.Ordinal) ||
                !region.Range.Overlaps(targetRange) ||
                string.Equals(region.RegionId, targetRegion.RegionId, StringComparison.Ordinal))
            {
                continue;
            }

            RegionAccessRule? rule = FindAccessRule(profile, region.RegionId);
            if (region.WritePolicy == RegionWritePolicy.Forbidden ||
                region.ProcessorDependencyIds.Count > 0 ||
                rule?.Access != RegionAccessKind.ExplicitRange)
            {
                return true;
            }
        }

        return false;
    }
}
