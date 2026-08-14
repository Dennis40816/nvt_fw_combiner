using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>Verifies CtrlRAM slots refresh to the FWConfig-selected postbuild category after base load.</summary>
    [Fact]
    public async Task CtrlRamBaseFirmwareRefreshesVersionedNt51926Slots()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));

        Assert.Contains(viewModel.Replace.ReplaceSlots, slot =>
            slot.SlotId == "replace-ctrlram-vn" &&
            slot.Description.Contains("VN_Ctrlram.bin", StringComparison.Ordinal));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);

        Assert.Contains(viewModel.Replace.CtrlRamRegions, region =>
            region.Name == "VN CtrlRAM" &&
            region.SizeHex == "len 0x1660");
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot =>
            slot.SlotId == "replace-ctrlram-vn" &&
            slot.Description.Contains("VN_Ctrlram.bin", StringComparison.Ordinal) &&
            slot.Description.Contains("max 5728 B", StringComparison.Ordinal));
        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.Equal("Waiting for required inputs", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);

        FirmwareSlotViewModel inputSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-vn");
        FirmwareInspectionSnapshot inspection = Assert.IsType<FirmwareInspectionSnapshot>(
            viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        inputSlot.FilePath = basePath;
        inputSlot.SetCurrentInspectionProjection(inspection);
        inputSlot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Same(inputSlot, viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-vn"));
        Assert.Same(inspection, inputSlot.CurrentInspectionProjection);
        Assert.Equal(FirmwareInputInspectionSeverity.Valid, inputSlot.InputInspectionSeverity);
        Assert.Equal("等待必要輸入", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
    }

    /// <summary>Verifies CtrlRAM guidance names accepted base forms once and retains source-size safety.</summary>
    [Fact]
    public void ReplaceBaseSlotNamesAcceptedFirmwareKindsWithoutBadges()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        var traditionalChinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);

        Assert.Equal("Base firmware (FlashCode / TP FW)", viewModel.Replace.ReplaceBaseSlot.Title);
        Assert.Equal(
            "基底韌體 (FlashCode / TP FW)",
            traditionalChinese.GetReplaceBaseTitle(ExperienceIds.CtrlRamReplace));
        Assert.Contains("complete FlashCode or TP FW", viewModel.Text.CtrlRamInputFilesDetail, StringComparison.Ordinal);
        Assert.Contains("short files stop at EOF", viewModel.Text.CtrlRamInputFilesDetail, StringComparison.Ordinal);
        Assert.Contains("完整 FlashCode 或 TP FW", traditionalChinese.CtrlRamInputFilesDetail, StringComparison.Ordinal);
        Assert.Contains("短檔於 EOF 停止", traditionalChinese.CtrlRamInputFilesDetail, StringComparison.Ordinal);
        Assert.Contains("Complete FlashCode or TP FW", viewModel.Replace.ReplaceBaseSlot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("base flash", viewModel.Text.CtrlRamInputFilesDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base flash", traditionalChinese.CtrlRamInputFilesDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FlashCode", viewModel.Text.CtrlRamFirmwareVersionCurrentLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("FlashCode", traditionalChinese.CtrlRamFirmwareVersionSourceDetail, StringComparison.Ordinal);
        Assert.Contains(viewModel.Replace.ReplaceSelectionMissingRows, row => row.Title == "Base firmware (FlashCode / TP FW)");

        OpenReplace(viewModel, ExperienceIds.DpReplace);
        Assert.Equal("Base firmware (FlashCode)", viewModel.Replace.ReplaceBaseSlot.Title);
        Assert.DoesNotContain("TP FW", viewModel.Replace.ReplaceBaseSlot.Title, StringComparison.Ordinal);

        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        Assert.Equal("Base firmware (FlashCode)", viewModel.Replace.ReplaceBaseSlot.Title);
        Assert.DoesNotContain("TP FW", viewModel.Replace.ReplaceBaseSlot.Title, StringComparison.Ordinal);
    }

    /// <summary>Verifies CtrlRAM plan rows promote readable region labels over raw postbuild filenames.</summary>
    [Fact]
    public void CtrlRamPlanRowsExposeReadablePrimaryLabels()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Equal("Waiting for required inputs", Assert.Single(viewModel.Replace.ReplaceMemoryRows).AfterSource);
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title.StartsWith("NF CtrlRAM", StringComparison.Ordinal));
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title.StartsWith("Normal CtrlRAM", StringComparison.Ordinal));
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title.StartsWith("VN CtrlRAM", StringComparison.Ordinal));
        Assert.All(
            viewModel.Replace.ReplaceSlots.Where(slot => slot.SlotId.StartsWith("replace-ctrlram-", StringComparison.Ordinal)),
            slot =>
            {
                Assert.DoesNotContain(".bin", slot.Title, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("_", slot.Title, StringComparison.Ordinal);
            });
    }

    /// <summary>Verifies topology-neutral NT51950 inputs use Common while DiffDLM belongs to Cascade.</summary>
    [Fact]
    public void Nt51950DiffDlmUsesCommonOnlyForCascade()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Equal(["Cascade", "Common"], viewModel.Replace.ReplaceSlotGroups.Select(group => group.Title));
        FirmwareSlotGroupViewModel cascade = viewModel.Replace.ReplaceSlotGroups[0];
        FirmwareSlotGroupViewModel common = viewModel.Replace.ReplaceSlotGroups[1];
        Assert.Contains(cascade.Slots, slot => slot.Title == "DiffDLM");
        Assert.DoesNotContain(common.Slots, slot => slot.Title == "DiffDLM");
        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.Equal("Waiting for required inputs", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
        Assert.DoesNotContain(viewModel.Replace.ReplaceSlotGroups, group => group.Title == "Single IC");

        viewModel.WorkflowSession.SelectedNumber = "single";

        Assert.DoesNotContain(viewModel.Replace.ReplaceSlots, slot => slot.Title == "DiffDLM");
        Assert.Equal("Common", Assert.Single(viewModel.Replace.ReplaceSlotGroups).Title);
    }

    /// <summary>Verifies NT51926 keeps DiffDLM in a dedicated cascade group.</summary>
    [Fact]
    public void Nt51926DiffDlmBelongsToCascade()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Equal(["Cascade", "Common"], viewModel.Replace.ReplaceSlotGroups.Select(group => group.Title));
        Assert.Contains(viewModel.Replace.ReplaceSlotGroups[0].Slots, slot => slot.SlotId == "replace-ctrlram-diff");
        Assert.DoesNotContain(viewModel.Replace.ReplaceSlotGroups[1].Slots, slot => slot.SlotId == "replace-ctrlram-diff");
        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.Equal("Waiting for required inputs", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
    }

    /// <summary>Verifies NT51927 three-chip CtrlRAM Replace exposes physical shared and per-chip inputs.</summary>
    [Fact]
    public void CtrlRamReplaceSlotsIncludeNt51927RightAndLeftSlaves()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title == "Normal CtrlRAM (Slave L)");
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title == "MP CtrlRAM (Slave R)");
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title == "MP CtrlRAM (Slave L)");
        Assert.Equal(8, viewModel.Replace.ReplaceSlots.Count(slot =>
            slot.SlotId.StartsWith("replace-ctrlram-", StringComparison.Ordinal)));
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title == "NF CtrlRAM (Shared)");
        Assert.Contains(viewModel.Replace.ReplaceSlots, slot => slot.Title == "VN CtrlRAM (Shared)");
        Assert.DoesNotContain(viewModel.Replace.ReplaceSlots, slot => slot.Title == "VN CtrlRAM (Slave L)");
        Assert.Contains(viewModel.Replace.CtrlRamRegions, region => region.Name == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.Replace.CtrlRamRegions, region => region.Name == "Normal CtrlRAM (Slave L)");
        Assert.Contains(viewModel.Replace.ReplaceSlotGroups, group => group.Title == "Common" && group.IsExpanded);
        Assert.Contains(viewModel.Replace.ReplaceSlotGroups, group => group.Title == "Master" && group.IsExpanded);
        Assert.Contains(viewModel.Replace.ReplaceSlotGroups, group => group.Title == "Slave R" && !group.IsExpanded);
        Assert.Contains(viewModel.Replace.ReplaceSlotGroups, group => group.Title == "Slave L" && !group.IsExpanded);
        Assert.Equal(
            ["Common", "Master", "Slave R", "Slave L"],
            viewModel.Replace.ReplaceSlotGroups.Select(group => group.Title));
        Assert.False(viewModel.Replace.IsReplaceCoverageGrouped);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.Equal("Waiting for required inputs", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
    }

    /// <summary>Verifies coverage summaries describe unchanged areas without presenting a Preserve region.</summary>
    [Fact]
    public void CtrlRamReplaceCoverageUsesBaseFirmwareAndChangedAreaWording()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.DoesNotContain(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.SourceLabel.Contains("Base flash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(viewModel.Replace.ReplaceMemoryRows, row =>
            row.BeforeSource.Contains("Base flash", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Waiting for required inputs", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
    }

    /// <summary>Verifies the Replace selection overview keeps collapsed CtrlRAM choices discoverable.</summary>
    [Fact]
    public void ReplaceSelectionOverviewTracksSelectedCtrlRamTargets()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-selection");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Equal("0 / 8 targets selected", viewModel.Replace.ReplaceSelectionCountLabel);
        Assert.DoesNotContain(viewModel.Replace.ReplaceCoverageSegments, static segment => segment.IsChanged);
        Assert.Equal("Waiting for required inputs", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
        Assert.Contains("Build blocked", viewModel.Replace.ReplaceSelectionStatusLabel, StringComparison.Ordinal);
        Assert.Contains(viewModel.Replace.ReplaceSelectionMissingRows, row => row.Title == "Base firmware (FlashCode / TP FW)");
        Assert.Contains(viewModel.Replace.ReplaceSelectionMissingRows, row => row.Title == "CtrlRAM replacement");
        FirmwareSlotGroupViewModel slaveLGroup = viewModel.Replace.ReplaceSlotGroups.Single(group => group.Title == "Slave L");
        Assert.Equal("0/2", slaveLGroup.CountLabel);
        Assert.Equal("2 areas. None selected.", slaveLGroup.SelectionSummary);
        FirmwareSlotGroupViewModel sharedGroup = viewModel.Replace.ReplaceSlotGroups.Single(group => group.Title == "Common");
        Assert.Equal("0/2", sharedGroup.CountLabel);

        FirmwareSlotViewModel vn = viewModel.Replace.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        viewModel.SetSlotFile(vn.SlotId, workspace.Write("vn.bin", [0x00]));

        Assert.Equal("Waiting for required inputs", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);

        viewModel.SetSlotFile("replace-base", workspace.PathFor("base.bin"));

        sharedGroup = viewModel.Replace.ReplaceSlotGroups.Single(group => group.Title == "Common");
        Assert.Equal("1 / 8 targets selected", viewModel.Replace.ReplaceSelectionCountLabel);
        Assert.Equal("1/2", sharedGroup.CountLabel);
        Assert.Equal("1 selected / 2 areas.", sharedGroup.SelectionSummary);
        Assert.Contains("Build blocked", viewModel.Replace.ReplaceSelectionStatusLabel, StringComparison.Ordinal);
        Assert.Empty(viewModel.Replace.ReplaceSelectionMissingRows);
        Assert.Contains(viewModel.Replace.ReplaceSelectionRows, row =>
            row.Title == "VN CtrlRAM (Shared)" &&
            row.Detail == "vn.bin" &&
            row.Meta.Contains("VN_Ctrlram.bin", StringComparison.Ordinal) &&
            row.Meta.Contains("VN CtrlRAM (Slave L)", StringComparison.Ordinal));
        Assert.Contains("Complete the required inputs", viewModel.Replace.ReplaceSelectionRunHint, StringComparison.Ordinal);

        Assert.False(viewModel.Replace.IsReplaceSelectionModalOpen);
        viewModel.Replace.ShowReplaceSelectionCommand.Execute(null);
        Assert.True(viewModel.Replace.IsReplaceSelectionModalOpen);
        viewModel.Replace.CloseReplaceSelectionCommand.Execute(null);
        Assert.False(viewModel.Replace.IsReplaceSelectionModalOpen);
    }
}
