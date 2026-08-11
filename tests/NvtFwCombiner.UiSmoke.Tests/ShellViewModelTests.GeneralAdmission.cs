using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>General Merge command state and Memory Layout consume the same canonical admission result.</summary>
    [Fact]
    public async Task GeneralMergeCanonicalAdmissionBlocksInvalidAndOverlappingMappings()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-merge-admission");
        string firstPath = workspace.Write("first.bin", [0x10, 0x11, 0x12, 0x13]);
        string secondPath = workspace.Write("second.bin", [0x20, 0x21, 0x22, 0x23]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.GeneralMergeOutputLength = "0x20";

        GeneralMergeMappingViewModel first = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        viewModel.SetSlotFile(first.MappingId, firstPath);
        Assert.False(viewModel.Merge.PreviewMergeCommand.CanExecute(null));

        first.Length = "0x4";
        await viewModel.Merge.GeneralMergeReadinessRefreshTask;
        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));

        viewModel.Merge.AddGeneralMergeMappingCommand.Execute(null);
        GeneralMergeMappingViewModel second = viewModel.Merge.GeneralMergeMappings[1];
        second.TargetStartAddress = "0x2";
        second.Length = "0x4";
        viewModel.SetSlotFile(second.MappingId, secondPath);
        await viewModel.Merge.GeneralMergeReadinessRefreshTask;

        Assert.False(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        MemoryCoverageSegmentViewModel overlap = Assert.Single(
            viewModel.Merge.MergeCoverageSegments,
            segment => segment.SourceLabel == "Overlap error");
        Assert.Equal("0x00002-0x00003 (len 0x2)", overlap.RangeLabel);
        Assert.Contains(first.MappingId, overlap.Detail, StringComparison.Ordinal);
        Assert.Contains(second.MappingId, overlap.Detail, StringComparison.Ordinal);
    }

    /// <summary>General Replace rejects overlap before Preview and marks only the exact intersection.</summary>
    [Fact]
    public async Task GeneralReplaceCanonicalAdmissionBlocksOverlappingMappings()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-admission");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string firstPath = workspace.Write("first.bin", [0x10, 0x11, 0x12, 0x13]);
        string secondPath = workspace.Write("second.bin", [0x20, 0x21, 0x22, 0x23]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);

        GeneralReplaceMappingViewModel first = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        first.TargetStartAddress = "0x100";
        first.Length = "0x4";
        viewModel.SetSlotFile(first.MappingId, firstPath);
        viewModel.Replace.AddGeneralReplaceMappingCommand.Execute(null);
        GeneralReplaceMappingViewModel second = viewModel.Replace.GeneralReplaceMappings[1];
        second.TargetStartAddress = "0x102";
        second.Length = "0x4";
        viewModel.SetSlotFile(second.MappingId, secondPath);
        await viewModel.Replace.GeneralReplaceReadinessRefreshTask;

        Assert.False(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.True(first.HasAuthoringIssue);
        Assert.True(second.HasAuthoringIssue);
        Assert.Contains(first.MappingId, first.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.Contains(second.MappingId, first.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.Contains("[0x102, 0x104)", first.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            viewModel.Replace.ReplaceCoverageSegments,
            segment => segment.SourceLabel == "Overlap error");
    }

    /// <summary>General Replace exposes canonical inline source kinds and validates their payload shape.</summary>
    [Theory]
    [InlineData(GeneralMappingSourceKind.HexOverwrite, "A55A", "0x2", true)]
    [InlineData(GeneralMappingSourceKind.HexOverwrite, "A55A", "0x3", false)]
    [InlineData(GeneralMappingSourceKind.HexFill, "FF", "0x4", true)]
    [InlineData(GeneralMappingSourceKind.HexFill, "FFFF", "0x4", false)]
    public async Task GeneralReplaceInlineSourcesUseCanonicalAdmission(
        GeneralMappingSourceKind sourceKind,
        string value,
        string length,
        bool expectedAdmitted)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-inline");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);

        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.SelectedSource = mapping.SourceOptions.Single(option => option.Kind == sourceKind);
        mapping.TargetStartAddress = "0x100";
        mapping.Length = length;
        mapping.InlineValue = value;
        await viewModel.Replace.GeneralReplaceReadinessRefreshTask;

        Assert.Equal(!expectedAdmitted, mapping.HasAuthoringIssue);
        Assert.Equal("Memory layout pending", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Equal(sourceKind == GeneralMappingSourceKind.HexFill ? "FILL" : "HEX", mapping.SourceKindIcon);
    }

    /// <summary>Malformed Start + Length remains visible beside the exact mapping id.</summary>
    [Fact]
    public void GeneralReplaceInvalidRangeKeepsCanonicalAuthoringDiagnostic()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-invalid-range");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string inputPath = workspace.Write("mapping.bin", [0xA5, 0x5A]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);

        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "invalid";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, inputPath);

        Assert.True(mapping.HasAuthoringIssue);
        Assert.Contains(mapping.MappingId, mapping.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.Contains("non-negative hexadecimal", mapping.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            viewModel.Replace.ReplaceMemoryRows,
            row => row.ActionLabel == "Error");
        Assert.False(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
    }
}
