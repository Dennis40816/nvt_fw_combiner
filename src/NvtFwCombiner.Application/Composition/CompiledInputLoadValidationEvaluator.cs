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
        foreach (ByteRange range in requirement.Ranges)
        {
            if (range.EndExclusive > bytes.Length)
            {
                return new InputLoadValidationEvaluationResult(
                    Issue(requirement,
                        $"Input validation range {range} is outside address space '{requirement.AddressSpaceId}'."),
                    new InputDiagnosticEvidence(
                        requirement.AddressSpaceId,
                        bytes.Length,
                        range.EndExclusive,
                        range,
                        repeatedByte: null));
            }

            ReadOnlySpan<byte> candidate = bytes.Slice(checked((int)range.Start), checked((int)range.Length));
            if (candidate[1..].IndexOfAnyExcept(candidate[0]) < 0)
            {
                return new InputLoadValidationEvaluationResult(
                    Issue(requirement,
                        $"Input range {range} in '{requirement.AddressSpaceId}' is a repeated-byte placeholder and cannot be used."),
                    new InputDiagnosticEvidence(
                        requirement.AddressSpaceId,
                        actualLength: null,
                        requiredEndExclusive: null,
                        range,
                        candidate[0]));
            }
        }

        return new InputLoadValidationEvaluationResult(Issue: null, DiagnosticEvidence: null);
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

/// <summary>One single-pass input-load evaluation and its optional path-free evidence.</summary>
internal sealed record InputLoadValidationEvaluationResult(
    CompositionIssue? Issue,
    InputDiagnosticEvidence? DiagnosticEvidence);
