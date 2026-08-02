using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

internal static class CompiledInputLoadValidationEvaluator
{
    internal static CompositionIssue? Evaluate(
        IReadOnlyDictionary<string, byte[]> inputBytes,
        CompiledUniformInputRangeValidation requirement)
    {
        return !inputBytes.TryGetValue(requirement.AddressSpaceId, out byte[]? bytes)
            ? Issue(requirement,
                $"Input validation cannot read required address space '{requirement.AddressSpaceId}'.")
            : Evaluate(bytes, requirement);
    }

    internal static CompositionIssue? Evaluate(
        ReadOnlySpan<byte> bytes,
        CompiledUniformInputRangeValidation requirement)
    {
        foreach (ByteRange range in requirement.Ranges)
        {
            if (range.EndExclusive > bytes.Length)
            {
                return Issue(requirement,
                    $"Input validation range {range} is outside address space '{requirement.AddressSpaceId}'.");
            }

            ReadOnlySpan<byte> candidate = bytes.Slice(checked((int)range.Start), checked((int)range.Length));
            if (candidate[1..].IndexOfAnyExcept(candidate[0]) < 0)
            {
                return Issue(requirement,
                    $"Input range {range} in '{requirement.AddressSpaceId}' is a repeated-byte placeholder and cannot be used.");
            }
        }

        return null;
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
