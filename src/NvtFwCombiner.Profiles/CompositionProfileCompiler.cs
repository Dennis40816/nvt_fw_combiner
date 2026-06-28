using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Compiles typed profile definitions and request mapping overlays into domain composition plans.</summary>
public static class CompositionProfileCompiler
{
    /// <summary>Compiles a profile and optional explicit mappings into a validated plan.</summary>
    public static ProfileCompileResult Compile(
        CompositionProfileDefinition profile,
        IReadOnlyList<ExplicitMapping> explicitMappings,
        IReadOnlyList<AddressSpace>? requestAddressSpaces = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(explicitMappings);
        requestAddressSpaces ??= [];

        List<CompositionIssue> issues = ValidateProfileHeader(profile, explicitMappings);
        issues.AddRange(ValidateExplicitMappings(profile, explicitMappings));
        if (issues.Count > 0)
        {
            return ProfileCompileResult.Failed(issues);
        }

        try
        {
            CompositionOperation[] operations = [
                .. profile.Operations,
                .. explicitMappings.Select(mapping => CompileExplicitMapping(profile, mapping)),
            ];
            AddressSpace[] addressSpaces = [
                .. profile.AddressSpaces,
                .. requestAddressSpaces,
            ];
            var provenance = new CompositionPlanProvenance(
                profile.ProfileId,
                profile.ProfileVersion,
                profile.IcId,
                profile.ModeId,
                profile.ExperienceId,
                profile.CompositionKind);
            var plan = new CompositionPlan(profile.Initialization, addressSpaces, operations, provenance);
            return ProfileCompileResult.Succeeded(plan);
        }
        catch (ArgumentException exception)
        {
            return ProfileCompileResult.Failed([
                new CompositionIssue("profile.plan.invalid", exception.Message),
            ]);
        }
    }

    private static List<CompositionIssue> ValidateProfileHeader(
        CompositionProfileDefinition profile,
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        List<CompositionIssue> issues = [];

        if (!ExperienceCatalog.TryFind(profile.ExperienceId, out ExperienceDescriptor? experience) ||
            experience is null)
        {
            issues.Add(new CompositionIssue(
                "profile.experience.unknown",
                $"Experience '{profile.ExperienceId}' is not in the approved catalog."));
            return issues;
        }

        if (experience.CompositionKind != profile.CompositionKind)
        {
            issues.Add(new CompositionIssue(
                "profile.composition-kind.mismatch",
                "Profile composition kind must match the approved experience catalog."));
        }

        if (experience.RequiredInitialization != profile.Initialization.Kind)
        {
            issues.Add(new CompositionIssue(
                "profile.initialization-kind.mismatch",
                "Profile initializer kind must match merge versus replace semantics."));
        }

        if (explicitMappings.Count > 0 && experience.LayoutPolicy != LayoutPolicy.UserDefined)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.not-allowed",
                "Explicit mappings are allowed only for general user-defined experiences."));
        }

        return issues;
    }

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
        if (mapping.TargetRegionId is null)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.target-region-required",
                $"Explicit mapping '{mapping.MappingId}' must name a profile-approved target region.",
                mapping.MappingId));
            return;
        }

        ProfileRegion? targetRegion = profile.Regions.SingleOrDefault(region =>
            string.Equals(region.RegionId, mapping.TargetRegionId, StringComparison.Ordinal));
        if (targetRegion is null)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.target-region-unknown",
                $"Explicit mapping '{mapping.MappingId}' targets unknown region '{mapping.TargetRegionId}'.",
                mapping.MappingId));
            return;
        }

        RegionAccessRule? accessRule = profile.RegionAccessRules.SingleOrDefault(rule =>
            string.Equals(rule.RegionId, targetRegion.RegionId, StringComparison.Ordinal));
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

        if (targetRegion.ProcessorDependencyIds.Count > 0)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.processor-dependency",
                $"Explicit mapping '{mapping.MappingId}' targets region '{targetRegion.RegionId}' with processor dependencies.",
                mapping.MappingId));
        }

        if (OverlapsProtectedRegion(profile, targetRegion, mapping))
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.protected-overlap",
                $"Explicit mapping '{mapping.MappingId}' overlaps a protected or non-explicit profile region.",
                mapping.MappingId));
        }
    }

    private static bool OverlapsProtectedRegion(
        CompositionProfileDefinition profile,
        ProfileRegion targetRegion,
        ExplicitMapping mapping)
    {
        foreach (ProfileRegion region in profile.Regions)
        {
            if (!string.Equals(region.AddressSpaceId, mapping.TargetSpaceId, StringComparison.Ordinal) ||
                !region.Range.Overlaps(mapping.TargetRange) ||
                string.Equals(region.RegionId, targetRegion.RegionId, StringComparison.Ordinal))
            {
                continue;
            }

            RegionAccessRule? rule = profile.RegionAccessRules.SingleOrDefault(item =>
                string.Equals(item.RegionId, region.RegionId, StringComparison.Ordinal));
            if (region.WritePolicy == RegionWritePolicy.Forbidden ||
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
                    mapping.Reason),
            ExplicitMappingOperationKind.ReplaceRange when profile.CompositionKind == CompositionKind.Replace =>
                CompositionOperation.ReplaceRange(
                    mapping.MappingId,
                    mapping.Sequence,
                    mapping.SourceBindingId,
                    mapping.SourceRange,
                    mapping.TargetSpaceId,
                    mapping.TargetRange,
                    mapping.OverlapPolicy,
                    mapping.Reason),
            _ => throw new ArgumentException(
                $"Explicit mapping '{mapping.MappingId}' kind is incompatible with profile composition kind '{profile.CompositionKind}'.",
                nameof(mapping)),
        };
    }
}
