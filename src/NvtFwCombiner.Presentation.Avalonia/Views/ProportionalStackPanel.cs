using Avalonia;
using Avalonia.Controls;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Arranges children across the available width by a positive display weight.</summary>
public sealed class ProportionalStackPanel : Panel
{
    /// <summary>Display-only proportional weight assigned to a child.</summary>
    public static readonly AttachedProperty<double> WeightProperty =
        AvaloniaProperty.RegisterAttached<ProportionalStackPanel, Control, double>(
            "Weight",
            defaultValue: 1d,
            validate: static value => double.IsFinite(value) && value >= 0d);

    /// <summary>Gets the display weight assigned to a child.</summary>
    public static double GetWeight(AvaloniaObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(WeightProperty);
    }

    /// <summary>Sets the display weight assigned to a child.</summary>
    public static void SetWeight(AvaloniaObject element, double value)
    {
        ArgumentNullException.ThrowIfNull(element);
        _ = element.SetValue(WeightProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double desiredWidth = 0d;
        double desiredHeight = 0d;
        foreach (Control child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            desiredWidth += child.DesiredSize.Width;
            desiredHeight = Math.Max(desiredHeight, child.DesiredSize.Height);
        }

        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : desiredWidth,
            double.IsFinite(availableSize.Height) ? availableSize.Height : desiredHeight);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        double totalWeight = Children.Sum(GetWeight);
        if (totalWeight <= 0d)
        {
            foreach (Control child in Children)
            {
                child.Arrange(new Rect(0d, 0d, 0d, finalSize.Height));
            }

            return finalSize;
        }

        double x = 0d;
        for (int index = 0; index < Children.Count; index++)
        {
            Control child = Children[index];
            double width = index == Children.Count - 1
                ? Math.Max(0d, finalSize.Width - x)
                : finalSize.Width * GetWeight(child) / totalWeight;
            child.Arrange(new Rect(x, 0d, width, finalSize.Height));
            x += width;
        }

        return finalSize;
    }
}
