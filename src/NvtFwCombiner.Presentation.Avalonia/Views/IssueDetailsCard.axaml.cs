using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Shared, noninteractive tooltip surface for typed issue explanations.</summary>
public sealed partial class IssueDetailsCard : UserControl
{
    /// <summary>Creates the compact issue surface.</summary>
    public IssueDetailsCard()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
