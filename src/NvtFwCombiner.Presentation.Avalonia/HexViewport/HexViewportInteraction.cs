using Avalonia;

namespace NvtFwCombiner.Presentation.Avalonia.HexViewport;

internal enum HexViewportInteractionTrigger
{
    Select,
    Activate,
    Context,
    StructuralContext,
    MoveSelection,
    Scroll,
}

/// <summary>Source-neutral interaction emitted by the always-read-only viewport.</summary>
internal readonly record struct HexViewportInteractionIntent(
    HexViewportInteractionTrigger Trigger,
    long? Address,
    Rect Bounds,
    int Delta = 0,
    int StructuralBlockIndex = -1);

internal sealed class HexViewportInteractionEventArgs(HexViewportInteractionIntent intent) : EventArgs
{
    public HexViewportInteractionIntent Intent { get; } = intent;
}
