using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Single-line report cell; exposes full text only when the renderer actually elides it.</summary>
public sealed class ReportTableText : TextBlock
{
    /// <summary>Initializes bounded centered text and an overflow-only tooltip.</summary>
    public ReportTableText()
    {
        TextTrimming = TextTrimming.CharacterEllipsis;
        TextWrapping = TextWrapping.NoWrap;
        TextAlignment = TextAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        LayoutUpdated += (_, _) =>
        {
            string? tip = TextLayout.TextLines.Any(line => line.HasCollapsed) ? Text : null;
            if (!Equals(ToolTip.GetTip(this), tip))
            {
                ToolTip.SetTip(this, tip);
            }
        };
    }
}
