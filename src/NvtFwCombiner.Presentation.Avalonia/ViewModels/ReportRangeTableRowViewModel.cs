namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One table row for operation source/target and processor read/write ranges.</summary>
internal sealed record ReportRangeTableRowViewModel(
    string Kind,
    string AddressSpace,
    string Range,
    string Source);
