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
        Assert.Empty(viewModel.GeneralMergeOutputLength);
        Assert.Empty(viewModel.GeneralMergeOutputFillByte);
        Assert.Empty(viewModel.MergeSlots);
        Assert.Empty(viewModel.ReplaceSlots);
        Assert.Empty(viewModel.GeneralMergeMappings);
        Assert.Empty(viewModel.GeneralReplaceMappings);
        Assert.Null(viewModel.LoadedHexEditorWorkspace);

        viewModel.ShowMergeCommand.Execute(null);

        Assert.NotEmpty(viewModel.NumberSelectionChoices);
        Assert.NotEmpty(viewModel.GeneralMergeOutputLength);
        Assert.NotEmpty(viewModel.GeneralMergeOutputFillByte);
        Assert.NotEmpty(viewModel.MergeSlots);
        _ = Assert.Single(viewModel.GeneralMergeMappings);
        _ = Assert.Single(viewModel.GeneralReplaceMappings);
    }

    /// <summary>Verifies Settings exposes catalog-backed status without requiring workflow context.</summary>
    [Fact]
    public void SettingsUsesCatalogBackedRowsWithoutDeviceContext()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.Empty(viewModel.SettingsOverviewRows);
        Assert.Empty(viewModel.SettingsCapabilityRows);

        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        string expectedVersion = File.ReadAllText(RepositoryPaths.FromRepositoryRoot("VERSION")).Trim();
        Assert.Equal(expectedVersion, viewModel.AppVersion);
        Assert.Contains(viewModel.SettingsOverviewRows, row => row.Title == "App version" && row.Value == expectedVersion);
        Assert.Contains(viewModel.SettingsOverviewRows, row => row.Title == "IC catalog" && row.Value == "10");
        Assert.Contains(viewModel.SettingsOverviewRows, row => row.Title == "Standard Merge" && row.Value == "10 profiles");
        Assert.Contains(viewModel.SettingsOverviewRows, row => row.Title == "DP Replace" && row.Value == "10 profiles");
        SettingSummaryViewModel capability = Assert.Single(viewModel.SettingsCapabilityRows);
        Assert.Equal("CtrlRAM Replace available ICs", capability.Title);
        Assert.Equal("10 ICs", capability.Value);
        Assert.Equal("Available", capability.Status);
        Assert.Equal(["System", "Light", "Dark"], viewModel.ThemeChoices);

        viewModel.SelectedTheme = "Dark";
        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("設定", viewModel.SettingsPreview.Title);
        Assert.Equal("建立", viewModel.Text.BuildActionLabel);
        Assert.Equal("首頁 > 設定", viewModel.NavigationPath);
        Assert.Empty(viewModel.MergeSlots);
        Assert.Equal("必填", viewModel.ReplaceBaseSlot.RequirementLabel);
        Assert.Equal("尚未選擇 BIN", viewModel.ReplaceBaseSlot.DisplayName);
        Assert.Contains(viewModel.SettingsOverviewRows, row => row.Title == "IC 目錄" && row.Status == "Catalog");
        Assert.Contains(viewModel.SettingsCapabilityRows, row =>
            row.Title == "CtrlRAM Replace 可用 IC" &&
            row.Value == "10 ICs" &&
            row.Status == "可用" &&
            row.Description.Contains("golden 驗證狀態", StringComparison.Ordinal));

        viewModel.ShowMergeCommand.Execute(null);

        Assert.Contains(viewModel.MergeSlots, slot =>
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

        Assert.True(viewModel.IsWorkflowContextModalOpen);
        Assert.True(viewModel.WorkflowContextSetup.IsNumberVisible);
        Assert.True(viewModel.IsHomeVisible);
        viewModel.WorkflowContextSetup.SelectedIc = "NT51927";
        viewModel.WorkflowContextSetup.SelectedNumberChoice = viewModel.WorkflowContextSetup.NumberChoices.Single(choice => choice.Token == "3");
        viewModel.ConfirmWorkflowContextCommand.Execute(null);

        Assert.False(viewModel.IsWorkflowContextModalOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsDeviceContextSelectionVisible);
        Assert.NotEmpty(viewModel.NumberSelectionChoices);
        Assert.Equal("NT51927", viewModel.SelectedIc);
        Assert.Equal("3", viewModel.SelectedNumber);
        Assert.Equal("3", viewModel.SelectedNumberChoice?.Token);

        viewModel.ShowHomeCommand.Execute(null);
        viewModel.BeginNormalMergeFromHomeCommand.Execute(null);
        Assert.True(viewModel.IsWorkflowContextModalOpen);
        Assert.False(viewModel.WorkflowContextSetup.IsNumberVisible);
        viewModel.ConfirmWorkflowContextCommand.Execute(null);
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
        viewModel.WorkflowContextSetup.SelectedIc = "NT51929";
        viewModel.ConfirmWorkflowContextCommand.Execute(null);
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.CascadeTwoToEight;
        viewModel.GoBackCommand.Execute(null);

        Assert.True(viewModel.IsHomeVisible);
        viewModel.BeginDpReplaceFromHomeCommand.Execute(null);

        Assert.True(viewModel.IsWorkflowContextModalOpen);
        Assert.Equal(expectedReplaceIc, viewModel.WorkflowContextSetup.SelectedIc);
        Assert.Equal(expectedReplaceNumber, viewModel.WorkflowContextSetup.SelectedNumber);
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

        Assert.True(viewModel.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51927", viewModel.FirmwareIcMismatchDetectedIc);
        viewModel.DismissFirmwareIcMismatchCommand.Execute(null);
        Assert.Equal("NT51926", viewModel.SelectedIc);

        using var golden = StandardMergeGoldenManifest.Load();
        string tpPath = golden.ManifestPath(golden.CaseByIc("51926").GetProperty("inputs").GetProperty("tp-input"));
        viewModel.SetSlotFile("merge-tp", tpPath);

        Assert.False(viewModel.IsFirmwareNumberMismatchModalOpen);
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

        Assert.True(viewModel.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51927", viewModel.FirmwareIcMismatchDetectedIc);
        viewModel.DismissFirmwareIcMismatchCommand.Execute(null);
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

        Assert.False(viewModel.IsFirmwareIcMismatchModalOpen);
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
