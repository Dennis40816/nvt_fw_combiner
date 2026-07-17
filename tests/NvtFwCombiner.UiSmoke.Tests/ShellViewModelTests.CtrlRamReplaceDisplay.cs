using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM slots refresh to the FWConfig-selected postbuild category after base load.</summary>
    [Fact]
    public void CtrlRamBaseFirmwareRefreshesVersionedNt51926Slots()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = "cascade";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));

        Assert.Contains(viewModel.ReplaceSlots, slot =>
            slot.SlotId == "replace-ctrlram-vn" &&
            slot.Description.Contains("VN_Ctrlram.bin", StringComparison.Ordinal) &&
            slot.Description.Contains("max 5278 bytes", StringComparison.Ordinal));

        viewModel.SetSlotFile("replace-base", basePath);

        Assert.Contains(viewModel.CtrlRamRegions, region =>
            region.Name == "VN CtrlRAM" &&
            region.SizeHex == "len 0x1660");
        Assert.Contains(viewModel.ReplaceSlots, slot =>
            slot.SlotId == "replace-ctrlram-vn" &&
            slot.Description.Contains("VN_Ctrlram.bin", StringComparison.Ordinal) &&
            slot.Description.Contains("max 5728 bytes", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM" &&
            segment.RangeLabel == "0x315D0-0x32C2F (len 0x1660)");
    }

    /// <summary>Verifies CtrlRAM plan rows promote readable region labels over raw postbuild filenames.</summary>
    [Fact]
    public void CtrlRamPlanRowsExposeReadablePrimaryLabels()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51950";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

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

    /// <summary>Verifies NT51927 three-chip CtrlRAM Replace exposes physical shared and per-chip inputs.</summary>
    [Fact]
    public void CtrlRamReplaceSlotsIncludeNt51927RightAndLeftSlaves()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

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
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Shared inputs" && group.IsExpanded);
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Master" && group.IsExpanded);
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Slave R" && !group.IsExpanded);
        Assert.Contains(viewModel.ReplaceSlotGroups, group => group.Title == "Slave L" && !group.IsExpanded);
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
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        Assert.Contains(viewModel.ReplaceCoverageGroups, group =>
            group.Title == "Base firmware" &&
            group.Summary.Contains("base flash BIN", StringComparison.Ordinal));
        Assert.All(viewModel.ReplaceCoverageGroups, group =>
        {
            Assert.DoesNotContain("preserv", group.ChangeSummary, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.ChangeSummary == "No replaceable areas.");
        Assert.Contains(viewModel.ReplaceCoverageGroups, group =>
            group.ChangeSummary.Contains("replaceable /", StringComparison.Ordinal));
    }

    /// <summary>Verifies the Replace selection overview keeps collapsed CtrlRAM choices discoverable.</summary>
    [Fact]
    public void ReplaceSelectionOverviewTracksSelectedCtrlRamTargets()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-selection");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        Assert.Equal("0 / 8 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Contains("Build blocked", viewModel.ReplaceSelectionStatusLabel, StringComparison.Ordinal);
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "Base flash BIN");
        Assert.Contains(viewModel.ReplaceSelectionMissingRows, row => row.Title == "CtrlRAM replacement");
        FirmwareSlotGroupViewModel slaveLGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Slave L");
        Assert.Equal("0/2", slaveLGroup.CountLabel);
        Assert.Equal("2 areas. None selected.", slaveLGroup.SelectionSummary);
        FirmwareSlotGroupViewModel sharedGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Shared inputs");
        Assert.Equal("0/2", sharedGroup.CountLabel);

        FirmwareSlotViewModel vn = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        viewModel.SetSlotFile("replace-base", workspace.PathFor("base.bin"));
        viewModel.SetSlotFile(vn.SlotId, workspace.PathFor("vn.bin"));

        sharedGroup = viewModel.ReplaceSlotGroups.Single(group => group.Title == "Shared inputs");
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
