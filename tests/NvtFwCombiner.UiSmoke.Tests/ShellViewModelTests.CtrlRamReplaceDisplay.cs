using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM slots refresh to the FWConfig-selected postbuild category after base load.</summary>
    [Fact]
    public async Task CtrlRamBaseFirmwareRefreshesVersionedNt51926Slots()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = "cascade";
        OpenReplace(viewModel, "CtrlRAM");
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));

        Assert.Contains(viewModel.ReplaceSlots, slot =>
            slot.SlotId == "replace-ctrlram-vn" &&
            slot.Description.Contains("VN_Ctrlram.bin", StringComparison.Ordinal));

        await viewModel.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);

        Assert.Contains(viewModel.CtrlRamRegions, region =>
            region.Name == "VN CtrlRAM" &&
            region.SizeHex == "len 0x1660");
        Assert.Contains(viewModel.ReplaceSlots, slot =>
            slot.SlotId == "replace-ctrlram-vn" &&
            slot.Description.Contains("VN_Ctrlram.bin", StringComparison.Ordinal) &&
            slot.Description.Contains("max 5728 B", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM" &&
            segment.RangeLabel == "0x315D0-0x32C2F (len 0x1660)");
    }

    /// <summary>Verifies CtrlRAM guidance names accepted base forms once and retains source-size safety.</summary>
    [Fact]
    public void ReplaceBaseSlotNamesAcceptedFirmwareKindsWithoutBadges()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        OpenReplace(viewModel, "CtrlRAM");
        var traditionalChinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);

        Assert.Equal("Base firmware (FlashCode / TP FW)", viewModel.ReplaceBaseSlot.Title);
        Assert.Equal(
            "基底韌體 (FlashCode / TP FW)",
            traditionalChinese.GetReplaceBaseTitle("CtrlRAM"));
        Assert.Contains("complete FlashCode or TP FW", viewModel.Text.CtrlRamInputFilesDetail, StringComparison.Ordinal);
        Assert.Contains("short files stop at EOF", viewModel.Text.CtrlRamInputFilesDetail, StringComparison.Ordinal);
        Assert.Contains("完整 FlashCode 或 TP FW", traditionalChinese.CtrlRamInputFilesDetail, StringComparison.Ordinal);
        Assert.Contains("短檔於 EOF 停止", traditionalChinese.CtrlRamInputFilesDetail, StringComparison.Ordinal);
        Assert.Contains("Complete FlashCode or TP FW", viewModel.ReplaceBaseSlot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("base flash", viewModel.Text.CtrlRamInputFilesDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base flash", traditionalChinese.CtrlRamInputFilesDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FlashCode", viewModel.Text.CtrlRamFirmwareVersionCurrentLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("FlashCode", traditionalChinese.CtrlRamFirmwareVersionSourceDetail, StringComparison.Ordinal);
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "Base firmware (FlashCode / TP FW)");

        OpenReplace(viewModel, "DP");
        Assert.Equal("Base firmware (FlashCode)", viewModel.ReplaceBaseSlot.Title);
        Assert.DoesNotContain("TP FW", viewModel.ReplaceBaseSlot.Title, StringComparison.Ordinal);

        OpenReplace(viewModel, "General");
        Assert.Equal("Base firmware (FlashCode)", viewModel.ReplaceBaseSlot.Title);
        Assert.DoesNotContain("TP FW", viewModel.ReplaceBaseSlot.Title, StringComparison.Ordinal);
    }

    /// <summary>Verifies CtrlRAM plan rows promote readable region labels over raw postbuild filenames.</summary>
    [Fact]
    public void CtrlRamPlanRowsExposeReadablePrimaryLabels()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51950";
        OpenReplace(viewModel, "CtrlRAM");

        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace + CRC" &&
            row.AfterSource == "NF_Ctrlram.bin" &&
            row.PrimaryLabel == "NF CtrlRAM");
        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace + CRC" &&
            row.AfterSource == "Normal_Ctrlram.bin" &&
            row.PrimaryLabel == "Normal CtrlRAM");
        Assert.Contains(viewModel.ReplaceMemoryRows, row =>
            row.ActionLabel == "Replace + CRC" &&
            row.AfterSource == "VN_Ctrlram.bin" &&
            row.PrimaryLabel == "VN CtrlRAM");
        Assert.All(
            viewModel.ReplaceMemoryRows.Where(row => row.ActionLabel == "Replace + CRC"),
            row =>
            {
                Assert.DoesNotContain(".bin", row.PrimaryLabel, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("_", row.PrimaryLabel, StringComparison.Ordinal);
            });
    }

    /// <summary>Verifies topology-neutral NT51950 inputs use Common while DiffDLM belongs to Cascade.</summary>
    [Fact]
    public void Nt51950DiffDlmUsesCommonOnlyForCascade()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.SelectedNumber = "cascade";
        OpenReplace(viewModel, "CtrlRAM");

        Assert.Equal(["Cascade", "Common"], viewModel.ReplaceSlotGroups.Select(group => group.Title));
        FirmwareSlotGroupViewModel cascade = viewModel.ReplaceSlotGroups[0];
        FirmwareSlotGroupViewModel common = viewModel.ReplaceSlotGroups[1];
        Assert.Contains(cascade.Slots, slot => slot.Title == "DiffDLM");
        Assert.DoesNotContain(common.Slots, slot => slot.Title == "DiffDLM");
        Assert.Contains(viewModel.ReplaceCoverageGroups, group =>
            group.Title == "Cascade" &&
            group.Segments.Any(segment => segment.SourceLabel == "DIFF CtrlRAM"));
        Assert.DoesNotContain(viewModel.ReplaceSlotGroups, group => group.Title == "Single IC");

        viewModel.SelectedNumber = "single";

        Assert.DoesNotContain(viewModel.ReplaceSlots, slot => slot.Title == "DiffDLM");
        Assert.Equal("Common", Assert.Single(viewModel.ReplaceSlotGroups).Title);
    }

    /// <summary>Verifies NT51926 keeps DiffDLM in a dedicated cascade group.</summary>
    [Fact]
    public void Nt51926DiffDlmBelongsToCascade()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = "cascade";
        OpenReplace(viewModel, "CtrlRAM");

        Assert.Equal(["Cascade", "Common"], viewModel.ReplaceSlotGroups.Select(group => group.Title));
        Assert.Contains(viewModel.ReplaceSlotGroups[0].Slots, slot => slot.SlotId == "replace-ctrlram-diff");
        Assert.DoesNotContain(viewModel.ReplaceSlotGroups[1].Slots, slot => slot.SlotId == "replace-ctrlram-diff");
        Assert.Contains(viewModel.ReplaceCoverageGroups, group =>
            group.Title == "Cascade" &&
            group.Segments.Any(segment => segment.SourceLabel == "DIFF CtrlRAM"));
    }

    /// <summary>Verifies NT51927 three-chip CtrlRAM Replace exposes physical shared and per-chip inputs.</summary>
    [Fact]
    public void CtrlRamReplaceSlotsIncludeNt51927RightAndLeftSlaves()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        OpenReplace(viewModel, "CtrlRAM");

        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "Normal CtrlRAM (Slave L)");
        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "MP CtrlRAM (Slave R)");
        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "MP CtrlRAM (Slave L)");
        Assert.Equal(8, viewModel.ReplaceSlots.Count(slot =>
            slot.SlotId.StartsWith("replace-ctrlram-", StringComparison.Ordinal)));
        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "NF CtrlRAM (Shared)");
        Assert.Contains(viewModel.ReplaceSlots, slot => slot.Title == "VN CtrlRAM (Shared)");
        Assert.DoesNotContain(viewModel.ReplaceSlots, slot => slot.Title == "VN CtrlRAM (Slave L)");
        Assert.Contains(viewModel.CtrlRamRegions, region => region.Name == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.CtrlRamRegions, region => region.Name == "Normal CtrlRAM (Slave L)");
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Common" && group.IsExpanded);
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Master" && group.IsExpanded);
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Slave R" && !group.IsExpanded);
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Slave L" && !group.IsExpanded);
        Assert.Equal(
            ["Common", "Master", "Slave R", "Slave L"],
            viewModel.ReplaceSlotGroups.Select(group => group.Title));
        Assert.True(viewModel.IsReplaceCoverageGrouped);
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Master" && group.IsExpanded);
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Slave R" && !group.IsExpanded);
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Slave L" && !group.IsExpanded);
    }

    /// <summary>Verifies coverage summaries describe unchanged areas without presenting a Preserve region.</summary>
    [Fact]
    public void CtrlRamReplaceCoverageUsesBaseFirmwareAndChangedAreaWording()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        OpenReplace(viewModel, "CtrlRAM");

        Assert.Contains(viewModel.ReplaceCoverageGroups, group =>
            group.Title == "Base firmware (FlashCode / TP FW)" &&
            group.Summary.Contains("base firmware BIN", StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel.Contains("Base flash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(viewModel.ReplaceMemoryRows, row =>
            row.BeforeSource.Contains("Base flash", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(viewModel.ReplaceCoverageGroups, group =>
            group.Title == "Base firmware (FlashCode / TP FW)" &&
            group.ChangeSummary == "Kept from base firmware.");
        Assert.All(
            viewModel.ReplaceCoverageGroups.Where(group => group.Title != "Base firmware (FlashCode / TP FW)"),
            group => Assert.StartsWith("0 selected /", group.ChangeSummary, StringComparison.Ordinal));
    }

    /// <summary>Verifies the Replace selection overview keeps collapsed CtrlRAM choices discoverable.</summary>
    [Fact]
    public void ReplaceSelectionOverviewTracksSelectedCtrlRamTargets()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-selection");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        OpenReplace(viewModel, "CtrlRAM");

        Assert.Equal("0 / 8 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.DoesNotContain(viewModel.ReplaceCoverageSegments, static segment => segment.IsChanged);
        Assert.All(viewModel.ReplaceCoverageSegments, static segment => Assert.True(segment.UsesKeptPattern));
        Assert.Contains("Build blocked", viewModel.ReplaceSelectionStatusLabel, StringComparison.Ordinal);
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "Base firmware (FlashCode / TP FW)");
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "CtrlRAM replacement");
        FirmwareSlotGroupViewModel slaveLGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Slave L");
        Assert.Equal("0/2", slaveLGroup.CountLabel);
        Assert.Equal("2 areas. None selected.", slaveLGroup.SelectionSummary);
        FirmwareSlotGroupViewModel sharedGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Common");
        Assert.Equal("0/2", sharedGroup.CountLabel);

        FirmwareSlotViewModel vn = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        viewModel.SetSlotFile(vn.SlotId, workspace.Write("vn.bin", [0x00]));

        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.RegionId == vn.RegionId && segment.IsChanged && !segment.UsesKeptPattern);
        Assert.DoesNotContain(viewModel.ReplaceCoverageSegments, segment =>
            segment.RegionId != vn.RegionId && segment.IsChanged);
        Assert.All(
            viewModel.ReplaceCoverageSegments.Where(segment => segment.RegionId != vn.RegionId),
            static segment => Assert.True(segment.UsesKeptPattern));

        viewModel.SetSlotFile("replace-base", workspace.PathFor("base.bin"));

        sharedGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Common");
        Assert.Equal("1 / 8 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Equal("1/2", sharedGroup.CountLabel);
        Assert.Equal("1 selected / 2 areas.", sharedGroup.SelectionSummary);
        Assert.Equal("Ready for Build", viewModel.ReplaceSelectionStatusLabel);
        Assert.Empty(viewModel.ReplaceSelectionMissingRows);
        Assert.Contains(viewModel.ReplaceSelectionRows, row =>
            row.Title == "VN CtrlRAM (Shared)" &&
            row.Detail == "vn.bin" &&
            row.Meta.Contains("VN_Ctrlram.bin", StringComparison.Ordinal) &&
            row.Meta.Contains("VN CtrlRAM (Slave L)", StringComparison.Ordinal));
        Assert.Contains("Build will validate", viewModel.ReplaceSelectionRunHint, StringComparison.Ordinal);

        Assert.False(viewModel.IsReplaceSelectionModalOpen);
        viewModel.ShowReplaceSelectionCommand.Execute(null);
        Assert.True(viewModel.IsReplaceSelectionModalOpen);
        viewModel.CloseReplaceSelectionCommand.Execute(null);
        Assert.False(viewModel.IsReplaceSelectionModalOpen);
    }
}
