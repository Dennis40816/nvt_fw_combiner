using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class CompositionProfileCompiler
{
    private static List<CompositionIssue> ValidateExplicitMappings(
        CompositionProfileDefinition profile,
        IEnumerable<ExplicitMapping> explicitMappings)
    {
        List<CompositionIssue> issues = [];
        foreach (ExplicitMapping mapping in explicitMappings)
        {
            ValidateMappingAlignment(mapping, issues);
            ValidateExplicitMappingRegionPolicy(profile, mapping, issues);
        }

        return issues;
    }

    private static void ValidateMappingAlignment(
        ExplicitMapping mapping,
        List<CompositionIssue> issues)
    {
        if (mapping.SourceRange.Start % mapping.Alignment == 0 &&
            mapping.SourceRange.Length % mapping.Alignment == 0 &&
            mapping.TargetRange.Start % mapping.Alignment == 0 &&
            mapping.TargetRange.Length % mapping.Alignment == 0)
        {
            return;
        }

        issues.Add(new CompositionIssue(
            "profile.explicit-mapping.alignment",
            $"Explicit mapping '{mapping.MappingId}' source and target ranges must satisfy alignment {mapping.Alignment}.",
            mapping.MappingId));
    }

    private static void ValidateExplicitMappingRegionPolicy(
        CompositionProfileDefinition profile,
        ExplicitMapping mapping,
        List<CompositionIssue> issues)
    {
        ProfileRegion? targetRegion = ResolveExplicitMappingTargetRegion(profile, mapping, issues);
        if (targetRegion is null)
        {
            return;
        }

        RegionAccessRule? accessRule = FindAccessRule(profile, targetRegion.RegionId);
        if (accessRule?.Access != RegionAccessKind.ExplicitRange ||
            targetRegion.WritePolicy != RegionWritePolicy.GeneralExplicit ||
            targetRegion.Atomicity != RegionAtomicity.ExplicitMapping)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.region-not-enabled",
                $"Explicit mapping '{mapping.MappingId}' targets region '{targetRegion.RegionId}' without explicit-range access and general-explicit write policy.",
                mapping.MappingId));
        }

        if (!string.Equals(targetRegion.AddressSpaceId, mapping.TargetSpaceId, StringComparison.Ordinal) ||
            !targetRegion.Range.Contains(mapping.TargetRange))
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.range-outside-region",
                $"Explicit mapping '{mapping.MappingId}' target range must stay inside region '{targetRegion.RegionId}'.",
                mapping.MappingId));
        }

        if (mapping.TargetRange.Start % targetRegion.Alignment != 0 ||
            mapping.TargetRange.Length % targetRegion.Alignment != 0)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.region-alignment",
                $"Explicit mapping '{mapping.MappingId}' target range does not satisfy region '{targetRegion.RegionId}' alignment.",
                mapping.MappingId));
        }

        ValidateExplicitMappingProcessorRequirement(profile, mapping, targetRegion, issues);

        if (OverlapsProtectedRegion(profile, targetRegion, mapping.TargetSpaceId, mapping.TargetRange))
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.protected-overlap",
                $"Explicit mapping '{mapping.MappingId}' overlaps a protected or processor-owned profile region.",
                mapping.MappingId));
        }
    }

    private static ProfileRegion? ResolveExplicitMappingTargetRegion(
        CompositionProfileDefinition profile,
        ExplicitMapping mapping,
        List<CompositionIssue> issues)
    {
        if (mapping.TargetRegionId is not null)
        {
            ProfileRegion? namedRegion = profile.Regions.FirstOrDefault(region =>
                string.Equals(region.RegionId, mapping.TargetRegionId, StringComparison.Ordinal));
            if (namedRegion is not null)
            {
                return namedRegion;
            }

            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.target-region-unknown",
                $"Explicit mapping '{mapping.MappingId}' targets unknown region '{mapping.TargetRegionId}'.",
                mapping.MappingId));
            return null;
        }

        return ResolveTargetRegionByRange(
            profile,
            mapping.TargetSpaceId,
            mapping.TargetRange,
            "profile.explicit-mapping.target-region-unresolved",
            "profile.explicit-mapping.target-region-ambiguous",
            mapping.MappingId,
            issues);
    }
}
