// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Actual-control states, commands and keyboard behavior with host-supplied synthetic facts.</summary>
public sealed class RecalculationConfirmModalTests
{
    /// <summary>The three approved states expose only their own content and action roles.</summary>
    [AvaloniaTheory]
    [InlineData(true, false, 3)]
    [InlineData(true, true, 3)]
    [InlineData(false, false, 2)]
    public void StatesShowOnlyTheApprovedContentAndButtons(bool canRecalculate, bool expanded, int actionCount)
    {
        using var host = new RecalculationConfirmTestHost(canRecalculate, expanded);
        Assert.Equal(canRecalculate, host.Find<ToggleButton>("DetailsButton").IsEffectivelyVisible);
        Assert.Equal(expanded, host.Find<Border>("DetailsPanel").IsEffectivelyVisible);
        Assert.Equal(canRecalculate, host.Find<TextBlock>("SavedChoiceNote").IsEffectivelyVisible);
        Assert.Equal(!canRecalculate, host.Find<TextBlock>("NoRuleText").IsEffectivelyVisible);
        Assert.Equal(!canRecalculate, host.Find<global::Avalonia.Controls.Shapes.Path>("NoRuleIcon").IsEffectivelyVisible);
        Assert.Equal(canRecalculate, host.Find<Button>("CancelButton").IsEffectivelyVisible);
        Assert.Equal(canRecalculate, host.Find<Button>("SkipButton").IsEffectivelyVisible);
        Assert.Equal(canRecalculate, host.Find<Button>("RecalculateButton").IsEffectivelyVisible);
        Assert.Equal(!canRecalculate, host.Find<Button>("NoRuleCancelButton").IsEffectivelyVisible);
        Assert.Equal(!canRecalculate, host.Find<Button>("WriteButton").IsEffectivelyVisible);
        Assert.Equal(actionCount, host.VisibleButtons.Length);
        Assert.Contains("secondary", host.Find<Button>("SkipButton").Classes);
        Assert.Contains("primary", host.Find<Button>("RecalculateButton").Classes);
        Assert.Contains("secondary", host.Find<Button>("NoRuleCancelButton").Classes);
        Assert.Contains("primary", host.Find<Button>("WriteButton").Classes);
        Assert.Equal("NT51927", Assert.Single(host.Find<Border>("IcBadge").GetVisualDescendants().OfType<TextBlock>()).Text);
    }

    /// <summary>Every actual action button reports once even if commands are invoked again during closing.</summary>
    [AvaloniaTheory]
    [InlineData(true, "RecalculateButton", 0)]
    [InlineData(true, "SkipButton", 1)]
    [InlineData(true, "CancelButton", 2)]
    [InlineData(false, "WriteButton", 1)]
    [InlineData(false, "NoRuleCancelButton", 2)]
    public void EachButtonReportsOneTerminalOutcome(bool canRecalculate, string buttonName, int expectedOutcome)
    {
        using var host = new RecalculationConfirmTestHost(canRecalculate);
        Button button = host.Find<Button>(buttonName);
        Assert.True(button.Focus(NavigationMethod.Tab));
        host.Press(Key.Space);
        button.Command!.Execute(null);
        host.Model.CancelCommand.Execute(null);
        host.Model.WriteCommand.Execute(null);
        host.Model.RecalculateCommand.Execute(null);
        Assert.Equal((RecalculationConfirmOutcome)expectedOutcome, Assert.Single(host.Outcomes));
        Assert.Equal((RecalculationConfirmOutcome)expectedOutcome, host.Model.Outcome);
        Assert.False(host.Model.IsPending);
        Assert.False(host.Modal.IsVisible);
        Assert.False(host.Model.CancelCommand.CanExecute(null));
        Assert.False(host.Model.WriteCommand.CanExecute(null));
        Assert.False(host.Model.RecalculateCommand.CanExecute(null));
        Assert.Same(host.Before, host.Window.FocusManager!.GetFocusedElement());
    }

    /// <summary>Escape cancels every state once and restores the trigger's focus.</summary>
    [AvaloniaTheory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void EscapeReportsCancelOnce(bool canRecalculate, bool expanded)
    {
        using var host = new RecalculationConfirmTestHost(canRecalculate, expanded);
        host.Press(Key.Escape);
        host.Press(Key.Escape);
        Assert.Equal(RecalculationConfirmOutcome.Cancel, Assert.Single(host.Outcomes));
        Assert.False(host.Modal.IsVisible);
        Assert.Same(host.Before, host.Window.FocusManager.GetFocusedElement());
    }

    /// <summary>Saved choice changes keyboard focus and its ring, while the button roles stay fixed.</summary>
    [AvaloniaTheory]
    [InlineData(true, false, false, false, "RecalculateButton")]
    [InlineData(true, true, false, false, "SkipButton")]
    [InlineData(true, false, true, false, "RecalculateButton")]
    [InlineData(true, true, true, false, "SkipButton")]
    [InlineData(false, false, false, false, "NoRuleCancelButton")]
    [InlineData(false, true, false, false, "NoRuleCancelButton")]
    [InlineData(true, false, false, true, "RecalculateButton")]
    [InlineData(true, true, false, true, "SkipButton")]
    [InlineData(false, false, false, true, "NoRuleCancelButton")]
    public void InitialFocusFollowsTheSuppliedChoice(bool canRecalculate, bool savedWrite, bool expanded, bool dark, string expectedButton)
    {
        using var host = new RecalculationConfirmTestHost(canRecalculate, expanded, savedWrite, dark);
        Button focused = host.Find<Button>(expectedButton);
        Assert.Same(focused, host.Window.FocusManager.GetFocusedElement());
        Assert.Contains(":focus-visible", focused.Classes);
        ContentPresenter presenter = Assert.Single(focused.GetVisualDescendants().OfType<ContentPresenter>(),
            control => control.Name == "PART_ContentPresenter");
        Assert.Equal(new global::Avalonia.Thickness(2), presenter.BorderThickness);
        Assert.NotEqual(presenter.Background, presenter.BorderBrush);
        if (focused.Classes.Contains("primary"))
        {
            // Drawn inside: the button clips an outer shadow. The gap must be wider than the border that covers it.
            Assert.Equal(1, presenter.BoxShadow.Count);
            Assert.True(presenter.BoxShadow[0].IsInset);
            Assert.True(presenter.BoxShadow[0].Spread > presenter.BorderThickness.Left);
            Assert.Equal(Assert.IsType<ISolidColorBrush>(host.Find<Border>("DialogSurface").Background, exactMatch: false).Color,
                presenter.BoxShadow[0].Color);
        }
        Assert.Contains("secondary", host.Find<Button>("SkipButton").Classes);
        Assert.Contains("primary", host.Find<Button>("RecalculateButton").Classes);
    }

    /// <summary>Tab and Shift+Tab traverse the disclosure and visual actions without reaching the host.</summary>
    [AvaloniaTheory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void TabCyclesInsideTheDialog(bool canRecalculate, bool reverse, bool expanded)
    {
        using var host = new RecalculationConfirmTestHost(canRecalculate, expanded);
        RawInputModifiers modifiers = reverse ? RawInputModifiers.Shift : RawInputModifiers.None;
        Assert.All(TabCycle(canRecalculate, reverse), expected =>
        {
            host.Press(Key.Tab, modifiers);
            Assert.Same(host.Find<Control>(expected), host.Window.FocusManager.GetFocusedElement());
            Assert.False(host.Before.IsFocused);
            Assert.False(host.After.IsFocused);
        });
    }

    /// <summary>The disclosure is keyboard operable and does not complete the dialog.</summary>
    [AvaloniaFact]
    public void DisclosureOpensAndClosesWithoutChangingTheOutcome()
    {
        using var host = new RecalculationConfirmTestHost();
        Assert.True(host.Find<ToggleButton>("DetailsButton").Focus(NavigationMethod.Tab));
        host.Press(Key.Space);
        Assert.True(host.Model.IsExpanded);
        Assert.True(host.Find<Border>("DetailsPanel").IsEffectivelyVisible);
        Assert.True(host.Find<ToggleButton>("DetailsButton").IsChecked);
        host.Press(Key.Space);
        Assert.False(host.Model.IsExpanded);
        Assert.False(host.Find<Border>("DetailsPanel").IsEffectivelyVisible);
        Assert.Empty(host.Outcomes);
    }

    /// <summary>Capabilities are explicit host facts even when the IC badge looks like a recalculable IC.</summary>
    [AvaloniaFact]
    public void NoRuleCannotReportRecalculationOrOpenDetails()
    {
        using var host = new RecalculationConfirmTestHost(canRecalculate: false);
        Assert.False(host.Model.RecalculateCommand.CanExecute(null));
        host.Model.RecalculateCommand.Execute(null);
        host.Model.ToggleDetailsCommand.Execute(null);
        Assert.False(host.Model.IsExpanded);
        Assert.Empty(host.Outcomes);
        Assert.True(host.Model.IsPending);
    }

    /// <summary>Host dismissal of a still-pending surface also delivers one cancel result.</summary>
    [AvaloniaFact]
    public void RemovingTheDialogReportsCancelOnce()
    {
        using var host = new RecalculationConfirmTestHost();
        Assert.True(Assert.IsType<Grid>(host.Window.Content).Children.Remove(host.Modal));
        host.Layout();
        host.Model.CancelCommand.Execute(null);
        Assert.Equal(RecalculationConfirmOutcome.Cancel, Assert.Single(host.Outcomes));
    }

    /// <summary>A pending model that is replaced is cancelled once, and the next one gets its own initial focus.</summary>
    [AvaloniaTheory]
    [InlineData(true, true, "SkipButton")]
    [InlineData(false, false, "NoRuleCancelButton")]
    public void ReplacingAPendingModelCancelsItAndRefocuses(bool canRecalculate, bool savedWrite, string expectedButton)
    {
        using var host = new RecalculationConfirmTestHost();
        List<RecalculationConfirmOutcome> next = [];
        RecalculationConfirmModel replacement = RecalculationConfirmTestHost.Example(next.Add, canRecalculate, savedWrite);
        host.Modal.Model = replacement;
        host.Layout();
        Assert.Equal(RecalculationConfirmOutcome.Cancel, Assert.Single(host.Outcomes));
        Assert.Empty(next);
        Assert.True(replacement.IsPending);
        Assert.True(host.Modal.IsVisible);
        Assert.Same(host.Find<Button>(expectedButton), host.Window.FocusManager.GetFocusedElement());
    }

    /// <summary>Clearing a pending model cancels it once and returns focus to the trigger.</summary>
    [AvaloniaFact]
    public void ClearingAPendingModelReportsCancelOnce()
    {
        using var host = new RecalculationConfirmTestHost();
        host.Modal.Model = null;
        host.Layout();
        Assert.Equal(RecalculationConfirmOutcome.Cancel, Assert.Single(host.Outcomes));
        Assert.False(host.Modal.IsVisible);
        Assert.Same(host.Before, host.Window.FocusManager.GetFocusedElement());
    }

    /// <summary>A model that is cleared and set back before the dialog settles is still the one being asked.</summary>
    [AvaloniaFact]
    public void SettingTheSameModelBackKeepsItPending()
    {
        using var host = new RecalculationConfirmTestHost();
        host.Modal.Model = null;
        host.Modal.Model = host.Model;
        host.Layout();
        Assert.Empty(host.Outcomes);
        Assert.True(host.Model.IsPending);
        Assert.True(host.Modal.IsVisible);
    }

    /// <summary>All interactive controls and the complete warning expose approved localized accessible names.</summary>
    [AvaloniaFact]
    public void AccessibleNamesDescribeTheDialogDisclosureWarningAndActions()
    {
        using var host = new RecalculationConfirmTestHost(expanded: true);
        Assert.Equal("Recalculate header and CRC before output?", AutomationProperties.GetName(host.Modal));
        Assert.Equal("Details", AutomationProperties.GetName(host.Find<ToggleButton>("DetailsButton")));
        Assert.Equal("Your segment [0x00200, 0x00300) covers 4 CRC words: 0x23C, 0x24C, 0x26C, 0x27C. Recalculation overwrites your bytes there.",
            AutomationProperties.GetName(host.Find<Border>("CoveringSegmentsWarning")));
        Assert.All(host.VisibleButtons, button => Assert.Equal(Assert.IsType<Button>(button).Content, AutomationProperties.GetName(button)));
    }

    private static string[] TabCycle(bool canRecalculate, bool reverse)
    {
        return (canRecalculate, reverse) switch
        {
            (true, false) => ["DetailsButton", "CancelButton", "SkipButton", "RecalculateButton", "DetailsButton", "CancelButton"],
            (true, true) => ["SkipButton", "CancelButton", "DetailsButton", "RecalculateButton", "SkipButton", "CancelButton"],
            _ => ["WriteButton", "NoRuleCancelButton", "WriteButton", "NoRuleCancelButton"],
        };
    }
}
