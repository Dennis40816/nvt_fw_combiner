// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

internal sealed class OutputMapTestHost : IDisposable
{
    internal OutputMapTestHost(OutputMapModel? model = null, int width = 960, bool dark = false, bool chinese = false)
    {
        Map = new OutputMap
        {
            Model = model ?? Example(),
            ReducedMotion = true,
            Labels = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English),
        };
        Window = new Window
        {
            Width = width,
            Height = 640,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new Border { Padding = new Thickness(16), Child = Map, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top },
        };
        Window.Show();
        Layout();
    }

    internal OutputMap Map { get; }
    internal Window Window { get; }
    internal Canvas Bar => Map.FindControl<Canvas>("MapBar")!;

    internal T Find<T>(string id) where T : Control
    {
        return Assert.Single(Map.GetVisualDescendants().OfType<T>(),
            c => string.Equals(AutomationProperties.GetAutomationId(c), id, StringComparison.Ordinal));
    }

    internal Button Block(string key)
    {
        return Find<Button>($"OutputMap.Block.{key}");
    }

    internal Button Region(string key)
    {
        return Find<Button>($"OutputMap.Region.{key}");
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

    public void Dispose()
    {
        Window.Close();
    }

    internal static OutputMapModel Example(bool includeHighlight = false)
    {
        return new("output", 0x40000,
            regions:
            [
                new("tp-region", "TP code", new ByteRange(0, 0x3C000)),
                new("reserved", "Reserved", new ByteRange(0x3C000, 0x1000)),
                new("customer", "Customer info", new ByteRange(0x3D000, 0x1000), true),
                new("dp-region", "DP", new ByteRange(0x3E000, 0x2000)),
            ],
            blocks:
            [
                new("tp", new ByteRange(0, 0x3C000), OutputMapBlockKind.Copy, "TP code", "tp-file", new ByteRange(0, 0x3C000)),
                new("fill", new ByteRange(0x3C000, 0x1000), OutputMapBlockKind.Fill, "Fill 0xFF"),
                new("dp", new ByteRange(0x3E000, 0x2000), OutputMapBlockKind.Copy, "DP table", "dp-file", new ByteRange(0, 0x2000)),
                new("patch", new ByteRange(0x3BE00, 0x400), OutputMapBlockKind.Copy, "Fix patch", "patch-file", new ByteRange(0, 0x400)),
            ],
            sources: [new("tp-file", "tp_code.bin", 0x3C000), new("dp-file", "dp_table.bin", 0x2000), new("patch-file", "fix_patch.bin", 0x400)],
            crcAnnotations: [new("TP CRC", new ByteRange(0, 0x3C000), new ByteRange(0x3BFFC, 4))],
            version: new("dp.version", new ByteRange(0x3E016, 3), "v4.12"),
            highlight: includeHighlight ? new OutputMapHighlight([new ByteRange(0x3BE00, 0x200)], OutputMapHighlightSeverity.Warning) : null);
    }
}
