using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>Detailed Vector CtrlRAM uses its distinct typed presentation role.</summary>
    [Fact]
    public void VectorCtrlRamUsesDedicatedCoverageFillRole()
    {
        Assert.Equal(
            MemoryCoverageFillRole.CtrlRamVector,
            UiCompositionRunner.ResolveCtrlRamCoverageFillRole(CtrlRamRegionRole.Vector));
    }

    /// <summary>Partial NT51923 replacement keeps subtype hue and base-owned customer information.</summary>
    [Fact]
    public async Task Nt51923CtrlRamCustomerInformationUsesBaseFirmwarePresentation()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nt51923-customer-info-memory");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51923";
        viewModel.WorkflowSession.SelectedNumber = "single";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            golden.ExpectedOutputPath(golden.CaseByIc("51923")),
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel normalSlot = Assert.Single(
            viewModel.Replace.ReplaceSlots,
            slot => slot.ReplaceInputRole == ReplaceInputRole.CtrlRam &&
                slot.CtrlRamDescriptionFacts?.TitleStem == "Normal CtrlRAM");
        long normalLength = Assert.Single(normalSlot.CtrlRamDescriptionFacts!.Sections).MaximumLength;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            normalSlot.SlotId,
            workspace.Write($"{normalSlot.SlotId}.bin", new byte[checked((int)normalLength)]),
            TestContext.Current.CancellationToken);
        MemoryCoverageSegmentViewModel keptNf = Assert.Single(
            viewModel.Replace.ReplaceCoverageSegments,
            segment => segment.RegionId is { } regionId &&
                DynamicCtrlRamReplacementIds.GetRegionFamilyToken(regionId) == "NF");
        Assert.Equal(MemoryCoverageFillRole.CtrlRamNf, keptNf.FillRole);
        Assert.True(keptNf.UsesKeptPattern);
        Assert.False(keptNf.IsChanged);
        Assert.Equal("Kept", keptNf.ChangeLabel);
        Assert.Equal("NF CtrlRAM", keptNf.SourceLabel);
        Assert.Equal(
            "Output range keeps bytes from the base firmware.",
            keptNf.CompactDetail);
        Assert.Contains("NF CtrlRAM", keptNf.AccessibleDetail, StringComparison.Ordinal);
        Assert.False(viewModel.Replace.ShowsGenericCoverageStateLegend);
        Assert.Contains(viewModel.Replace.ReplaceCoverageGroups, group =>
            group.Segments.Any(segment => segment.IsSelectedForWrite) && group.IsExpanded);
        (string SlotId, long MaximumLength)[] replacements =
        [
            .. viewModel.Replace.ReplaceSlots
                .Where(slot => slot.ReplaceInputRole == ReplaceInputRole.CtrlRam &&
                    slot.CtrlRamDescriptionFacts?.TitleStem is
                        "NF CtrlRAM" or "Normal CtrlRAM" or "MP CtrlRAM" or "VN CtrlRAM")
                .Select(slot => (
                    slot.SlotId,
                    Assert.Single(slot.CtrlRamDescriptionFacts!.Sections).MaximumLength)),
        ];
        Assert.Equal(4, replacements.Length);
        foreach ((string slotId, long maximumLength) in replacements)
        {
            await viewModel.WorkflowSession.SetSlotFileAsync(
                slotId,
                workspace.Write($"{slotId}.bin", new byte[checked((int)maximumLength)]),
                TestContext.Current.CancellationToken);
        }

        MemoryCoverageSegmentViewModel customerInformation = Assert.Single(
            viewModel.Replace.ReplaceCoverageSegments,
            segment => segment.RegionId == "customer-info");
        Assert.Equal("0x3D000-0x3DFFF", customerInformation.AddressRangeLabel);
        Assert.Equal("Base flash", customerInformation.SourceLabel);
        Assert.Equal(MemoryCoverageFillRole.Kept, customerInformation.FillRole);
        Assert.True(customerInformation.UsesKeptPattern);
        Assert.Equal(
            "Output range keeps bytes from the base firmware.",
            customerInformation.CompactDetail);
        MemoryMapRowViewModel customerInformationRow = Assert.Single(
            viewModel.Replace.ReplaceMemoryRows,
            row => row.RangeLabel.StartsWith("0x3D000-0x3DFFF", StringComparison.Ordinal));
        Assert.Equal("Preserve", customerInformationRow.ActionLabel);
        Assert.Equal("Base flash", customerInformationRow.AfterSource);
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.FillRole == MemoryCoverageFillRole.CtrlRamNf);
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.FillRole == MemoryCoverageFillRole.CtrlRamNormal);
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.FillRole == MemoryCoverageFillRole.CtrlRamMp);
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.FillRole == MemoryCoverageFillRole.CtrlRamVn);
        MemoryCoverageSegmentViewModel plannedNf = Assert.Single(
            viewModel.Replace.ReplaceCoverageSegments,
            segment => segment.RegionId is { } regionId &&
                DynamicCtrlRamReplacementIds.GetRegionFamilyToken(regionId) == "NF");
        Assert.False(plannedNf.IsChanged);
        Assert.Equal("Will replace", plannedNf.ChangeLabel);
        Assert.Equal(
            "Output range will be written from NF CtrlRAM.",
            plannedNf.CompactDetail);
    }

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
        Assert.Equal("Waiting for CtrlRAM replacement", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        MemoryCoverageSegmentViewModel pendingCoverage = Assert.Single(
            viewModel.Replace.ReplaceCoverageSegments);
        Assert.Equal("Waiting for CtrlRAM replacement", pendingCoverage.SourceLabel);
        Assert.Contains("at least one CtrlRAM region BIN", pendingCoverage.Detail, StringComparison.Ordinal);

        FirmwareSlotViewModel inputSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-vn");
        FirmwareInspectionSnapshot inspection = Assert.IsType<FirmwareInspectionSnapshot>(
            viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        inputSlot.FilePath = basePath;
        inputSlot.SetCurrentInspectionProjection(inspection);
        inputSlot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        Assert.Contains(viewModel.Replace.ReplaceSlotGroups, group => group.Title == "Cascade");
        FirmwareSlotGroupViewModel cascadeGroup = viewModel.Replace.ReplaceSlotGroups.Single(
            group => group.Title == "Cascade");
        cascadeGroup.IsExpanded = false;

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Same(inputSlot, viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-vn"));
        Assert.Same(inspection, inputSlot.CurrentInspectionProjection);
        Assert.Equal(FirmwareInputInspectionSeverity.Valid, inputSlot.InputInspectionSeverity);
        pendingCoverage = Assert.Single(viewModel.Replace.ReplaceCoverageSegments);
        Assert.Equal("等待 CtrlRAM 替換輸入", pendingCoverage.SourceLabel);
        Assert.Contains("至少一個 CtrlRAM 區域 BIN", pendingCoverage.Detail, StringComparison.Ordinal);
        Assert.Same(cascadeGroup, viewModel.Replace.ReplaceSlotGroups.Single(group => group.Title == "串接"));
        Assert.False(cascadeGroup.IsExpanded);
        Assert.Equal("1 個區域，尚未選擇。", cascadeGroup.SelectionSummary);
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

    /// <summary>CtrlRAM input titles consume typed group and DiffNF facts instead of English suffixes.</summary>
    [Fact]
    public void CtrlRamInputTitleUsesTypedGroupingFactsWithoutDeclaredSuffixes()
    {
        var facts = new CtrlRamInputDescriptionFacts(
            "NF_Ctrlram.bin",
            [new CtrlRamInputDescriptionSection(
                "NF CtrlRAM",
                ReplaceRegionGroup.Master,
                0x100,
                0x200,
                TitleStem: "NF CtrlRAM")],
            RequiresDiffNfMerge: true,
            TitleStem: "NF CtrlRAM",
            IsShared: false);

        (string englishTitle, _) = ShellTextResources.For(ShellLanguage.English).GetReplaceInputText(
            addressSpaceId: "replace-ctrlram-nf",
            ReplaceInputRole.CtrlRam,
            ReplaceRegionGroup.Master,
            declaredTitle: "NF CtrlRAM",
            declaredDescription: "Declared detail",
            facts);
        (string chineseTitle, _) = ShellTextResources.For(ShellLanguage.ChineseTraditional).GetReplaceInputText(
            addressSpaceId: "replace-ctrlram-nf",
            ReplaceInputRole.CtrlRam,
            ReplaceRegionGroup.Master,
            declaredTitle: "NF CtrlRAM",
            declaredDescription: "Declared detail",
            facts);

        Assert.Equal("NF CtrlRAM (Master) (DiffNFMerge output)", englishTitle);
        Assert.Equal("NF CtrlRAM（主控）（DiffNFMerge 輸出）", chineseTitle);
    }

    /// <summary>Verifies CtrlRAM plan rows promote readable region labels over raw postbuild filenames.</summary>
    [Fact]
    public void CtrlRamPlanRowsExposeReadablePrimaryLabels()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Equal("Waiting for Base BIN", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Equal("Waiting for Base BIN", Assert.Single(viewModel.Replace.ReplaceMemoryRows).AfterSource);
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
        Assert.Equal("Waiting for Base BIN", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.Equal("Waiting for Base BIN", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
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
        Assert.Equal("Waiting for Base BIN", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.Equal("Waiting for Base BIN", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
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
        Assert.Equal("Waiting for Base BIN", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
    }

    /// <summary>Verifies coverage summaries describe unchanged areas without presenting a Preserve region.</summary>
    [Fact]
    public void CtrlRamReplaceCoverageUsesBaseFirmwareAndChangedAreaWording()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.Equal("Waiting for Base BIN", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.Replace.ReplaceCoverageGroups);
        Assert.DoesNotContain(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.SourceLabel.Contains("Base flash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(viewModel.Replace.ReplaceMemoryRows, row =>
            row.BeforeSource.Contains("Base flash", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Waiting for Base BIN", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
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
        Assert.Equal("Waiting for Base BIN", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);
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

        Assert.Equal("Waiting for Base BIN", Assert.Single(viewModel.Replace.ReplaceCoverageSegments).SourceLabel);

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
