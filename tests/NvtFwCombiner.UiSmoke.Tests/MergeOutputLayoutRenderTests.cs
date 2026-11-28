// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Captures the complete production Merge page for the approved layout review matrix.</summary>
public sealed class MergeOutputLayoutRenderTests
{
    /// <summary>Every state is captured in both themes, languages and supported review widths.</summary>
    public static TheoryData<string, int, bool, bool> Cases
    {
        get
        {
            var cases = new TheoryData<string, int, bool, bool>();
            foreach (string state in new[] { "standard", "general", "expanded", "ab", "waiting", "blocked" })
            {
                foreach (int width in new[] { 1280, 960 })
                {
                    cases.Add(state, width, false, false);
                    cases.Add(state, width, false, true);
                    cases.Add(state, width, true, false);
                    cases.Add(state, width, true, true);
                }
            }
            return cases;
        }
    }

    /// <summary>Writes 48 production-page screenshots and asserts the reference hierarchy and containment.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public async Task RenderProductionMergeReviewMatrix(string state, int width, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("merge-layout-render");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace, width, height: 1800, dark: dark, chinese: chinese);
        await PrepareAsync(host, workspace, state);
        host.Render();
        OutputLayoutPanel panel = host.Panel;
        TextBlock title = panel.FindControl<TextBlock>("LayoutTitle")!;
        StackPanel map = panel.FindControl<StackPanel>("LayoutMap")!;
        StackPanel notice = panel.FindControl<StackPanel>("LayoutNotice")!;
        Assert.InRange(host.Bounds(panel).Right, 1, width);
        Grid layout = Assert.IsType<Grid>(panel.Parent);
        Assert.Equal(width == 960 ? 2 : 1, Grid.GetColumnSpan(panel));
        Assert.Equal(width == 960 ? 2 : 1, Grid.GetRow(panel));
        Assert.Equal(width == 960 ? 0 : 1, Grid.GetColumn(panel));
        Assert.InRange(Math.Abs(panel.Bounds.Width - (width == 960 ? layout.Bounds.Width : 430)), 0, 0.5);
        Assert.True(host.Bounds(title).Bottom <= host.Bounds(notice.IsVisible ? notice : map).Top);
        Assert.All(panel.GetVisualDescendants().OfType<TextBlock>().Where(static text => text.IsEffectivelyVisible), text =>
        {
            Assert.True(host.Bounds(text).Left >= host.Bounds(panel).Left);
            Assert.True(host.Bounds(text).Right <= host.Bounds(panel).Right + 0.5);
        });
        host.Capture($"merge-output-layout-{state}-{width}-{(dark ? "dark" : "light")}-{(chinese ? "zh-TW" : "en")}");
    }

    /// <summary>Uses accepted production states for planned, pending and admission-blocked captures.</summary>
    private static async Task PrepareAsync(MergeOutputLayoutTestHost host, TempWorkspace workspace, string state)
    {
        if (state == "waiting")
        {
            host.Shell.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
            host.Render();
            Assert.Equal(OutputLayoutNoticeKind.Waiting, host.Model.Notice!.Kind);
            return;
        }
        if (state == "general") { await host.LoadGeneralAsync(workspace); return; }
        if (state == "ab") { await host.LoadAbAsync(); return; }
        if (state == "blocked")
        {
            await host.LoadGeneralAsync(workspace);
            Assert.Single(host.Shell.Merge.GeneralMergeMappings).TargetStartAddress = "0x50000";
            await host.SettleAsync();
            Assert.Equal(OutputLayoutNoticeKind.Blocked, host.Model.Notice!.Kind);
            IssueCardViewModel card = host.Shell.MergeBuildBlockerCard;
            Assert.Equal($"{card.Summary} {card.DiagnosticCode}", host.Model.Notice.Reason);
            return;
        }
        await host.LoadStandardAsync();
        if (state == "expanded")
        {
            host.Open(host.Model.Rows.First(static row => row.Group ==
                Presentation.Avalonia.ViewModels.OutputLayoutGroupKind.Write).Members[0]);
        }
    }
}
