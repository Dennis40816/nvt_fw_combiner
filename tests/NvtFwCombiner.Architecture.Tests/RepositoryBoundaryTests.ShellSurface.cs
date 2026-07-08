namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies the shell follows the owner-approved clean home and independent page direction.</summary>
    [Fact]
    public void ShellUsesCleanHomeAndIndependentWorkflowPages()
    {
        string shell = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");
        string shellStyles = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowStyles.axaml");
        string sharedTemplates = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
        string reportTemplates = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportTemplates.axaml");
        string reportAuditTemplates = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportAuditTemplates.axaml");
        string reportPanels = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportPanels.axaml");
        string pageTemplates = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml");
        string shellPanels = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowShellPanels.axaml");
        string firmwareSlotCard = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/FirmwareSlotCard.axaml");
        string firmwareSlotCardCode = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/FirmwareSlotCard.axaml.cs");
        string generalReplaceMappingRow = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/GeneralReplaceMappingRow.axaml");
        string generalReplaceMappingRowCode = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Views/GeneralReplaceMappingRow.axaml.cs");
        string generalMergeMappingRow = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/GeneralMergeMappingRow.axaml");
        string generalMergeMappingRowCode = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Views/GeneralMergeMappingRow.axaml.cs");
        string dropZoneDragState = ReadText("src/NvtFwCombiner.Presentation.Avalonia/DropZoneDragState.cs");
        string replaceSelectionModal = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/ReplaceSelectionModal.axaml");
        string reportModal = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/ReportModal.axaml");
        string shellSurface = string.Join(
            Environment.NewLine,
            shell,
            pageTemplates,
            reportPanels,
            reportAuditTemplates,
            shellPanels,
            sharedTemplates,
            firmwareSlotCard,
            generalReplaceMappingRow,
            generalMergeMappingRow,
            replaceSelectionModal,
            reportModal);

        Assert.Contains("IsHomeVisible", shell, StringComparison.Ordinal);
        Assert.Contains("IsMergeVisible", shell, StringComparison.Ordinal);
        Assert.Contains("IsReplaceVisible", shell, StringComparison.Ordinal);
        Assert.Contains("ShowDpReplaceCommand", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("ShowNormalMergeCommand", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("WindowState=\"Maximized\"", shell, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto,*,Auto\"", shell, StringComparison.Ordinal);
        Assert.Contains("DeviceContextTitle", shellPanels, StringComparison.Ordinal);
        Assert.Contains("IsDeviceContextVisible", shell, StringComparison.Ordinal);
        Assert.Contains("NavigationTrail", shellPanels, StringComparison.Ordinal);
        Assert.Contains("GoBackCommand", shellPanels, StringComparison.Ordinal);
        Assert.Contains("IcChoices", shellPanels, StringComparison.Ordinal);
        Assert.Contains("SelectedIc", shellPanels, StringComparison.Ordinal);
        Assert.Contains("NumberChoices", shellPanels, StringComparison.Ordinal);
        Assert.Contains("SelectedNumber", shellPanels, StringComparison.Ordinal);
        Assert.Contains("ToggleButton", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"nav\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"command\"", shellSurface, StringComparison.Ordinal);
        Assert.Contains("Classes=\"iconButton\"", shellSurface, StringComparison.Ordinal);
        Assert.Contains("Classes=\"breadcrumb\"", shellPanels, StringComparison.Ordinal);
        Assert.Contains("Classes=\"primary\"", shellSurface, StringComparison.Ordinal);
        Assert.Contains("Classes=\"action\"", shellSurface, StringComparison.Ordinal);
        Assert.Contains("MainWindowStyles.axaml", shell, StringComparison.Ordinal);
        Assert.Contains("MainWindowSharedTemplates.axaml", shell, StringComparison.Ordinal);
        Assert.Contains("MainWindowReportTemplates.axaml", shell, StringComparison.Ordinal);
        Assert.Contains("MainWindowReportAuditTemplates.axaml", shell, StringComparison.Ordinal);
        Assert.Contains("MainWindowReportPanels.axaml", shell, StringComparison.Ordinal);
        Assert.Contains("MainWindowPageTemplates.axaml", shell, StringComparison.Ordinal);
        Assert.Contains("MainWindowShellPanels.axaml", shell, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource HomePageTemplate}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource SettingsPageTemplate}\"", shell, StringComparison.Ordinal);
        Assert.Contains("DataTemplate x:Key=\"HomePageTemplate\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("DataTemplate x:Key=\"SettingsPageTemplate\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"44\" />", shellStyles, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"CornerRadius\" Value=\"999\" />", shellStyles, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"1.15*,430\"", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.OutputLayoutTitle}\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"False\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("LoadReportJsonButton_OnClick", shell, StringComparison.Ordinal);
        Assert.Contains("SaveReportButton_OnClick", reportModal, StringComparison.Ordinal);
        Assert.Contains("BuildMergeButton_OnClick", shell, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowReportCommand}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsReportModalOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsReplaceSelectionModalOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.TargetsLabel}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ShowReplaceSelectionCommand}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ReplaceOutputLayoutPanelTemplate", shell, StringComparison.Ordinal);
        Assert.Contains("MergeOutputLayoutPanelTemplate", shell, StringComparison.Ordinal);
        Assert.Contains("MemoryCoverageSegmentBarTemplate", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("MemoryCoveragePlainSegmentBarTemplate", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("MemoryCoverageGroupTemplate", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("ReplaceMemoryMapRowTemplate", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("MergeMemoryMapRowTemplate", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("MemoryCoverageTooltipTemplate", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource MemoryCoverageTooltipTemplate}\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("Text=\"{ReflectionBinding $parent[Window].DataContext.Text.RangeLabel}\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("Text=\"{ReflectionBinding $parent[Window].DataContext.Text.ResultLabel}\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("DataTemplate x:Key=\"FirmwareSlotFactTemplate\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("<views:FirmwareSlotCard", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotDrop_OnDrop", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("BrowseSlotButton_OnClick", shell, StringComparison.Ordinal);
        Assert.Contains("SlotDrop_OnDrop", firmwareSlotCard, StringComparison.Ordinal);
        Assert.Contains("SlotDragOver_OnDragOver", firmwareSlotCard, StringComparison.Ordinal);
        Assert.Contains("BrowseButton_OnClick", firmwareSlotCard, StringComparison.Ordinal);
        Assert.Contains("SetSlotFile", firmwareSlotCardCode, StringComparison.Ordinal);
        Assert.Contains("DropZoneDragState.ApplyFileDropEffect", firmwareSlotCardCode, StringComparison.Ordinal);
        Assert.Contains("DropZoneDragState.GetFirstLocalFilePath", firmwareSlotCardCode, StringComparison.Ordinal);
        Assert.Contains("GetFirstLocalFilePath", dropZoneDragState, StringComparison.Ordinal);
        Assert.Contains("DragActiveClass", dropZoneDragState, StringComparison.Ordinal);
        Assert.Contains("<views:GeneralReplaceMappingRow", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("BrowseGeneralMappingButton_OnClick", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralMappingDrop_OnDrop", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveGeneralMappingButton_OnClick", shell, StringComparison.Ordinal);
        Assert.Contains("MappingDrop_OnDrop", generalReplaceMappingRow, StringComparison.Ordinal);
        Assert.Contains("BrowseButton_OnClick", generalReplaceMappingRow, StringComparison.Ordinal);
        Assert.Contains("RemoveButton_OnClick", generalReplaceMappingRow, StringComparison.Ordinal);
        Assert.Contains("SetGeneralReplaceMappingFile", generalReplaceMappingRowCode, StringComparison.Ordinal);
        Assert.Contains("RemoveGeneralReplaceMappingRow", generalReplaceMappingRowCode, StringComparison.Ordinal);
        Assert.Contains("IsNonCtrlRamStructuredReplaceModeSelected", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.GeneralReplaceMappingTitle}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Workbench wiring pending", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.GeneralReplaceRuleBoundsTitle}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.GeneralReplaceRuleLengthTitle}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.ExplicitMappingsTitle}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ReplaceBaseSlot", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding GeneralReplaceMappings}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding MergeCoverageSegments}\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceSlots}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceSlotGroups}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceCoverageGroups}\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding MergeSlots}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceMemoryRows}\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding MergeMemoryRows}\"", sharedTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding PreviewMergeCommand}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding PreviewReplaceCommand}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanBuildMerge}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding MergeBuildActionTip}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding ReplaceBuildActionTip}\"", shellSurface, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.GeneralMergeMappingTitle}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding GeneralMergeMappings}\"", shell, StringComparison.Ordinal);
        Assert.Contains("<views:GeneralMergeMappingRow", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralMergeMappingDrop_OnDrop", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("BrowseGeneralMergeMappingButton_OnClick", shell, StringComparison.Ordinal);
        Assert.Contains("MappingDrop_OnDrop", generalMergeMappingRow, StringComparison.Ordinal);
        Assert.Contains("BrowseButton_OnClick", generalMergeMappingRow, StringComparison.Ordinal);
        Assert.Contains("RemoveButton_OnClick", generalMergeMappingRow, StringComparison.Ordinal);
        Assert.Contains("SetGeneralMergeMappingFile", generalMergeMappingRowCode, StringComparison.Ordinal);
        Assert.Contains("RemoveGeneralMergeMappingRow", generalMergeMappingRowCode, StringComparison.Ordinal);
        Assert.Contains("Button.reportAction", shellStyles, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanOpenReport}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ReportActionLabel", shell, StringComparison.Ordinal);
        Assert.Contains("ReportActionStatus", shell, StringComparison.Ordinal);
        Assert.Contains("<Window.KeyBindings>", shell, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+H\" Command=\"{Binding ShowReportHistoryCommand}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+Shift+Delete\" Command=\"{Binding ClearReportHistoryCommand}\"", shell, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.OpenReportHistoryAutomationName}\"", reportPanels, StringComparison.Ordinal);
        int reportHeaderIndex = reportPanels.IndexOf("Text=\"{Binding LoadedReport.Title}\"", StringComparison.Ordinal);
        bool hasAuditDetailsTemplate = reportAuditTemplates.Contains("ReportAuditDetailsPanelTemplate", StringComparison.Ordinal);
        int historyActionIndex = reportPanels.IndexOf(
            "AutomationProperties.Name=\"{Binding Text.OpenReportHistoryAutomationName}\"",
            StringComparison.Ordinal);
        Assert.True(
            reportHeaderIndex >= 0 && hasAuditDetailsTemplate && historyActionIndex > reportHeaderIndex,
            "Report history should remain a secondary evidence action instead of returning to the report modal header.");
        Assert.DoesNotContain("ReportHistoryActionLabel", shell, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource ReportModalHeaderTemplate}\"", reportModal, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource ReportHistoryPanelTemplate}\"", reportModal, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource ReportSummaryPanelTemplate}\"", reportModal, StringComparison.Ordinal);
        Assert.Contains("LoadedReport.OutcomeTitle", reportPanels, StringComparison.Ordinal);
        Assert.Contains("LoadedReport.ByteDifferenceTitle", reportPanels, StringComparison.Ordinal);
        Assert.Contains("LoadedReport.OutputDifferenceSummaryRows", reportPanels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.ChangeReviewTitle}\"", reportPanels, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource ReportAuditDetailsPanelTemplate}\"", reportModal, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.EvidenceTitle}\"", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("ReportDifferenceSummaryChipTemplate", reportPanels, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose IC and Number inside", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("HomeReplaceStatus", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("HomeMergeStatus", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{ReflectionBinding $parent[Window].DataContext.Text.RangeTableTitle}\"", reportTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding RangeRows}\"", reportTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("Where to look first", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Evidence map", shell, StringComparison.Ordinal);
        Assert.Contains("<TabControl", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding Text.ReportTabInputs}\"", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding Text.ReportTabChanges}\"", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding Text.ReportTabOperations}\"", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding Text.ReportTabPostbuild}\"", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding Text.ReportTabIssues}\"", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding Text.ReportTabRaw}\"", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("ReportOutputDifferenceRowTemplate", reportTemplates, StringComparison.Ordinal);
        Assert.Contains("ReportOperationFlowNodeTemplate", reportTemplates, StringComparison.Ordinal);
        Assert.Contains("LoadedReport.OperationFlow", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("LoadedReport.CommandOperations", reportAuditTemplates, StringComparison.Ordinal);
        Assert.Contains("LoadedReport.StepOperations", reportAuditTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"24,*\"", shell, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, Segoe UI\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"secondary\" Content=\"{Binding PreviewActionLabel}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#0F172A\" CornerRadius=\"8\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Merge / Replace workspace", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"220,*\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnostics.", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SavedRulesAndReports", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Policy display only", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("0x0000 - 0xFFFF", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("AB disabled", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("LOADED REPORT", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"{Binding ReportModalActionLabel}\"", shell, StringComparison.Ordinal);

        string viewModel = ReadViewModelPartials();
        string mergeViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Merge.cs");
        string reportViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Report.cs");
        string settingsViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Settings.cs");
        string navigationViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Navigation.cs");
        Assert.Contains("LoadReportJson", reportViewModel, StringComparison.Ordinal);
        Assert.Contains("ReportReviewViewModel", reportViewModel, StringComparison.Ordinal);
        Assert.Contains("CanOpenReport", reportViewModel, StringComparison.Ordinal);
        Assert.Contains("ReportToastText", reportViewModel, StringComparison.Ordinal);
        Assert.Contains("UiCompositionRunner.GetNumberChoices", viewModel, StringComparison.Ordinal);
        Assert.Contains("UiCompositionRunner.GetStandardMergeMemoryMapRows", viewModel, StringComparison.Ordinal);
        Assert.Contains("UiCompositionRunner.GetStandardMergeCoverageSegments", viewModel, StringComparison.Ordinal);
        Assert.Contains("UiCompositionRunner.GetReplaceMemoryMapRows", viewModel, StringComparison.Ordinal);
        Assert.Contains("ReplaceModeChoices", viewModel, StringComparison.Ordinal);
        Assert.Contains("GeneralReplaceMappings", viewModel, StringComparison.Ordinal);
        Assert.Contains("ReplaceBaseSlot", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsStructuredReplaceModeSelected", viewModel, StringComparison.Ordinal);
        Assert.Contains("PreviewMergeCommand", viewModel, StringComparison.Ordinal);
        Assert.Contains("BuildMergeCommand", viewModel, StringComparison.Ordinal);
        Assert.Contains("BuildStandardMergeAsync", mergeViewModel, StringComparison.Ordinal);
        Assert.Contains("UiCompositionRunner", mergeViewModel, StringComparison.Ordinal);
        Assert.Contains("RunStandardMergeAsync", mergeViewModel, StringComparison.Ordinal);
        Assert.Contains("SettingsProfileRows", settingsViewModel, StringComparison.Ordinal);
        Assert.Contains("SettingsToolRows", settingsViewModel, StringComparison.Ordinal);
        Assert.Contains("NavigationPath", navigationViewModel, StringComparison.Ordinal);

        string flashMapCatalog = ReadFlashMapCatalogPartials();
        Assert.Contains("NF CtrlRAM", flashMapCatalog, StringComparison.Ordinal);
        Assert.Contains("Normal CtrlRAM", flashMapCatalog, StringComparison.Ordinal);
        Assert.Contains("DIFF CtrlRAM", flashMapCatalog, StringComparison.Ordinal);
        Assert.Contains("Vector CtrlRAM", flashMapCatalog, StringComparison.Ordinal);
        Assert.Contains("NT51917", flashMapCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("CtrlRamRegionCatalog", shell, StringComparison.Ordinal);
    }
}
