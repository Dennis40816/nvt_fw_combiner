// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Hosts only standalone layout parts and synthetic host fallbacks in the shared Avalonia session.</summary>
internal sealed class OutputLayoutPartsTestHost : IDisposable
{
    internal OutputLayoutPartsTestHost(CtrlRamZoomData? data, int width = 960, bool dark = false, bool chinese = false)
    {
        var text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        Zoom = new CtrlRamZoomBar { Data = data, Labels = text };
        Banks = new OutputLayoutBankSwitch
        {
            CanSwitchBanks = true,
            Labels = text,
            BankALabel = text.FormatCtrlRamBankView(false, true),
            BankBLabel = text.FormatCtrlRamBankView(true, false),
        };
        Hint = new SharedInputHintRow { Hint = text.FormatMemorySharedInputHint("NF / Normal"), Labels = text };
        Fallback = new TextBlock { Text = "Normal list", Name = "NormalListFallback" };
        FullOverview = new TextBlock { Text = "Full overview", Name = "FullOverviewFallback" };
        _ = Fallback.Bind(Visual.IsVisibleProperty, new Binding("!IsVisible") { Source = Zoom });
        _ = FullOverview.Bind(Visual.IsVisibleProperty, new Binding("!CanSwitchBanks") { Source = Banks });
        Window = new Window
        {
            Width = width,
            Height = 800,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new Border
            {
                Padding = new Thickness(24),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Child = new StackPanel { Spacing = 12, Children = { Banks, FullOverview, Zoom, Fallback, Hint } },
            },
        };
        Window.Show();
        Layout();
    }

    internal Window Window { get; }
    internal CtrlRamZoomBar Zoom { get; }
    internal OutputLayoutBankSwitch Banks { get; }
    internal SharedInputHintRow Hint { get; }
    internal TextBlock Fallback { get; }
    internal TextBlock FullOverview { get; }
    internal Canvas Rail => Assert.Single(Zoom.GetLogicalDescendants().OfType<Canvas>(), c => c.Name == "CtrlRamZoomRail");

    internal T Find<T>(string id) where T : Control
    {
        return Assert.Single(Window.GetVisualDescendants().OfType<T>(),
            c => string.Equals(AutomationProperties.GetAutomationId(c), id, StringComparison.Ordinal));
    }

    internal Rect Bounds(Control control)
    {
        return new(control.TranslatePoint(default, Window)!.Value, control.Bounds.Size);
    }

    internal void Layout()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    internal void Press(Key key, PhysicalKey physicalKey)
    {
        Window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        Window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        Layout();
    }

    public void Dispose()
    {
        Window.Close();
    }

    internal static MemoryFocusLaneViewModel Lane(bool tiny = false, bool chinese = false)
    {
        var text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        MemoryCoverageSegmentViewModel[] slices =
        [
            Slice(0x1000, tiny ? 0x1001 : 0x1100, CtrlRamRegionRole.Nf, MemoryCoverageFillRole.CtrlRamNf, text),
            Slice(tiny ? 0x1001 : 0x1100, tiny ? 0x1002 : 0x1300, CtrlRamRegionRole.Normal, MemoryCoverageFillRole.CtrlRamNormal, text),
            Slice(tiny ? 0x1002 : 0x1300, 0x1400, CtrlRamRegionRole.Vn, MemoryCoverageFillRole.CtrlRamVn, text),
        ];
        return Assert.Single(MemoryFocusLaneViewModel.Create(
            slices.Select(s => new MemoryCoverageLogicalItemViewModel(s.RegionId!, [s], text)), text, isSingleIc: true));
    }

    internal static MemoryCoverageSegmentViewModel Slice(long start, long end, CtrlRamRegionRole role,
        MemoryCoverageFillRole fill, ShellTextResources text)
    {
        string source = $"{ShellTextResources.GetMemoryFocusLabel(role)} CtrlRAM";
        return new(FormattableString.Invariant($"flash [0x{start:X5},0x{end:X5})"), source,
            text.Language == ShellLanguage.ChineseTraditional ? "合成輸入的顯示細節。" : "Display details from synthetic input.",
            fill, barWidth: 1, disposition: MemoryWorkflowDisposition.WillReplace,
            regionId: source, sourceSlotId: source, text: text,
            rangeStart: start, rangeEndExclusive: end,
            addressRangeLabel: FormattableString.Invariant($"0x{start:X5}–0x{end - 1:X5}"),
            contentRole: MemoryContentRole.CtrlRam, ctrlRamRegionRole: role, addressSpaceId: "flash");
    }
}
