using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static List<InputLoadValidationEvaluation> EvaluateInputLoad(
        CompiledComposition compiledComposition,
        IReadOnlyDictionary<string, byte[]> inputBytes)
    {
        var evaluations = new List<InputLoadValidationEvaluation>();
        foreach (CompiledValidationRequirement requirement in compiledComposition.V2Details.Provenance.ValidationRequirements.Where(
                     static requirement => requirement.Stage == CompiledValidationStage.InputLoad))
        {
            InputLoadValidationEvaluationResult evaluation = requirement switch
            {
                CompiledBankScopedValidation bank => EvaluateBankInputLoad(inputBytes, bank),
                CompiledUniformInputRangeValidation uniform =>
                    CompiledInputLoadValidationEvaluator.Evaluate(inputBytes, uniform),
                _ => new InputLoadValidationEvaluationResult(
                    new CompositionIssue(
                        requirement.IssueCode,
                        $"Input-load validation rule '{requirement.RuleId}' has no executable runtime evaluator.",
                        requirement.RuleId),
                    DiagnosticEvidence: null),
            };
            evaluations.Add(new InputLoadValidationEvaluation(
                new ValidationRunSummary(
                    requirement.RuleId,
                    requirement.Stage,
                    evaluation.Issue is null
                        ? ValidationRunStatus.Passed
                        : ValidationRunStatus.Failed,
                    requirement.Severity,
                    evaluation.Issue?.Code ?? requirement.IssueCode),
                evaluation.Issue,
                evaluation.DiagnosticEvidence));
        }

        return evaluations;
    }

    private static List<InputLoadValidationEvaluation> CreateSkippedInputLoadValidations(
        CompiledComposition compiledComposition)
    {
        return
        [
            .. compiledComposition.V2Details.Provenance.ValidationRequirements
                .Where(static requirement => requirement.Stage == CompiledValidationStage.InputLoad)
                .Select(static requirement => new InputLoadValidationEvaluation(
                    new ValidationRunSummary(
                        requirement.RuleId,
                        requirement.Stage,
                        ValidationRunStatus.Skipped,
                        requirement.Severity,
                        requirement.IssueCode),
                    Issue: null,
                    DiagnosticEvidence: null)),
        ];
    }

    private sealed record InputLoadValidationEvaluation(
        ValidationRunSummary Summary,
        CompositionIssue? Issue,
        InputDiagnosticEvidence? DiagnosticEvidence);
}
