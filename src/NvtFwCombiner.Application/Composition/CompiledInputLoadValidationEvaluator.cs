using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

internal static class CompiledInputLoadValidationEvaluator
{
    internal static InputLoadValidationEvaluationResult Evaluate(
        IReadOnlyDictionary<string, byte[]> inputBytes,
        CompiledUniformInputRangeValidation requirement)
    {
        return !inputBytes.TryGetValue(requirement.AddressSpaceId, out byte[]? bytes)
            ? new InputLoadValidationEvaluationResult(
                Issue(requirement,
                    $"Input validation cannot read required address space '{requirement.AddressSpaceId}'."),
                DiagnosticEvidence: null)
            : Evaluate(bytes, requirement);
    }

    internal static InputLoadValidationEvaluationResult Evaluate(
        ReadOnlySpan<byte> bytes,
        CompiledUniformInputRangeValidation requirement)
    {
        InputLoadValidationEvaluationResult? firstFailure = null;
        List<InputLoadValidationRangeFinding> findings = [];
        foreach (ByteRange range in requirement.Ranges)
        {
            if (range.EndExclusive > bytes.Length)
            {
                firstFailure ??= new InputLoadValidationEvaluationResult(
                    Issue(requirement,
                        $"Input validation range {range} is outside address space '{requirement.AddressSpaceId}'."),
                    new InputDiagnosticEvidence(
                        requirement.AddressSpaceId,
                        bytes.Length,
                        range.EndExclusive,
                        range,
                        repeatedByte: null));
                continue;
            }

            ReadOnlySpan<byte> candidate = bytes.Slice(checked((int)range.Start), checked((int)range.Length));
            if (candidate[1..].IndexOfAnyExcept(candidate[0]) < 0)
            {
                CompositionIssue issue = Issue(requirement,
                    $"Input range {range} in '{requirement.AddressSpaceId}' is a repeated-byte placeholder and cannot be used.");
                InputDiagnosticEvidence evidence = new(
                    requirement.AddressSpaceId,
                    actualLength: null,
                    requiredEndExclusive: null,
                    range,
                    candidate[0]);
                findings.Add(new InputLoadValidationRangeFinding(issue, evidence));
                firstFailure ??= new InputLoadValidationEvaluationResult(issue, evidence);
            }
        }

        return (firstFailure ?? new InputLoadValidationEvaluationResult(Issue: null, DiagnosticEvidence: null)) with
        {
            RangeFindings = Array.AsReadOnly<InputLoadValidationRangeFinding>([.. findings]),
        };
    }

    private static CompositionIssue Issue(
        CompiledUniformInputRangeValidation requirement,
        string message)
    {
        return new CompositionIssue(
            requirement.IssueCode,
            message,
            requirement.RuleId,
            CompositionIssueSeverity.FromCompiled(requirement.Severity));
    }
}

/// <summary>One single-pass evaluation retaining the first failure and every evaluable range finding.</summary>
internal sealed record InputLoadValidationEvaluationResult(
    CompositionIssue? Issue,
    InputDiagnosticEvidence? DiagnosticEvidence)
{
    /// <summary>Repeated-byte violations in canonical range order; unreadable ranges have no finding.</summary>
    internal IReadOnlyList<InputLoadValidationRangeFinding> RangeFindings { get; init; } = [];
}

/// <summary>One evaluable range violation produced by the canonical input-load evaluator.</summary>
internal sealed record InputLoadValidationRangeFinding(
    CompositionIssue Issue,
    InputDiagnosticEvidence DiagnosticEvidence);
