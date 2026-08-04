using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static List<InputLoadValidationEvaluation> EvaluateInputLoad(
        CompiledComposition compiledComposition,
        IReadOnlyDictionary<string, byte[]> inputBytes)
    {
        var evaluations = new List<InputLoadValidationEvaluation>();
        foreach (CompiledValidationRequirement requirement in compiledComposition.ValidationRequirements.Where(
                     static requirement => requirement.Stage == CompiledValidationStage.InputLoad))
        {
            CompositionIssue? issue = requirement switch
            {
                CompiledUniformInputRangeValidation uniform =>
                    CompiledInputLoadValidationEvaluator.Evaluate(inputBytes, uniform),
                _ => new CompositionIssue(
                    requirement.IssueCode,
                    $"Input-load validation rule '{requirement.RuleId}' has no executable runtime evaluator.",
                    requirement.RuleId),
            };
            evaluations.Add(new InputLoadValidationEvaluation(
                new ValidationRunSummary(
                    requirement.RuleId,
                    requirement.Stage,
                    issue is null
                        ? ValidationRunStatus.Passed
                        : ValidationRunStatus.Failed,
                    requirement.Severity,
                    issue?.Code ?? requirement.IssueCode),
                issue));
        }

        return evaluations;
    }

    private static List<InputLoadValidationEvaluation> CreateSkippedInputLoadValidations(
        CompiledComposition compiledComposition)
    {
        return
        [
            .. compiledComposition.ValidationRequirements
                .Where(static requirement => requirement.Stage == CompiledValidationStage.InputLoad)
                .Select(static requirement => new InputLoadValidationEvaluation(
                    new ValidationRunSummary(
                        requirement.RuleId,
                        requirement.Stage,
                        ValidationRunStatus.Skipped,
                        requirement.Severity,
                        requirement.IssueCode),
                    Issue: null)),
        ];
    }

    private sealed record InputLoadValidationEvaluation(
        ValidationRunSummary Summary,
        CompositionIssue? Issue);
}
