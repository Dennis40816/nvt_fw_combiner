using NvtFwCombiner.Domain.Composition;
using System.Text.Json.Serialization;

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
        string sectionLabel,
        string beforeSha256,
        string afterSha256,
        string beforeHexPreview = "",
        string afterHexPreview = "",
        int hexPreviewByteCount = 0,
        bool isHexPreviewComplete = false,
        OutputDifferenceSemantic? semantic = null,
        OutputDifferenceReplaySegment? replay = null)
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
        SectionLabel = sectionLabel ?? string.Empty;
        BeforeSha256 = beforeSha256;
        AfterSha256 = afterSha256;
        BeforeHexPreview = beforeHexPreview ?? string.Empty;
        AfterHexPreview = afterHexPreview ?? string.Empty;
        HexPreviewByteCount = hexPreviewByteCount;
        IsHexPreviewComplete = isHexPreviewComplete;
        Semantic = semantic;
        if (replay is not null && !replay.Range.Contains(range))
        {
            throw new ArgumentException(
                "Replay bytes must contain the complete output difference range.",
                nameof(replay));
        }

        Replay = replay;
    }

    /// <summary>Stable row id in report order.</summary>
    public string DifferenceId { get; }

    /// <summary>Final-image range that differs from the Replace reference base.</summary>
    public ByteRange Range { get; }

    /// <summary>Changed byte count inside <see cref="Range" />.</summary>
    public long ChangedByteCount { get; }

    /// <summary>Machine-readable classification from <c>OutputDifferenceClassifications</c>.</summary>
    public string Classification { get; }

    /// <summary>True when the difference is declared by the profile or by IC-number-specific postbuild CRC/header ranges.</summary>
    public bool IsAccepted { get; }

    /// <summary>Operation or processor evidence that authorizes this difference.</summary>
    public string Evidence { get; }

    /// <summary>Readable explanation shown in report review surfaces.</summary>
    public string Explanation { get; }

    /// <summary>Human-readable output section label such as Header / CRC refresh or NF CtrlRAM.</summary>
    public string SectionLabel { get; }

    /// <summary>SHA-256 of the reference-base bytes inside <see cref="Range" />.</summary>
    public string BeforeSha256 { get; }

    /// <summary>SHA-256 of the final-output bytes inside <see cref="Range" />.</summary>
    public string AfterSha256 { get; }

    /// <summary>Hex preview of the reference-base bytes inside <see cref="Range" />.</summary>
    public string BeforeHexPreview { get; }

    /// <summary>Hex preview of the final-output bytes inside <see cref="Range" />.</summary>
    public string AfterHexPreview { get; }

    /// <summary>Number of bytes included in each hex preview.</summary>
    public int HexPreviewByteCount { get; }

    /// <summary>True when the hex preview covers the whole difference range.</summary>
    public bool IsHexPreviewComplete { get; }

    /// <summary>
    /// Application-owned category and field subject for novice-first report rendering. Older report files can omit it.
    /// </summary>
    public OutputDifferenceSemantic? Semantic { get; }

    /// <summary>Exact persisted bytes sufficient to replay this range; absent in legacy reports.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OutputDifferenceReplaySegment? Replay { get; }
}
