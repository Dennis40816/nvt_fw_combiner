namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Focused counts from one canonical capability publication.</summary>
public sealed record CapabilityCatalogSummary(
    int CatalogIcCount,
    int StandardMergeProfileCount,
    int DpReplaceProfileCount,
    int CtrlRamReplaceAvailableIcCount);
