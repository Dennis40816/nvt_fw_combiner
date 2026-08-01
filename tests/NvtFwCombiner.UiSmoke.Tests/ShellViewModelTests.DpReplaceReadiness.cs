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
                workspace.Write("initial-code.bin", new byte[0x1000]));
        }

        if (selectLdc)
        {
            viewModel.SetSlotFile(
                WorkbenchSlotIds.ReplaceLdc,
                workspace.Write("ldc.bin", new byte[0x1000]));
        }

        FirmwareSlotViewModel ldc = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceLdc);
        FirmwareSlotViewModel initialCode = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == WorkbenchSlotIds.ReplaceDp);
        Assert.All(viewModel.ReplaceSlots, static slot => Assert.True(slot.UsesSharedSlotPresentation));
        Assert.Equal(referenceLength != 0x40000, initialCode.IsOptional);
        Assert.Equal(WorkbenchAddressSpaceIds.LdcReplacement, ldc.AddressSpaceId);
        Assert.Equal(expectedLdcState, ldc.SelectionReadinessState);
        Assert.Equal(expectedCanBuild, viewModel.CanBuildReplace);
        Assert.Equal(expectedCanBuild, viewModel.BuildReplaceCommand.CanExecute(null));

        if (referenceLength == 0)
        {
            Assert.Equal("Pending input", ldc.SelectionReadinessLabel);
            Assert.Contains("Load Reference first", ldc.SelectionReadinessDetail, StringComparison.Ordinal);
            Assert.Equal(FirmwareSlotSemanticState.Checking, ldc.SemanticState);
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
        Assert.Equal("Reference 長度不包含 LDC。", ldc.SelectionReadinessDetail);
        Assert.Equal("不適用", ldc.SemanticStateLabel);
        Assert.Equal(FirmwareSlotSemanticState.NotApplicable, ldc.SemanticState);
        Assert.Equal(
            $"{ldc.SelectionReadinessLabel}。{ldc.SelectionReadinessDetail}",
            ldc.SelectionReadinessAutomationText);
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
    }
}
