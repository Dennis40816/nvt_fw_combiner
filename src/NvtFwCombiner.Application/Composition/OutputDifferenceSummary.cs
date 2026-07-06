using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Run-level final-output difference row comparing a Replace output with its reference base.</summary>
public sealed class OutputDifferenceSummary
{
    /// <summary>Creates an output difference summary.</summary>
    public OutputDifferenceSummary(
        string differenceId,
        ByteRange range,
        long changedByteCount,
        string classification,
        bool isAccepted,
        string evidence,
        string explanation,
        string beforeSha256,
        string afterSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(differenceId);
        ArgumentOutOfRangeException.ThrowIfNegative(changedByteCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(classification);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(afterSha256);

        DifferenceId = differenceId;
        Range = range;
        ChangedByteCount = changedByteCount;
        Classification = classification;
        IsAccepted = isAccepted;
        Evidence = evidence;
        Explanation = explanation;
        BeforeSha256 = beforeSha256;
        AfterSha256 = afterSha256;
    }

    /// <summary>Stable row id in report order.</summary>
    public string DifferenceId { get; }

    /// <summary>Final-image range that differs from the Replace reference base.</summary>
    public ByteRange Range { get; }

    /// <summary>Changed byte count inside <see cref="Range" />.</summary>
    public long ChangedByteCount { get; }

    /// <summary>Machine-readable classification such as DeclaredReplacement or PostbuildCrcHeader.</summary>
    public string Classification { get; }

    /// <summary>True when the difference is declared by the profile or by IC-number-specific postbuild CRC/header ranges.</summary>
    public bool IsAccepted { get; }

    /// <summary>Operation or processor evidence that authorizes this difference.</summary>
    public string Evidence { get; }

    /// <summary>Readable explanation shown in report review surfaces.</summary>
    public string Explanation { get; }

    /// <summary>SHA-256 of the reference-base bytes inside <see cref="Range" />.</summary>
    public string BeforeSha256 { get; }

    /// <summary>SHA-256 of the final-output bytes inside <see cref="Range" />.</summary>
    public string AfterSha256 { get; }
}
