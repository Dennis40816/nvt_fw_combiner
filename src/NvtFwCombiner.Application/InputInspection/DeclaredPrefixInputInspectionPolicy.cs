using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>
/// Immutable declared-prefix input policy awaiting projection from a compiler-owned artifact.
/// Presentation and Bootstrap must never construct this policy from an IC label or filename.
/// </summary>
internal sealed class DeclaredPrefixInputInspectionPolicy
{
    private readonly long[] _expectedOuterLengths;

    /// <summary>Creates a checked policy for one immutable declared-prefix source.</summary>
    internal DeclaredPrefixInputInspectionPolicy(
        long requiredEndExclusive,
        IEnumerable<long> expectedOuterLengths,
        string shortInputIssueCode,
        string unexpectedOuterLengthIssueCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredEndExclusive);
        if (requiredEndExclusive > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredEndExclusive),
                requiredEndExclusive,
                "The in-memory inspection snapshot cannot exceed the runtime array limit.");
        }

        ArgumentNullException.ThrowIfNull(expectedOuterLengths);
        _expectedOuterLengths = [.. expectedOuterLengths];
        if (_expectedOuterLengths.Length is 0 or > InputLengthPolicyLimits.MaximumExpectedInputLengths)
        {
            throw new ArgumentException(
                $"Expected outer lengths must contain between 1 and {InputLengthPolicyLimits.MaximumExpectedInputLengths} values.",
                nameof(expectedOuterLengths));
        }

        long previous = 0;
        for (int index = 0; index < _expectedOuterLengths.Length; index++)
        {
            long value = _expectedOuterLengths[index];
            if (value < requiredEndExclusive ||
                value > int.MaxValue ||
                (index > 0 && value <= previous))
            {
                throw new ArgumentException(
                    "Expected outer lengths must fit the runtime array limit, be at least the required end, and be strictly ascending.",
                    nameof(expectedOuterLengths));
            }

            previous = value;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(shortInputIssueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(unexpectedOuterLengthIssueCode);

        RequiredEndExclusive = requiredEndExclusive;
        ExpectedOuterLengths = Array.AsReadOnly(_expectedOuterLengths);
        ShortInputIssueCode = shortInputIssueCode;
        UnexpectedOuterLengthIssueCode = unexpectedOuterLengthIssueCode;
    }

    /// <summary>First unavailable byte that makes a shorter source blocking.</summary>
    internal long RequiredEndExclusive { get; }

    /// <summary>Known complete source lengths that do not require an outer-length warning.</summary>
    internal IReadOnlyList<long> ExpectedOuterLengths { get; }

    /// <summary>Compiler-owned stable issue code for a source shorter than the required prefix.</summary>
    internal string ShortInputIssueCode { get; }

    /// <summary>Compiler-owned stable issue code for an unexpected accepted outer length.</summary>
    internal string UnexpectedOuterLengthIssueCode { get; }
}
