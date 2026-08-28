using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>A first-entry mode choice commits once without waiting for another page/context gesture.</summary>
    [Fact]
    public void FirstMergePageEntryCommitsModeImmediately()
    {
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, _) => []);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51932";
        Assert.True(viewModel.IsMergeVisible);
        Assert.Contains(ExperienceIds.AbMerge, viewModel.Merge.MergeModeChoices);

        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.True(viewModel.Merge.IsAbCodeMergeModeSelected);
    }

    /// <summary>Standard and AB mode drafts survive a round trip without sharing their authoring state.</summary>
    [Fact]
    public async Task PairwiseNavigationStandardAndAbMergePreservesEachModeDraft()
    {
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, _) => []);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.MergeDpSlot.FilePath = @"C:\standard-draft.bin";
        string standardSignature = StandardMergeDraftSignature(viewModel);

        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        string abSignature = AbMergeDraftSignature(viewModel);

        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;

        Assert.Equal(standardSignature, StandardMergeDraftSignature(viewModel));
        Assert.True(viewModel.IsMergeVisible);

        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        Assert.Equal(abSignature, AbMergeDraftSignature(viewModel));
    }

    /// <summary>AB and General Merge keep independent mode-specific authoring drafts.</summary>
    [Fact]
    public async Task PairwiseNavigationAbAndGeneralMergePreservesEachModeDraft()
    {
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, _) => []);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        string abSignature = AbMergeDraftSignature(viewModel);

        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.GeneralMergeOutputLength = "0x70000";
        viewModel.Merge.GeneralMergeOutputFillByte = "0xA5";
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x00100";
        mapping.TargetStartAddress = "0x20100";
        mapping.Length = "0x01000";
        string generalSignature = GeneralMergeDraftSignature(viewModel);

        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        Assert.Equal(abSignature, AbMergeDraftSignature(viewModel));

        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;

        Assert.Equal(generalSignature, GeneralMergeDraftSignature(viewModel));
    }

    /// <summary>CtrlRAM and General Replace keep independent mode-specific authoring state.</summary>
    [Fact]
    public void PairwiseNavigationCtrlRamAndGeneralReplacePreservesEachModeDraft()
    {
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, _) => []);
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";
        string ctrlRamSignature = CtrlRamReplaceDraftSignature(viewModel);

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x18000";
        mapping.Length = "0x00800";
        string generalSignature = GeneralReplaceDraftSignature(viewModel);

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;

        Assert.Equal(ctrlRamSignature, CtrlRamReplaceDraftSignature(viewModel));
        Assert.True(viewModel.IsReplaceVisible);

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;

        Assert.Equal(generalSignature, GeneralReplaceDraftSignature(viewModel));
    }

    /// <summary>General Merge and General Replace retain separate page-owned drafts and device contexts.</summary>
    [Fact]
    public void PairwiseNavigationGeneralMergeAndReplacePreservesIndependentPageDrafts()
    {
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, _) => []);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.GeneralMergeOutputLength = "0x80000";
        viewModel.Merge.GeneralMergeOutputFillByte = "0xFF";
        GeneralMergeMappingViewModel mergeMapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mergeMapping.SourceStartAddress = "0x01000";
        mergeMapping.TargetStartAddress = "0x21000";
        mergeMapping.Length = "0x02000";
        string mergeSignature = GeneralMergeDraftSignature(viewModel);

        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "2";
        GeneralReplaceMappingViewModel replaceMapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        replaceMapping.TargetStartAddress = "0x31000";
        replaceMapping.Length = "0x00400";
        string replaceSignature = GeneralReplaceDraftSignature(viewModel);

        viewModel.ShowMergeCommand.Execute(null);

        Assert.Equal(mergeSignature, GeneralMergeDraftSignature(viewModel));
        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.Equal(replaceSignature, GeneralReplaceDraftSignature(viewModel));
        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("2", viewModel.WorkflowSession.SelectedNumber);
    }

    /// <summary>Non-navigation overlays preserve the active workflow draft and page context.</summary>
    [Theory]
    [InlineData("settings-over-ab")]
    [InlineData("system-information-over-ctrlram")]
    [InlineData("report-over-general-replace")]
    public async Task PairwiseNavigationOverlayPreservesActiveWorkflowDraft(string scenario)
    {
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, _) => []);
        string signature;

        switch (scenario)
        {
            case "settings-over-ab":
                viewModel.ShowMergeCommand.Execute(null);
                viewModel.WorkflowSession.SelectedIc = "NT51950";
                viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
                await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
                signature = ActiveWorkflowDraftSignature(viewModel);

                viewModel.OpenSettingsCommand.Execute(null);

                Assert.True(viewModel.IsSettingsModalOpen);
                Assert.Equal(signature, ActiveWorkflowDraftSignature(viewModel));
                viewModel.CloseSettingsCommand.Execute(null);
                Assert.False(viewModel.IsSettingsModalOpen);
                break;

            case "system-information-over-ctrlram":
                OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
                viewModel.WorkflowSession.SelectedIc = "NT51927";
                viewModel.WorkflowSession.SelectedNumber = "3";
                signature = ActiveWorkflowDraftSignature(viewModel);

                viewModel.MessageCenter.OpenCommand.Execute(null);
                viewModel.MessageCenter.ShowSystemInformationCommand.Execute(null);

                Assert.True(viewModel.MessageCenter.IsOpen);
                Assert.True(viewModel.MessageCenter.IsSystemInformationSelected);
                Assert.Equal(signature, ActiveWorkflowDraftSignature(viewModel));
                viewModel.MessageCenter.CloseCommand.Execute(null);
                Assert.False(viewModel.MessageCenter.IsOpen);
                break;

            case "report-over-general-replace":
                OpenReplace(viewModel, ExperienceIds.GeneralReplace);
                GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
                mapping.TargetStartAddress = "0x22000";
                mapping.Length = "0x01000";
                signature = ActiveWorkflowDraftSignature(viewModel);
                viewModel.Reports.LoadReportJson(
                    ReportJsonSamples.Succeeded(runId: "pairwise-navigation"),
                    "pairwise-navigation-report.json");

                viewModel.Reports.ShowReportCommand.Execute(null);

                Assert.True(viewModel.Reports.IsReportModalOpen);
                Assert.Equal(signature, ActiveWorkflowDraftSignature(viewModel));
                viewModel.Reports.CloseReportCommand.Execute(null);
                Assert.False(viewModel.Reports.IsReportModalOpen);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        Assert.Equal(signature, ActiveWorkflowDraftSignature(viewModel));
        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
    }
}
