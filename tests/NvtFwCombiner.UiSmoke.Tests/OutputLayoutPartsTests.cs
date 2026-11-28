// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Protects bank viewport inputs, bidirectional selection and passive shared-input hints.</summary>
public sealed class OutputLayoutPartsTests
{
    /// <summary>A bank keyboard action changes only the bound viewport and retains supplied per-bank states.</summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter, PhysicalKey.Enter)]
    [InlineData(Key.Space, PhysicalKey.Space)]
    public void BankActivationExchangesTheBoundViewportAndUsesPublishedLabels(Key key, PhysicalKey physicalKey)
    {
        using var host = new OutputLayoutPartsTestHost(null);
        var selection = new OutputLayoutPartsSelection();
        using IDisposable binding = host.Banks.Bind(OutputLayoutBankSwitch.IsViewingBankBProperty,
            new Binding(nameof(selection.BankB)) { Source = selection });
        RadioButton b = host.Find<RadioButton>("OutputLayout.Bank.B");
        Assert.Equal("B Code · Preserved", AutomationProperties.GetName(b));
        Assert.True(b.Focus());
        host.Press(key, physicalKey);
        Assert.True(selection.BankB);
        Assert.True(b.IsChecked);
        Assert.Contains(host.Banks.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "B Code · Preserved");
        selection.BankB = false;
        host.Layout();
        Assert.False(host.Banks.IsViewingBankB);
        Assert.True(host.Find<RadioButton>("OutputLayout.Bank.A").IsChecked);
        Assert.Equal("A Code · Will replace", host.Banks.BankALabel);
        Assert.Equal("B Code · Preserved", host.Banks.BankBLabel);
    }

    /// <summary>Arrow keys, Home and End select the matching bank with a visible keyboard focus target.</summary>
    [AvaloniaFact]
    public void BankNavigationSelectsAndFocusesTheRequestedBank()
    {
        using var host = new OutputLayoutPartsTestHost(null);
        Assert.True(host.Find<RadioButton>("OutputLayout.Bank.A").Focus());
        host.Press(Key.Right, PhysicalKey.ArrowRight);
        Assert.True(host.Banks.IsViewingBankB);
        Assert.True(host.Find<RadioButton>("OutputLayout.Bank.B").IsFocused);
        host.Press(Key.Home, PhysicalKey.Home);
        Assert.False(host.Banks.IsViewingBankB);
        host.Press(Key.End, PhysicalKey.End);
        Assert.True(host.Banks.IsViewingBankB);
        host.Press(Key.Left, PhysicalKey.ArrowLeft);
        Assert.False(host.Banks.IsViewingBankB);
        Assert.True(host.Find<RadioButton>("OutputLayout.Bank.A").IsFocused);
    }

    /// <summary>The upstream availability flag hides asymmetric bank choices and guards even a stale command.</summary>
    [AvaloniaFact]
    public void NonSwitchableBanksKeepTheFullOverviewAndRejectSelection()
    {
        using var host = new OutputLayoutPartsTestHost(null);
        RadioButton b = host.Find<RadioButton>("OutputLayout.Bank.B");
        host.Banks.CanSwitchBanks = false;
        host.Layout();
        Assert.False(host.Banks.IsVisible);
        Assert.True(host.FullOverview.IsEffectivelyVisible);
        Assert.False(b.Command!.CanExecute(true));
        b.Command.Execute(true);
        Assert.False(host.Banks.IsViewingBankB);
        Assert.Equal(0, host.Banks.DesiredSize.Height);
        host.Banks.CanSwitchBanks = true;
        host.Layout();
        Assert.False(host.FullOverview.IsVisible);
        Assert.True(b.Command.CanExecute(true));
    }

    /// <summary>Disabled selectors reject keyboard and command activation without changing their input.</summary>
    [AvaloniaFact]
    public void DisabledBankSelectionDoesNotWriteTheBoundValue()
    {
        using var host = new OutputLayoutPartsTestHost(null);
        RadioButton b = host.Find<RadioButton>("OutputLayout.Bank.B");
        host.Banks.IsEnabled = false;
        host.Layout();
        Assert.False(b.Command!.CanExecute(true));
        b.Command.Execute(true);
        Assert.False(host.Banks.IsViewingBankB);
        Assert.False(b.Focus());
    }

    /// <summary>Slice selection and hover use the default two-way binding contract for a future host row.</summary>
    [AvaloniaFact]
    public void ZoomSelectionAndHoverExchangeValuesWithTheHost()
    {
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(OutputLayoutPartsTestHost.Lane()));
        var selection = new OutputLayoutPartsSelection();
        using IDisposable selected = host.Zoom.Bind(CtrlRamZoomBar.SelectedIndexProperty,
            new Binding(nameof(selection.Index)) { Source = selection });
        using IDisposable hovered = host.Zoom.Bind(CtrlRamZoomBar.HoveredIndexProperty,
            new Binding(nameof(selection.Hovered)) { Source = selection });
        selection.Index = 2;
        host.Layout();
        Assert.Equal(2, host.Zoom.SelectedIndex);
        Point point = host.Bounds(host.Find<Button>("CtrlRamZoom.Slice.0")).Center;
        host.Window.MouseMove(point, RawInputModifiers.None);
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseUp(point, MouseButton.Left);
        host.Layout();
        Assert.Equal(0, selection.Index);
        Assert.Equal(0, selection.Hovered);
        host.Window.MouseMove(new Point(1, 1), RawInputModifiers.None);
        Assert.Equal(-1, selection.Hovered);
        Assert.Equal(0, selection.Index);
    }

    /// <summary>The row renders the complete published shared-input message without a popup or tab stop.</summary>
    [AvaloniaFact]
    public void SharedInputHintDisplaysOnlyTheSuppliedMessage()
    {
        using var host = new OutputLayoutPartsTestHost(null);
        Assert.True(host.Hint.IsEffectivelyVisible);
        Assert.Equal("NF / Normal: shared inputs, separate target ranges.",
            Assert.Single(host.Hint.GetVisualDescendants().OfType<TextBlock>()).Text);
        Assert.Contains(host.Hint.Hint!, AutomationProperties.GetName(host.Hint), StringComparison.Ordinal);
        Assert.False(host.Hint.Focusable);
        Assert.Null(ToolTip.GetTip(host.Hint));
    }

    /// <summary>No shared-input publication leaves no row or reserved space.</summary>
    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptySharedInputHintCollapsesTheRow(string? hint)
    {
        using var host = new OutputLayoutPartsTestHost(null);
        host.Hint.Hint = hint;
        host.Layout();
        Assert.False(host.Hint.IsVisible);
        Assert.Equal(0, host.Hint.DesiredSize.Height);
    }

    /// <summary>Language replacement refreshes every supplied bank state and shared hint on the live controls.</summary>
    [AvaloniaFact]
    public void PublishedBankAndSharedTextCanSwitchLanguageInPlace()
    {
        using var host = new OutputLayoutPartsTestHost(null);
        host.Banks.IsViewingBankB = true;
        var text = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        host.Banks.Labels = text;
        host.Banks.BankALabel = text.FormatCtrlRamBankView(false, true);
        host.Banks.BankBLabel = text.FormatCtrlRamBankView(true, false);
        host.Hint.Labels = text;
        host.Hint.Hint = text.FormatMemorySharedInputHint("NF / Normal");
        host.Layout();
        Assert.True(host.Banks.IsViewingBankB);
        Assert.Equal("B Code · 保留", AutomationProperties.GetName(host.Find<RadioButton>("OutputLayout.Bank.B")));
        Assert.Equal("Bank 檢視", AutomationProperties.GetName(host.Banks));
        Assert.Contains("共用輸入", AutomationProperties.GetName(host.Hint), StringComparison.Ordinal);
        Assert.Contains(host.Banks.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "B Code · 保留");
        Assert.Equal("NF / Normal：共用輸入，各自保留獨立目標範圍。", host.Hint.Hint);
    }

    /// <summary>Automation peers receive complete original range and source text even for subpixel slices.</summary>
    [AvaloniaFact]
    public void TinyTargetsExposeOriginalDetailsToAutomationPeers()
    {
        MemoryFocusLaneViewModel lane = OutputLayoutPartsTestHost.Lane(tiny: true);
        using var host = new OutputLayoutPartsTestHost(CtrlRamZoomDisplay.FromLane(lane));
        Button tiny = host.Find<Button>("CtrlRamZoom.Tiny.1");
        Assert.Equal(lane.Ranges[1].AccessibleDetail, ControlAutomationPeer.CreatePeerForElement(tiny).GetName());
        Assert.Null(ToolTip.GetTip(tiny));
    }
}

/// <summary>External binding state used to detect selection binding replacement.</summary>
internal sealed partial class OutputLayoutPartsSelection : ObservableObject
{
    [ObservableProperty]
    public partial bool BankB { get; set; }

    [ObservableProperty]
    public partial int Index { get; set; } = -1;

    [ObservableProperty]
    public partial int Hovered { get; set; } = -1;
}
