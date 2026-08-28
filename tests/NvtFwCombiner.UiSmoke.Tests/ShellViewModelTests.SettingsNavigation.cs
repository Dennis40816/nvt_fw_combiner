using System.Text;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>Verifies Home does not compile page-specific workflow projections before navigation.</summary>
    [Fact]
    public void HomeDefersWorkflowProjectionUntilWorkflowNavigation()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Assert.Empty(viewModel.WorkflowSession.NumberSelectionChoices);
        Assert.Empty(viewModel.Merge.GeneralMergeOutputLength);
        Assert.Empty(viewModel.Merge.GeneralMergeOutputFillByte);
        Assert.Empty(viewModel.Merge.MergeSlots);
        Assert.Empty(viewModel.Replace.ReplaceSlots);
        Assert.Empty(viewModel.Merge.GeneralMergeMappings);
        Assert.Empty(viewModel.Replace.GeneralReplaceMappings);
        Assert.Null(viewModel.LoadedHexEditorWorkspace);

        viewModel.ShowMergeCommand.Execute(null);

        Assert.NotEmpty(viewModel.WorkflowSession.NumberSelectionChoices);
        Assert.NotEmpty(viewModel.Merge.GeneralMergeOutputLength);
        Assert.NotEmpty(viewModel.Merge.GeneralMergeOutputFillByte);
        Assert.NotEmpty(viewModel.Merge.MergeSlots);
        _ = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        _ = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
    }

    /// <summary>Verifies Settings opens as an application modal without becoming workflow navigation.</summary>
    [Fact]
    public void SettingsUsesCatalogBackedRowsWithoutDeviceContext()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Assert.Empty(viewModel.Settings.OverviewRows);
        Assert.Empty(viewModel.Settings.CapabilityRows);

        ShellPage pageBefore = viewModel.SelectedPage;
        string navigationBefore = viewModel.Navigation.NavigationPath;

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsModalOpen);
        Assert.Equal(pageBefore, viewModel.SelectedPage);
        Assert.Equal(navigationBefore, viewModel.Navigation.NavigationPath);
        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.False(viewModel.IsDeviceContextVisible);
        Assert.True(viewModel.Settings.IsPreferencesSelected);
        Assert.Equal(
            "Installed version and authoring availability from the current catalog.",
            viewModel.Text.SettingsOverviewSubtitle);
        Assert.Equal(
            "Status summarizes verification evidence and any route blockers; focus a cell for details.",
            viewModel.Text.SupportMatrixHoverHint);
        string expectedVersion = File.ReadAllText(RepositoryPaths.FromRepositoryRoot("VERSION")).Trim();
        Assert.Equal(expectedVersion, viewModel.AppVersion);
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "App version" && row.Value == expectedVersion);
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "IC catalog" && row.Value == "10");
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "Standard Merge" && row.Value == "10 ICs");
        Assert.DoesNotContain(viewModel.Settings.OverviewRows, row => row.Title == "DP Replace");
        SettingSummaryViewModel capability = Assert.Single(viewModel.Settings.CapabilityRows);
        Assert.Equal("CtrlRAM Replace available ICs", capability.Title);
        Assert.Equal("10 ICs", capability.Value);
        Assert.Equal("Available", capability.Status);
        Assert.DoesNotContain(
            viewModel.Settings.OverviewRows.Concat(viewModel.Settings.CapabilityRows),
            static row => row.Description.Contains("executable", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            ["System", "Light", "Dark"],
            viewModel.Settings.ThemeChoices.Select(static choice => choice.Label));
        Assert.Equal(
            ["System", "Light", "Dark"],
            viewModel.Settings.ThemeChoices.Select(static choice => choice.Value));

        viewModel.SelectedTheme = "Dark";
        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("設定", viewModel.SettingsPreview.Title);
        Assert.Equal(
            ["跟隨系統", "淺色", "深色"],
            viewModel.Settings.ThemeChoices.Select(static choice => choice.Label));
        Assert.Equal(
            ["英文", "繁體中文"],
            viewModel.Settings.LanguageChoices.Select(static choice => choice.Label));
        Assert.Equal("建立", viewModel.Text.BuildActionLabel);
        Assert.Equal("首頁", viewModel.Navigation.NavigationPath);
        Assert.Equal("已安裝版本，以及目前目錄中的編輯可用性。", viewModel.Text.SettingsOverviewSubtitle);
        Assert.Empty(viewModel.Merge.MergeSlots);
        Assert.Equal("必填", viewModel.Replace.ReplaceBaseSlot.RequirementLabel);
        Assert.Equal("尚未選擇 BIN", viewModel.Replace.ReplaceBaseSlot.DisplayName);
        Assert.Contains(viewModel.Settings.OverviewRows, row => row.Title == "IC 目錄" && row.Status == "目錄");
        Assert.Contains(viewModel.Settings.CapabilityRows, row =>
            row.Title == "CtrlRAM Replace 可用的 IC" &&
            row.Value == "10 個 IC" &&
            row.Status == "可用" &&
            row.Description.Contains("支援矩陣", StringComparison.Ordinal));

        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.SupportMatrix);

        Assert.True(viewModel.Settings.IsSupportMatrixOpen);
        Assert.False(viewModel.Settings.IsPreferencesSelected);

        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.Overview);

        Assert.True(viewModel.Settings.IsOverviewSelected);
        Assert.False(viewModel.Settings.IsSupportMatrixOpen);

        viewModel.CloseSettingsCommand.Execute(null);

        Assert.False(viewModel.IsSettingsModalOpen);
        Assert.Equal(pageBefore, viewModel.SelectedPage);

        viewModel.ShowMergeCommand.Execute(null);

        Assert.Contains(viewModel.Merge.MergeSlots, slot =>
            slot.Title == "DP BIN" &&
            slot.RequirementLabel == "必填" &&
            slot.DisplayName == "尚未選擇 BIN");
    }

    /// <summary>The shipped catalog hides DP Replace consistently in Settings while preserving the other authoring routes.</summary>
    [Fact]
    public void ProductSettingsProjectTheShippedHiddenDpReplacePolicy()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateProductViewModel();

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.Contains(
            viewModel.Settings.OverviewRows,
            row => row.Title == "Standard Merge" && row.Value == "10 ICs");
        Assert.DoesNotContain(
            viewModel.Settings.OverviewRows,
            row => row.Title == "DP Replace");
        Assert.Contains(
            viewModel.Settings.CapabilityRows,
            row => row.Title == "CtrlRAM Replace available ICs" && row.Value == "10 ICs");

        SupportMatrixRowViewModel[] availableRows =
        [
            .. viewModel.Settings.SupportMatrix.Rows.Where(static row => row.IsAuthoringAvailable),
        ];
        Assert.Equal(
            5,
            availableRows
                .Where(static row => row.WorkflowId == ExperienceIds.AbMerge)
                .Select(static row => row.IcId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.DoesNotContain(
            availableRows,
            static row => row.WorkflowId == ExperienceIds.DpReplace);
    }

    /// <summary>Opening Settings preserves selected Replace files, mappings, inspection identity and readiness.</summary>
    [Fact]
    public void SettingsModalPreservesReplaceAuthoringState()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-settings-replace-isolation");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x120";
        mapping.Length = "0x2";
        string basePath = workspace.Write("base.bin", [0x10, 0x11]);
        string mappingPath = workspace.Write("mapping.bin", [0x20, 0x21]);
        viewModel.SetSlotFile("replace-base", basePath);
        viewModel.SetSlotFile(mapping.MappingId, mappingPath);
        string readiness = viewModel.Replace.ReplaceReadinessStatus;
        FileStamp? acceptedStamp = mapping.AcceptedFileStamp;

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.CloseSettingsCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(ExperienceIds.GeneralReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(basePath, viewModel.Replace.ReplaceBaseSlot.FilePath);
        Assert.Equal(mappingPath, mapping.FilePath);
        Assert.Equal(acceptedStamp, mapping.AcceptedFileStamp);
        Assert.Equal("0x120", mapping.TargetStartAddress);
        Assert.Equal("0x2", mapping.Length);
        Assert.Equal(readiness, viewModel.Replace.ReplaceReadinessStatus);
    }

    /// <summary>Opening Settings preserves selected Merge inputs, mappings and readiness.</summary>
    [Fact]
    public void SettingsModalPreservesMergeAuthoringState()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-settings-merge-isolation");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x10";
        mapping.TargetStartAddress = "0x20";
        mapping.Length = "0x2";
        string mappingPath = workspace.Write("mapping.bin", [0x20, 0x21]);
        viewModel.SetSlotFile(mapping.MappingId, mappingPath);
        string readiness = viewModel.Merge.MergeReadinessStatus;
        FileStamp? acceptedStamp = mapping.AcceptedFileStamp;
        Assert.True(viewModel.IsCompositionActionRailVisible);

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.False(viewModel.IsCompositionActionRailVisible);
        viewModel.CloseSettingsCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsCompositionActionRailVisible);
        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.GeneralMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(mappingPath, mapping.FilePath);
        Assert.Equal(acceptedStamp, mapping.AcceptedFileStamp);
        Assert.Equal("0x10", mapping.SourceStartAddress);
        Assert.Equal("0x20", mapping.TargetStartAddress);
        Assert.Equal("0x2", mapping.Length);
        Assert.Equal(readiness, viewModel.Merge.MergeReadinessStatus);
    }

    /// <summary>The legacy settings launch destination opens the modal over Home without page history.</summary>
    [Fact]
    public void UiLaunchOptionsMigrateSettingsPageToHomeModal()
    {
        UiLaunchOptions options = UiLaunchOptions.Parse(["--page", "settings"]);

        Assert.Equal(ShellPage.Home, options.Page);
        Assert.True(options.OpenSettings);
        Assert.Empty(options.Issues);
    }

    /// <summary>Verifies breadcrumbs show page hierarchy while Back returns to the previous page.</summary>
    [Fact]
    public void NavigationTrailShowsHierarchyAndBackReturnsToPreviousPage()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Replace", viewModel.Navigation.NavigationPath);
        Assert.DoesNotContain("Merge > Replace", viewModel.Navigation.NavigationPath, StringComparison.Ordinal);
        Assert.False(viewModel.Navigation.NavigationTrail[^1].IsChevronVisible);

        viewModel.Navigation.GoBackCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Merge", viewModel.Navigation.NavigationPath);
    }

    /// <summary>Verifies the Home Hex Editor entry opens an independent raw utility without device context.</summary>
    [Fact]
    public void HomeHexEditorEntryOpensIndependentPage()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.ShowHexEditorCommand.Execute(null);

        Assert.True(viewModel.IsHexEditorVisible);
        Assert.False(viewModel.IsReplaceVisible);
        Assert.False(viewModel.IsDeviceContextVisible);
        Assert.Equal("Home > Hex Editor", viewModel.Navigation.NavigationPath);
        Assert.False(viewModel.Replace.ReplaceBaseSlot.HasFile);
    }

    /// <summary>Verifies Home workflow entries collect a cancellable context while programmatic navigation remains direct.</summary>
    [Fact]
    public void HomeWorkflowEntriesCollectContextBeforeOpeningWorkflow()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.True(viewModel.WorkflowSession.IsDeviceContextSelectionVisible);
        viewModel.BeginCtrlRamReplaceFromHomeCommand.Execute(null);

        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.True(viewModel.WorkflowSession.WorkflowContextSetup.IsNumberVisible);
        Assert.True(viewModel.IsHomeVisible);
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51927";
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumberChoice = viewModel.WorkflowSession.WorkflowContextSetup.NumberChoices.Single(choice => choice.Token == "3");
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);

        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.True(viewModel.WorkflowSession.IsDeviceContextSelectionVisible);
        Assert.NotEmpty(viewModel.WorkflowSession.NumberSelectionChoices);
        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("3", viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal("3", viewModel.WorkflowSession.SelectedNumberChoice?.Token);

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
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        string expectedReplaceIc = viewModel.WorkflowSession.SelectedIc;
        string expectedReplaceNumber = viewModel.WorkflowSession.SelectedNumber;

        viewModel.BeginAbMergeFromHomeCommand.Execute(null);
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51950";
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        viewModel.Navigation.GoBackCommand.Execute(null);

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
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ic-marker");
        string markedPath = workspace.Write("NT51927TT_test.bin", [0x00]);

        viewModel.SetSlotFile("replace-base", markedPath);

        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51927", viewModel.WorkflowSession.FirmwareIcMismatchDetectedIc);
        viewModel.WorkflowSession.DismissFirmwareIcMismatchCommand.Execute(null);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);

        using var golden = StandardMergeGoldenManifest.Load();
        string tpPath = golden.ManifestPath(golden.CaseByIc("51926").GetProperty("inputs").GetProperty("tp-input"));
        viewModel.SetSlotFile("merge-tp", tpPath);

        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("single", viewModel.WorkflowSession.SelectedNumber);
    }

    /// <summary>Verifies a printable header marker is advisory in the same way as a filename marker.</summary>
    [Fact]
    public void SlotLoadingPromptsForPrintableHeaderIcMarker()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-header-ic-marker");
        byte[] bytes = new byte[0x40000];
        Encoding.ASCII.GetBytes("firmware marker: NT51927TT").CopyTo(bytes, 0x120);
        string path = workspace.Write("base.bin", bytes);

        viewModel.SetSlotFile("replace-base", path);

        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51927", viewModel.WorkflowSession.FirmwareIcMismatchDetectedIc);
        viewModel.WorkflowSession.DismissFirmwareIcMismatchCommand.Execute(null);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);
    }

    /// <summary>Verifies filename markers outside the supported catalog cannot change the workbench context.</summary>
    [Fact]
    public void SlotLoadingIgnoresUnsupportedIcMarker()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-unsupported-ic-marker");
        string markedPath = workspace.Write("NT51999TT_test.bin", [0x00]);

        viewModel.SetSlotFile("replace-base", markedPath);

        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);
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
