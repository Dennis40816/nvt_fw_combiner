using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Compiles typed profile definitions and request mapping overlays into domain composition plans.</summary>
public static class CompositionProfileCompiler
{
    /// <summary>Compiles a profile and optional explicit mappings into a validated plan.</summary>
    public static ProfileCompileResult Compile(
        CompositionProfileDefinition profile,
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(explicitMappings);

        List<CompositionIssue> issues = ValidateProfileHeader(profile, explicitMappings);
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
            var plan = new CompositionPlan(profile.Initialization, profile.AddressSpaces, operations);
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
