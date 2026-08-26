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
    /// <summary>NF CtrlRAM.</summary>
    CtrlRamNf,
    /// <summary>Normal CtrlRAM.</summary>
    CtrlRamNormal,
    /// <summary>MP CtrlRAM.</summary>
    CtrlRamMp,
    /// <summary>VN CtrlRAM.</summary>
    CtrlRamVn,
    /// <summary>Vector CtrlRAM.</summary>
    CtrlRamVector,
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
        MemoryWorkflowDisposition disposition = MemoryWorkflowDisposition.Resolved,
        MemoryObservedChange observedChange = MemoryObservedChange.NotObserved,
        MemoryDiagnosticSeverity diagnosticSeverity = MemoryDiagnosticSeverity.None,
        bool usesBaseFirmwarePattern = false,
        string? regionId = null,
        string? sourceSlotId = null,
        string? logicalSourceLabel = null,
        IReadOnlyList<MemoryLayoutPreservationDetail>? preservationDetails = null,
        ShellTextResources? text = null,
        ReplaceRegionGroup regionGroup = ReplaceRegionGroup.Common,
        long? rangeStart = null,
        long? rangeEndExclusive = null,
        string? addressRangeLabel = null,
        string? lengthLabel = null,
        string? compactDetail = null,
        string? changeLabel = null,
        string? logicalCoverageGroupId = null,
        MemoryContentRole contentRole = MemoryContentRole.General,
        CtrlRamRegionRole ctrlRamRegionRole = CtrlRamRegionRole.Other)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        if (!Enum.IsDefined(fillRole))
        {
            throw new ArgumentOutOfRangeException(nameof(fillRole));
        }
        if (!Enum.IsDefined(diagnosticSeverity))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnosticSeverity));
        }
        if (!Enum.IsDefined(contentRole))
        {
            throw new ArgumentOutOfRangeException(nameof(contentRole));
        }
        if (!Enum.IsDefined(ctrlRamRegionRole))
        {
            throw new ArgumentOutOfRangeException(nameof(ctrlRamRegionRole));
        }

        if (addressRangeLabel is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(addressRangeLabel);
        }
        if (compactDetail is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(compactDetail);
        }
        if (logicalSourceLabel is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logicalSourceLabel);
        }
        if (logicalCoverageGroupId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logicalCoverageGroupId);
        }
        if (rangeStart.HasValue != rangeEndExclusive.HasValue ||
            (rangeStart is { } start && rangeEndExclusive <= start))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rangeEndExclusive),
                "A typed display range requires start < endExclusive.");
        }

        RangeLabel = rangeLabel;
        AddressRangeLabel = addressRangeLabel ?? rangeLabel;
        LengthLabel = lengthLabel ?? string.Empty;
        SourceLabel = sourceLabel;
        LogicalSourceLabel = logicalSourceLabel ?? sourceLabel;
        Detail = detail;
        CompactDetail = compactDetail ?? detail;
        FillRole = fillRole;
        BarWidth = barWidth;
        Disposition = disposition;
        ObservedChange = observedChange;
        DiagnosticSeverity = diagnosticSeverity;
        RegionId = string.IsNullOrWhiteSpace(regionId) ? null : regionId;
        LogicalCoverageGroupId = logicalCoverageGroupId;
        SourceSlotId = string.IsNullOrWhiteSpace(sourceSlotId) ? null : sourceSlotId;
        ContentRole = contentRole;
        CtrlRamRegionRole = ctrlRamRegionRole;
        UsesKeptPattern = usesBaseFirmwarePattern;
        text ??= ShellTextResources.For(ShellLanguage.English);
        ChangeLabel = changeLabel ?? text.GetOutputLayoutStateLabel(disposition, observedChange);
        RegionGroup = regionGroup;
        RegionGroupLabel = text.GetReplaceRegionGroupTitle(regionGroup);
        RangeStart = rangeStart;
        RangeEndExclusive = rangeEndExclusive;
        PreservationDetails =
        [
            .. (preservationDetails ?? []).Select(detail =>
                new DiffDlmPreservationDetailViewModel(detail, text)),
        ];
        PreservationSummary = PreservationDetails.Count > 0
            ? text.FormatDiffDlmPreservationSummary(PreservationDetails.Count)
            : fillRole == MemoryCoverageFillRole.DiffDlm
                ? text.FormatEntireDiffDlmSummary()
                : CompactDetail;
        DetailsLabel = text.FormatDiffDlmDetailsLabel();
        string preservationAccessibility = string.Join(
            ". ",
            PreservationDetails.Select(item =>
                $"{item.IcLabel}, {item.BlockLabel}, {item.ArtifactRangeLabel}, {item.FlashRangeLabel}, {item.DispositionLabel}"));
        string stateAccessibility = HasChangeState ? $"{ChangeLabel}. " : string.Empty;
        AccessibleDetail = string.IsNullOrEmpty(preservationAccessibility)
            ? $"{SourceLabel}. {RangeLabel}. {stateAccessibility}{PreservationSummary}. {Detail}"
            : $"{SourceLabel}. {RangeLabel}. {stateAccessibility}{PreservationSummary}. {preservationAccessibility}. {Detail}";
    }

    /// <summary>Shared display-only interaction state for a row and its proportional segments.</summary>
    public MemoryCoverageInteractionState Interaction { get; internal set; } = new();

    /// <summary>Address range in half-open hex notation.</summary>
    public string RangeLabel { get; }

    /// <summary>Inclusive display range without the length suffix.</summary>
    public string AddressRangeLabel { get; }

    /// <summary>Display length kept in a separately aligned column.</summary>
    public string LengthLabel { get; }

    /// <summary>Final source occupying this range.</summary>
    public string SourceLabel { get; }

    /// <summary>Topology-neutral source identity used by a cross-group logical item.</summary>
    public string LogicalSourceLabel { get; }

    public string Detail { get; }

    public string CompactDetail { get; }

    public MemoryCoverageFillRole FillRole { get; }

    public double BarWidth { get; }

    public MemoryWorkflowDisposition Disposition { get; }

    public MemoryObservedChange ObservedChange { get; }

    public MemoryDiagnosticSeverity DiagnosticSeverity { get; }

    public bool IsChanged => ObservedChange == MemoryObservedChange.Changed;

    public bool IsSelectedForWrite => Disposition is
        MemoryWorkflowDisposition.WillWrite or
        MemoryWorkflowDisposition.WillReplace or
        MemoryWorkflowDisposition.DpAbBase or
        MemoryWorkflowDisposition.TpaOverlay or
        MemoryWorkflowDisposition.TpbOverlay;

    public bool HasAttentionDiagnostic => DiagnosticSeverity is
        MemoryDiagnosticSeverity.Warning or MemoryDiagnosticSeverity.Error;

    /// <summary>True when retained base-firmware bytes need a non-color visual pattern.</summary>
    public bool UsesKeptPattern { get; }

    /// <summary>Profile-owned selection identity for a replaceable physical region, when present.</summary>
    public string? RegionId { get; }

    /// <summary>Application-owned final identity for logical information grouping.</summary>
    public string? LogicalCoverageGroupId { get; }

    /// <summary>Typed canonical input slot supplying this range, when one exists.</summary>
    public string? SourceSlotId { get; }

    /// <summary>Application-owned physical/logical content role used only for compatible row coalescing.</summary>
    public MemoryContentRole ContentRole { get; }

    /// <summary>Application-owned CtrlRAM subtype used only for compatible row coalescing.</summary>
    public CtrlRamRegionRole CtrlRamRegionRole { get; }

    public string ChangeLabel { get; }

    /// <summary>Whether changed/kept state applies to this written or retained range.</summary>
    public bool HasChangeState => ChangeLabel.Length > 0;

    /// <summary>Typed Replace grouping supplied before localized display text.</summary>
    public ReplaceRegionGroup RegionGroup { get; }

    /// <summary>Localized endpoint/group context shown beside a compact physical range.</summary>
    public string RegionGroupLabel { get; }

    public long? RangeStart { get; }

    public long? RangeEndExclusive { get; }

    public IReadOnlyList<DiffDlmPreservationDetailViewModel> PreservationDetails { get; }

    public bool HasPreservationDetails => PreservationDetails.Count > 0;

    public string PreservationSummary { get; }

    public string DetailsLabel { get; }

    public string AccessibleDetail { get; }

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
