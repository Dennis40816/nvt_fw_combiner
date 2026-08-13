using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum MemoryCoverageFillRole
{
    Neutral,
    Dp,
    /// <summary>Normal touch firmware.</summary>
    Tp,
    /// <summary>Backup touch firmware.</summary>
    TpBackup,
    /// <summary>LDC firmware.</summary>
    Ldc,
    /// <summary>General source or vector data.</summary>
    Source,
    /// <summary>Reference-owned bytes that remain preserved.</summary>
    Kept,
    Conflict,
    /// <summary>CtrlRAM without a more specific reviewed subtype.</summary>
    CtrlRam,
    DiffDlm,
}

internal sealed class MemoryCoverageSegmentViewModel
{
    public MemoryCoverageSegmentViewModel(
        string rangeLabel,
        string sourceLabel,
        string detail,
        MemoryCoverageFillRole fillRole,
        double barWidth,
        bool isChanged = false,
        bool usesBaseFirmwarePattern = false,
        string? regionId = null,
        bool isDiffDlm = false,
        IReadOnlyList<MemoryLayoutPreservationDetail>? preservationDetails = null,
        ShellTextResources? text = null,
        ReplaceRegionGroup regionGroup = ReplaceRegionGroup.Common)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        if (!Enum.IsDefined(fillRole))
        {
            throw new ArgumentOutOfRangeException(nameof(fillRole));
        }

        string displaySourceLabel = NormalizeSourceLabel(sourceLabel);

        RangeLabel = rangeLabel;
        SourceLabel = displaySourceLabel;
        Detail = detail;
        CompactDetail = CreateCompactDetail(displaySourceLabel, isChanged);
        FillRole = fillRole;
        BarWidth = barWidth;
        IsChanged = isChanged;
        UsesBaseFirmwarePattern = usesBaseFirmwarePattern;
        RegionId = string.IsNullOrWhiteSpace(regionId) ? null : regionId;
        text ??= ShellTextResources.For(ShellLanguage.English);
        ChangeLabel = isChanged
            ? text.OutputLayoutChangedStateLabel
            : text.OutputLayoutKeptStateLabel;
        IsDiffDlm = isDiffDlm;
        RegionGroup = regionGroup;
        PreservationDetails =
        [
            .. (preservationDetails ?? []).Select(detail =>
                new DiffDlmPreservationDetailViewModel(detail, text)),
        ];
        PreservationSummary = PreservationDetails.Count > 0
            ? text.FormatDiffDlmPreservationSummary(PreservationDetails.Count)
            : isDiffDlm
                ? text.FormatEntireDiffDlmSummary()
                : CompactDetail;
        DetailsLabel = text.FormatDiffDlmDetailsLabel();
        string preservationAccessibility = string.Join(
            ". ",
            PreservationDetails.Select(item =>
                $"{item.IcLabel}, {item.BlockLabel}, {item.ArtifactRangeLabel}, {item.FlashRangeLabel}, {item.DispositionLabel}"));
        AccessibleDetail = string.IsNullOrEmpty(preservationAccessibility)
            ? $"{SourceLabel}. {RangeLabel}. {PreservationSummary}. {Detail}"
            : $"{SourceLabel}. {RangeLabel}. {PreservationSummary}. {preservationAccessibility}. {Detail}";
    }

    /// <summary>Address range in half-open hex notation.</summary>
    public string RangeLabel { get; }

    /// <summary>Final source occupying this range.</summary>
    public string SourceLabel { get; }

    public string Detail { get; }

    public string CompactDetail { get; }

    public MemoryCoverageFillRole FillRole { get; }

    public double BarWidth { get; }

    public bool IsChanged { get; }

    /// <summary>True when retained base-firmware bytes need a non-color visual pattern.</summary>
    public bool UsesBaseFirmwarePattern { get; }

    public bool UsesKeptPattern =>
        UsesBaseFirmwarePattern || (RegionId is not null && !IsChanged);

    /// <summary>Profile-owned selection identity for a replaceable physical region, when present.</summary>
    public string? RegionId { get; }

    public string ChangeLabel { get; }

    public bool IsDiffDlm { get; }

    /// <summary>Typed Replace grouping supplied before localized display text.</summary>
    public ReplaceRegionGroup RegionGroup { get; }

    public IReadOnlyList<DiffDlmPreservationDetailViewModel> PreservationDetails { get; }

    public bool HasPreservationDetails => PreservationDetails.Count > 0;

    public string PreservationSummary { get; }

    public string DetailsLabel { get; }

    public string AccessibleDetail { get; }

    private static string CreateCompactDetail(string sourceLabel, bool isChanged)
    {
        return sourceLabel switch
        {
            "Reserved" => "Output range remains reserved; no input writes it.",
            "DP length pending" => "Output range will follow the selected DP BIN length.",
            "Reference FlashCode required" => "Output range will follow the selected Reference FlashCode length.",
            "Unsupported reference" => "This Reference FlashCode length is blocked by profile policy.",
            string label when label.Equals("Preserve", StringComparison.OrdinalIgnoreCase) ||
                label.Equals("Base flash", StringComparison.OrdinalIgnoreCase) =>
                "Output range keeps bytes from the base firmware.",
            string label when label.Contains("Restored", StringComparison.OrdinalIgnoreCase) =>
                "Output range restores bytes from the base firmware.",
            _ => isChanged
                ? $"Output range is written from {FormatSourcePhrase(sourceLabel)}."
                : $"Output range uses bytes from {FormatSourcePhrase(sourceLabel)}.",
        };
    }

    private static string FormatSourcePhrase(string sourceLabel)
    {
        return sourceLabel.StartsWith("Changed ", StringComparison.OrdinalIgnoreCase)
            ? sourceLabel["Changed ".Length..]
            : sourceLabel;
    }

    private static string NormalizeSourceLabel(string sourceLabel)
    {
        return sourceLabel.StartsWith("Blank", StringComparison.OrdinalIgnoreCase)
            ? "Reserved"
            : sourceLabel;
    }
}

/// <summary>Localized display-only projection of one canonical kept Diff NF range.</summary>
internal sealed class DiffDlmPreservationDetailViewModel
{
    internal DiffDlmPreservationDetailViewModel(
        MemoryLayoutPreservationDetail detail,
        ShellTextResources text)
    {
        IcLabel = text.FormatDiffDlmIcLabel(detail.BlockIndex);
        BlockLabel = text.FormatDiffDlmBlockLabel(detail.BlockIndex);
        ArtifactRangeLabel = text.FormatDiffDlmArtifactRangeLabel(
            detail.SourceSpaceId,
            FormatRange(
                detail.ArtifactRelativeRange.Start,
                detail.ArtifactRelativeRange.EndExclusive));
        FlashRangeLabel = text.FormatDiffDlmFlashRangeLabel(FormatRange(
            detail.ResolvedRange.Start,
            detail.ResolvedRange.EndExclusive));
        DispositionLabel = text.FormatDiffDlmKeptDisposition();
    }

    /// <summary>One-based physical IC label.</summary>
    public string IcLabel { get; }
    public string BlockLabel { get; }
    /// <summary>Source-artifact identity and half-open relative range.</summary>
    public string ArtifactRangeLabel { get; }
    /// <summary>Half-open resolved Flash range.</summary>
    public string FlashRangeLabel { get; }
    public string DispositionLabel { get; }

    private static string FormatRange(long start, long endExclusive)
    {
        return FormattableString.Invariant($"[0x{start:X}, 0x{endExclusive:X})");
    }
}
