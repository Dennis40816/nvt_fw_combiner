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
                    ValidateUniformInputRanges(inputBytes, uniform),
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

    private static CompositionIssue? ValidateUniformInputRanges(
        IReadOnlyDictionary<string, byte[]> inputBytes,
        CompiledUniformInputRangeValidation requirement)
    {
        if (!inputBytes.TryGetValue(
                requirement.AddressSpaceId,
                out byte[]? bytes))
        {
            return new CompositionIssue(
                requirement.IssueCode,
                $"Input validation cannot read required address space '{requirement.AddressSpaceId}'.",
                requirement.RuleId,
                ToIssueSeverity(requirement.Severity));
        }

        foreach (ByteRange range in requirement.Ranges)
        {
            if (range.EndExclusive > bytes.LongLength)
            {
                return new CompositionIssue(
                    requirement.IssueCode,
                    $"Input validation range {range} is outside address space '{requirement.AddressSpaceId}'.",
                    requirement.RuleId,
                    ToIssueSeverity(requirement.Severity));
            }

            ReadOnlySpan<byte> candidate = bytes.AsSpan(
                checked((int)range.Start),
                checked((int)range.Length));
            byte first = candidate[0];
            if (candidate[1..].IndexOfAnyExcept(first) < 0)
            {
                return new CompositionIssue(
                    requirement.IssueCode,
                    $"Input range {range} in '{requirement.AddressSpaceId}' is a repeated-byte placeholder and cannot be used.",
                    requirement.RuleId,
                    ToIssueSeverity(requirement.Severity));
            }
        }

        return null;
    }

    private sealed record InputLoadValidationEvaluation(
        ValidationRunSummary Summary,
        CompositionIssue? Issue);
}
