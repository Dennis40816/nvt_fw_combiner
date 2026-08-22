// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string BinInspectorTitle { get; private init; } = string.Empty;

    public string BinInspectorViewportTitle { get; private init; } = string.Empty;

    public string BinInspectorRangeScrollAutomationName { get; private init; } = string.Empty;

    public string BinInspectorResizeAutomationName { get; private init; } = string.Empty;

    public string BinInspectorStructuresTitle { get; private init; } = string.Empty;

    public string BinInspectorFieldsTitle { get; private init; } = string.Empty;

    public string BinInspectorNoFieldsLabel { get; private init; } = string.Empty;

    public string BinInspectorNoByteSelectedLabel { get; private init; } = string.Empty;

    public string BinInspectorNoFieldLabel { get; private init; } = string.Empty;

    public string BinInspectorSelectedByteFormat { get; private init; } = string.Empty;
}

#pragma warning restore CS1591
