using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>
    /// Keeps NT51928 DP Replace Build and LDC applicability on the Application-owned
    /// reference-capacity truth table.
    /// </summary>
    [Theory]
    [InlineData(0, false, false, ResolvedChildReadiness.PendingInput, false)]
    [InlineData(0, true, false, ResolvedChildReadiness.PendingInput, false)]
    [InlineData(0x40000, false, false, ResolvedChildReadiness.NotApplicable, false)]
    [InlineData(0x40000, true, false, ResolvedChildReadiness.NotApplicable, true)]
    [InlineData(0x40000, false, true, ResolvedChildReadiness.Blocked, false)]
    [InlineData(0x40000, true, true, ResolvedChildReadiness.Blocked, false)]
    [InlineData(0x80000, false, false, ResolvedChildReadiness.Ready, false)]
    [InlineData(0x80000, true, false, ResolvedChildReadiness.Ready, true)]
    [InlineData(0x80000, false, true, ResolvedChildReadiness.Ready, true)]
    [InlineData(0x80000, true, true, ResolvedChildReadiness.Ready, true)]
    public void Nt51928DpReplaceUsesTypedSelectionReadiness(
        int referenceLength,
        bool selectInitialCode,
        bool selectLdc,
        ResolvedChildReadiness expectedLdcState,
        bool expectedCanBuild)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-readiness");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51928";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);

        if (referenceLength > 0)
        {
            viewModel.SetSlotFile(
                WorkbenchSlotIds.ReplaceBase,
                workspace.Write($"reference-{referenceLength:X}.bin", new byte[referenceLength]));
        }

        if (selectInitialCode)
        {
            viewModel.SetSlotFile(
                WorkbenchSlotIds.ReplaceDp,
                workspace.Write("initial-code.bin", new byte[referenceLength]));
        }

        if (selectLdc)
        {
            viewModel.SetSlotFile(
                WorkbenchSlotIds.ReplaceLdc,
                workspace.Write("ldc.bin", new byte[referenceLength]));
        }

        FirmwareSlotViewModel ldc = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceLdc);
        FirmwareSlotViewModel initialCode = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
        FirmwareSlotViewModel reference = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceBase);
        Assert.All(viewModel.ReplaceSlots, static slot => Assert.True(slot.UsesSharedSlotPresentation));
        if (referenceLength > 0 && expectedLdcState != ResolvedChildReadiness.Blocked)
        {
            Assert.Equal(WorkbenchInputInspectionSeverity.Valid, reference.InputInspectionSeverity);
            Assert.Equal(FirmwareSlotSemanticState.Verified, reference.SemanticState);
        }

        Assert.Equal(referenceLength != 0x40000, initialCode.IsOptional);
        Assert.Equal(WorkbenchAddressSpaceIds.LdcReplacement, ldc.AddressSpaceId);
        Assert.Equal(expectedLdcState, ldc.SelectionReadinessState);
        Assert.Equal(expectedLdcState == ResolvedChildReadiness.Ready, ldc.CanSelectFile);
        if (expectedCanBuild)
        {
            Assert.All(
                viewModel.ReplaceSlots.Where(static slot => slot.HasFile),
                static slot => Assert.True(
                    slot.InputInspectionSeverity is not null && !slot.BlocksBuild,
                    $"{slot.SlotId}: {slot.InputInspectionSeverity}; {slot.InputInspectionStatus}"));
            Assert.Equal(
                WorkbenchInputInspectionSeverity.Valid,
                viewModel.ReplaceSlots.Single(static slot =>
                    slot.SlotId == WorkbenchSlotIds.ReplaceBase).InputInspectionSeverity);
            Assert.All(
                viewModel.ReplaceSlots.Where(static slot =>
                    slot.HasFile && slot.SlotId != WorkbenchSlotIds.ReplaceBase),
                static slot => Assert.Equal(
                    WorkbenchInputInspectionSeverity.Warning,
                    slot.InputInspectionSeverity));
        }

        Assert.Equal(expectedCanBuild, viewModel.CanBuildReplace);
        Assert.Equal(expectedCanBuild, viewModel.BuildReplaceCommand.CanExecute(null));

        if (referenceLength == 0)
        {
            Assert.Equal("Pending input", ldc.SelectionReadinessLabel);
            Assert.Contains("Load Reference first", ldc.SelectionReadinessDetail, StringComparison.Ordinal);
            Assert.Equal(FirmwareSlotSemanticState.Checking, ldc.SemanticState);
            Assert.True(ldc.IsSemanticStatePendingInput);
            Assert.Equal(ldc.SelectionReadinessDetail, ldc.SemanticStateDetail);
        }
        else if (referenceLength == 0x40000 && !selectLdc)
        {
            if (!selectInitialCode)
            {
                Assert.True(initialCode.IsRequirementLabelVisible);
                Assert.Equal("Required", initialCode.RequirementLabel);
            }

            Assert.Equal("Not applicable", ldc.SelectionReadinessLabel);
            Assert.Equal("Reference length does not include LDC.", ldc.SelectionReadinessDetail);
            Assert.Equal(FirmwareSlotSemanticState.NotApplicable, ldc.SemanticState);
            Assert.False(ldc.IsRequirementLabelVisible);
        }
        else if (referenceLength == 0x40000)
        {
            Assert.Equal("Blocked", ldc.SelectionReadinessLabel);
            Assert.Contains(
                "Reference length does not include LDC.",
                ldc.SelectionReadinessDetail,
                StringComparison.Ordinal);
            Assert.Equal(FirmwareSlotSemanticState.Error, ldc.SemanticState);
        }
        else
        {
            Assert.Equal("Applicable", ldc.SelectionReadinessLabel);
            Assert.True(ldc.IsOptional);
            if (!selectLdc)
            {
                Assert.Equal(FirmwareSlotSemanticState.Empty, ldc.SemanticState);
                Assert.False(ldc.HasSemanticState);
                Assert.True(ldc.IsRequirementLabelVisible);
            }
        }

        Assert.Equal(
            $"{ldc.SelectionReadinessLabel}. {ldc.SelectionReadinessDetail}",
            ldc.SelectionReadinessAutomationText);
    }

    /// <summary>One admitted non-uniform DP source reaches the green terminal UI state.</summary>
    [Fact]
    public void Nt51928DpReplacePublishesVerifiedAfterSelection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-verified");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51928";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);

        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceDp,
            workspace.Write("initial-code.bin", CreatePattern(0x40000, 0x41)));

        FirmwareSlotViewModel initialCode = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
        Assert.Equal(WorkbenchInputInspectionSeverity.Valid, initialCode.InputInspectionSeverity);
        Assert.Equal(FirmwareSlotSemanticState.Verified, initialCode.SemanticState);
        Assert.Equal("Verified", initialCode.SemanticStateLabel);
        Assert.False(initialCode.BlocksBuild);
        Assert.True(viewModel.CanBuildReplace);
    }

    /// <summary>A selected source shorter than the compiled view is a terminal blocking error.</summary>
    [Fact]
    public void Nt51928DpReplacePublishesErrorForShortSelectedSource()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-short");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51928";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);

        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceDp,
            workspace.Write("initial-code-short.bin", new byte[0x1000]));

        FirmwareSlotViewModel initialCode = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
        Assert.Equal(WorkbenchInputInspectionSeverity.Blocking, initialCode.InputInspectionSeverity);
        Assert.Equal(FirmwareSlotSemanticState.Error, initialCode.SemanticState);
        Assert.True(initialCode.BlocksBuild);
        Assert.False(viewModel.CanBuildReplace);
    }

    /// <summary>The shared DP pilot applies typed input health to routes without selection groups too.</summary>
    [Fact]
    public void DpReplaceWithoutSelectionGroupBlocksShortSelectedSource()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-short");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51929";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);

        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceDp,
            workspace.Write("dp-short.bin", new byte[0x1000]));

        FirmwareSlotViewModel dp = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
        Assert.Equal(WorkbenchInputInspectionSeverity.Blocking, dp.InputInspectionSeverity);
        Assert.True(dp.BlocksBuild);
        Assert.False(viewModel.CanBuildReplace);
    }

    /// <summary>NT51950 production admission requires the replacement to match the selected base capacity exactly.</summary>
    [Fact]
    public void Nt51950DpReplaceBlocksNonMatchingReplacementCapacity()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51950-exact-pair");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);

        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceBase,
            workspace.Write("reference-40000.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceDp,
            workspace.Write("dp-3f000.bin", new byte[0x3F000]));

        FirmwareSlotViewModel dp = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
        Assert.Equal(WorkbenchInputInspectionSeverity.Blocking, dp.InputInspectionSeverity);
        Assert.Equal(FirmwareSlotSemanticState.Error, dp.SemanticState);
        Assert.True(dp.BlocksBuild);
        Assert.False(viewModel.CanBuildReplace);
    }

    /// <summary>Keeps shared slot adoption scoped to the DP Replace pilot until issue #208.</summary>
    [Fact]
    public void SharedSlotPresentationDoesNotPrematurelyAdoptOtherDesktopRoutes()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);

        Assert.All(viewModel.ReplaceSlots, static slot => Assert.True(slot.UsesLegacySlotPresentation));

        viewModel.SelectedMergeMode = WorkbenchMergeModes.Standard;
        viewModel.ShowMergeCommand.Execute(null);

        Assert.All(viewModel.MergeSlots, static slot => Assert.True(slot.UsesLegacySlotPresentation));
    }

    /// <summary>Localizes the typed LDC state and its next action without changing its meaning.</summary>
    [Fact]
    public void Nt51928DpReplaceSelectionReadinessIsLocalized()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-readiness-zh");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51928";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);

        FirmwareSlotViewModel ldc = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceLdc);
        Assert.Equal("Pending input", ldc.SelectionReadinessLabel);
        Assert.Contains("Load Reference first", ldc.SelectionReadinessDetail, StringComparison.Ordinal);

        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceBase,
            workspace.Write("reference-40000.bin", new byte[0x40000]));

        Assert.Equal("Not applicable", ldc.SelectionReadinessLabel);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("不適用", ldc.SelectionReadinessLabel);
        Assert.Equal(
            "此輸入不適用。Reference length does not include LDC.",
            ldc.SelectionReadinessDetail);
        Assert.Equal("不適用", ldc.SemanticStateLabel);
        Assert.Equal(FirmwareSlotSemanticState.NotApplicable, ldc.SemanticState);
        Assert.Equal(
            $"{ldc.SelectionReadinessLabel}。{ldc.SelectionReadinessDetail}",
            ldc.SelectionReadinessAutomationText);
    }

    /// <summary>Language changes reproject cached terminal health without changing its semantic result.</summary>
    [Fact]
    public void Nt51928DpReplaceTerminalInspectionIsRelocalized()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-health-zh");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51928";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceDp,
            workspace.Write("initial-code.bin", CreatePattern(0x40000, 0x41)));
        FirmwareSlotViewModel initialCode = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
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

    /// <summary>Confirmed navigation clears terminal input health together with the selected DP files.</summary>
    [Fact]
    public void DpReplaceNavigationClearRemovesTerminalInspection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-health-clear");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51928";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceDp,
            workspace.Write("initial-code.bin", CreatePattern(0x40000, 0x41)));
        Assert.Contains(viewModel.ReplaceSlots, static slot => slot.InputInspectionSeverity is not null);

        viewModel.ShowSettingsCommand.Execute(null);
        viewModel.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.All(viewModel.ReplaceSlots, static slot =>
        {
            Assert.Null(slot.InputInspectionSeverity);
            Assert.Equal(string.Empty, slot.InputInspectionStatus);
        });
    }

    /// <summary>An unresolved selection snapshot fails closed instead of falling back to optional-slot heuristics.</summary>
    [Fact]
    public void Nt51928UnsupportedReferenceCannotBypassSelectionReadiness()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-nt51928-readiness-invalid");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51928";
        OpenReplace(viewModel, WorkbenchReplaceModes.Dp);

        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceBase,
            workspace.Write("reference-60000.bin", new byte[0x60000]));
        viewModel.SetSlotFile(
            WorkbenchSlotIds.ReplaceDp,
            workspace.Write("initial-code.bin", new byte[0x1000]));

        Assert.False(viewModel.CanBuildReplace);
        Assert.False(viewModel.BuildReplaceCommand.CanExecute(null));
        Assert.Equal(FirmwareSlotSemanticState.Error, viewModel.ReplaceBaseSlot.SemanticState);
        Assert.True(viewModel.ReplaceBaseSlot.IsSemanticStateError);
        Assert.Equal(
            WorkbenchInputInspectionSeverity.Blocking,
            viewModel.ReplaceBaseSlot.InputInspectionSeverity);
        FirmwareSlotViewModel selected = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
        Assert.Equal(FirmwareSlotSemanticState.Error, selected.SemanticState);
        Assert.False(selected.IsSemanticStateChecking);
    }
}
