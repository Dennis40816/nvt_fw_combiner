using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real input-loaded pages keep firmware context and physical CtrlRAM targets readable.</summary>
public sealed class CtrlRamMemoryLayoutTests
{
    /// <summary>Shared NF and VN slot guidance retains exact limits and all three targets across input changes.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCtrlRamCardsKeepSizeAndEveryTargetAfterLoading(bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("ctrlram-shared-guidance");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
            width: 1180, height: 1040, dark: darkChinese);
        await host.LoadThreeChipAsync();
        string[] shared = [.. host.Shell.Replace.ReplaceSlots.Where(static slot =>
            slot.HasFile && slot.CtrlRamDescriptionFacts is { IsShared: true }).Select(static slot => slot.SlotId)];
        Assert.Equal(2, shared.Length);
        foreach (string slotId in shared)
        {
            FirmwareSlotViewModel current = Assert.Single(host.Shell.Replace.ReplaceSlots,
                slot => slot.SlotId == slotId);
            string path = Assert.IsType<string>(current.FilePath);
            for (int step = 0; step < 3; step++)
            {
                if (step == 1)
                {
                    await host.Shell.WorkflowSession.ClearSlotFileAsync(slotId, TestContext.Current.CancellationToken);
                }
                else if (step == 2)
                {
                    await host.Shell.WorkflowSession.SetSlotFileAsync(slotId, path, TestContext.Current.CancellationToken);
                }
                foreach (bool chinese in new[] { false, true })
                {
                    host.Shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
                    host.Render();
                    FirmwareSlotViewModel slot = Assert.Single(host.Shell.Replace.ReplaceSlots,
                        candidate => candidate.SlotId == slotId);
                    Assert.Equal(step != 1, slot.HasFile);
                    Assert.Equal(step == 1, slot.IsGuidanceVisible);
                    FirmwareSlotCard card = Assert.Single(host.Window.GetVisualDescendants().OfType<FirmwareSlotCard>(),
                        control => ReferenceEquals(control.DataContext, slot));
                    card.BringIntoView();
                    host.Render();
                    AssertSharedCtrlRamGuidance(card, slot, chinese);
                }
            }
            host.Capture($"nt51927-shared-{slotId}-{darkChinese}");
        }
    }

    /// <summary>Expanded CtrlRAM details retain the selected or kept state of each physical region.</summary>
    [AvaloniaTheory]
    [InlineData(CtrlRamRegionRole.Mp, true)]
    [InlineData(CtrlRamRegionRole.Normal, true)]
    [InlineData(CtrlRamRegionRole.Mp, false)]
    [InlineData(CtrlRamRegionRole.Normal, false)]
    public async Task LoadedCtrlRamWindowStartsWithOnlyTheOverview(CtrlRamRegionRole role, bool selected)
    {
        using var workspace = TempWorkspace.Create("ctrlram-layout-collapsed");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
            width: 1180, height: 1040);
        await host.LoadThreeChipAsync();
        if (!selected)
        {
            string[] unselected = [.. host.Shell.Replace.ReplaceSlots.Where(static slot =>
                slot.HasFile && slot.CtrlRamDescriptionFacts is { IsShared: false }).Select(static slot => slot.SlotId)];
            foreach (string slotId in unselected)
            {
                await host.Shell.WorkflowSession.ClearSlotFileAsync(slotId, TestContext.Current.CancellationToken);
            }
            await host.SettleAsync();
        }
        OutputLayoutPanelModel model = host.Model;
        Assert.Equal(3, host.Shell.Replace.CtrlRamFocusLanes.Count);
        Assert.NotEmpty(model.Segments);
        Assert.Equal("0x00000", model.StartAddress);
        Assert.Equal("0x3FFFF", model.EndAddress);
        Assert.DoesNotContain(host.Window.GetVisualDescendants(),
            static control => control.Name is "MemoryLocalView" or "MemorySliceCard");
        MemoryCoverageSegmentViewModel target = model.RowSegments.First(segment =>
            segment.CtrlRamRegionRole == role && segment.IsSelectedForWrite == selected);
        Button row = host.RowFor(target);
        Assert.False(OutputLayoutPanelTestHost.Body(row).IsVisible);
        Assert.Contains(model.LaneLinks, link => link.Row == OutputLayoutRowIdentity.For(target));
        host.AssertDetails(target);
        Assert.Equal(selected, target.IsSelectedForWrite);
        host.Capture($"nt51927-inline-{role}-{selected}");
    }

    /// <summary>Hover on the TP overview slice highlights its numbered row without opening a floating card.</summary>
    [AvaloniaFact]
    public async Task HoveringTheTpFirmwareSliceLightsItsLegendRowAndConnectsItsCard()
    {
        using var workspace = TempWorkspace.Create("ctrlram-layout-tp-hover");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
            width: 1180, height: 1040);
        await host.LoadThreeChipAsync();
        MemoryCoverageSegmentViewModel tp = host.Model.Rows.SelectMany(static row => row.Members).First(
            static segment => segment.ContentRole == MemoryContentRole.Tp);
        Button row = host.RowFor(tp);
        Border highlight = Assert.Single(row.GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("outputLayoutRow"));
        host.Window.MouseMove(host.Bounds(row).Center, Avalonia.Input.RawInputModifiers.None);
        host.Render();
        Assert.Contains("active", highlight.Classes);
        Assert.False(OutputLayoutPanelTestHost.Body(row).IsVisible);
        host.Window.MouseMove(new Point(2, 2), Avalonia.Input.RawInputModifiers.None);
        host.Render();
        Assert.DoesNotContain("active", highlight.Classes);
        host.AssertDetails(tp);
    }

    /// <summary>Eight inputs project twelve targets into three independent physical lanes.</summary>
    [AvaloniaFact]
    public async Task ThreeChipWindowShowsFirmwareOverviewAndSeparatePhysicalLanes()
    {
        using var workspace = TempWorkspace.Create("ctrlram-layout-threechip");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
            width: 1180, height: 1040);
        await host.LoadThreeChipAsync();
        Assert.Equal("NT51927", host.Shell.WorkflowSession.SelectedIc);
        Assert.Equal("3", host.Shell.WorkflowSession.SelectedNumber);
        Assert.Equal(8, host.Shell.Replace.ReplaceSlots.Count(static slot => slot.SlotKind == FirmwareSlotKind.CtrlRam && slot.HasFile));
        Assert.Empty(host.Shell.Reports.ReportHistoryEntries);
        OutputLayoutPanelModel model = host.Model;
        Assert.Contains(model.Segments, static segment => segment.ContentRole == MemoryContentRole.Tp);
        Assert.Contains(model.Segments, static segment => segment.ContentRole == MemoryContentRole.Dp);
        Assert.Equal(["Master", "Slave R", "Slave L"],
            model.LaneLinks.Select(static link => link.Lane.Title).Distinct());
        Assert.Equal([0x16800L, 0x1F800L, 0x28800L],
            model.LaneLinks.Select(static link => link.Lane.Start).Distinct());
        foreach (MemoryFocusLaneViewModel lane in host.Shell.Replace.CtrlRamFocusLanes)
        {
            Assert.Equal([CtrlRamRegionRole.Nf, CtrlRamRegionRole.Normal, CtrlRamRegionRole.Mp, CtrlRamRegionRole.Vn],
                lane.Ranges.Select(static range => range.CtrlRamRegionRole));
            Assert.Contains(model.LaneLinks, link => ReferenceEquals(link.Lane, lane) && link.Zoom is not null);
        }
        MemoryCoverageSegmentViewModel rightNf = model.RowSegments.First(static segment =>
            segment.CtrlRamRegionRole == CtrlRamRegionRole.Nf && segment.AddressRangeLabel == "0x1F800-0x207CF");
        Assert.Equal("NF CtrlRAM · Slave R", rightNf.DisplayTitle);
        host.AssertDetails(rightNf);
        host.Capture("nt51927-threechip-inline");
        host.Window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        host.Shell.SelectedLanguage = "Traditional Chinese";
        host.Render();
        Assert.Equal(["主 IC", "右從 IC", "左從 IC"],
            host.Model.LaneLinks.Select(static link => link.Lane.Title).Distinct());
        Assert.Equal(model.RowSegments.Select(static segment => (segment.RangeStart, segment.RangeEndExclusive)),
            host.Model.RowSegments.Select(static segment => (segment.RangeStart, segment.RangeEndExclusive)));
        await host.Shell.WorkflowSession.ClearSlotFileAsync(host.Shell.Replace.ReplaceBaseSlot.SlotId,
            TestContext.Current.CancellationToken);
        await host.SettleAsync();
        Assert.Empty(host.Model.LaneLinks);
        Assert.False(host.Shell.Replace.HasCtrlRamFocusLayout);
    }

    /// <summary>Standard Merge still exposes DP, TP and LDC through its own production panel.</summary>
    [AvaloniaFact]
    public async Task Nt51928StandardWindowKeepsDpTpAndLdcCoverage()
    {
        using var workspace = TempWorkspace.Create("memory-standard-928");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace,
            width: 1180, height: 1040);
        await host.LoadStandardAsync("NT51928");
        Assert.True(host.Shell.Merge.IsNormalMergeModeSelected);
        Assert.Equal(3, host.Shell.Merge.MergeSlots.Count(static slot => slot.HasFile));
        Assert.Empty(host.Shell.Reports.ReportHistoryEntries);
        Assert.Contains(host.Model.Segments, static segment => segment.ContentRole == MemoryContentRole.Dp);
        Assert.Contains(host.Model.Segments, static segment => segment.ContentRole == MemoryContentRole.Tp);
        Assert.Contains(host.Model.Segments, static segment => segment.ContentRole == MemoryContentRole.Ldc);
        Canvas bar = host.Panel.FindControl<Canvas>("LayoutBar")!;
        Assert.Equal(34, bar.Bounds.Height);
        Assert.NotEmpty(bar.Children);
        host.Capture("nt51928-standard-actual");
    }

    private static void AssertSharedCtrlRamGuidance(FirmwareSlotCard card, FirmwareSlotViewModel slot, bool chinese)
    {
        bool nf = slot.SlotId == "replace-ctrlram-nf";
        Assert.True(nf || slot.SlotId == "replace-ctrlram-vn");
        string targets = nf
            ? chinese ? "主 IC: 0x16800\n右從 IC: 0x1F800\n左從 IC: 0x28800" : "Master: 0x16800\nSlave R: 0x1F800\nSlave L: 0x28800"
            : chinese ? "主 IC: 0x1CBD0\n右從 IC: 0x25BD0\n左從 IC: 0x2EBD0" : "Master: 0x1CBD0\nSlave R: 0x25BD0\nSlave L: 0x2EBD0";
        string[] expected = [chinese ? "大小上限" : "Max Size", nf ? "12,112\u00a0B" : "5,728\u00a0B",
            chinese ? "目標位址" : "Target Addr", targets];
        foreach (string text in expected)
        {
            TextBlock block = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), candidate => candidate.Text == text);
            Assert.True(block.IsEffectivelyVisible, $"{slot.SlotId}: '{text}' disappeared (HasFile={slot.HasFile}).");
            Assert.Equal(text.Split('\n').Length, block.TextLayout.TextLines.Count);
            Assert.All(block.TextLayout.TextLines, static line => Assert.False(line.HasCollapsed));
            Assert.True(block.TextLayout.Height <= block.Bounds.Height + 1, $"{slot.SlotId}: '{text}' is clipped vertically.");
            Point position = block.TranslatePoint(default, card)!.Value;
            Assert.InRange(position.X, 0, card.Bounds.Width - block.Bounds.Width + 1);
            Assert.InRange(position.Y, 0, card.Bounds.Height - block.Bounds.Height + 1);
        }
        Assert.Equal(3, slot.CtrlRamDescriptionFacts!.InputGuidanceTargets.Count);
        Assert.Equal(nf ? 5 : 3, slot.CtrlRamDescriptionFacts.Sections.Count);
    }
}
