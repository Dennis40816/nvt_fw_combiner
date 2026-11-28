// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Issue list interaction tests.</summary>
public sealed partial class IssueListInteractionTests
{
    /// <summary>Errors precede warnings without merging or reordering peers.</summary>
    [AvaloniaFact]
    public void ErrorsPrecedeWarningsWithoutMergingOrReorderingPeers()
    {
        using var host = new IssueListTestHost(new IssueListModel(
        [
            IssueListTestHost.Item("warning-a", sentence: "Repeated sentence."),
            IssueListTestHost.Item("error-a", IssueListSeverity.Error),
            IssueListTestHost.Item("warning-b", sentence: "Repeated sentence."),
            IssueListTestHost.Item("error-b", IssueListSeverity.Error),
        ]));

        Assert.Equal(["error-a", "error-b", "warning-a", "warning-b"], IssueListTestHost.Keys(host.Rows));
    }

    /// <summary>Header counts occurrences and rows show only repeated multiplicity.</summary>
    [AvaloniaFact]
    public void HeaderCountsOccurrencesAndRowsShowOnlyRepeatedMultiplicity()
    {
        using var host = new IssueListTestHost();

        Assert.Equal("Errors: 1", IssueListTestHost.Name(host.List.FindControl<StackPanel>("IssueListErrors")!));
        Assert.Equal("Warnings: 4", IssueListTestHost.Name(host.List.FindControl<StackPanel>("IssueListWarnings")!));
        Assert.Equal("1", Assert.Single(host.List.FindControl<StackPanel>("IssueListErrors")!.Children.OfType<TextBlock>()).Text);
        Assert.Equal("4", Assert.Single(host.List.FindControl<StackPanel>("IssueListWarnings")!.Children.OfType<TextBlock>()).Text);
        Assert.Equal("×2", Assert.Single(host.List.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Classes.Contains("issueListMultiplicity")).Text);
        Assert.DoesNotContain(host.List.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "×1");
    }

    /// <summary>Zero severity count is hidden.</summary>
    [AvaloniaTheory]
    [InlineData(true, "IssueListWarnings", "IssueListErrors")]
    [InlineData(false, "IssueListErrors", "IssueListWarnings")]
    public void ZeroSeverityCountIsHidden(bool error, string hidden, string visible)
    {
        IssueListSeverity severity = error ? IssueListSeverity.Error : IssueListSeverity.Warning;
        using var host = new IssueListTestHost(new IssueListModel([IssueListTestHost.Item("only", severity)]));

        Assert.False(host.List.FindControl<StackPanel>(hidden)!.IsEffectivelyVisible);
        Assert.True(host.List.FindControl<StackPanel>(visible)!.IsEffectivelyVisible);
    }

    /// <summary>Pointer selects one row and selecting it again closes details.</summary>
    [AvaloniaFact]
    public void PointerSelectsOneRowAndSelectingItAgainClosesDetails()
    {
        using var host = new IssueListTestHost();
        Assert.All(host.Rows, row => Assert.False(host.Details((string)row.CommandParameter!).IsEffectivelyVisible));

        host.Click(host.Row("past-end"));
        Assert.Equal("past-end", host.List.SelectedKey);
        Assert.True(host.Details("past-end").IsEffectivelyVisible);
        Assert.False(host.Details("overlap").IsEffectivelyVisible);

        host.Click(host.Row("overlap"));
        Assert.Equal("overlap", host.List.SelectedKey);
        Assert.False(host.Details("past-end").IsEffectivelyVisible);
        Assert.True(host.Details("overlap").IsEffectivelyVisible);

        host.Click(host.Row("overlap"));
        Assert.Null(host.List.SelectedKey);
        Assert.False(host.Details("overlap").IsEffectivelyVisible);
    }

    /// <summary>Clicking a location chip selects the row without inventing details.</summary>
    [AvaloniaFact]
    public void ClickingALocationChipSelectsTheRowWithoutInventingDetails()
    {
        using var host = new IssueListTestHost();
        Border chip = host.Row("read-only").GetVisualDescendants().OfType<Border>()
            .First(border => border.Classes.Contains("issueListChip"));

        host.Click(chip);

        Assert.Equal("read-only", host.List.SelectedKey);
        Assert.Contains("selected", host.Row("read-only").Classes);
        Assert.False(host.Details("read-only").IsEffectivelyVisible);
    }

    /// <summary>Keyboard activation selects and toggles the focused row.</summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter, PhysicalKey.Enter, null)]
    [InlineData(Key.Space, PhysicalKey.Space, " ")]
    public void KeyboardActivationSelectsAndTogglesTheFocusedRow(Key key, PhysicalKey physicalKey, string? symbol)
    {
        using var host = new IssueListTestHost();
        Assert.True(host.Row("past-end").Focus(NavigationMethod.Tab));

        host.Press(key, physicalKey, symbol);
        Assert.Equal("past-end", host.List.SelectedKey);
        Assert.True(host.Details("past-end").IsEffectivelyVisible);
        Assert.True(host.Row("past-end").IsFocused);

        host.Press(key, physicalKey, symbol);
        Assert.Null(host.List.SelectedKey);
        Assert.False(host.Details("past-end").IsEffectivelyVisible);
    }

    /// <summary>Arrows move focus and escape clears selection.</summary>
    [AvaloniaFact]
    public void ArrowsMoveFocusAndEscapeClearsSelection()
    {
        using var host = new IssueListTestHost(selectedKey: "past-end");
        Assert.True(host.Row("past-end").Focus(NavigationMethod.Tab));

        host.Press(Key.Down, PhysicalKey.ArrowDown);
        Assert.True(host.Row("overlap").IsFocused);
        Assert.Equal("past-end", host.List.SelectedKey);

        host.Press(Key.Up, PhysicalKey.ArrowUp);
        Assert.True(host.Row("past-end").IsFocused);

        host.Press(Key.Escape, PhysicalKey.Escape);
        Assert.Null(host.List.SelectedKey);
        Assert.False(host.Details("past-end").IsEffectivelyVisible);
        Assert.True(host.Row("past-end").IsFocused);
    }

    /// <summary>Tab reaches every row.</summary>
    [AvaloniaFact]
    public void TabReachesEveryRow()
    {
        using var host = new IssueListTestHost();
        Assert.True(host.Row("past-end").Focus(NavigationMethod.Tab));

        string[] visited = WalkRows(host, Key.Tab, PhysicalKey.Tab, host.Rows.Length);

        Assert.Equal(["past-end", "overlap", "read-only", "crc"], visited);
    }

    /// <summary>Selected key binding exchanges host and pointer selection.</summary>
    [AvaloniaFact]
    public void SelectedKeyBindingExchangesHostAndPointerSelection()
    {
        using var host = new IssueListTestHost();
        var state = new SelectionState();
        using IDisposable binding = host.List.Bind(IssueList.SelectedKeyProperty,
            new Binding(nameof(SelectionState.SelectedKey)) { Source = state });

        state.SelectedKey = "past-end";
        host.Layout();
        Assert.True(host.Details("past-end").IsEffectivelyVisible);

        host.Click(host.Row("overlap"));
        Assert.Equal("overlap", state.SelectedKey);

        host.Click(host.Row("overlap"));
        Assert.Null(state.SelectedKey);
    }

    /// <summary>Unknown key selects no row and shows no details.</summary>
    [AvaloniaFact]
    public void UnknownKeySelectsNoRowAndShowsNoDetails()
    {
        using var host = new IssueListTestHost(selectedKey: "unknown");

        Assert.All(host.Rows, row => Assert.DoesNotContain("selected", row.Classes));
        Assert.All(host.Rows, row => Assert.False(host.Details((string)row.CommandParameter!).IsEffectivelyVisible));
    }

    /// <summary>Accessible names localize owned text and preserve host text.</summary>
    [AvaloniaTheory]
    [InlineData(false, "Issues", "Error", "Warnings: 4", "Selected")]
    [InlineData(true, "問題", "錯誤", "警告: 4", "已選取")]
    public void AccessibleNamesLocalizeOwnedTextAndPreserveHostText(bool chinese, string title,
        string severity, string warnings, string selected)
    {
        using var host = new IssueListTestHost(chinese: chinese);
        Button row = host.Row("past-end");

        Assert.Equal(title, host.List.FindControl<TextBlock>("IssueListTitle")!.Text);
        Assert.Equal(warnings, IssueListTestHost.Name(host.List.FindControl<StackPanel>("IssueListWarnings")!));
        Assert.Equal($"{severity} · Step 3 writes past the output end. · patch.bin · → [0x7F000, 0x80400) · end 0x80000",
            ControlAutomationPeer.CreatePeerForElement(row).GetName());

        host.Click(row);
        Assert.Equal(selected, AutomationProperties.GetHelpText(row));
        Assert.EndsWith("Over by 0x400 bytes · OUTPUT_RANGE_EXCEEDED · step 3", IssueListTestHost.Name(row), StringComparison.Ordinal);
    }

    private static string[] WalkRows(IssueListTestHost host, Key key, PhysicalKey physicalKey, int count)
    {
        var visited = new List<string>();
        for (int index = 0; index < count; index++)
        {
            Button focused = Assert.Single(host.Rows, row => row.IsFocused);
            visited.Add(Assert.IsType<string>(focused.CommandParameter));
            host.Press(key, physicalKey);
        }
        return [.. visited];
    }

    // UI thread only, representing the later host's selection binding.
    private sealed partial class SelectionState : ObservableObject
    {
        [ObservableProperty]
        public partial string? SelectedKey { get; set; }
    }
}
