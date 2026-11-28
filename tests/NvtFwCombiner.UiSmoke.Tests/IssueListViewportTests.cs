// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>What the user can still see after the host or the user changes the list.</summary>
public sealed class IssueListViewportTests
{
    /// <summary>Host visibility survives model and language changes.</summary>
    [AvaloniaFact]
    public void HostVisibilitySurvivesModelAndLanguageChanges()
    {
        using var host = new IssueListTestHost();
        host.List.IsVisible = false;

        host.List.Model = LongList();
        host.List.Labels = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        host.Layout();

        Assert.False(host.List.IsVisible);
    }

    /// <summary>Selection set by the host is scrolled into view with its details.</summary>
    [AvaloniaFact]
    public void SelectionSetByTheHostIsScrolledIntoViewWithItsDetails()
    {
        using var host = new IssueListTestHost(LongList());

        host.List.SelectedKey = "item-29";
        host.Layout();

        AssertInView(host, host.Details("item-29"));
    }

    /// <summary>A selection set before its row arrives is scrolled into view with the new model.</summary>
    [AvaloniaFact]
    public void SelectionSetBeforeItsRowArrivesIsScrolledIntoView()
    {
        using var host = new IssueListTestHost();
        host.List.SelectedKey = "item-29";

        host.List.Model = LongList();
        host.Layout();

        AssertInView(host, host.Details("item-29"));
    }

    /// <summary>Escape with a selected key that matches no row is left to the host.</summary>
    [AvaloniaFact]
    public void EscapeWithAStaleSelectionIsLeftToTheHost()
    {
        using var host = new IssueListTestHost(selectedKey: "past-end");
        host.List.Model = new IssueListModel([IssueListTestHost.Item("different")]);
        host.Layout();
        int unhandled = 0;
        host.Window.AddHandler(InputElement.KeyDownEvent,
            (_, e) => unhandled += e.Key == Key.Escape ? 1 : 0, RoutingStrategies.Bubble);
        Assert.True(host.Row("different").Focus(NavigationMethod.Tab));

        host.Press(Key.Escape, PhysicalKey.Escape);

        Assert.Equal(1, unhandled);
        Assert.Equal("past-end", host.List.SelectedKey);
    }

    /// <summary>Expanding the lowest row brings its details into view.</summary>
    [AvaloniaFact]
    public void ExpandingTheLowestRowBringsItsDetailsIntoView()
    {
        using var host = new IssueListTestHost(LongList());
        Button row = host.Row("item-29");
        Assert.True(row.Focus(NavigationMethod.Tab));
        row.BringIntoView();
        host.Layout();

        host.Press(Key.Enter, PhysicalKey.Enter);

        AssertInView(host, host.Details("item-29"));
    }

    /// <summary>Replacing the snapshot keeps the focused row in view.</summary>
    [AvaloniaFact]
    public void ReplacingTheSnapshotKeepsTheFocusedRowInView()
    {
        using var host = new IssueListTestHost(LongList());
        Assert.True(host.Row("item-29").Focus(NavigationMethod.Tab));
        host.Row("item-29").BringIntoView();
        host.Layout();

        host.List.Model = LongList();
        host.Layout();

        Assert.True(host.Row("item-29").IsFocused);
        AssertInView(host, host.Row("item-29"));
    }

    /// <summary>A selected row without details still looks selected.</summary>
    [AvaloniaFact]
    public void ASelectedRowWithoutDetailsStillLooksSelected()
    {
        using var host = new IssueListTestHost();

        host.Click(host.Row("read-only"));

        Assert.NotNull(Chevron(host.Row("read-only")).RenderTransform);
        Assert.Null(Chevron(host.Row("crc")).RenderTransform);
    }

    /// <summary>Escape without selection is left to the host.</summary>
    [AvaloniaFact]
    public void EscapeWithoutSelectionIsLeftToTheHost()
    {
        using var host = new IssueListTestHost();
        int unhandled = 0;
        host.Window.AddHandler(InputElement.KeyDownEvent,
            (_, e) => unhandled += e.Key == Key.Escape ? 1 : 0, RoutingStrategies.Bubble);
        Assert.True(host.Row("past-end").Focus(NavigationMethod.Tab));

        host.Press(Key.Escape, PhysicalKey.Escape);

        Assert.Equal(1, unhandled);
    }

    /// <summary>Pointer focus does not become keyboard focus after a replacement.</summary>
    [AvaloniaFact]
    public void PointerFocusDoesNotBecomeKeyboardFocusAfterAReplacement()
    {
        using var host = new IssueListTestHost();
        host.Click(host.Row("past-end"));
        Assert.True(host.Row("past-end").IsFocused);

        host.List.Model = IssueListTestHost.Example();
        host.Layout();

        Assert.True(host.Row("past-end").IsFocused);
        Assert.DoesNotContain(":focus-visible", host.Row("past-end").Classes);
    }

    private static TextBlock Chevron(Button row)
    {
        return Assert.Single(row.GetVisualDescendants().OfType<TextBlock>(), text => text.Classes.Contains("issueListChevron"));
    }

    private static void AssertInView(IssueListTestHost host, Control control)
    {
        Assert.True(host.Bounds(host.Scroll).Inflate(new Thickness(0.5)).Contains(host.Bounds(control)),
            $"{host.Bounds(control)} is outside {host.Bounds(host.Scroll)}");
    }

    private static IssueListModel LongList()
    {
        return new IssueListModel([.. Enumerable.Range(0, 30).Select(index => new IssueListItem(
            string.Create(CultureInfo.InvariantCulture, $"item-{index}"), IssueListSeverity.Warning,
            "A supplied sentence.", [], 1, ["first detail", "second detail"]))]);
    }
}
