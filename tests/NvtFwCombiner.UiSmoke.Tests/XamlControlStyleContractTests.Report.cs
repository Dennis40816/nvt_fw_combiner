using System.Text.RegularExpressions;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Report input groups, headers, and rows use the self-padding panel contract.</summary>
    [Fact]
    public void ReportInputsUseSpaciousPanelBoundaries()
    {
        string inputs = ReadPresentationFile("Resources/MainWindowReportInputTemplates.axaml");
        string audit = ReadPresentationFile("Resources/MainWindowReportAuditTemplates.axaml");

        Assert.Equal(3, inputs.Split("<views:SpaciousPanel Classes=\"compact\">", StringSplitOptions.None).Length - 1);
        Assert.Contains("<ItemsControl Classes=\"spaciousList\" ItemsSource=\"{Binding Rows}\">", inputs, StringComparison.Ordinal);
        Assert.Contains("<views:SpaciousPanel Classes=\"compactSurface\" IsVisible=\"{Binding LoadedReport.HasInputGroups}\">", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"compactSurface contentPanel\" IsVisible=\"{Binding LoadedReport.HasInputGroups}\"", audit, StringComparison.Ordinal);
    }

    /// <summary>Ensures the Report raw payload uses the shared read-only text control.</summary>
    [Fact]
    public void ReportRawPayloadUsesTheSharedReadOnlyTextBox()
    {
        string rawTemplate = ReadPresentationFile("Resources/MainWindowReportAuditTemplates.axaml");
        Assert.Contains("<TextBox", rawTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes=\"readOnlyRaw\"", rawTemplate, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LoadedReportJson, Mode=OneWay}\"", rawTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer MaxHeight=\"320\"", rawTemplate, StringComparison.Ordinal);
    }

    /// <summary>Large report collections render bounded pages instead of binding complete evidence lists.</summary>
    [Fact]
    public void ReportDetailCollectionsUseBoundedPagerBindings()
    {
        string templates = ReadPresentationFile("Resources/MainWindowReportTemplates.axaml");
        string changes = ReadPresentationFile("Resources/MainWindowReportChangeTemplates.axaml");
        string panels = ReadPresentationFile("Resources/MainWindowReportPanels.axaml");
        string audit = ReadPresentationFile("Resources/MainWindowReportAuditTemplates.axaml");
        string reportUi = string.Join(Environment.NewLine, changes, panels, audit);

        Assert.Contains("x:Key=\"ReportPagerTemplate\"", templates, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ReportWindowedPagerTemplate\"", templates, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVisible=\"{Binding HasMoreItems}\"", templates, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding PageStatus}\"", templates, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", templates, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding LoadMoreLabel}\"", templates, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousPageCommand}\"", templates, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextPageCommand}\"", templates, StringComparison.Ordinal);
        Assert.Contains("RowsPage.Items", changes, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceSummaryPage.Items", panels, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceGroupPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("HexDiff.NavigatorPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("HexDiff.VisibleRows", audit, StringComparison.Ordinal);
        Assert.Contains("MutationPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("OperationFlowPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("StepOperationPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("PostbuildInvocationPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("IssuePage.Items", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding LoadedReport.OutputDifferenceGroups}\"", reportUi, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding LoadedReport.Mutations}\"", reportUi, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding LoadedReport.Issues}\"", reportUi, StringComparison.Ordinal);
    }

    /// <summary>The Changes tab exposes a bounded, read-only, accessible Hex Diff workspace.</summary>
    [Fact]
    public void ReportHexDiffWorkspaceUsesSharedAccessibleStaticVisualRoles()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string changes = ReadPresentationFile("Resources/MainWindowReportChangeTemplates.axaml");
        string audit = ReadPresentationFile("Resources/MainWindowReportAuditTemplates.axaml");
        string hexDiffSurface = string.Join(Environment.NewLine, changes, audit);
        string changedRowStyle = ExtractStyle(styles, "Border.reportHexDiffRow.changed");
        string selectedRangeStyle = ExtractStyle(styles, "RadioButton.reportHexDiffRange:checked");

        Assert.Contains("ColumnDefinitions=\"2*,Auto,*\"", audit, StringComparison.Ordinal);
        Assert.Contains("<GridSplitter", audit, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.HexDiffResizeAutomationName}\"", audit, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding LoadedReport.HexDiff.VisibleRows}\"", audit, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding LoadedReport.HexDiff.NavigatorPage.Items}\"", audit, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding LoadedReport.HexDiff.HasCompleteDifferenceWorkspace}\"", audit, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource ReportWindowedPagerTemplate}\"", audit, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding LoadedReport.HexDiff.PinnedSelectedRange}\"", audit, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding LoadedReport.HexDiff.HasPinnedSelectedRange}\"", audit, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding LoadedReport.HexDiff.ShowOriginalRows, Mode=TwoWay}\"", audit, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Enter\" Command=\"{Binding LoadedReport.HexDiff.JumpAddressCommand}\"", audit, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", audit, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding LoadedReport.HexDiff.HasPreviewFallback}\"", audit, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ReportHexDiffViewportRowTemplate\"", changes, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleLabel}\"", changes, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsOriginalVisible}\"", changes, StringComparison.Ordinal);
        Assert.Equal(
            2,
            Regex.Count(changes, Regex.Escape("AutomationProperties.Name=\"{Binding AccessibleLabel}\"")));
        Assert.Contains("<RadioButton", changes, StringComparison.Ordinal);
        Assert.Contains("GroupName=\"ReportHexDiffRangeNavigator\"", changes, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsSelected, Mode=OneWay}\"", changes, StringComparison.Ordinal);
        Assert.Contains("Content=\"{ReflectionBinding $parent[Window].DataContext.Text.HexDiffSelectedRangeLabel}\"", changes, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"{Binding}\"", changes, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding Reason}\"", changes, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.HexDiffBeforeSha256Label}\"", audit, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.HexDiffAfterSha256Label}\"", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedRange.Detail.BeforeLabel", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedRange.Detail.AfterLabel", audit, StringComparison.Ordinal);
        Assert.Contains("NfcWarningSurfaceBrush", changedRowStyle, StringComparison.Ordinal);
        Assert.Contains("Selector=\"ToggleSwitch.reportHexDiffOriginalToggle\"", styles, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", selectedRangeStyle, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", selectedRangeStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("HexEditorPanel", hexDiffSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestSave", hexDiffSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("UndoCommand", hexDiffSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("Transition", hexDiffSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("Animation", hexDiffSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#", hexDiffSurface, StringComparison.Ordinal);
    }

    /// <summary>Changed and selected Hex Diff states remain distinguishable without color perception.</summary>
    [Fact]
    public void ReportHexDiffHighContrastCuesDoNotDependOnColor()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string changes = ReadPresentationFile("Resources/MainWindowReportChangeTemplates.axaml");
        string viewportRow = ExtractDataTemplate(changes, "ReportHexDiffViewportRowTemplate");
        string rangeRow = ExtractDataTemplate(changes, "ReportHexDiffRangeRowTemplate");
        string selectedRangeStyle = ExtractStyle(styles, "RadioButton.reportHexDiffRange:checked");

        Assert.Contains("Content=\"{ReflectionBinding $parent[Window].DataContext.Text.HexDiffChangedRowLabel}\"", viewportRow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasChanges}\"", viewportRow, StringComparison.Ordinal);
        Assert.Contains("Text=\"{ReflectionBinding $parent[Window].DataContext.Text.HexDiffReferenceRowLabel}\"", viewportRow, StringComparison.Ordinal);
        Assert.Contains("Content=\"{ReflectionBinding $parent[Window].DataContext.Text.HexDiffSelectedRangeLabel}\"", rangeRow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsSelected}\"", rangeRow, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding Status}\"", rangeRow, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", selectedRangeStyle, StringComparison.Ordinal);
    }
}
