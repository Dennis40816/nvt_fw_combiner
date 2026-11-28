// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Issue list lifetime tests.</summary>
public sealed class IssueListLifetimeTests
{
    /// <summary>Empty snapshot hides the whole control without an empty caption.</summary>
    [AvaloniaFact]
    public void EmptySnapshotHidesTheWholeControlWithoutAnEmptyCaption()
    {
        using var host = new IssueListTestHost(new IssueListModel([]));

        Assert.False(host.Card.IsEffectivelyVisible);
        Assert.Empty(host.Rows);

        host.List.Model = IssueListTestHost.Example();
        host.Layout();
        Assert.True(host.Card.IsEffectivelyVisible);
        Assert.Equal(4, host.Rows.Length);
    }

    /// <summary>Null model hides the control and clears rows.</summary>
    [AvaloniaFact]
    public void NullModelHidesTheControlAndClearsRows()
    {
        using var host = new IssueListTestHost(selectedKey: "past-end");

        host.List.Model = null;
        host.Layout();

        Assert.False(host.Card.IsEffectivelyVisible);
        Assert.Empty(host.Rows);
    }

    /// <summary>Replacing selected snapshot refreshes content and retains the key.</summary>
    [AvaloniaFact]
    public void ReplacingSelectedSnapshotRefreshesContentAndRetainsTheKey()
    {
        using var host = new IssueListTestHost(selectedKey: "past-end");

        host.List.Model = new IssueListModel(
        [
            new("past-end", IssueListSeverity.Warning, "The replacement sentence.",
                ["opaque replacement chip"], 3, ["replacement detail"]),
        ]);
        host.Layout();

        Assert.Equal("past-end", host.List.SelectedKey);
        Assert.True(host.Details("past-end").IsEffectivelyVisible);
        Assert.Equal("Warning · The replacement sentence. · ×3 · opaque replacement chip · replacement detail",
            IssueListTestHost.Name(host.Row("past-end")));
        Assert.DoesNotContain(host.List.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Over by 0x400 bytes");
    }

    /// <summary>Replacing snapshot without selected key shows no details.</summary>
    [AvaloniaFact]
    public void ReplacingSnapshotWithoutSelectedKeyShowsNoDetails()
    {
        using var host = new IssueListTestHost(selectedKey: "past-end");

        host.List.Model = new IssueListModel([IssueListTestHost.Item("different")]);
        host.Layout();

        Assert.DoesNotContain("selected", host.Row("different").Classes);
        Assert.False(host.Details("different").IsEffectivelyVisible);
    }

    /// <summary>Replacing focused snapshot restores focus by key after reordering.</summary>
    [AvaloniaFact]
    public void ReplacingFocusedSnapshotRestoresFocusByKeyAfterReordering()
    {
        using var host = new IssueListTestHost(selectedKey: "overlap");
        Assert.True(host.Row("overlap").Focus(NavigationMethod.Tab));

        host.List.Model = new IssueListModel(
        [
            IssueListTestHost.Item("other"),
            new("overlap", IssueListSeverity.Error, "Updated issue.", [], 1, ["new details"]),
        ]);
        host.Layout();

        Assert.True(host.Row("overlap").IsFocused);
        Assert.Contains(":focus-visible", host.Row("overlap").Classes);
        Assert.True(host.Details("overlap").IsEffectivelyVisible);
        Assert.Equal(["overlap", "other"], IssueListTestHost.Keys(host.Rows));

        host.Press(Key.Space, PhysicalKey.Space, " ");
        Assert.Null(host.List.SelectedKey);
    }

    /// <summary>Removing focused key moves focus to the nearest survivor.</summary>
    [AvaloniaFact]
    public void RemovingFocusedKeyMovesFocusToTheNearestSurvivor()
    {
        using var host = new IssueListTestHost();
        Assert.True(host.Row("read-only").Focus(NavigationMethod.Tab));

        host.List.Model = new IssueListModel(
        [
            IssueListTestHost.Item("first"),
            IssueListTestHost.Item("last"),
        ]);
        host.Layout();

        Assert.True(host.Row("last").IsFocused);

        host.Press(Key.Enter, PhysicalKey.Enter);
        Assert.Equal("last", host.List.SelectedKey);
    }

    /// <summary>Replacing with empty snapshot while focused can recover.</summary>
    [AvaloniaFact]
    public void ReplacingWithEmptySnapshotWhileFocusedCanRecover()
    {
        using var host = new IssueListTestHost();
        Assert.True(host.Row("overlap").Focus(NavigationMethod.Tab));

        host.List.Model = new IssueListModel([]);
        host.Layout();
        Assert.False(host.Card.IsEffectivelyVisible);

        host.List.Model = IssueListTestHost.Example();
        host.Layout();
        Assert.True(host.Row("past-end").Focus(NavigationMethod.Tab));
        host.Press(Key.Enter, PhysicalKey.Enter);
        Assert.Equal("past-end", host.List.SelectedKey);
    }

    /// <summary>Detached model replacement lays out on reattach.</summary>
    [AvaloniaTheory]
    [InlineData(280)]
    [InlineData(360)]
    public void DetachedModelReplacementLaysOutOnReattach(int width)
    {
        using var host = new IssueListTestHost(width: width, selectedKey: "past-end");
        object? root = host.Window.Content;
        Assert.True(host.Row("past-end").Focus(NavigationMethod.Tab));
        host.Window.Content = null;
        host.Layout();

        host.List.Model = new IssueListModel(
        [
            new("past-end", IssueListSeverity.Warning,
                "This replacement sentence must wrap correctly after the detached control joins the visual tree again.",
                ["a supplied location chip that also wraps in a narrow column"], 2,
                ["A replacement detail line that stays entirely readable."]),
        ]);
        host.Window.Content = root;
        host.Layout();

        Assert.Equal("past-end", host.List.SelectedKey);
        Assert.True(host.Details("past-end").IsEffectivelyVisible);
        IssueListRenderTests.AssertTextFits(host);
        Assert.True(host.Row("past-end").Focus(NavigationMethod.Tab));
        host.Press(Key.Space, PhysicalKey.Space, " ");
        Assert.Null(host.List.SelectedKey);
    }

    /// <summary>Reattach does not duplicate pointer activation.</summary>
    [AvaloniaFact]
    public void ReattachDoesNotDuplicatePointerActivation()
    {
        using var host = new IssueListTestHost();
        object? root = host.Window.Content;
        host.Window.Content = null;
        host.Layout();
        host.Window.Content = root;
        host.Layout();

        host.Click(host.Row("past-end"));
        Assert.Equal("past-end", host.List.SelectedKey);

        host.Click(host.Row("past-end"));
        Assert.Null(host.List.SelectedKey);
    }
}
