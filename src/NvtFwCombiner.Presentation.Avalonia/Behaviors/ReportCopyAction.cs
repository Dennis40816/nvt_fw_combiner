using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Behaviors;

/// <summary>Shares the failure-safe clipboard event between Report text actions.</summary>
public sealed class ReportCopyAction : AvaloniaObject
{
    /// <summary>The complete, unmodified text copied by the attached button.</summary>
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<ReportCopyAction, Button, string?>("Text");

    static ReportCopyAction()
    {
        _ = TextProperty.Changed.AddClassHandler<Button>(OnTextChanged);
    }

    private ReportCopyAction()
    {
    }

    /// <summary>Gets the complete text to copy.</summary>
    public static string? GetText(AvaloniaObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(TextProperty);
    }

    /// <summary>Sets the complete text to copy.</summary>
    public static void SetText(AvaloniaObject element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        _ = element.SetValue(TextProperty, value);
    }

    private static void OnTextChanged(Button button, AvaloniaPropertyChangedEventArgs e)
    {
        button.Click -= CopyButton_OnClick;
        if (e.NewValue is string)
        {
            button.Click += CopyButton_OnClick;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "An optional OS clipboard operation must not escape the async UI event; failure is reported through the existing shell toast.")]
    private static async void CopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || GetText(button) is not { } text || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(button);
        try
        {
            if (topLevel?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
                return;
            }
        }
        catch (Exception)
        {
            // A missing or busy clipboard has the same recoverable UI outcome.
        }

        if (topLevel?.DataContext is MainWindowViewModel shell)
        {
            shell.Reports.SetShellToast(shell.Text.ReportCopyFailedTitle, shell.Text.ReportCopyFailedDetail);
        }
    }
}
