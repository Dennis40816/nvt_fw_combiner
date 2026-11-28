// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class IssueList
{
    private void Rebuild()
    {
        if (_parts is not { } parts)
        {
            return;
        }
        Button[] oldRows = [.. parts.Rows.Children.OfType<Button>()];
        int focusedIndex = Array.FindIndex(oldRows, row => row.IsFocused);
        string? focusedKey = focusedIndex < 0 ? null : oldRows[focusedIndex].CommandParameter as string;
        bool keyboardFocus = focusedIndex >= 0 && oldRows[focusedIndex].Classes.Contains(":focus-visible");
        parts.Title.Text = Text.Title;
        AutomationProperties.SetName(this, Text.Title);
        BuildSummary(parts.Errors, IssueListSeverity.Error, Model?.ErrorCount ?? 0);
        BuildSummary(parts.Warnings, IssueListSeverity.Warning, Model?.WarningCount ?? 0);
        parts.Rows.Children.Clear();
        // The card, not the control: the host owns the control's visibility.
        parts.Card.IsVisible = Model is { Issues.Count: > 0 };
        if (Model is { } model)
        {
            // OrderBy is stable: host order within each severity is retained.
            foreach (IssueListItem issue in model.Issues.OrderBy(issue => issue.Severity))
            {
                parts.Rows.Children.Add(BuildRow(issue));
            }
        }
        if (parts.Rows.Children.Count > 0)
        {
            parts.Rows.Children[^1].Classes.Add("last");
        }
        RefreshSelection();
        // The focused row wins; without one the selection may have arrived before or moved with its row.
        if (!RestoreRowFocus(focusedKey, focusedIndex, keyboardFocus))
        {
            RevealSelection();
        }
    }

    private void BuildSummary(StackPanel panel, IssueListSeverity severity, long count)
    {
        panel.Children.Clear();
        panel.IsVisible = count > 0;
        AutomationProperties.SetName(panel, Text.Summary(severity, count));
        panel.Children.Add(SeverityIcon(severity));
        TextBlock label = Label(count.ToString(CultureInfo.InvariantCulture), "issueListCount");
        label.Classes.Add(SeverityClass(severity));
        panel.Children.Add(label);
    }

    private Button BuildRow(IssueListItem issue)
    {
        var message = new StackPanel { Spacing = 4 };
        TextBlock sentence = Label(issue.Sentence, "issueListSentence");
        message.Children.Add(sentence);
        if (issue.OccurrenceCount > 1)
        {
            sentence.Text = null;
            sentence.Inlines =
            [
                new Run(issue.Sentence),
                new Run(" "),
                new InlineUIContainer
                {
                    Child = Surface(Label(IssueListTextResources.Multiplicity(issue.OccurrenceCount), "issueListMultiplicity"),
                        "issueListBadge"),
                },
            ];
        }
        var chips = new WrapPanel { IsVisible = issue.LocationChips.Count > 0 };
        foreach (string chip in issue.LocationChips)
        {
            chips.Children.Add(Surface(Label(chip, "issueListTechnical"), "issueListChip"));
        }
        message.Children.Add(chips);
        var lines = new StackPanel { Spacing = 4 };
        foreach (string line in issue.DetailLines)
        {
            lines.Children.Add(Label(line, "issueListTechnical"));
        }
        Border details = Surface(lines, "issueListDetails");
        message.Children.Add(details);
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("20,*,16"),
            ColumnSpacing = 12,
        };
        content.Children.Add(SeverityIcon(issue.Severity));
        Grid.SetColumn(message, 1);
        content.Children.Add(message);
        TextBlock arrow = Label("›", "issueListChevron");
        Grid.SetColumn(arrow, 2);
        content.Children.Add(arrow);
        var button = new Button
        {
            Content = content,
            Command = _select,
            CommandParameter = issue.Key,
            Tag = new RowContent(issue, details),
        };
        button.Classes.Add("issueListRow");
        button.Classes.Add(SeverityClass(issue.Severity));
        AutomationProperties.SetAutomationId(button, $"IssueList.Row.{issue.Key}");
        return button;
    }

    private void RefreshSelection()
    {
        if (_parts is not { } parts)
        {
            return;
        }
        foreach (Button button in parts.Rows.Children.OfType<Button>())
        {
            var row = (RowContent)button.Tag!;
            bool selected = string.Equals(SelectedKey, row.Item.Key, StringComparison.Ordinal);
            button.Classes.Set("selected", selected);
            row.Details.IsVisible = selected && row.Item.DetailLines.Count > 0;
            AutomationProperties.SetName(button, Text.IssueName(row.Item, selected));
            AutomationProperties.SetHelpText(button, selected ? Text.Selected : Text.NotSelected);
        }
    }

    private static TextBlock Label(string text, string style)
    {
        var label = new TextBlock { Text = text };
        label.Classes.Add(style);
        return label;
    }

    private static Border Surface(Control content, string style)
    {
        var border = new Border { Child = content };
        border.Classes.Add(style);
        return border;
    }

    private static ShapePath SeverityIcon(IssueListSeverity severity)
    {
        string geometry = severity switch
        {
            IssueListSeverity.Error => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01",
            IssueListSeverity.Warning => "M12 3L22 20H2L12 3 M12 9V14 M12 17H12.01",
            _ => throw new ArgumentOutOfRangeException(nameof(severity)),
        };
        var icon = new ShapePath
        {
            Data = Geometry.Parse(geometry),
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Top,
        };
        icon.Classes.Add("issueListSeverity");
        icon.Classes.Add(SeverityClass(severity));
        AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
        return icon;
    }

    private static string SeverityClass(IssueListSeverity severity)
    {
        return severity switch
        {
            IssueListSeverity.Error => "error",
            IssueListSeverity.Warning => "warning",
            _ => throw new ArgumentOutOfRangeException(nameof(severity)),
        };
    }
}
