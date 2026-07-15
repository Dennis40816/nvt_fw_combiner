using System.Text;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies Settings exposes catalog-backed status without requiring workflow context.</summary>
    [Fact]
    public void SettingsUsesCatalogBackedRowsWithoutDeviceContext()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        string expectedVersion = File.ReadAllText(RepositoryPaths.FromRepositoryRoot("VERSION")).Trim();
        Assert.Equal(expectedVersion, viewModel.AppVersion);
        Assert.Contains(viewModel.SettingsProfileRows, row => row.Title == "Built-in profiles" && row.Value.Contains("merge", StringComparison.Ordinal));
        Assert.Contains(viewModel.SettingsToolRows, row => row.Title == "CRC/header refresh" && row.Value == "Configured");
        Assert.Contains(viewModel.SettingsDiagnosticsRows, row => row.Title == "Report review");
        Assert.Contains(viewModel.SettingsReadinessRows, row => row.Title == "Device context" && row.Value == "Workflow pages only");

        viewModel.SelectedTheme = "Dark";
        viewModel.SelectedStrictness = "Warn only";
        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("設定", viewModel.SettingsPreview.Title);
        Assert.Equal("建立", viewModel.Text.BuildActionLabel);
        Assert.Equal("首頁 > 設定", viewModel.NavigationPath);
        Assert.Equal("目前視窗已套用暗色主題。", viewModel.ThemePreferenceStatus);
        Assert.Equal("只調整 UI review 語氣；韌體 gate 仍維持 fail-closed。", viewModel.StrictnessPreferenceStatus);
        Assert.Equal("繁體中文介面已套用並會在啟動時還原。", viewModel.LanguagePreferenceStatus);
        Assert.Contains(viewModel.MergeSlots, slot =>
            slot.Title == "DP BIN" &&
            slot.RequirementLabel == "必填" &&
            slot.DisplayName == "尚未選擇 BIN");
        Assert.Equal("必填", viewModel.ReplaceBaseSlot.RequirementLabel);
        Assert.Equal("尚未選擇 BIN", viewModel.ReplaceBaseSlot.DisplayName);
        Assert.Contains(viewModel.SettingsProfileRows, row => row.Title == "內建 profiles" && row.Status == "已串接");
        Assert.Contains(viewModel.SettingsDiagnosticsRows, row =>
            row.Title == "Report history 儲存" &&
            row.Status == "已啟用");
        Assert.Contains(viewModel.SettingsReadinessRows, row =>
            row.Title == "偏好設定" &&
            row.Value == "本機儲存");
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

        viewModel.BeginCtrlRamReplaceFromHomeCommand.Execute(null);

        Assert.True(viewModel.IsWorkflowContextModalOpen);
        Assert.True(viewModel.WorkflowContextSetup.IsNumberVisible);
        Assert.True(viewModel.IsHomeVisible);
        viewModel.WorkflowContextSetup.SelectedIc = "NT51927";
        viewModel.WorkflowContextSetup.SelectedNumberChoice = viewModel.WorkflowContextSetup.NumberChoices.Single(choice => choice.Token == "3");
        viewModel.ConfirmWorkflowContextCommand.Execute(null);

        Assert.False(viewModel.IsWorkflowContextModalOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal("NT51927", viewModel.SelectedIc);
        Assert.Equal("3", viewModel.SelectedNumber);

        viewModel.ShowHomeCommand.Execute(null);
        viewModel.BeginNormalMergeFromHomeCommand.Execute(null);
        Assert.True(viewModel.IsWorkflowContextModalOpen);
        Assert.False(viewModel.WorkflowContextSetup.IsNumberVisible);
        viewModel.ConfirmWorkflowContextCommand.Execute(null);
        Assert.True(viewModel.IsMergeVisible);
    }

    /// <summary>Verifies filename markers ask before changing the selected IC and verified TP FWConfig updates the number token.</summary>
    [Fact]
    public void SlotLoadingPromptsForIcMarkerAndAppliesVerifiedTpNumber()
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

        Assert.Equal("cascade", viewModel.SelectedNumber);
        Assert.Equal("Context updated", viewModel.ShellToastTitle);
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
