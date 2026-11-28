// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

internal sealed class IssueListTestHost : IDisposable
{
    internal IssueListTestHost(IssueListModel? model = null, int width = 360, bool dark = false,
        bool chinese = false, string? selectedKey = null)
    {
        List = new IssueList
        {
            Model = model ?? Example(),
            Labels = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English),
            SelectedKey = selectedKey,
        };
        Window = new Window
        {
            Width = width + 32,
            Height = 560,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = List,
            },
        };
        Window.Show();
        Layout();
    }

    internal IssueList List { get; }
    internal Window Window { get; }
    internal Button[] Rows => [.. List.GetVisualDescendants().OfType<Button>().Where(row => row.Classes.Contains("issueListRow"))];
    internal ScrollViewer Scroll => List.FindControl<ScrollViewer>("IssueListScroll")!;

    internal Border Card => List.FindControl<Border>("IssueListCard")!;

    internal Button Row(string key)
    {
        return Assert.Single(Rows, row => string.Equals(row.CommandParameter as string, key, StringComparison.Ordinal));
    }

    internal Border Details(string key)
    {
        return Assert.Single(Row(key).GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("issueListDetails"));
    }

    internal Rect Bounds(Control control)
    {
        return new Rect(control.TranslatePoint(default, Window)!.Value, control.Bounds.Size);
    }

    internal void Click(Control control)
    {
        Point point = Bounds(control).Center;
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left);
        Window.MouseUp(point, MouseButton.Left);
        Layout();
    }

    internal void Press(Key key, PhysicalKey physicalKey, string? symbol = null)
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

    internal static IssueListModel Example()
    {
        // Interleaved on purpose: only severity may change the supplied order.
        return new IssueListModel(
        [
            new("overlap", IssueListSeverity.Warning, "Two steps write the same bytes.",
                ["[0x07C00, 0x08400)", "[0x7E000, 0x7E100)"], 2, ["OVERLAPPING_WRITES"]),
            new("past-end", IssueListSeverity.Error, "Step 3 writes past the output end.",
                ["patch.bin", "→ [0x7F000, 0x80400)", "end 0x80000"], 1,
                ["Over by 0x400 bytes", "OUTPUT_RANGE_EXCEEDED · step 3"]),
            new("read-only", IssueListSeverity.Warning, "Writes into a read-only region.",
                ["customer-info", "[0x7E000, 0x7E100)"], 1, []),
            new("crc", IssueListSeverity.Warning, "CRC range changed, no recalculation step.",
                ["TP code CRC", "[0x00000, 0x40000)"], 1, ["CRC_COVERAGE_CHANGED"]),
        ]);
    }

    internal static IssueListItem Item(string key, IssueListSeverity severity = IssueListSeverity.Warning,
        int count = 1, string sentence = "A supplied sentence.")
    {
        return new IssueListItem(key, severity, sentence, [], count, []);
    }

    internal static string[] Keys(IEnumerable<Button> rows)
    {
        return [.. rows.Select(row => Assert.IsType<string>(row.CommandParameter))];
    }

    internal static string Name(Control control)
    {
        return AutomationProperties.GetName(control) ?? string.Empty;
    }
}
