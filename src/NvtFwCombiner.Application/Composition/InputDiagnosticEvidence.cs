using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Path-free, compiler-owned evidence for one input issue retained by Preview and Build reports.</summary>
public sealed record InputDiagnosticEvidence
{
    /// <summary>Creates immutable evidence without retaining source paths or source content.</summary>
    public InputDiagnosticEvidence(
        string addressSpaceId,
        long? actualLength,
        long? requiredEndExclusive,
        ByteRange? sourceRange,
        byte? repeatedByte)
    {
        AddressSpaceId = CompositionSummaryValue.NotBlank(addressSpaceId, nameof(addressSpaceId));
        if (actualLength is { } actual)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(actual, nameof(actualLength));
        }

        if (requiredEndExclusive is { } required)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(required, nameof(requiredEndExclusive));
        }

        if (sourceRange is { Length: <= 0 })
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRange));
        }

        if (repeatedByte.HasValue && !sourceRange.HasValue)
        {
            throw new ArgumentException(
                "A repeated-byte diagnostic requires its exact declared source range.",
                nameof(repeatedByte));
        }

        if (actualLength is null && requiredEndExclusive is null &&
            sourceRange is null && repeatedByte is null)
        {
            throw new ArgumentException("Input diagnostic evidence must contain one fact.", nameof(actualLength));
        }

        ActualLength = actualLength;
        RequiredEndExclusive = requiredEndExclusive;
        SourceRange = sourceRange;
        RepeatedByte = repeatedByte;
    }

    /// <summary>Compiler-declared immutable input address space.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Observed source length when the finding depends on coverage.</summary>
    public long? ActualLength { get; }

    /// <summary>Required half-open end when the finding depends on source coverage.</summary>
    public long? RequiredEndExclusive { get; }

    /// <summary>Exact compiler-declared half-open range when one exists.</summary>
    public ByteRange? SourceRange { get; }

    /// <summary>One repeated byte observed in <see cref="SourceRange"/>, never source contents.</summary>
    public byte? RepeatedByte { get; }
}

/// <summary>One input diagnostic bound by index to the final report issue list.</summary>
public sealed record InputDiagnosticSummary
{
    /// <summary>Creates one report entry with an unambiguous final issue index.</summary>
    public InputDiagnosticSummary(int issueIndex, string slotId, InputDiagnosticEvidence evidence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(issueIndex);

        IssueIndex = issueIndex;
        SlotId = CompositionSummaryValue.NotBlank(slotId, nameof(slotId));
        ArgumentNullException.ThrowIfNull(evidence);
        Evidence = evidence;
    }

    /// <summary>Zero-based index into the same report's final <c>Issues</c> collection.</summary>
    public int IssueIndex { get; }

    /// <summary>Compiler-declared input slot associated with the issue.</summary>
    public string SlotId { get; }

    /// <summary>Immutable path-free diagnostic evidence.</summary>
    public InputDiagnosticEvidence Evidence { get; }
}
