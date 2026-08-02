using System.Text;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies Home does not compile page-specific workflow projections before navigation.</summary>
    [Fact]
    public void HomeDefersWorkflowProjectionUntilWorkflowNavigation()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.Empty(viewModel.NumberSelectionChoices);
        Assert.Empty(viewModel.Merge.GeneralMergeOutputLength);
        Assert.Empty(viewModel.Merge.GeneralMergeOutputFillByte);
        Assert.Empty(viewModel.Merge.MergeSlots);
        Assert.Empty(viewModel.ReplaceSlots);
        Assert.Empty(viewModel.Merge.GeneralMergeMappings);
        Assert.Empty(viewModel.GeneralReplaceMappings);
        Assert.Null(viewModel.LoadedHexEditorWorkspace);

        viewModel.ShowMergeCommand.Execute(null);

        Assert.NotEmpty(viewModel.NumberSelectionChoices);
        Assert.NotEmpty(viewModel.Merge.GeneralMergeOutputLength);
        Assert.NotEmpty(viewModel.Merge.GeneralMergeOutputFillByte);
        Assert.NotEmpty(viewModel.Merge.MergeSlots);
        _ = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);
    }

    /// <summary>Verifies Settings exposes catalog-backed status without requiring workflow context.</summary>
    [Fact]
    public void SettingsUsesCatalogBackedRowsWithoutDeviceContext()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.Empty(viewModel.Settings.OverviewRows);
        Assert.Empty(viewModel.Settings.CapabilityRows);

        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        string expectedVersion = File.ReadAllText(RepositoryPaths.FromRepositoryRoot("VERSION")).Trim();
        Assert.Equal(expectedVersion, viewModel.AppVersion);
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "App version" && row.Value == expectedVersion);
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "IC catalog" && row.Value == "10");
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "Standard Merge" && row.Value == "10 profiles");
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "DP Replace" && row.Value == "10 profiles");
        SettingSummaryViewModel capability = Assert.Single(viewModel.Settings.CapabilityRows);
        Assert.Equal("CtrlRAM Replace available ICs", capability.Title);
        Assert.Equal("10 ICs", capability.Value);
        Assert.Equal("Available", capability.Status);
        Assert.Equal(["System", "Light", "Dark"], viewModel.Settings.ThemeChoices);

        viewModel.SelectedTheme = "Dark";
        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("設定", viewModel.SettingsPreview.Title);
        Assert.Equal("建立", viewModel.Text.BuildActionLabel);
        Assert.Equal("首頁 > 設定", viewModel.NavigationPath);
        Assert.Empty(viewModel.Merge.MergeSlots);
        Assert.Equal("必填", viewModel.ReplaceBaseSlot.RequirementLabel);
        Assert.Equal("尚未選擇 BIN", viewModel.ReplaceBaseSlot.DisplayName);
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "IC 目錄" && row.Status == "Catalog");
        Assert.Contains(viewModel.Settings.CapabilityRows, row =>
            row.Title == "CtrlRAM Replace 可用 IC" &&
            row.Value == "10 ICs" &&
            row.Status == "可用" &&
            row.Description.Contains("golden 驗證狀態", StringComparison.Ordinal));

        viewModel.ShowMergeCommand.Execute(null);

        Assert.Contains(viewModel.Merge.MergeSlots, slot =>
            slot.Title == "DP BIN" &&
            slot.RequirementLabel == "必填" &&
            slot.DisplayName == "尚未選擇 BIN");
    }

    /// <summary>Verifies breadcrumbs show page hierarchy while Back returns to the previous page.</summary>
    [Fact]
    public void NavigationTrailShowsHierarchyAndBackReturnsToPreviousPage()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Replace", viewModel.NavigationPath);
        Assert.DoesNotContain("Merge > Replace", viewModel.NavigationPath, StringComparison.Ordinal);
        Assert.False(viewModel.NavigationTrail[^1].IsChevronVisible);

        viewModel.GoBackCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Merge", viewModel.NavigationPath);
    }

    /// <summary>Verifies the Home Hex Editor entry opens an independent raw utility without device context.</summary>
    [Fact]
    public void HomeHexEditorEntryOpensIndependentPage()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowHexEditorCommand.Execute(null);

        Assert.True(viewModel.IsHexEditorVisible);
        Assert.False(viewModel.IsReplaceVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Hex Editor", viewModel.NavigationPath);
        Assert.False(viewModel.ReplaceBaseSlot.HasFile);
    }

    /// <summary>Verifies Home workflow entries collect a cancellable context while programmatic navigation remains direct.</summary>
    [Fact]
    public void HomeWorkflowEntriesCollectContextBeforeOpeningWorkflow()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.False(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsDeviceContextSelectionVisible);
        viewModel.BeginCtrlRamReplaceFromHomeCommand.Execute(null);

        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.True(viewModel.WorkflowSession.WorkflowContextSetup.IsNumberVisible);
        Assert.True(viewModel.IsHomeVisible);
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51927";
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumberChoice = viewModel.WorkflowSession.WorkflowContextSetup.NumberChoices.Single(choice => choice.Token == "3");
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);

        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsDeviceContextSelectionVisible);
        Assert.NotEmpty(viewModel.NumberSelectionChoices);
        Assert.Equal("NT51927", viewModel.SelectedIc);
        Assert.Equal("3", viewModel.SelectedNumber);
        Assert.Equal("3", viewModel.SelectedNumberChoice?.Token);

        viewModel.ShowHomeCommand.Execute(null);
        viewModel.BeginNormalMergeFromHomeCommand.Execute(null);
        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.False(viewModel.WorkflowSession.WorkflowContextSetup.IsNumberVisible);
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        Assert.True(viewModel.IsMergeVisible);
    }

    /// <summary>Returning from AB Code does not replace the saved Replace device context.</summary>
    [Fact]
    public void ReplaceContextDoesNotInheritAbCodeSelectionOnFirstOpen()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        string expectedReplaceIc = viewModel.SelectedIc;
        string expectedReplaceNumber = viewModel.SelectedNumber;

        viewModel.BeginAbMergeFromHomeCommand.Execute(null);
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51929";
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.CascadeTwoToEight;
        viewModel.GoBackCommand.Execute(null);

        Assert.True(viewModel.IsHomeVisible);
        viewModel.BeginDpReplaceFromHomeCommand.Execute(null);

        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.Equal(expectedReplaceIc, viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc);
        Assert.Equal(expectedReplaceNumber, viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumber);
    }

    /// <summary>Verifies an IC marker remains actionable while a hidden Merge TP number stays informational.</summary>
    [Fact]
    public void SlotLoadingPromptsForIcMarkerButDoesNotApplyMergeTpNumber()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ic-marker");
        string markedPath = workspace.Write("NT51927TT_test.bin", [0x00]);

        viewModel.SetSlotFile("replace-base", markedPath);

        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51927", viewModel.WorkflowSession.FirmwareIcMismatchDetectedIc);
        viewModel.WorkflowSession.DismissFirmwareIcMismatchCommand.Execute(null);
        Assert.Equal("NT51926", viewModel.SelectedIc);

        using var golden = StandardMergeGoldenManifest.Load();
        string tpPath = golden.ManifestPath(golden.CaseByIc("51926").GetProperty("inputs").GetProperty("tp-input"));
        viewModel.SetSlotFile("merge-tp", tpPath);

        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("single", viewModel.SelectedNumber);
    }

    /// <summary>Verifies a printable header marker is advisory in the same way as a filename marker.</summary>
    [Fact]
    public void SlotLoadingPromptsForPrintableHeaderIcMarker()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-header-ic-marker");
        byte[] bytes = new byte[0x40000];
        Encoding.ASCII.GetBytes("firmware marker: NT51927TT").CopyTo(bytes, 0x120);
        string path = workspace.Write("base.bin", bytes);

        viewModel.SetSlotFile("replace-base", path);

        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51927", viewModel.WorkflowSession.FirmwareIcMismatchDetectedIc);
        viewModel.WorkflowSession.DismissFirmwareIcMismatchCommand.Execute(null);
        Assert.Equal("NT51926", viewModel.SelectedIc);
    }

    /// <summary>Verifies filename markers outside the supported catalog cannot change the workbench context.</summary>
    [Fact]
    public void SlotLoadingIgnoresUnsupportedIcMarker()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-unsupported-ic-marker");
        string markedPath = workspace.Write("NT51999TT_test.bin", [0x00]);

        viewModel.SetSlotFile("replace-base", markedPath);

        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51926", viewModel.SelectedIc);
    }

    /// <summary>Verifies command-line launch arguments select a reviewable UI state.</summary>
    [Fact]
    public void UiLaunchOptionsParsePageReportAndOpenReport()
    {
        var options = UiLaunchOptions.Parse(
            ["--page", "merge", "--load-report", "preview-report.json", "--open-report"]);

        Assert.Equal(ShellPage.Merge, options.Page);
        Assert.Equal("preview-report.json", options.ReportPath);
        Assert.True(options.OpenReport);
        Assert.Empty(options.Issues);
    }

    /// <summary>Verifies command-line launch can open the independent Hex Editor page for visual review.</summary>
    [Fact]
    public void UiLaunchOptionsParseHexEditorPage()
    {
        var options = UiLaunchOptions.Parse(["--page", "hex-editor"]);

        Assert.Equal(ShellPage.HexEditor, options.Page);
        Assert.Empty(options.Issues);
    }

    /// <summary>Verifies invalid UI launch arguments fail as reportable issues.</summary>
    [Fact]
    public void UiLaunchOptionsCollectsInvalidArguments()
    {
        var options = UiLaunchOptions.Parse(
            ["--page=unknown", "--report=", "--open-report"]);

        Assert.Null(options.Page);
        Assert.Null(options.ReportPath);
        Assert.True(options.OpenReport);
        Assert.Contains(options.Issues, issue => issue.Contains("Unsupported --page value", StringComparison.Ordinal));
        Assert.Contains(options.Issues, issue => issue.Contains("--report requires a value.", StringComparison.Ordinal));
    }
}
