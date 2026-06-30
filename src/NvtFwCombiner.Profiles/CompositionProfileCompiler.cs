using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Compiles typed profile definitions and request mapping overlays into domain composition plans.</summary>
public static class CompositionProfileCompiler
{
    private const string TpHardwareReplaceExperienceId = "tp-hw-replace";

    /// <summary>Compiles a profile and optional explicit mappings into a validated plan.</summary>
    public static ProfileCompileResult Compile(
        CompositionProfileDefinition profile,
        IReadOnlyList<ExplicitMapping> explicitMappings,
        IReadOnlyList<AddressSpace>? requestAddressSpaces = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(explicitMappings);
        requestAddressSpaces ??= [];

        List<CompositionIssue> issues = ValidateProfileHeader(profile, explicitMappings, requestAddressSpaces);
        issues.AddRange(ValidateProfileOperations(profile));
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
        IReadOnlyList<ExplicitMapping> explicitMappings,
        IReadOnlyList<AddressSpace> requestAddressSpaces)
    {
        List<CompositionIssue> issues = [];
        ValidateInputPaddingPolicy(profile, requestAddressSpaces, issues);

        AddDuplicateIssues(
            profile.Regions,
            region => region.RegionId,
            "profile.region.duplicate",
            "Profile region id is declared more than once.",
            issues);
        AddDuplicateIssues(
            profile.RegionAccessRules,
            rule => rule.RegionId,
            "profile.region-access.duplicate",
            "Profile region access rule is declared more than once.",
            issues);

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

        if (requestAddressSpaces.Count > 0 && experience.InputPolicy != InputPolicy.Extensible)
        {
            issues.Add(new CompositionIssue(
                "profile.request-address-space.not-allowed",
                "Runtime address spaces are allowed only for extensible-input experiences."));
        }

        return issues;
    }

    private static void ValidateInputPaddingPolicy(
        CompositionProfileDefinition profile,
        IReadOnlyList<AddressSpace> requestAddressSpaces,
        List<CompositionIssue> issues)
    {
        foreach (AddressSpace addressSpace in requestAddressSpaces.Where(space => space.InputPaddingByte is not null))
        {
            issues.Add(new CompositionIssue(
                "profile.input-padding.request-not-allowed",
                $"Runtime address space '{addressSpace.AddressSpaceId}' cannot declare input padding.",
                addressSpace.AddressSpaceId));
        }

        if (!RequiresStrictInputLength(profile))
        {
            return;
        }

        foreach (AddressSpace addressSpace in profile.AddressSpaces.Where(space => space.InputPaddingByte is not null))
        {
            issues.Add(new CompositionIssue(
                "profile.input-padding.processor-conflict",
                $"Address space '{addressSpace.AddressSpaceId}' declares input padding in a profile with processor-dependent integrity.",
                addressSpace.AddressSpaceId));
        }
    }

    private static bool RequiresStrictInputLength(CompositionProfileDefinition profile)
    {
        return string.Equals(profile.ExperienceId, TpHardwareReplaceExperienceId, StringComparison.Ordinal) ||
            profile.Operations.Any(operation => operation.Kind == CompositionOperationKind.RunExternalProcessor) ||
            profile.Regions.Any(region => region.ProcessorDependencyIds.Count > 0);
    }

    private static void AddDuplicateIssues<T>(
        IEnumerable<T> items,
        Func<T, string> getId,
        string issueCode,
        string message,
        List<CompositionIssue> issues)
    {
        foreach (string id in items.Select(getId).GroupBy(id => id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            issues.Add(new CompositionIssue(issueCode, $"{message} Duplicate id: '{id}'.", id));
        }
    }

    private static List<CompositionIssue> ValidateProfileOperations(CompositionProfileDefinition profile)
    {
        List<CompositionIssue> issues = [];
        if (profile.Regions.Count == 0)
        {
            return issues;
        }

        foreach (CompositionOperation operation in profile.Operations)
        {
            if (operation.Kind == CompositionOperationKind.RunExternalProcessor)
            {
                ValidateExternalProcessorOperation(profile, operation, issues);
                continue;
            }

            ProfileRegion? targetRegion = ResolveTargetRegionByRange(
                profile,
                operation.TargetSpaceId,
                operation.TargetRange,
                "profile.operation.target-region-unresolved",
                "profile.operation.target-region-ambiguous",
                operation.OperationId,
                issues);
            if (targetRegion is null)
            {
                continue;
            }

            RegionAccessRule? accessRule = FindAccessRule(profile, targetRegion.RegionId);
            ValidateProfileOperationRegionPolicy(operation, targetRegion, accessRule, issues);
            if (OverlapsProtectedRegion(profile, targetRegion, operation.TargetSpaceId, operation.TargetRange))
            {
                issues.Add(new CompositionIssue(
                    "profile.operation.protected-overlap",
                    $"Operation '{operation.OperationId}' overlaps a protected or processor-owned profile region.",
                    operation.OperationId));
            }
        }

        return issues;
    }

    private static void ValidateExternalProcessorOperation(
        CompositionProfileDefinition profile,
        CompositionOperation operation,
        List<CompositionIssue> issues)
    {
        ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation!;
        foreach (ByteRange writeRange in invocation.AllowedWriteRanges)
        {
            ProfileRegion? targetRegion = ResolveTargetRegionByRange(
                profile,
                operation.TargetSpaceId,
                writeRange,
                "profile.external-processor.target-region-unresolved",
                "profile.external-processor.target-region-ambiguous",
                operation.OperationId,
                issues);
            if (targetRegion is null)
            {
                continue;
            }

            if (!targetRegion.ProcessorDependencyIds.Contains(invocation.ProcessorId, StringComparer.Ordinal))
            {
                issues.Add(new CompositionIssue(
                    "profile.external-processor.region-not-owned",
                    $"Operation '{operation.OperationId}' writes region '{targetRegion.RegionId}' without matching processor ownership.",
                    operation.OperationId));
            }

            if (writeRange.Start % targetRegion.Alignment != 0 ||
                writeRange.Length % targetRegion.Alignment != 0)
            {
                issues.Add(new CompositionIssue(
                    "profile.external-processor.region-alignment",
                    $"Operation '{operation.OperationId}' write range does not satisfy region '{targetRegion.RegionId}' alignment.",
                    operation.OperationId));
            }
        }
    }

    private static void ValidateProfileOperationRegionPolicy(
        CompositionOperation operation,
        ProfileRegion targetRegion,
        RegionAccessRule? accessRule,
        List<CompositionIssue> issues)
    {
        if (accessRule is null ||
            accessRule.Access is RegionAccessKind.Hidden or RegionAccessKind.ReadOnly ||
            targetRegion.WritePolicy == RegionWritePolicy.Forbidden)
        {
            issues.Add(new CompositionIssue(
                "profile.operation.region-not-enabled",
                $"Operation '{operation.OperationId}' targets region '{targetRegion.RegionId}' without write access.",
                operation.OperationId));
        }

        if (targetRegion.ProcessorDependencyIds.Count > 0)
        {
            issues.Add(new CompositionIssue(
                "profile.operation.processor-dependency",
                $"Operation '{operation.OperationId}' targets region '{targetRegion.RegionId}' with processor dependencies.",
                operation.OperationId));
        }

        if ((targetRegion.Atomicity == RegionAtomicity.Whole ||
                targetRegion.WritePolicy == RegionWritePolicy.WholeOnly ||
                accessRule?.Access == RegionAccessKind.Whole) &&
            operation.TargetRange != targetRegion.Range)
        {
            issues.Add(new CompositionIssue(
                "profile.operation.atomicity",
                $"Operation '{operation.OperationId}' must write whole region '{targetRegion.RegionId}'.",
                operation.OperationId));
        }

        if (operation.TargetRange.Start % targetRegion.Alignment != 0 ||
            operation.TargetRange.Length % targetRegion.Alignment != 0)
        {
            issues.Add(new CompositionIssue(
                "profile.operation.region-alignment",
                $"Operation '{operation.OperationId}' target range does not satisfy region '{targetRegion.RegionId}' alignment.",
                operation.OperationId));
        }
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

        if (targetRegion.ProcessorDependencyIds.Count > 0)
        {
            issues.Add(new CompositionIssue(
                "profile.explicit-mapping.processor-dependency",
                $"Explicit mapping '{mapping.MappingId}' targets region '{targetRegion.RegionId}' with processor dependencies.",
                mapping.MappingId));
        }

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
