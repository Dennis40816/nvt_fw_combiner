using System.Text.RegularExpressions;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Blocking reports expose the exact reason, failed step, output impact, and next action without another click.</summary>
    [Fact]
    public void ReportBlockingIssueSummaryIsExpandedAndConcrete()
    {
        string panels = ReadPresentationFile("Resources/MainWindowReportPanels.axaml");

        Assert.Contains("IsExpanded=\"{Binding LoadedReport.HasPrimaryIssue}\"", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LoadedReport.PrimaryIssue.Detail}\"", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LoadedReport.PrimaryIssue.Title}\"", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LoadedReport.PrimaryIssue.Meta}\"", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LoadedReport.OutcomeDetail}\"", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LoadedReport.NextStepDetail}\"", panels, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding LoadedReport.IsOutputNotGenerated}\"", panels, StringComparison.Ordinal);
        Assert.Contains("NfcDangerTextBrush", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.PrimaryReasonLabel}\"", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.FailedStepLabel}\"", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.OutputImpactLabel}\"", panels, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.NextActionLabel}\"", panels, StringComparison.Ordinal);
    }

    /// <summary>Shared Report group headers remain visibly inset from the Expander frame.</summary>
    [Fact]
    public void ReportGroupHeadersKeepAnInsetFromTheExpanderFrame()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string inputs = ReadPresentationFile("Resources/MainWindowReportInputTemplates.axaml");
        string headerStyle = ExtractStyle(styles, "Border.reportInputGroupHeader");

        Assert.Contains("Property=\"Margin\" Value=\"8\"", headerStyle, StringComparison.Ordinal);
        Assert.Contains("Classes=\"reportInputGroupHeader\"", inputs, StringComparison.Ordinal);
    }

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
        Assert.Contains("<TabControl Grid.Row=\"1\" MinHeight=\"0\"", audit, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", audit, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Stretch\"", audit, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", audit, StringComparison.Ordinal);
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
        Assert.DoesNotContain("RowsPage.Items", changes, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceSummaryPage.Items", panels, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputDifferenceGroupPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("HexDiff.Ranges", audit, StringComparison.Ordinal);
        Assert.Contains("<VirtualizingStackPanel", audit, StringComparison.Ordinal);
        Assert.Contains("HexDiff.ViewportSnapshot", audit, StringComparison.Ordinal);
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
        int originalToggleStart = audit.IndexOf("<ToggleSwitch", StringComparison.Ordinal);
        int originalToggleEnd = audit.IndexOf("/>", originalToggleStart, StringComparison.Ordinal);
        string originalToggle = audit[originalToggleStart..(originalToggleEnd + 2)];

        Assert.Contains("ColumnDefinitions=\"2*,Auto,*\"", audit, StringComparison.Ordinal);
        Assert.Contains("<GridSplitter", audit, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.HexDiffResizeAutomationName}\"", audit, StringComparison.Ordinal);
        Assert.Contains("<views:HexViewportControl", audit, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AccessibilityView=\"Content\"", audit, StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.HelpText=\"{Binding LoadedReport.HexDiff.SelectedByteAccessibleLabel}\"",
            audit,
            StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"0\"", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("MinHeight=\"300\"", audit, StringComparison.Ordinal);
        Assert.Contains("Snapshot=\"{Binding LoadedReport.HexDiff.ViewportSnapshot}\"", audit, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding LoadedReport.HexDiff.Ranges}\"", audit, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding LoadedReport.HexDiff.Ranges.Count}\"", audit, StringComparison.Ordinal);
        Assert.Contains("<VirtualizingStackPanel", audit, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding LoadedReport.HexDiff.HasDifferenceWorkspace}\"", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("HexDiff.NavigatorPage", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("HexDiff.PinnedSelectedRange", audit, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"{Binding LoadedReport.HexDiff.RangeScrollMaximum}\"", audit, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding LoadedReport.HexDiff.RangeScrollRow, Mode=TwoWay}\"", audit, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding LoadedReport.HexDiff.ShowOriginalRows, Mode=TwoWay}\"", audit, StringComparison.Ordinal);
        Assert.Contains("Width=\"42\"", originalToggle, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"24\"", originalToggle, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=", originalToggle, StringComparison.Ordinal);
        Assert.Contains("Classes=\"fieldLabel\"", audit, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.HexDiffShowOriginalRowsLabel}\"", audit, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Grid.Row=\"2\" HorizontalAlignment=\"Right\" Orientation=\"Horizontal\" Spacing=\"7\">", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("HexDiff.JumpAddress", audit, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("HasPreviewFallback", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportOutputDifferenceGroupTemplate", changes, StringComparison.Ordinal);
        Assert.Contains("HexDiff.AvailabilityDetail", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedRange.PreviewCoverage", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Key=\"ReportHexDiffViewportRowTemplate\"", changes, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleLabel}\"", changes, StringComparison.Ordinal);
        Assert.Equal(
            1,
            Regex.Count(changes, Regex.Escape("AutomationProperties.Name=\"{Binding AccessibleLabel}\"")));
        Assert.DoesNotContain("<RadioButton", changes, StringComparison.Ordinal);
        Assert.Contains(
            "SelectedItem=\"{Binding LoadedReport.HexDiff.SelectedRange, Mode=TwoWay}\"",
            audit,
            StringComparison.Ordinal);
        Assert.Contains("Content=\"{ReflectionBinding $parent[Window].DataContext.Text.HexDiffSelectedRangeLabel}\"", changes, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding OutputSpaceId}\"", changes, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DisplayRange}\"", changes, StringComparison.Ordinal);
        Assert.Contains("Text.HexDiffWhyLabel", changes, StringComparison.Ordinal);
        Assert.Contains("Text.ResultLabel", changes, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(changes, Regex.Escape("IsVisible=\"{Binding IsSelected}\"")));
        Assert.DoesNotContain("Text.PrimaryReasonLabel", ExtractDataTemplate(changes, "ReportHexDiffRangeRowTemplate"), StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding Reason}\"", changes, StringComparison.Ordinal);
        Assert.DoesNotContain("HexDiffBeforeSha256Label", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("HexDiffAfterSha256Label", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedRange.Detail.BeforeLabel", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedRange.Detail.AfterLabel", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Border.reportHexDiffRow.changed", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"ToggleSwitch.reportHexDiffOriginalToggle\"", styles, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ReportHexDiffRangeCardTheme\"", changes, StringComparison.Ordinal);
        Assert.Contains("ResourceKey=\"ReportHexDiffRangeCardTheme\"", audit, StringComparison.Ordinal);
        Assert.Contains("Property=\"CornerRadius\" Value=\"{DynamicResource NfcSurfaceCornerRadius}\"", changes, StringComparison.Ordinal);
        Assert.Contains("Border#PART_SelectedRail", changes, StringComparison.Ordinal);
        Assert.Contains("^:selected /template/ Border#PART_RangeCard", changes, StringComparison.Ordinal);
        Assert.Contains("^:focus-visible /template/ Border#PART_RangeCard", changes, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector=\"RadioButton.reportHexDiffRange:checked\"", styles, StringComparison.Ordinal);
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
        string rangeRow = ExtractDataTemplate(changes, "ReportHexDiffRangeRowTemplate");

        Assert.Contains("Content=\"{ReflectionBinding $parent[Window].DataContext.Text.HexDiffSelectedRangeLabel}\"", rangeRow, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Top\"", rangeRow, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsSelected}\"", rangeRow, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding Status}\"", rangeRow, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(rangeRow, Regex.Escape("Content=\"{Binding Status}\"")));
        Assert.Contains("Text=\"{Binding OutputSpaceId}\"", rangeRow, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DisplayRange}\"", rangeRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding AccessibleRange}\"", rangeRow, StringComparison.Ordinal);
        Assert.Contains("Text.HexDiffWhyLabel", rangeRow, StringComparison.Ordinal);
        Assert.Contains("Text.ResultLabel", rangeRow, StringComparison.Ordinal);
        Assert.Contains("^:selected /template/ Border#PART_SelectedRail", changes, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", changes, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector=\"RadioButton.reportHexDiffRange:checked\"", styles, StringComparison.Ordinal);
    }
}
