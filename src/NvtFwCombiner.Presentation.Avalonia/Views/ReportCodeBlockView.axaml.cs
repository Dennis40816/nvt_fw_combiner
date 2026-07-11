using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Read-only command plan block with a clipboard action.</summary>
public sealed partial class ReportCodeBlockView : UserControl
{
    /// <summary>Defines the read-only text shown and copied by this view.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ReportCodeBlockView, string>(nameof(Text), string.Empty);

    /// <summary>Initializes the command plan view.</summary>
    public ReportCodeBlockView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the read-only text shown and copied by this view.</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private async void CopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Text) ||
            TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(Text);
    }
}
