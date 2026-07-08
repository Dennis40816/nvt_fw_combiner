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

    private static CompositionOperation CompileExplicitMapping(
        CompositionProfileDefinition profile,
        ExplicitMapping mapping)
    {
        return mapping.OperationKind switch
        {
            ExplicitMappingOperationKind.CopyRange when profile.CompositionKind == CompositionKind.Merge =>
                CompositionOperation.CopyRange(
                    mapping.MappingId,
                    mapping.Sequence,
                    mapping.SourceBindingId,
                    mapping.SourceRange,
                    mapping.TargetSpaceId,
                    mapping.TargetRange,
                    mapping.OverlapPolicy,
                    mapping.Reason,
                    mapping.Provenance),
            ExplicitMappingOperationKind.ReplaceRange when profile.CompositionKind == CompositionKind.Replace =>
                CompositionOperation.ReplaceRange(
                    mapping.MappingId,
                    mapping.Sequence,
                    mapping.SourceBindingId,
                    mapping.SourceRange,
                    mapping.TargetSpaceId,
                    mapping.TargetRange,
                    mapping.OverlapPolicy,
                    mapping.Reason,
                    mapping.Provenance),
            _ => throw new ArgumentException(
                $"Explicit mapping '{mapping.MappingId}' kind is incompatible with profile composition kind '{profile.CompositionKind}'.",
                nameof(mapping)),
        };
    }
}
