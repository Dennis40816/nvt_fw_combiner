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
            var plan = new CompositionPlan(profile.Initialization, addressSpaces, operations);
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
            if (profile.CompositionKind == CompositionKind.Replace)
            {
                ValidateReplaceMappingPolicy(profile, mapping, issues);
            }
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
            $"Explicit mapping '{mapping.MappingId}' source and target ranges must satisfy alignment {mapping.Alignment}."));
    }

    private static void ValidateReplaceMappingPolicy(
        CompositionProfileDefinition profile,
        ExplicitMapping mapping,
        List<CompositionIssue> issues)
    {
        if (mapping.OperationKind != ExplicitMappingOperationKind.ReplaceRange)
        {
            return;
        }

        if (mapping.TargetRegionId is null)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.region-policy.missing",
                $"Explicit replace mapping '{mapping.MappingId}' must cite an approved target region."));
            return;
        }

        ExplicitMappingTargetPolicy? policy = profile.ExplicitMappingTargetPolicies.SingleOrDefault(candidate =>
            string.Equals(candidate.RegionId, mapping.TargetRegionId, StringComparison.Ordinal) &&
            string.Equals(candidate.TargetSpaceId, mapping.TargetSpaceId, StringComparison.Ordinal));
        if (policy is null)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.region-policy.missing",
                $"Explicit replace mapping '{mapping.MappingId}' does not match an approved target region policy."));
            return;
        }

        if (policy.AccessKind != RegionAccessKind.ExplicitRange ||
            policy.WritePolicy != RegionWritePolicy.GeneralExplicit ||
            policy.Atomicity != RegionAtomicity.ExplicitMapping)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.region-policy.denied",
                $"Explicit replace mapping '{mapping.MappingId}' targets a region that is not writable by explicit range."));
        }

        if (!policy.ContainsAllowedRange(mapping.TargetRange))
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.range-denied",
                $"Explicit replace mapping '{mapping.MappingId}' target range is outside profile-approved ranges."));
        }

        if (policy.OverlapsProtectedRange(mapping.TargetRange))
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.protected-range",
                $"Explicit replace mapping '{mapping.MappingId}' overlaps a protected range."));
        }

        if (policy.RequiredProcessorIds.Count > 0)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.processor-dependency",
                $"Explicit replace mapping '{mapping.MappingId}' requires unresolved processors: {string.Join(", ", policy.RequiredProcessorIds)}."));
        }

        if (mapping.TargetRange.Start % policy.Alignment != 0 || mapping.TargetRange.Length % policy.Alignment != 0)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.region-alignment",
                $"Explicit replace mapping '{mapping.MappingId}' target range must satisfy region alignment {policy.Alignment}."));
        }
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
