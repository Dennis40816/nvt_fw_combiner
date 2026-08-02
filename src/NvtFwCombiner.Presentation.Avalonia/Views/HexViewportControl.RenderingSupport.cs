using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class HexViewportControl
{
    private static string FormatReferenceLabel(long address)
    {
        return FormattableString.Invariant($"0x{address:X6}  orig");
    }

    private static void DrawRoundedRectangle(
        DrawingContext context,
        IBrush? brush,
        IPen? pen,
        Rect rect,
        double radius)
    {
        context.DrawRectangle(brush, pen, rect, radius, radius, default);
    }

    private static void DrawText(
        DrawingContext context,
        string value,
        IBrush brush,
        Typeface typeface,
        double x,
        double y)
    {
        FormattedText text = CreateText(value, brush, typeface);
        context.DrawText(text, new Point(x, y + ((RowHeight - text.Height) / 2)));
    }

    private static FormattedText[] CreateHexTextCache(IBrush brush, Typeface typeface)
    {
        var result = new FormattedText[256];
        for (int value = 0; value < result.Length; value++)
        {
            result[value] = CreateText(value.ToString("X2", CultureInfo.InvariantCulture), brush, typeface);
        }

        return result;
    }

    private static FormattedText[] CreateAsciiTextCache(IBrush brush, Typeface typeface)
    {
        var result = new FormattedText[128];
        for (int value = 0; value < result.Length; value++)
        {
            char character = value is >= 0x20 and <= 0x7E ? (char)value : '.';
            result[value] = CreateText(character.ToString(), brush, typeface);
        }

        return result;
    }

    private static FormattedText CreateText(string value, IBrush brush, Typeface typeface)
    {
        return new FormattedText(
            value,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            13,
            brush);
    }

    private static FormattedText CreateBoundaryText(string value)
    {
        return new FormattedText(
            value,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            StrongTypeface,
            9,
            StructuralTextBrush);
    }
}
