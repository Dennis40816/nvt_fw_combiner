using System.Text.Json;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM Replace exposes physical input slots and reports generated postbuild commands.</summary>
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
        OpenReplace(viewModel, "CtrlRAM");

        FirmwareSlotViewModel regionSlot = viewModel.ReplaceSlots.Single(slot =>
            slot.Title == "Normal CtrlRAM (Master)");
        Assert.True(regionSlot.IsOptional);
        Assert.Contains("CtrlRAM", regionSlot.Title, StringComparison.Ordinal);
        CtrlRamRegionViewModel region = viewModel.CtrlRamRegions.Single(item => item.Name == regionSlot.Title);
        (int start, int length) = ParseCtrlRamRegion(region);
        string regionPath = workspace.Write("ctrlram.bin", baseBytes[start..(start + length)]);

        await viewModel.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        viewModel.SetSlotFile(regionSlot.SlotId, regionPath);

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.NotEmpty(GetCommandOperations(viewModel.LoadedReport));
        Assert.False(viewModel.LoadedReport.HasStepOperations);
        Assert.Contains(GetCommandOperations(viewModel.LoadedReport), operation =>
            operation.Title.Contains("Postbuild refresh", StringComparison.Ordinal) &&
            operation.Meta.Contains("legacy Combiner postbuild", StringComparison.Ordinal) &&
            !operation.Meta.Contains("Combiner command", StringComparison.Ordinal) &&
            !operation.Meta.Contains("Combiner.exe", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("Postbuild refresh", StringComparison.Ordinal) &&
            operation.Meta.Contains("legacy Combiner postbuild", StringComparison.Ordinal) &&
            !operation.Meta.Contains("Combiner command", StringComparison.Ordinal) &&
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
        OpenReplace(viewModel, "CtrlRAM");

        await viewModel.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);

        // The verified FWConfig may choose the base image's branch. This fixture deliberately
        // exercises the owner-selected three-chip branch afterwards.
        viewModel.SelectedNumber = "3";
        await viewModel.FirmwareInspectionRefreshTask;
        FirmwareSlotViewModel normalRight = viewModel.ReplaceSlots.Single(slot => slot.Title == "Normal CtrlRAM (Slave R)");
        FirmwareSlotViewModel vn = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        (int normalRightStart, int normalRightLength) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == normalRight.Title));
        (int vnLeftStart, int vnLeftLength) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == "VN CtrlRAM (Slave L)"));
        string normalRightPath = workspace.Write("normal-slave-r.bin", baseBytes[normalRightStart..(normalRightStart + normalRightLength)]);
        string vnLeftPath = workspace.Write("vn-slave-l.bin", baseBytes[vnLeftStart..(vnLeftStart + vnLeftLength)]);
        viewModel.SetSlotFile(normalRight.SlotId, normalRightPath);
        viewModel.SetSlotFile(vn.SlotId, vnLeftPath);

        Assert.Equal("2 / 8 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "VN CtrlRAM (Shared)");
        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Contains(GetCommandOperations(viewModel.LoadedReport), operation =>
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
        OpenReplace(viewModel, "CtrlRAM");

        await viewModel.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        viewModel.SelectedNumber = "3";
        await viewModel.FirmwareInspectionRefreshTask;
        FirmwareSlotViewModel vn = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        Assert.Contains("VN_Ctrlram.bin", vn.Description, StringComparison.Ordinal);
        Assert.Contains("VN CtrlRAM (Master): max 5728 bytes", vn.Description, StringComparison.Ordinal);
        Assert.Contains("VN CtrlRAM (Slave L): max 5728 bytes", vn.Description, StringComparison.Ordinal);
        (int start, int length) = ParseCtrlRamRegion(
            viewModel.CtrlRamRegions.Single(region => region.Name == "VN CtrlRAM (Slave L)"));
        string vnPath = workspace.Write("vn-ctrlram.bin", baseBytes[start..(start + length)]);
        viewModel.SetSlotFile(vn.SlotId, vnPath);

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));

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
        OpenReplace(viewModel, "CtrlRAM");

        string basePath = workspace.Write("base-from-golden.bin", baseBytes);
        await viewModel.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        viewModel.SelectedNumber = "3";
        await viewModel.FirmwareInspectionRefreshTask;
        FirmwareSlotViewModel vnSlot = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        CtrlRamRegionViewModel vnLeftRegion = viewModel.CtrlRamRegions.Single(region => region.Name == "VN CtrlRAM (Slave L)");
        (int start, int length) = ParseCtrlRamRegion(vnLeftRegion);
        string replacementPath = workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]);

        viewModel.SetSlotFile(vnSlot.SlotId, replacementPath);

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.HasCodeBlock &&
            operation.CodeBlock.Contains("Combiner.exe", StringComparison.Ordinal));
        using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
        AssertNoUnexpectedOutputDifferenceIssue(reportDocument.RootElement);
        JsonElement[] differences = [.. reportDocument.RootElement.GetProperty("OutputDifferences").EnumerateArray()];
        Assert.All(differences, difference =>
        {
            Assert.True(difference.GetProperty("IsAccepted").GetBoolean());
            Assert.True(difference.GetProperty("Classification").GetString() is
                OutputDifferenceClassifications.DeclaredReplacement or
                OutputDifferenceClassifications.PostbuildCrcHeader);
            Assert.Contains("postbuild-threechip", difference.GetProperty("Evidence").GetString(), StringComparison.Ordinal);
        });
        Assert.Contains(differences, difference =>
            difference.GetProperty("Classification").GetString() == OutputDifferenceClassifications.DeclaredReplacement);
        Assert.Contains(differences, difference =>
            difference.GetProperty("Classification").GetString() == OutputDifferenceClassifications.PostbuildCrcHeader);
        Assert.All(differences, difference => Assert.True(difference.TryGetProperty("Semantic", out _)));
        Assert.Contains(differences, difference =>
            difference.GetProperty("Semantic").GetProperty("CategoryId").GetString() == "tp-flash-header" &&
            difference.GetProperty("Semantic").GetProperty("Explanation").GetString()!
                .Contains("Header copy / master", StringComparison.Ordinal));
        Assert.Contains(differences, difference =>
            difference.GetProperty("Semantic").GetProperty("SubjectLabel").GetString() == "DLM CRC 0");
        Assert.Contains(differences, difference =>
            difference.GetProperty("Semantic").GetProperty("SubjectLabel").GetString() == "Header CRC 0");
        Assert.Contains(differences, difference =>
            difference.GetProperty("Semantic").GetProperty("SubjectLabel").GetString() == "ILM CRC 3");
        Assert.Contains(differences, difference =>
            difference.GetProperty("Semantic").GetProperty("SubjectLabel").GetString() == "Header CRC 3");
    }
}
