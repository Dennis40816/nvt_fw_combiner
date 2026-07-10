using System.Globalization;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies hexadecimal viewport labels follow the selected shell language.</summary>
    [Fact]
    public void GeneralReplaceHexViewportLabelsAreLocalized()
    {
        var english = ShellTextResources.For(ShellLanguage.English);
        var traditionalChinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);

        Assert.Equal("Address", english.GeneralReplaceHexAddressColumnLabel);
        Assert.Equal("位址", traditionalChinese.GeneralReplaceHexAddressColumnLabel);
        Assert.Equal("ASCII", english.GeneralReplaceHexAsciiColumnLabel);
        Assert.Equal("ASCII", traditionalChinese.GeneralReplaceHexAsciiColumnLabel);
    }

    /// <summary>Verifies General Replace authors base BIN and explicit range rows as separate UI state.</summary>
    [Fact]
    public void GeneralReplaceUsesIndependentBaseAndEditableMappings()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowGeneralReplaceCommand.Execute(null);

        Assert.True(viewModel.IsGeneralReplaceModeSelected);
        Assert.False(viewModel.IsStructuredReplaceModeSelected);
        Assert.Empty(viewModel.ReplaceSlots);
        Assert.Equal("replace-base", viewModel.ReplaceBaseSlot.SlotId);
        Assert.NotEmpty(viewModel.ReplaceCoverageSegments);
        Assert.Contains("len 0x", viewModel.ReplaceMemoryRangeLabel, StringComparison.Ordinal);
        Assert.Contains("explicit profile-approved", viewModel.SelectedReplaceModeDescription, StringComparison.Ordinal);
        Assert.False(viewModel.CanPreviewReplace);
        Assert.Equal(
            "Build blocked: base BIN and at least one explicit replacement mapping are required.",
            viewModel.ReplaceReadinessStatus);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);

        viewModel.AddGeneralReplaceMappingCommand.Execute(null);
        Assert.Equal(2, viewModel.GeneralReplaceMappings.Count);

        viewModel.RemoveGeneralReplaceMappingRow(viewModel.GeneralReplaceMappings[0]);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);
        Assert.Equal(1, viewModel.GeneralReplaceMappings[0].Index);
        Assert.Equal("No replacement BIN selected", viewModel.GeneralReplaceMappings[0].DisplayName);
        Assert.Equal(string.Empty, viewModel.GeneralReplaceMappings[0].DisplayDetail);
    }

    /// <summary>Verifies General Replace UI runs a DP explicit mapping through Preview and Build.</summary>
    [Fact]
    public async Task GeneralReplacePreviewAndBuildUseExplicitMappingRows()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace");
        byte[] baseBytes = CreatePattern(0x40000, 0x40);
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        string outputPath = workspace.PathFor("general-replace.bin");

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowGeneralReplaceCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.GeneralReplaceMappings);
        mapping.StartAddress = "0x00100";
        mapping.EndAddress = "0x00101";
        viewModel.SetGeneralReplaceMappingFile(mapping.MappingId, replacementPath);

        Assert.True(viewModel.CanPreviewReplace);
        Assert.Contains("Ready", viewModel.ReplaceReadinessStatus, StringComparison.Ordinal);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.CanBuildReplace);

        await viewModel.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(0xA5, output[0x100]);
        Assert.Equal(0x5A, output[0x101]);
        Assert.Equal(baseBytes[0x102], output[0x102]);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("general-map-1", StringComparison.Ordinal));
    }

    /// <summary>Verifies General Replace UI routes TP-touching explicit mappings through postbuild.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRunsPostbuildForTpMapping()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.PathFromRelative("expected/51950/dp-256k/flash.bin");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-tp");
        byte[] baseBytes = File.ReadAllBytes(basePath);
        string replacementPath = workspace.Write("self-nf.bin", baseBytes[0x22C00..0x22C02]);

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowGeneralReplaceCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.GeneralReplaceMappings);
        mapping.StartAddress = "0x22C00";
        mapping.EndAddress = "0x22C01";
        viewModel.SetGeneralReplaceMappingFile(mapping.MappingId, replacementPath);

        Assert.True(viewModel.CanPreviewReplace);
        Assert.Contains("run postbuild", viewModel.ReplaceReadinessStatus, StringComparison.Ordinal);

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("Postbuild refresh", StringComparison.Ordinal) &&
            operation.HasCodeBlock &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
        Assert.Contains(viewModel.LoadedReport.CommandOperations, operation =>
            operation.Title.Contains("Postbuild refresh", StringComparison.Ordinal) &&
            operation.CodeBlock.StartsWith("Combiner.exe ", StringComparison.Ordinal));
    }

    /// <summary>Verifies General Replace hex authoring previews, stages, undoes, redoes, and builds virtual patches.</summary>
    [Fact]
    public async Task GeneralReplaceHexPatchAuthoringUsesVirtualDiffAndSharedBuildPipeline()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-hex-patch");
        byte[] baseBytes = CreatePattern(0x40000, 0x52);
        string basePath = workspace.Write("base.bin", baseBytes);
        string outputPath = workspace.PathFor("hex-patch.bin");

        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowHexEditorCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);

        Assert.True(viewModel.IsHexEditorVisible);
        Assert.False(viewModel.IsReplaceVisible);
        Assert.NotEmpty(viewModel.GeneralReplaceEditableRanges);
        Assert.False(viewModel.CanPreviewReplace);
        GeneralReplaceHexViewportRowViewModel viewportRow = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            row => row.Address == "0x000100" && !row.IsReferenceRow);
        Assert.False(viewportRow.IsEditedRow);

        viewModel.GeneralReplacePatchDraft.StartAddress = "0x00100";
        viewModel.GeneralReplacePatchDraft.EndAddress = "0x00101";
        viewModel.GeneralReplacePatchDraft.Value = "A5 5A";
        viewModel.SelectGeneralReplaceHexByteCommand.Execute(viewportRow.Bytes[0]);
        Assert.Equal("0x000100", viewModel.GeneralReplacePatchDraft.StartAddress);
        Assert.Equal("0x000100", viewModel.GeneralReplacePatchDraft.EndAddress);
        viewModel.GeneralReplacePatchDraft.EndAddress = "0x00101";
        viewModel.GeneralReplacePatchDraft.Value = "A5 5A";
        Assert.Same(viewportRow, viewModel.GeneralReplaceHexViewportRows.Single(row =>
            row.Address == "0x000100" && !row.IsReferenceRow));

        viewModel.ApplyGeneralReplacePatchCommand.Execute(null);

        viewportRow = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            row => row.Address == "0x000100" && !row.IsReferenceRow);
        Assert.True(viewportRow.IsEditedRow);
        Assert.True(viewportRow.Bytes[0].IsChanged);
        Assert.Equal("A5", viewportRow.Bytes[0].ValueHex);
        Assert.True(viewportRow.Bytes[1].IsChanged);
        Assert.Equal("5A", viewportRow.Bytes[1].ValueHex);
        Assert.Contains(
            viewModel.GeneralReplaceHexViewportRows,
            row => row.Address == "0x000100" && row.IsReferenceRow &&
                   !row.IsEditedRow &&
                   row.Bytes[0].ValueHex == baseBytes[0x100].ToString("X2", CultureInfo.InvariantCulture));
        Assert.True(viewModel.HasGeneralReplacePatches);
        Assert.True(viewModel.CanBuildHexEditor);
        Assert.False(viewModel.CanBuildReplace);
        GeneralReplacePatchViewModel stagedPatch = Assert.Single(viewModel.GeneralReplacePatches);
        Assert.Equal("A5 5A", stagedPatch.Value);
        Assert.True(viewModel.UndoGeneralReplacePatchCommand.CanExecute(null));
        viewportRow = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            row => row.Address == "0x000100" && !row.IsReferenceRow);
        Assert.Equal("A5", viewportRow.Bytes[0].ValueHex);

        viewModel.UndoGeneralReplacePatchCommand.Execute(null);
        Assert.False(viewModel.HasGeneralReplacePatches);
        Assert.False(viewModel.CanBuildHexEditor);
        viewportRow = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            row => row.Address == "0x000100" && !row.IsReferenceRow);
        Assert.Equal(baseBytes[0x100].ToString("X2", CultureInfo.InvariantCulture), viewportRow.Bytes[0].ValueHex);

        viewModel.RedoGeneralReplacePatchCommand.Execute(null);
        Assert.True(viewModel.HasGeneralReplacePatches);

        await viewModel.BuildHexEditorAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal([0xA5, 0x5A], output[0x100..0x102]);
        Assert.Equal(baseBytes, File.ReadAllBytes(basePath));
        Assert.Contains(viewModel.LoadedReport.Operations, operation =>
            operation.Title.Contains("hex-patch-1", StringComparison.Ordinal));
    }

    /// <summary>Verifies direct inline byte edits stage one-byte overwrites and retain optional base rows.</summary>
    [Fact]
    public void GeneralReplaceHexEditorSupportsInlineByteEditAndBaseReferenceToggle()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-hex-direct");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x71));
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowHexEditorCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);

        GeneralReplaceHexViewportRowViewModel row = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            candidate => candidate.Address == "0x000100" && !candidate.IsReferenceRow);
        Assert.False(row.IsEditedRow);
        string initialValue = row.Bytes[0].ValueHex;
        viewModel.BeginGeneralReplaceHexByteEditCommand.Execute(row.Bytes[0]);

        Assert.True(row.Bytes[0].IsEditing);
        Assert.Equal(initialValue, row.Bytes[0].EditValue);
        row.Bytes[0].EditValue = "A5";
        viewModel.CommitGeneralReplaceHexByteEditCommand.Execute(row.Bytes[0]);
        Assert.False(row.Bytes[0].IsEditing);
        GeneralReplacePatchViewModel directPatch = Assert.Single(viewModel.GeneralReplacePatches);
        Assert.Equal("A5", directPatch.Value);

        GeneralReplaceHexViewportRowViewModel changedRow = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            candidate => candidate.Address == "0x000100" && !candidate.IsReferenceRow);
        Assert.Same(row, changedRow);
        Assert.Equal("A5", changedRow.Bytes[0].ValueHex);
        viewModel.BeginGeneralReplaceHexByteEditCommand.Execute(changedRow.Bytes[0]);
        changedRow.Bytes[0].EditValue = "5A";
        viewModel.CommitGeneralReplaceHexByteEditCommand.Execute(changedRow.Bytes[0]);
        GeneralReplacePatchViewModel updatedPatch = Assert.Single(viewModel.GeneralReplacePatches);
        Assert.Equal("5A", updatedPatch.Value);
        Assert.Equal("5A", changedRow.Bytes[0].ValueHex);

        changedRow = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            candidate => candidate.Address == "0x000100" && !candidate.IsReferenceRow);
        viewModel.ClearGeneralReplaceHexByteCommand.Execute(changedRow.Bytes[0]);
        GeneralReplacePatchViewModel clearedPatch = Assert.Single(viewModel.GeneralReplacePatches);
        Assert.Equal("FF", clearedPatch.Value);
        Assert.Contains(viewModel.GeneralReplaceHexViewportRows, candidate =>
            candidate.Address == "0x000100" && candidate.IsReferenceRow);

        GeneralReplaceHexViewportRowViewModel referenceRow = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            candidate => candidate.Address == "0x000100" && candidate.IsReferenceRow);
        viewModel.SelectGeneralReplaceHexByteCommand.Execute(referenceRow.Bytes[0]);
        viewModel.BeginGeneralReplaceHexByteEditCommand.Execute(referenceRow.Bytes[0]);
        Assert.False(referenceRow.Bytes[0].IsEditable);
        Assert.False(referenceRow.Bytes[0].IsEditing);

        viewModel.IsGeneralReplaceHexReferenceRowsVisible = false;
        Assert.DoesNotContain(viewModel.GeneralReplaceHexViewportRows, candidate => candidate.IsReferenceRow);

        viewModel.GeneralReplaceHexViewportAddress = "0x000100";
        viewModel.GoToGeneralReplaceHexViewportCommand.Execute(null);
        Assert.Contains(viewModel.GeneralReplaceHexViewportRows, candidate =>
            candidate.Address == "0x0000C0" && !candidate.IsReferenceRow);
    }

    /// <summary>Verifies unapproved draft ranges stay in memory only and cannot enter the staged patch list.</summary>
    [Fact]
    public void GeneralReplaceHexEditorRejectsUnapprovedDraftRangeBeforeStaging()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-hex-unauthorized");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x49));
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowHexEditorCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);

        viewModel.GeneralReplacePatchDraft.StartAddress = "0x000000";
        viewModel.GeneralReplacePatchDraft.EndAddress = "0x000000";
        viewModel.GeneralReplacePatchDraft.Value = "A5";
        viewModel.ApplyGeneralReplacePatchCommand.Execute(null);

        Assert.Empty(viewModel.GeneralReplacePatches);
        Assert.Contains("profile-authorized", viewModel.GeneralReplaceHexViewportStatus, StringComparison.Ordinal);
        Assert.Equal(CreatePattern(0x40000, 0x49), File.ReadAllBytes(basePath));
    }

    /// <summary>Verifies an inline byte edit follows the same approved-range policy before it reaches staged memory.</summary>
    [Fact]
    public void GeneralReplaceHexEditorRejectsUnapprovedInlineByteBeforeStaging()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-hex-inline-unauthorized");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x83));
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowHexEditorCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.GeneralReplaceHexViewportAddress = "0x000000";
        viewModel.GoToGeneralReplaceHexViewportCommand.Execute(null);

        GeneralReplaceHexViewportRowViewModel row = Assert.Single(
            viewModel.GeneralReplaceHexViewportRows,
            candidate => candidate.Address == "0x000000" && !candidate.IsReferenceRow);
        GeneralReplaceHexByteCellViewModel cell = row.Bytes[0];
        viewModel.BeginGeneralReplaceHexByteEditCommand.Execute(cell);
        cell.EditValue = "A5";
        viewModel.CommitGeneralReplaceHexByteEditCommand.Execute(cell);

        Assert.Empty(viewModel.GeneralReplacePatches);
        Assert.True(cell.IsEditing);
        Assert.Contains("profile-authorized", cell.InlineValidationMessage, StringComparison.Ordinal);
        Assert.Equal(CreatePattern(0x40000, 0x83), File.ReadAllBytes(basePath));
    }

    /// <summary>Verifies Save requires confirmation and never mutates the selected base BIN in memory.</summary>
    [Fact]
    public void GeneralReplaceHexEditorSaveRequestsConfirmationBeforeGeneratedOutput()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-hex-save");
        byte[] baseBytes = CreatePattern(0x40000, 0x39);
        string basePath = workspace.Write("base.bin", baseBytes);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowHexEditorCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.GeneralReplacePatchDraft.StartAddress = "0x000100";
        viewModel.GeneralReplacePatchDraft.EndAddress = "0x000100";
        viewModel.GeneralReplacePatchDraft.Value = "A5";
        viewModel.ApplyGeneralReplacePatchCommand.Execute(null);

        viewModel.RequestHexEditorSaveCommand.Execute(null);

        Assert.True(viewModel.IsHexEditorSaveConfirmationOpen);
        Assert.Equal(baseBytes, File.ReadAllBytes(basePath));
        viewModel.CancelHexEditorSaveCommand.Execute(null);
        Assert.False(viewModel.IsHexEditorSaveConfirmationOpen);
    }

    /// <summary>Verifies draft typing and address entry keep the existing viewport rows until a deliberate refresh action.</summary>
    [Fact]
    public void GeneralReplaceHexEditorDefersViewportRefreshUntilGoToOrStage()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-hex-refresh");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x51));
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.ShowHexEditorCommand.Execute(null);
        viewModel.SetSlotFile("replace-base", basePath);

        GeneralReplaceHexViewportRowViewModel initialRow = viewModel.GeneralReplaceHexViewportRows[0];
        viewModel.GeneralReplaceHexViewportAddress = "0x000100";
        viewModel.GeneralReplacePatchDraft.StartAddress = "0x000100";
        viewModel.GeneralReplacePatchDraft.EndAddress = "0x000101";
        viewModel.GeneralReplacePatchDraft.Value = "A5 5A";

        Assert.Same(initialRow, viewModel.GeneralReplaceHexViewportRows[0]);

        viewModel.GoToGeneralReplaceHexViewportCommand.Execute(null);

        Assert.NotSame(initialRow, viewModel.GeneralReplaceHexViewportRows[0]);
    }
}
