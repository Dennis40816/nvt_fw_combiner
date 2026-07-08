using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM Replace exposes per-region slots and reports generated postbuild commands.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsPostbuildCommandTrace()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram");
        string basePath = workspace.Write("base.bin", baseBytes);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "2";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel regionSlot = viewModel.ReplaceSlots.First(slot =>
            slot.SlotId.StartsWith("replace-ctrlram-", StringComparison.Ordinal));
        Assert.True(regionSlot.IsOptional);
        Assert.Contains("CtrlRAM", regionSlot.Title, StringComparison.Ordinal);
        CtrlRamRegionViewModel region = viewModel.CtrlRamRegions.Single(item => item.Name == regionSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(region);
        string regionPath = workspace.Write("ctrlram.bin", baseBytes[start..(start + length)]);

        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(regionSlot.SlotId, regionPath);

        Assert.True(viewModel.CanPreviewReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.LoadedReport.HasCommandOperations);
        Assert.False(viewModel.LoadedReport.HasStepOperations);
        Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
            operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
            operation.Meta.Contains("Combiner command", StringComparison.Ordinal) &&
            !operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("postbuild-", StringComparison.Ordinal) &&
            operation.Meta.Contains("Combiner command", StringComparison.Ordinal) &&
            !operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.IsChanged);
    }

    /// <summary>Verifies one CtrlRAM Replace run can select and report multiple region replacements.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsMultipleSelectedRegions()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-multi");
        string basePath = workspace.Write("base.bin", baseBytes);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel normalRight = viewModel.ReplaceSlots.Single(slot => slot.Title == "Normal CtrlRAM (Slave R)");
        FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        (int normalRightStart, int normalRightLength) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == normalRight.Title));
        (int vnLeftStart, int vnLeftLength) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == vnLeft.Title));
        string normalRightPath = workspace.Write("normal-slave-r.bin", baseBytes[normalRightStart..(normalRightStart + normalRightLength)]);
        string vnLeftPath = workspace.Write("vn-slave-l.bin", baseBytes[vnLeftStart..(vnLeftStart + vnLeftLength)]);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(normalRight.SlotId, normalRightPath);
        viewModel.SetSlotFile(vnLeft.SlotId, vnLeftPath);

        Assert.Equal("2 / 12 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "VN CtrlRAM (Slave L)");
        Assert.True(viewModel.CanPreviewReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
            operation.CodeBlock.Contains("Normal_Ctrlram_R.bin", StringComparison.Ordinal) &&
            operation.CodeBlock.Contains("Normal_Ctrlram_L.bin", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "Normal CtrlRAM (Slave R)" &&
            segment.RangeLabel == "0x207D0-0x237CF (len 0x3000)");
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
            segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
    }

    /// <summary>Verifies CtrlRAM Replace can preview a golden-backed VN self replacement with traceable region naming.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewAcceptsGoldenBackedVnSelfReplacement()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-vn-ctrlram");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        string basePath = workspace.Write("base-from-golden.bin", baseBytes);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel vnLeft = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        Assert.Equal("Replace this area only when needed. TP position 0x2EBD0-0x3022F (len 0x1660).", vnLeft.Description);
        (int start, int length) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == vnLeft.Title));
        string vnPath = workspace.Write("vn-ctrlram.bin", baseBytes[start..(start + length)]);

        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(vnLeft.SlotId, vnPath);

        Assert.True(viewModel.CanPreviewReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.HasCodeBlock &&
            operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
            segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Slave L");
    }

    /// <summary>Verifies a CtrlRAM replacement sliced from the same base runs through the real postbuild path.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewSelfReplacementRunsPostbuild()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51927"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-self");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        viewModel.ShowCtrlRamReplaceCommand.Execute(null);

        FirmwareSlotViewModel vnLeftSlot = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Slave L)");
        CtrlRamRegionViewModel vnLeftRegion = viewModel.CtrlRamRegions.Single(region => region.Name == "VN CtrlRAM (Slave L)");
        (int start, int length) = ParseCtrlRamRegion(vnLeftRegion);
        string basePath = workspace.Write("base-from-golden.bin", baseBytes);
        string replacementPath = workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]);

        byte[] simulatedBeforePostbuild = [.. baseBytes];
        File.ReadAllBytes(replacementPath).CopyTo(simulatedBeforePostbuild.AsSpan(start, length));
        Assert.Equal(baseBytes, simulatedBeforePostbuild);

        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(vnLeftSlot.SlotId, replacementPath);

        Assert.True(viewModel.CanPreviewReplace);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.HasCodeBlock &&
            operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));
        using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
        AssertAcceptedPostbuildOnlyOutputDifferences(reportDocument.RootElement, "postbuild-threechip");
    }
}
