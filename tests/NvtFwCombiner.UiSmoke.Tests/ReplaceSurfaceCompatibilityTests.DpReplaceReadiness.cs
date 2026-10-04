using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ReplaceSurfaceCompatibilityTests
{
    /// <summary>Readiness refresh tolerates a slot collection change raised by a slot projection update.</summary>
    [Fact]
    public void CtrlRamReadinessUsesOneSlotSnapshotDuringProjection()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        FirmwareSlotViewModel firstInput = viewModel.Replace.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot));
        firstInput.ClearSelectionReadiness();
        firstInput.IsOptional = false;
        bool collectionChanged = false;
        firstInput.PropertyChanged += (_, _) =>
        {
            if (!collectionChanged)
            {
                collectionChanged = true;
                viewModel.Replace.ReplaceSlots.RemoveAt(viewModel.Replace.ReplaceSlots.Count - 1);
            }
        };

        viewModel.Replace.NotifyCommandStateChanged();

        Assert.True(collectionChanged);
    }

    /// <summary>One admitted CtrlRAM source reaches the green terminal UI state.</summary>
    [Fact]
    public void CtrlRamReplacePublishesVerifiedAfterSelection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-verified");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", ReadCtrlRamReference()));
        viewModel.SetSlotFile(
            "replace-ctrlram-normal",
            workspace.Write("initial-code.bin", new byte[0x1000]));

        FirmwareSlotViewModel initialCode = Assert.Single(
            viewModel.Replace.ReplaceSlots,
            static slot => slot.SlotId == "replace-ctrlram-normal");
        Assert.Equal(FirmwareInputInspectionSeverity.Valid, initialCode.InputInspectionSeverity);
        Assert.Equal(FirmwareSlotSemanticState.Verified, initialCode.SemanticState);
        Assert.Equal("Verified", initialCode.SemanticStateLabel);
        Assert.False(initialCode.BlocksBuild);
        Assert.True(viewModel.Replace.CanBuildReplace);
        AssertInspectionTerminal(viewModel.Replace.Inspection);
    }

    /// <summary>An invalid required CtrlRAM reference publishes terminal blocking health.</summary>
    [Fact]
    public void CtrlRamReplacePublishesErrorForInvalidReference()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-short");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x60000]));
        viewModel.SetSlotFile(
            "replace-ctrlram-normal",
            workspace.Write("ctrlram.bin", new byte[0x1000]));

        FirmwareSlotViewModel initialCode = Assert.Single(
            viewModel.Replace.ReplaceSlots,
            static slot => slot.SlotId == CompositionSlotIds.ReplaceBase);
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, initialCode.InputInspectionSeverity);
        Assert.Equal(FirmwareSlotSemanticState.Error, initialCode.SemanticState);
        Assert.True(initialCode.BlocksBuild);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    /// <summary>Standard Merge and CtrlRAM Replace use the same localized semantic card.</summary>
    [Fact]
    public void StandardMergeAndCtrlRamReplaceStartWithTheSharedEmptyFactProjection()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.All(viewModel.Replace.ReplaceSlots, slot =>
            Assert.Empty(slot.PrimaryFirmwareFacts));

        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        viewModel.ShowMergeCommand.Execute(null);

        Assert.All(viewModel.Merge.MergeSlots, slot =>
            Assert.Empty(slot.PrimaryFirmwareFacts));
    }

    /// <summary>Language changes reproject cached terminal health without changing its semantic result.</summary>
    [Fact]
    public void CtrlRamReplaceTerminalInspectionIsRelocalized()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-health-zh");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", ReadCtrlRamReference()));
        viewModel.SetSlotFile(
            "replace-ctrlram-normal",
            workspace.Write("initial-code.bin", new byte[0x1000]));
        FirmwareSlotViewModel initialCode = Assert.Single(
            viewModel.Replace.ReplaceSlots,
            static slot => slot.SlotId == "replace-ctrlram-normal");
        Assert.Equal(
            "Ready: the selected BIN satisfies the compiled input contract.",
            initialCode.InputInspectionStatus);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal(FirmwareSlotSemanticState.Verified, initialCode.SemanticState);
        Assert.Equal("已驗證", initialCode.SemanticStateLabel);
        Assert.Equal(
            "Ready：所選 BIN 符合 compiled input contract。",
            initialCode.InputInspectionStatus);
    }

    /// <summary>Settings preserves terminal input health together with the selected CtrlRAM files.</summary>
    [Fact]
    public void SettingsModalPreservesCtrlRamReplaceTerminalInspection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-health-clear");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", ReadCtrlRamReference()));
        viewModel.SetSlotFile(
            "replace-ctrlram-normal",
            workspace.Write("initial-code.bin", new byte[0x1000]));
        Assert.Contains(viewModel.Replace.ReplaceSlots, static slot => slot.InputInspectionSeverity is not null);

        (string SlotId, FirmwareInputInspectionSeverity? InputInspectionSeverity, string InputInspectionStatus)[] health =
        [
            .. viewModel.Replace.ReplaceSlots.Select(slot => (
                slot.SlotId,
                slot.InputInspectionSeverity,
                slot.InputInspectionStatus)),
        ];

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.CloseSettingsCommand.Execute(null);

        Assert.Equal(
            health,
            viewModel.Replace.ReplaceSlots.Select(slot => (
                slot.SlotId,
                slot.InputInspectionSeverity,
                slot.InputInspectionStatus)));
    }

    /// <summary>An unresolved selection snapshot fails closed instead of falling back to optional-slot heuristics.</summary>
    [Fact]
    public void CtrlRamUnsupportedReferenceCannotBypassSelectionReadiness()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-readiness-invalid");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference-60000.bin", new byte[0x60000]));
        viewModel.SetSlotFile(
            "replace-ctrlram-normal",
            workspace.Write("initial-code.bin", new byte[0x1000]));

        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(viewModel.Replace.BuildReplaceCommand.CanExecute(null));
        Assert.Equal(FirmwareSlotSemanticState.Error, viewModel.Replace.ReplaceBaseSlot.SemanticState);
        Assert.True(viewModel.Replace.ReplaceBaseSlot.IsSemanticStateError);
        Assert.Equal(
            FirmwareInputInspectionSeverity.Blocking,
            viewModel.Replace.ReplaceBaseSlot.InputInspectionSeverity);
        FirmwareSlotViewModel selected = Assert.Single(
            viewModel.Replace.ReplaceSlots,
            static slot => slot.SlotId == "replace-ctrlram-normal");
        Assert.Null(selected.SelectionReadinessState);
        Assert.Null(selected.CurrentInspectionProjection?.InputSlotStatus?.CompilationFingerprint);
    }
}
