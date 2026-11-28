// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Shared synthetic host for the standalone layered panel; no workflow page is involved.</summary>
internal sealed class OutputLayoutPanelTestHost : IDisposable
{
    internal OutputLayoutPanelTestHost(OutputLayoutPanelModel? model = null, int width = 1280, bool dark = false,
        ShellLanguage language = ShellLanguage.English)
    {
        Panel = new OutputLayoutPanel
        {
            Model = model ?? Example(language),
            Labels = ShellTextResources.For(language),
            ReducedMotion = true,
        };
        Window = new Window
        {
            Width = width,
            Height = 1000,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new Border { Padding = new Thickness(28), Child = Panel, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top },
        };
        Window.Show();
        Layout();
    }

    internal OutputLayoutPanel Panel { get; }
    internal Window Window { get; }
    internal Canvas Bar => Panel.FindControl<Canvas>("LayoutBar")!;
    internal Canvas Pins => Panel.FindControl<Canvas>("LayoutPins")!;
    internal Button Row(string pin)
    {
        return Find<Button>($"OutputLayout.Row.{pin}");
    }

    internal Button Group(OutputLayoutGroupKind kind)
    {
        return Find<Button>($"OutputLayout.Group.{kind}");
    }

    internal T Find<T>(string id) where T : Control
    {
        return Assert.Single(Panel.GetVisualDescendants().OfType<T>(),
        control => string.Equals(AutomationProperties.GetAutomationId(control), id, StringComparison.Ordinal));
    }

    internal static Control Body(Button header)
    {
        return ((StackPanel)header.Parent!).Children[1];
    }

    internal Button Technical(string pin)
    {
        return Assert.Single(Body(Row(pin)).GetVisualDescendants().OfType<Button>(),
        button => AutomationProperties.GetAutomationId(button) == "OutputLayout.Technical");
    }

    internal Rect Bounds(Control control)
    {
        return new(control.TranslatePoint(default, Window)!.Value, control.Bounds.Size);
    }

    internal void Click(Control target)
    {
        Point point = Bounds(target).Center;
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left);
        Window.MouseUp(point, MouseButton.Left);
        Layout();
    }

    internal void Key(Key key, PhysicalKey physicalKey, string? symbol = null)
    {
        Window.KeyPress(key, RawInputModifiers.None, physicalKey, symbol);
        Window.KeyRelease(key, RawInputModifiers.None, physicalKey, symbol);
        Layout();
    }

    internal void Layout()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        Window.Close();
    }

    internal static OutputLayoutPanelModel Example(ShellLanguage language = ShellLanguage.English)
    {
        return new(
        [Slice(0, 0x6000, "DP BIN", role: MemoryCoverageFillRole.Dp, slot: "dp", language: language),
            Slice(0x6000, 0x7000, language == ShellLanguage.English ? "Unmapped" : "未對應",
                MemoryWorkflowDisposition.Blank, language: language),
            Slice(0x7000, 0x40000, "TP BIN", role: MemoryCoverageFillRole.Tp, slot: "tp", language: language)],
        "256 KiB", "0x00000–0x3FFFF", "0x00000", "0x3FFFF", text: ShellTextResources.For(language));
    }

    internal static MemoryCoverageSegmentViewModel Slice(long start, long end, string title,
        MemoryWorkflowDisposition disposition = MemoryWorkflowDisposition.WillWrite,
        MemoryObservedChange observed = MemoryObservedChange.NotObserved,
        MemoryDiagnosticSeverity severity = MemoryDiagnosticSeverity.None,
        MemoryCoverageFillRole role = MemoryCoverageFillRole.Neutral, string? artifact = null, string? slot = null,
        ShellLanguage language = ShellLanguage.English, double? weight = null)
    {
        var text = ShellTextResources.For(language);
        string address = FormattableString.Invariant($"0x{start:X5}–0x{end - 1:X5}");
        return new(address, disposition == MemoryWorkflowDisposition.Blank ? text.MemorySourceNotAssignedLabel : title,
            disposition == MemoryWorkflowDisposition.Blank ? text.MemorySourceNotAssignedDetail : text.FormatOutputLayoutSourceDetail(title, disposition),
            role, weight ?? (end - start), disposition, observed, severity,
            usesBaseFirmwarePattern: disposition == MemoryWorkflowDisposition.Kept, regionId: "region", sourceSlotId: slot,
            text: text, rangeStart: start, rangeEndExclusive: end, addressRangeLabel: address,
            displayTitle: title, addressSpaceId: "flash", contentArtifactIdentity: artifact);
    }
}
