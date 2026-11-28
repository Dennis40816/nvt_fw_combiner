// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regressions for host visibility, shared-input absence and unconsumed bank Escape keys.</summary>
public sealed partial class OutputLayoutPartsTests
{
    /// <summary>Bank choices have no transient selection to clear, so Escape always reaches the host.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BankEscapeBubblesWithoutChangingTheViewedBankOrFocus(bool bankB)
    {
        using OutputLayoutPartsTestHost host = new(null);
        host.Banks.IsViewingBankB = bankB;
        RadioButton bank = host.Find<RadioButton>(bankB ? "OutputLayout.Bank.B" : "OutputLayout.Bank.A");
        Assert.True(bank.Focus());
        int escapes = 0;
        host.Window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape) { escapes++; }
        });
        host.Press(Key.Escape, PhysicalKey.Escape);
        Assert.Equal(1, escapes);
        Assert.Equal(bankB, host.Banks.IsViewingBankB);
        Assert.True(bank.IsFocused);
    }

    /// <summary>The existing empty shared-input formatter remains unchanged while its hover fallback is suppressed.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericHoverInstructionDoesNotAppearAsASharedInputRow(bool chinese)
    {
        using OutputLayoutPartsTestHost host = new(null);
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        string emptyHint = text.FormatMemorySharedInputHint(string.Empty);
        Assert.Equal(text.MemoryFocusHint, emptyHint);
        // Hint and Labels may be published separately during a language change.
        host.Hint.Hint = emptyHint;
        host.Layout();
        TextBlock row = Assert.IsType<TextBlock>(host.Hint.Content);
        Assert.False(row.IsEffectivelyVisible);
        Assert.Equal(0, host.Hint.DesiredSize.Height);
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(host.Hint)));
        host.Hint.Labels = text;
        host.Layout();
        Assert.False(row.IsEffectivelyVisible);
        Assert.Equal(0, host.Hint.DesiredSize.Height);
        host.Hint.Hint = text.FormatMemorySharedInputHint("NF / Normal");
        host.Layout();
        Assert.True(row.IsEffectivelyVisible);
        Assert.Equal(host.Hint.Hint, row.Text);
        host.Hint.Hint = emptyHint;
        host.Layout();
        Assert.False(row.IsEffectivelyVisible);
    }

    /// <summary>Local host visibility remains authoritative while data and availability are republished.</summary>
    [AvaloniaFact]
    public void RepublishingPartsDoesNotOverwriteLocalHostVisibility()
    {
        using OutputLayoutPartsTestHost host = new(null);
        host.Zoom.IsVisible = false;
        host.Banks.IsVisible = false;
        host.Hint.IsVisible = false;
        RepublishParts(host);
        Assert.False(host.Zoom.IsVisible);
        Assert.False(host.Banks.IsVisible);
        Assert.False(host.Hint.IsVisible);
        host.Zoom.IsVisible = true;
        host.Banks.IsVisible = true;
        host.Hint.IsVisible = true;
        host.Layout();
        Assert.True(Assert.IsType<StackPanel>(host.Zoom.Content).IsEffectivelyVisible);
        Assert.True(Assert.IsType<Grid>(host.Banks.Content).IsEffectivelyVisible);
        Assert.True(Assert.IsType<TextBlock>(host.Hint.Content).IsEffectivelyVisible);
    }

    /// <summary>Internal availability hides only inner content and never writes through host visibility bindings.</summary>
    [AvaloniaFact]
    public void AvailabilityDoesNotWriteThroughHostVisibilityBindings()
    {
        using OutputLayoutPartsTestHost host = new(null);
        OutputLayoutPartsSelection selection = new() { ShowParts = false };
        using IDisposable zoom = host.Zoom.Bind(Visual.IsVisibleProperty,
            new Binding(nameof(selection.ShowParts)) { Source = selection, Mode = BindingMode.TwoWay });
        using IDisposable banks = host.Banks.Bind(Visual.IsVisibleProperty,
            new Binding(nameof(selection.ShowParts)) { Source = selection, Mode = BindingMode.TwoWay });
        using IDisposable hint = host.Hint.Bind(Visual.IsVisibleProperty,
            new Binding(nameof(selection.ShowParts)) { Source = selection, Mode = BindingMode.TwoWay });
        RepublishParts(host);
        Assert.False(selection.ShowParts);
        Assert.False(host.Zoom.IsVisible);
        Assert.False(host.Banks.IsVisible);
        Assert.False(host.Hint.IsVisible);
        selection.ShowParts = true;
        host.Layout();
        host.Zoom.Data = null;
        host.Banks.CanSwitchBanks = false;
        host.Hint.Hint = null;
        host.Layout();
        Assert.True(selection.ShowParts);
        Assert.True(host.Zoom.IsVisible);
        Assert.True(host.Banks.IsVisible);
        Assert.True(host.Hint.IsVisible);
        Assert.False(Assert.IsType<StackPanel>(host.Zoom.Content).IsEffectivelyVisible);
        Assert.False(Assert.IsType<Grid>(host.Banks.Content).IsEffectivelyVisible);
        Assert.False(Assert.IsType<TextBlock>(host.Hint.Content).IsEffectivelyVisible);
        RepublishParts(host);
        Assert.True(Assert.IsType<StackPanel>(host.Zoom.Content).IsEffectivelyVisible);
        Assert.True(Assert.IsType<Grid>(host.Banks.Content).IsEffectivelyVisible);
        Assert.True(Assert.IsType<TextBlock>(host.Hint.Content).IsEffectivelyVisible);
    }

    private static void RepublishParts(OutputLayoutPartsTestHost host)
    {
        host.Zoom.Data = null;
        host.Banks.CanSwitchBanks = false;
        host.Hint.Hint = null;
        host.Zoom.Data = CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane());
        host.Banks.CanSwitchBanks = true;
        host.Hint.Hint = ShellTextResources.For(ShellLanguage.English).FormatMemorySharedInputHint("NF / Normal");
        host.Layout();
    }
}
