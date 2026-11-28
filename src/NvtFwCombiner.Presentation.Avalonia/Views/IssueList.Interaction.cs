// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

public sealed partial class IssueList
{
    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        // A key that matches no row is not a selection the user can see.
        if (e.Key == Key.Escape && SelectedRow() is not null)
        {
            _clear.Execute(null);
            e.Handled = true;
        }
        else if (e.Key is Key.Up or Key.Down && _parts is { } parts)
        {
            Button[] rows = [.. parts.Rows.Children.OfType<Button>()];
            int index = Array.FindIndex(rows, row => row.IsFocused);
            if (index < 0)
            {
                return;
            }
            int next = Math.Clamp(index + (e.Key == Key.Up ? -1 : 1), 0, rows.Length - 1);
            _ = rows[next].Focus(NavigationMethod.Directional);
            rows[next].BringIntoView();
            e.Handled = true;
        }
    }

    private bool RestoreRowFocus(string? key, int previousIndex, bool keyboard)
    {
        if (key is null || _parts is not { } parts)
        {
            return false;
        }
        Button[] rows = [.. parts.Rows.Children.OfType<Button>()];
        if (rows.Length == 0)
        {
            return false;
        }
        Button target = rows.FirstOrDefault(row => string.Equals(row.CommandParameter as string, key, StringComparison.Ordinal))
            ?? rows[Math.Min(previousIndex, rows.Length - 1)];
        _ = target.Focus(keyboard ? NavigationMethod.Directional : NavigationMethod.Unspecified);
        RevealAfterLayout(target);
        return true;
    }

    private void RevealSelection()
    {
        if (SelectedRow() is { } selected)
        {
            RevealAfterLayout(selected);
        }
    }

    private Button? SelectedRow()
    {
        return _parts?.Rows.Children.OfType<Button>().FirstOrDefault(row => row.Classes.Contains("selected"));
    }

    // Posted: a new or expanded row has no final bounds until layout has run.
    private static void RevealAfterLayout(Button row)
    {
        Dispatcher.UIThread.Post(row.BringIntoView, DispatcherPriority.Background);
    }
}
