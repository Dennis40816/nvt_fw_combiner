using Avalonia;
using Avalonia.Controls;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Content container with safe padding even when no application style is loaded.</summary>
public sealed class SpaciousPanel : Border
{
    static SpaciousPanel()
    {
        PaddingProperty.OverrideDefaultValue<SpaciousPanel>(new Thickness(18, 16, 20, 18));
    }
}
