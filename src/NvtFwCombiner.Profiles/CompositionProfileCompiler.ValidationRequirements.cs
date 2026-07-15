using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class CompositionProfileCompiler
{
    private static List<CompositionIssue> ValidateRuntimeValidationRequirements(
        IReadOnlyList<CompiledValidationRequirement> requirements)
    {
        var issues = new List<CompositionIssue>();
        foreach (CompiledValidationRequirement requirement in requirements)
        {
            if (requirement is CompiledFirmwareConfigBackupVersionValidation &&
                requirement.Stage == CompiledValidationStage.FinalOutput &&
                requirement.Severity == CompiledValidationSeverity.Error)
            {
                continue;
            }

            issues.Add(new CompositionIssue(
                "profile.validation.runtime-unsupported",
                $"Legacy profile validation rule '{requirement.RuleId}' is not executable by the current runtime.",
                requirement.RuleId));
        }

        return issues;
    }
}
