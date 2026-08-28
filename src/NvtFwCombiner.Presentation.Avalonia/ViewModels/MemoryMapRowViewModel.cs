namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum MemoryPlanSourceKind
{
    NoOutput,
    Reserved,
    Unmapped,
    BaseFirmware,
    Output,
    DpBin,
    DpReplacementBin,
    TpBin,
    Tpb,
    LdcBin,
    LdcReplacementBin,
    CtrlRamBin,
    DpAb,
    Tpa,
    OverlapError,
    Technical,
    Localized,
}

internal readonly record struct MemoryPlanSource(
    MemoryPlanSourceKind Kind,
    string? DisplayText = null);

internal enum MemoryPlanActionKind
{
    Browse,
    Blocked,
    Restore,
    TransformAndOverlay,
    Postbuild,
    Copy,
    ReplaceAndCrc,
    Replace,
    Preserve,
    Initialize,
    Overlay,
    Project,
}

internal enum MemoryPlanDetailKind
{
    ProtectedCustomerInformationFromDp,
    ProtectedCustomerInformationFromDpReplacement,
    ReservedUnwritten,
    Unmapped,
    CopiedFromDp,
    OverlaidFromTp,
}

/// <summary>One readable before/after memory-map row shown on Merge and Replace pages.</summary>
internal sealed class MemoryMapRowViewModel
{
    public MemoryMapRowViewModel(
        string rangeLabel,
        MemoryPlanSource beforeSource,
        MemoryPlanActionKind action,
        MemoryPlanSource afterSource,
        string detail,
        ShellTextResources text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentNullException.ThrowIfNull(text);

        RangeLabel = rangeLabel;
        BeforeSourceFact = beforeSource;
        Action = action;
        AfterSourceFact = afterSource;
        BeforeSource = text.GetMemoryPlanSourceLabel(beforeSource);
        ActionLabel = text.GetMemoryPlanActionLabel(action);
        AfterSource = text.GetMemoryPlanSourceLabel(afterSource);
        PrimaryLabel = ToPrimaryLabel(AfterSource);
        Detail = detail;
    }

    /// <summary>Address range or symbolic range label for the displayed plan row.</summary>
    public string RangeLabel { get; }

    /// <summary>Source or state before the workflow operation.</summary>
    public string BeforeSource { get; }

    public MemoryPlanSource BeforeSourceFact { get; }

    /// <summary>Short operation label such as Copy, Replace, or Preserve.</summary>
    public string ActionLabel { get; }

    public MemoryPlanActionKind Action { get; }

    /// <summary>Source or state after the workflow operation.</summary>
    public string AfterSource { get; }

    public MemoryPlanSource AfterSourceFact { get; }

    public string PrimaryLabel { get; }

    /// <summary>Compact before/after source summary.</summary>
    public string FlowLabel => $"{BeforeSource} -> {AfterSource}";

    /// <summary>Short evidence or policy note.</summary>
    public string Detail { get; }

    private static string ToPrimaryLabel(string source)
    {
        string label = Path.GetFileNameWithoutExtension(source)
            .Replace('_', ' ')
            .Replace('-', ' ');
        return label.Replace("Ctrlram", "CtrlRAM", StringComparison.OrdinalIgnoreCase);
    }
}
