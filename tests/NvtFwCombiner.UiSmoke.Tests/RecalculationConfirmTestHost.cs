// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

internal sealed class RecalculationConfirmTestHost : IDisposable
{
    internal RecalculationConfirmTestHost(bool canRecalculate = true, bool expanded = false,
        bool savedWrite = false, bool dark = false, bool chinese = false, int height = 760,
        IReadOnlyList<long>? offsets = null, IReadOnlyList<ByteRange>? coverage = null, bool includeSegments = true,
        string icName = "NT51927")
    {
        Modal = new RecalculationConfirmModal();
        Before = new Button { Content = "Before modal", Width = 1, Height = 1, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Left };
        After = new Button { Content = "After modal", Width = 1, Height = 1, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Right };
        var backdrop = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        AddBackdropPanel(backdrop, 56);
        AddBackdropPanel(backdrop, 64);
        AddBackdropPanel(backdrop, 200);
        AddBackdropPanel(backdrop, 240);
        Window = new Window
        {
            Width = 960,
            Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new Grid { Children = { backdrop, Before, After, Modal } },
        };
        _ = Window.Bind(TemplatedControl.BackgroundProperty, Window.GetResourceObservable("NfcAppBackgroundBrush"));
        Window.Show();
        Layout();
        Assert.True(Before.Focus(NavigationMethod.Tab));
        Model = Example(Outcomes.Add, canRecalculate, savedWrite, chinese, offsets, coverage, includeSegments, icName);
        Expand(Model, expanded);
        Modal.Model = Model;
        Layout();
    }

    internal RecalculationConfirmModel Model { get; }
    internal RecalculationConfirmModal Modal { get; }
    internal Window Window { get; }
    internal Button Before { get; }
    internal Button After { get; }
    internal List<RecalculationConfirmOutcome> Outcomes { get; } = [];

    internal T Find<T>(string name) where T : Control
    {
        return Modal.FindControl<T>(name)!;
    }

    internal Button[] VisibleButtons => [.. Modal.GetVisualDescendants().OfType<Button>()
        .Where(button => button is not ToggleButton && button.IsEffectivelyVisible)];

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

    internal void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        // Tab, Escape, Space and Enter share their names in both enums.
        PhysicalKey physicalKey = Enum.Parse<PhysicalKey>(key.ToString());
        Window.KeyPress(key, modifiers, physicalKey, null);
        Window.KeyRelease(key, modifiers, physicalKey, null);
        Layout();
    }

    internal static RecalculationConfirmModel Example(Action<RecalculationConfirmOutcome> reportOutcome,
        bool canRecalculate = true, bool savedWrite = false, bool chinese = false,
        IReadOnlyList<long>? offsets = null, IReadOnlyList<ByteRange>? coverage = null, bool includeSegments = true,
        string icName = "NT51927")
    {
        IReadOnlyList<long> storage = offsets ?? [0x23C, 0x24C, 0x26C, 0x27C];
        RecalculationConfirmSegment[] segments = includeSegments
            ? [new(new ByteRange(0x200, 0x100), storage)] : [];
        return new(icName, canRecalculate, storage, coverage, segments, "1.3.0", "1.13.0",
            "NT51927BASED_GEN_CRC_MODE", "CRC32", reportOutcome,
            savedWrite ? RecalculationConfirmChoice.WriteWithoutRecalculation : RecalculationConfirmChoice.Recalculate,
            chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
    }

    internal static string InlineText(TextBlock text)
    {
        return string.Concat(text.Inlines!.OfType<Run>().Select(run => run.Text));
    }

    internal static void AssertUnclipped(TextBlock text)
    {
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.All(text.TextLayout.TextLines, line =>
        {
            Assert.False(line.HasCollapsed);
            Assert.False(line.HasOverflowed);
            Assert.InRange(line.Width, 0, text.Bounds.Width + 1);
        });
        Assert.InRange(text.TextLayout.Height, 0, text.Bounds.Height + 1);
    }

    private static void Expand(RecalculationConfirmModel model, bool expanded)
    {
        if (expanded) { model.ToggleDetailsCommand.Execute(null); }
    }

    private static void AddBackdropPanel(StackPanel backdrop, double height)
    {
        var panel = new Border { Height = height, BorderThickness = new Thickness(2) };
        _ = panel.Bind(Border.BackgroundProperty, panel.GetResourceObservable("NfcSurfaceBrush"));
        _ = panel.Bind(Border.BorderBrushProperty, panel.GetResourceObservable("NfcBorderMutedBrush"));
        _ = panel.Bind(Border.CornerRadiusProperty, panel.GetResourceObservable("NfcSurfaceCornerRadius"));
        backdrop.Children.Add(panel);
    }

    public void Dispose()
    {
        Window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
