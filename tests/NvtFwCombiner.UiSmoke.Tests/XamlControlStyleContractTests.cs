using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Regression coverage for shared Avalonia visual-control contracts.</summary>
public sealed partial class XamlControlStyleContractTests
{
    private static readonly Regex ThemeTokenDefinitionPattern = ThemeTokenDefinitionRegex();

    private static readonly Regex ThemeShadowTokenDefinitionPattern = ThemeShadowTokenDefinitionRegex();

    private static readonly Regex ThemeCornerRadiusTokenDefinitionPattern = ThemeCornerRadiusTokenDefinitionRegex();

    private static readonly Regex ThemeSpacingTokenDefinitionPattern = ThemeSpacingTokenDefinitionRegex();

    private static readonly Regex ThemeFontSizeTokenDefinitionPattern = ThemeFontSizeTokenDefinitionRegex();

    private static readonly Regex ThemeFontFamilyTokenDefinitionPattern = ThemeFontFamilyTokenDefinitionRegex();

    private static readonly Regex DynamicThemeReferencePattern = DynamicThemeReferenceRegex();

    private static readonly Regex ColorLiteralPattern = ColorLiteralRegex();

    private static readonly Regex RawCommonSpacingPattern = RawCommonSpacingRegex();

    private static readonly Regex RawCommonFontSizePattern = RawCommonFontSizeRegex();

    /// <summary>Keeps every technical hexadecimal field on one canonical display format.</summary>
    [Fact]
    public void HexInputBehaviorNormalizesAddressesBytesAndExcelPaste()
    {
        Assert.Equal("0xAB12", NormalizeHexText("0Xab12g", HexTextInputMode.Address));
        Assert.Equal("0x123A", NormalizeHexText("123a", HexTextInputMode.Address));
        Assert.Equal("C5", NormalizeHexText("c5z", HexTextInputMode.Byte));
        Assert.Equal(
            "A5\t5A\r\n01,FF",
            NormalizeHexText("a5\t5a\r\n01,ffz", HexTextInputMode.ByteSequence));
    }

    private static string NormalizeHexText(string text, HexTextInputMode mode)
    {
        var textBox = new TextBox { Text = text, CaretIndex = text.Length };
        HexTextInputBehavior.SetMode(textBox, mode);
        return textBox.Text;
    }

    /// <summary>Ensures badge alignment and raw-text scrolling remain centralized.</summary>
    [Fact]
    public void SharedControlStylesDefineTheBadgeAndReadOnlyRawContracts()
    {
        string controlStyles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string windowStyles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        string compactBadge = ExtractStyle(controlStyles, "Label.compactBadge");
        string neutralBadge = ExtractStyle(controlStyles, "Label.neutralBadge");
        string reportBadge = ExtractStyle(controlStyles, "Label.reportBadge");
        string countBadge = ExtractStyle(controlStyles, "Label.countBadge");
        string technicalInput = ExtractStyle(windowStyles, "TextBox.technicalCenteredInput");
        string addressInput = ExtractStyle(windowStyles, "TextBox.addressInput");
        string hexByteInput = ExtractStyle(windowStyles, "TextBox.hexByteInput");

        Assert.Contains("HorizontalContentAlignment\" Value=\"Center\"", compactBadge, StringComparison.Ordinal);
        Assert.Contains("VerticalContentAlignment\" Value=\"Center\"", compactBadge, StringComparison.Ordinal);
        Assert.Contains("MinHeight\" Value=\"22\"", compactBadge, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceSubtleBrush", neutralBadge, StringComparison.Ordinal);
        Assert.DoesNotContain("MinHeight", reportBadge, StringComparison.Ordinal);
        Assert.DoesNotContain("MinHeight", countBadge, StringComparison.Ordinal);
        Assert.Contains("NfcTechnicalFontFamily", technicalInput, StringComparison.Ordinal);
        Assert.DoesNotContain("FontFamily", addressInput, StringComparison.Ordinal);
        Assert.DoesNotContain("FontFamily", hexByteInput, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBox.readOnlyRaw\"", controlStyles, StringComparison.Ordinal);
        Assert.Contains("IsReadOnly\" Value=\"True\"", controlStyles, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Auto\"", controlStyles, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", controlStyles, StringComparison.Ordinal);
    }

    /// <summary>Ensures General mapping rows use shared surface roles instead of repeating visual setters.</summary>
    [Fact]
    public void GeneralMappingRowsUseSharedSurfaceTokens()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string mappingRow = ReadPresentationFile("Views/GeneralMappingRow.axaml");
        string sharedTemplates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string reportHistoryTemplates = ReadPresentationFile("Resources/MainWindowReportHistoryTemplates.axaml");

        Assert.Contains("Selector=\"Border.fileDropZone\"", styles, StringComparison.Ordinal);
        Assert.Contains("Classes=\"subtleSurface\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("Classes=\"fileDropZone\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding SelectBinTooltip, ElementName=Root}\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding RemoveMappingTooltip, ElementName=Root}\"", mappingRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#F8FAFC\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("Classes=\"surface\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compactSurface\"", reportHistoryTemplates, StringComparison.Ordinal);
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
        Assert.DoesNotContain("IsVisible=\"{Binding HasMoreItems}\"", templates, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding PageStatus}\"", templates, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", templates, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding LoadMoreLabel}\"", templates, StringComparison.Ordinal);
        Assert.Contains("RowsPage.Items", changes, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceSummaryPage.Items", panels, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceGroupPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("MutationPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("OperationFlowPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("StepOperationPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("PostbuildInvocationPage.Items", audit, StringComparison.Ordinal);
        Assert.Contains("IssuePage.Items", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding LoadedReport.OutputDifferenceGroups}\"", reportUi, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding LoadedReport.Mutations}\"", reportUi, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding LoadedReport.Issues}\"", reportUi, StringComparison.Ordinal);
    }

    /// <summary>Ensures application resources expose the shared control style library to all views.</summary>
    [Fact]
    public void SharedControlStyleLibraryIsIncludedByTheApplication()
    {
        string application = ReadPresentationFile("App.axaml");
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");

        Assert.Contains("Styles/MainWindowControlStyles.axaml", application, StringComparison.Ordinal);
        Assert.Contains("<Label", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compactBadge slotBadge firmwareSlotRequirement\"", slotCard, StringComparison.Ordinal);
    }

    /// <summary>Loads the application resource tree and resolves every shared visual token.</summary>
    [Fact]
    public void ThemeTokensResolveFromTheApplicationResourceTree()
    {
        var app = new App();
        app.Initialize();

        foreach (string key in ReadThemeTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<SolidColorBrush>(resource);
        }

        foreach (string key in ReadThemeShadowTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<BoxShadows>(resource);
        }

        foreach (string key in ReadThemeCornerRadiusTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<CornerRadius>(resource);
        }

        foreach (string key in ReadThemeSpacingTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<double>(resource);
        }

        foreach (string key in ReadThemeFontSizeTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<double>(resource);
        }

        foreach (string key in ReadThemeFontFamilyTokenDefinitions()
                     .Select(static definition => definition.Groups["key"].Value))
        {
            Assert.True(
                app.TryGetResource(key, ThemeVariant.Default, out object? resource),
                $"Theme token '{key}' was not available from Application.Resources.");
            _ = Assert.IsType<FontFamily>(resource);
        }
    }

    /// <summary>Ensures Hex Editor uses the shared safe-save and immutable-reference interaction contracts.</summary>
    [Fact]
    public void HexEditorUsesConfirmedSaveAndReadOnlyReferenceRows()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string viewport = ReadPresentationFile("Views/HexEditorViewportControl.cs");
        string historyFeedback = ReadPresentationFile("Views/HexEditorViewportControl.HistoryFeedback.cs");
        string renderingSupport = ReadPresentationFile("Views/HexEditorViewportControl.RenderingSupport.cs");
        string sharedStyles = ReadPresentationFile("Styles/MainWindowStyles.axaml");

        Assert.Contains("Gesture=\"Ctrl+S\"", shell, StringComparison.Ordinal);
        Assert.Contains("RequestHexEditorSaveCommand", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HexInlineEditor\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("<views:HexEditorViewportControl", hexEditor, StringComparison.Ordinal);
        Assert.Contains("row.IsOriginalRowVisible", viewport, StringComparison.Ordinal);
        Assert.Contains("DrawReferenceRow", viewport, StringComparison.Ordinal);
        Assert.Contains("ReferenceChangedBrush", viewport, StringComparison.Ordinal);
        Assert.Contains("DrawAsciiStructuralBlocks", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("IBrush StructuralBrush =", viewport, StringComparison.Ordinal);
        Assert.Contains("$\"{displayAddress}  orig\"", renderingSupport, StringComparison.Ordinal);
        Assert.Contains("HistoryFeedbackVersion", viewport, StringComparison.Ordinal);
        Assert.Contains("DispatcherTimer", historyFeedback, StringComparison.Ordinal);
        Assert.Contains("DrawHistoryFeedback", historyFeedback, StringComparison.Ordinal);
        Assert.Contains("DrawAsciiSearchRanges", viewport, StringComparison.Ordinal);
        Assert.Equal(2, viewport.Split("DrawHoverOutline(context, rect", StringSplitOptions.None).Length - 1);
        Assert.Contains("TextChanged=\"HexByteEditBox_OnTextChanged\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("behaviors:HexTextInputBehavior.Mode=\"Byte\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("behaviors:HexTextInputBehavior.Mode=\"ByteSequence\"", hexEditor, StringComparison.Ordinal);
        Assert.Equal(3, hexEditor.Split("behaviors:HexTextInputBehavior.Mode=\"Address\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("behaviors:HexTextInputBehavior.Mode", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("input:InputMethod.IsInputMethodEnabled=\"False\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("HexEditorSourceDrop_OnDrop", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Height=\"{Binding HexViewportHeight}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding EditorStatus}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"hexInspectorHint\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes.error=\"{Binding HasEditFeedback}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding EditNotice}\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"hexInspectorFeedback\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding EditFeedback}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.hexInspectorHint\"", ReadPresentationFile("Styles/MainWindowControlStyles.axaml"), StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.hexInspectorHint.error\"", ReadPresentationFile("Styles/MainWindowControlStyles.axaml"), StringComparison.Ordinal);
    }

    /// <summary>Uses one hover overlay policy for both Hex and ASCII across every visible byte state.</summary>
    [Fact]
    public void HexEditorHoverOutlineDoesNotDisappearBehindByteState()
    {
        foreach (HexEditorCellVisualState state in Enum.GetValues<HexEditorCellVisualState>())
        {
            Assert.True(HexEditorViewportControl.ShouldDrawHoverOutline(isReference: false, isHovered: true, state));
            Assert.False(HexEditorViewportControl.ShouldDrawHoverOutline(isReference: false, isHovered: false, state));
            Assert.False(HexEditorViewportControl.ShouldDrawHoverOutline(isReference: true, isHovered: true, state));
        }
    }

    /// <summary>Ensures the Hex Editor inspector remains compact and the source and data surfaces share one workbench grid.</summary>
    [Fact]
    public void HexEditorInspectorUsesCompactTopAlignedLayout()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string codeBehind = ReadPresentationFile("Views/HexEditorPanel.axaml.cs");

        Assert.Contains("<ScrollViewer Grid.Row=\"3\">", shell, StringComparison.Ordinal);
        Assert.True(
            shell.IndexOf("ContentTemplate=\"{StaticResource HexEditorPageTemplate}\"", StringComparison.Ordinal) <
            shell.IndexOf("</ScrollViewer>", StringComparison.Ordinal));
        Assert.Contains("MaxWidth=\"1720\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,336\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"1\" Classes=\"compactSurface\" VerticalAlignment=\"Stretch\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,*\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("<ScrollBar", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,20\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"{Binding DocumentScrollMaximum}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ViewportSize=\"{Binding VisibleRowCount}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ValueChanged=\"HexDocumentScrollBar_OnValueChanged\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.HexEditorDocumentScrollBarAutomationName}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"iconButton hexGoToAddress\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding Text.HexEditorGoToAddressLabel}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding FindAsciiCommand}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding SelectedByteAccessibleLabel}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GoToAddressTextBox\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"GoToAddressTextBox_OnKeyDown\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AsciiSearchTextBox\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"AsciiSearchTextBox_OnKeyDown\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AddHandler(KeyDownEvent, HexEditorPanel_OnKeyDown, RoutingStrategies.Tunnel)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"Auto,*,196\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ToggleSwitch", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"hexOriginalRowsToggle\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("SelectNextChangedBlockCommand", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ChangedBlockCount", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ApplyRangeEditCommand}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"hexWriteModeToggle\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CurrentWriteModeLabel}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"iconButton hexInspectorAction\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"primary hexApplyChange\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ChangedBlocks}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding ReasonTooltip}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding ReasonTooltip}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("TextAlignment=\"Left\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"hexEditMode\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved region", hexEditor, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Maintains the compact Utility card hierarchy while keeping the full raw-editor warning on hover.</summary>
    [Fact]
    public void HomeUtilityCardUsesOneLineSummaryAndHoverDetail()
    {
        string homeTemplates = ReadPresentationFile("Resources/MainWindowPageTemplates.axaml");

        Assert.Contains("Text=\"{Binding Text.UtilToolsLabel}\"", homeTemplates, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.UtilToolsHomeTitle}\"", homeTemplates, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.UtilToolsHomeDetail}\"", homeTemplates, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", homeTemplates, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding Text.HexEditorDetail}\"", homeTemplates, StringComparison.Ordinal);
    }

    /// <summary>Keeps bounded multi-byte insertion explicit, accessible, and separate from overwrite/fill mode.</summary>
    [Fact]
    public void HexEditorBatchInsertUsesAContextActionAndBoundedModal()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string insertModal = ReadPresentationFile("Views/HexEditorInsertBytesModal.axaml");

        Assert.Contains("<views:HexEditorInsertBytesModal", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsInsertBytesPromptOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("<NumericUpDown", insertModal, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"{Binding MaximumInsertByteCount}\"", insertModal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ConfirmInsertBytesCommand}\"", insertModal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelInsertBytesCommand}\"", insertModal, StringComparison.Ordinal);
    }

    /// <summary>Keeps CtrlRAM version confirmation in a typed UI contract without firmware layout details.</summary>
    [Fact]
    public void CtrlRamBuildFirmwareVersionModalUsesTypedPreserveOrEditChoice()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string modal = ReadPresentationFile("Views/CtrlRamFirmwareVersionModal.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowStyles.axaml");

        Assert.Contains("<views:CtrlRamFirmwareVersionModal", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsCtrlRamFirmwareVersionModalOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("SelectCtrlRamFirmwareVersionPreserveCommand", modal, StringComparison.Ordinal);
        Assert.Contains("SelectCtrlRamFirmwareVersionEditCommand", modal, StringComparison.Ordinal);
        Assert.Contains("TryCreateCtrlRamFirmwareVersionEdit", ReadPresentationFile("Views/CtrlRamFirmwareVersionModal.axaml.cs"), StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.CtrlRamFirmwareVersionByteLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.CtrlRamFirmwareSubVersionByteLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBox.hexByteInput\"", styles, StringComparison.Ordinal);
        Assert.Contains("Classes=\"technicalCenteredInput hexByteInput\"", modal, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compactBadge neutralBadge\"", modal, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{DynamicResource NfcTechnicalFontFamily}\"", modal, StringComparison.Ordinal);
        Assert.Contains("behaviors:HexTextInputBehavior.Mode\" Value=\"ByteSequence\"", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigLayout", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("Combiner.exe", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("0x", modal, StringComparison.Ordinal);
    }

    /// <summary>Every composition run exposes typed steps plus a reduced-motion-safe activity indicator.</summary>
    [Fact]
    public void CompositionRunShowsGlobalWorkflowProgress()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string contextPanel = ReadPresentationFile("Resources/MainWindowShellPanels.axaml");
        var contextDocument = XDocument.Parse(contextPanel);
        XElement progressBar = Assert.Single(
            contextDocument.Descendants(),
            static element => element.Name.LocalName == "ProgressBar");

        Assert.DoesNotContain("<ProgressBar", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsRunInProgress}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding CompositionProgress.Steps}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleLabel}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ActiveRunIc}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ActiveRunNumber}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ActiveRunContextLabel}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsDeviceContextSelectionVisible}\"", contextPanel, StringComparison.Ordinal);
        Assert.Equal("True", progressBar.Attribute("IsIndeterminate")?.Value);
        Assert.Equal("{Binding ShouldAnimateRunProgress}", progressBar.Attribute("IsVisible")?.Value);
        Assert.Equal(
            "{Binding RunProgressStatusLabel}",
            progressBar.Attribute("AutomationProperties.Name")?.Value);
        Assert.DoesNotContain("AutomationProperties.Name=\"{Binding DeviceContextStatus}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"{Binding RunProgress", contextPanel, StringComparison.Ordinal);
    }

    /// <summary>Keeps each byte value centered on the calculated 16-column viewport geometry.</summary>
    [Fact]
    public void HexEditorByteCellsCenterTheirContentUnderTheHeader()
    {
        string viewport = ReadPresentationFile("Views/HexEditorViewportControl.cs");

        Assert.Contains("private const int BytesPerRow = 16", viewport, StringComparison.Ordinal);
        Assert.Contains("rect.X + ((rect.Width - text.Width) / 2)", viewport, StringComparison.Ordinal);
        Assert.Contains("rect.Y + ((rect.Height - text.Height) / 2)", viewport, StringComparison.Ordinal);
        Assert.Contains("GetCellWidth()", viewport, StringComparison.Ordinal);
    }

    /// <summary>Prevents document scrolling from recreating a control, binding, and template for every visible byte.</summary>
    [Fact]
    public void HexEditorUsesReadMostlyCellsAndOneSharedContextMenu()
    {
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string codeBehind = ReadPresentationFile("Views/HexEditorPanel.axaml.cs");
        string viewport = ReadPresentationFile("Views/HexEditorViewportControl.cs");
        string viewportInteraction = ReadPresentationFile("Views/HexEditorViewportControl.Interaction.cs");
        string viewportStructuralBlocks = ReadPresentationFile("Views/HexEditorViewportControl.StructuralBlocks.cs");
        string renderingSupport = ReadPresentationFile("Views/HexEditorViewportControl.RenderingSupport.cs");
        string hexInputBehavior = ReadPresentationFile("Behaviors/HexTextInputBehavior.cs");

        Assert.Contains("<views:HexEditorViewportControl", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding ViewportRows}\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("<ContentControl", hexEditor, StringComparison.Ordinal);
        Assert.Contains("public override void Render(DrawingContext context)", viewport, StringComparison.Ordinal);
        Assert.Contains("DrawByte(context", viewport, StringComparison.Ordinal);
        Assert.True(viewport.Split("_hoveredAddress,", StringSplitOptions.None).Length >= 3);
        Assert.Contains("CreateHexTextCache", renderingSupport, StringComparison.Ordinal);
        Assert.Contains("PointerPressed += OnPointerPressed", viewport, StringComparison.Ordinal);
        Assert.Contains("TryHitTestAscii", viewportInteraction, StringComparison.Ordinal);
        Assert.True(
            viewportInteraction.IndexOf("TryHitTestAscii(point", StringComparison.Ordinal) <
            viewportInteraction.IndexOf("TryHitTestStructuralAscii(point", StringComparison.Ordinal));
        Assert.Contains("!TryHitTestAscii(point, out cell, out bounds)", viewportInteraction, StringComparison.Ordinal);
        Assert.Contains("HexViewport.TryGetAsciiCellAt(point", codeBehind, StringComparison.Ordinal);
        Assert.Contains("protected override void OnPointerWheelChanged", viewportInteraction, StringComparison.Ordinal);
        Assert.Contains("ScrollRequested?.Invoke", viewportInteraction, StringComparison.Ordinal);
        Assert.Contains("Background=\"Transparent\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("KeyDown += OnKeyDown", viewport, StringComparison.Ordinal);
        Assert.Contains("GotFocus=\"HexTextInput_OnGotFocus\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("LostFocus=\"HexTextInput_OnLostFocus\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button.ContextMenu>", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAuthoringDisplayVisible", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEditorVisible", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualizingStackPanel", hexEditor, StringComparison.Ordinal);
        Assert.Contains("new ContextMenu()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_hexByteContextMenu.Open(HexViewport)", codeBehind, StringComparison.Ordinal);
        string[] byteContextBindings =
        [
            "BindContextCommand(_contextInsertBefore, viewModel.Text.HexEditorContextInsertZeroBeforeLabel, viewModel.InsertZeroBeforeCommand, e.Cell);",
            "BindContextCommand(_contextInsertAfter, viewModel.Text.HexEditorContextInsertZeroAfterLabel, viewModel.InsertZeroAfterCommand, e.Cell);",
            "BindContextCommand(_contextInsertManyBefore, viewModel.Text.HexEditorContextInsertBytesBeforeLabel, viewModel.RequestInsertBytesBeforeCommand, e.Cell);",
            "BindContextCommand(_contextInsertManyAfter, viewModel.Text.HexEditorContextInsertBytesAfterLabel, viewModel.RequestInsertBytesAfterCommand, e.Cell);",
            "BindContextCommand(_contextDeleteByte, viewModel.Text.HexEditorContextDeleteByteLabel, viewModel.DeleteByteCommand, e.Cell);",
            "BindContextCommand(_contextSetToZero, viewModel.Text.HexEditorContextSetToZeroLabel, viewModel.SetByteToZeroCommand, e.Cell);",
            "BindContextCommand(_contextSetToFf, viewModel.Text.HexEditorContextSetToFfLabel, viewModel.SetByteToFfCommand, e.Cell);",
        ];
        Assert.All(byteContextBindings, binding => Assert.Contains(binding, codeBehind, StringComparison.Ordinal));
        Assert.Contains("menuItem.Header = header", codeBehind, StringComparison.Ordinal);
        Assert.Contains("menuItem.Command = command", codeBehind, StringComparison.Ordinal);
        Assert.Contains("menuItem.CommandParameter = parameter", codeBehind, StringComparison.Ordinal);
        Assert.Contains("StructuralBlockContextMenuRequested", viewport, StringComparison.Ordinal);
        Assert.Contains("TryHitTestStructuralAscii", viewportStructuralBlocks, StringComparison.Ordinal);
        Assert.Contains("ContainsStructuralPoint", viewportStructuralBlocks, StringComparison.Ordinal);
        Assert.Contains("row.IsOriginalRowVisible ? RowHeight * 2 : RowHeight", viewportStructuralBlocks, StringComparison.Ordinal);
        Assert.Contains("HexViewport.TryGetStructuralBlockAt", codeBehind, StringComparison.Ordinal);
        Assert.True(
            codeBehind.IndexOf("HexViewport.TryGetStructuralBlockAt", StringComparison.Ordinal) <
            codeBehind.IndexOf("HexViewport.TryGetCellAt(point", StringComparison.Ordinal));
        Assert.Contains(
            "BindContextCommand(_structuralGoToStart, viewModel.Text.HexEditorContextGoToBlockStartLabel, viewModel.GoToChangedBlockStartCommand, block);",
            codeBehind,
            StringComparison.Ordinal);
        Assert.Contains(
            "BindContextCommand(_structuralGoToEnd, viewModel.Text.HexEditorContextGoToBlockEndLabel, viewModel.GoToChangedBlockEndCommand, block);",
            codeBehind,
            StringComparison.Ordinal);
        Assert.Contains("NormalizeAddress", hexInputBehavior, StringComparison.Ordinal);
        Assert.Contains("NormalizeByteSequence", hexInputBehavior, StringComparison.Ordinal);
        Assert.DoesNotContain("KeepAsciiHexOnly", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexDocumentScrollBar_OnValueChanged", codeBehind, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HexDocumentSurface\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("HexViewport_OnScrollRequested", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexDocumentSurface_OnPointerWheelChanged", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexDocumentSurface_OnDoubleTapped", codeBehind, StringComparison.Ordinal);
        Assert.Contains("QueueDocumentScroll", codeBehind, StringComparison.Ordinal);
        Assert.Contains("QueueViewportLayout", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexEditorSourceDrop_OnDrop", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexTextInput_OnGotFocus", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CompleteInlineEdit", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SetViewportStartRowCommand.Execute", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isDocumentScrollQueued", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Render", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>Right-click hit testing includes blank pixels inside one wrapped structural outline.</summary>
    [Fact]
    public void HexEditorStructuralOutlineIncludesBlankAndCrossRowAreas()
    {
        MethodInfo hitTest = typeof(HexEditorViewportControl).GetMethod(
            "ContainsStructuralPoint",
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("Structural outline hit test was not found.");
        Rect[] segments =
        [
            new Rect(140, 10, 30, 48),
            new Rect(80, 60, 40, 48),
        ];

        bool Contains(Point point)
        {
            return (bool)hitTest.Invoke(null, [segments, point, 80d, 220d])!;
        }

        Assert.True(Contains(new Point(200, 30))); // Blank tail of the first wrapped row.
        Assert.True(Contains(new Point(100, 59))); // Blank gap between wrapped rows.
        Assert.True(Contains(new Point(90, 90))); // Blank head of the final wrapped row.
        Assert.False(Contains(new Point(70, 59)));
        Assert.False(Contains(new Point(221, 30)));
    }

    /// <summary>Ensures the desktop shell and distributed executable use the dedicated compact app icon.</summary>
    [Fact]
    public void PresentationUsesTheDedicatedApplicationIcon()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string project = ReadPresentationFile("NvtFwCombiner.Presentation.Avalonia.csproj");
        string icon = RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "Assets",
            "AppIcon.ico");

        Assert.Contains("Icon=\"/Assets/AppIcon.ico\"", shell, StringComparison.Ordinal);
        Assert.Contains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.True(File.Exists(icon));
        Assert.NotEqual(0, new FileInfo(icon).Length);
    }

    /// <summary>Ensures startup never loads a repository fixture without an explicit launch option.</summary>
    [Fact]
    public void PresentationStartupHasNoImplicitDebugFixtureLoading()
    {
        string startup = ReadPresentationFile("MainWindow.Report.cs");
        string presentation = RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Presentation.Avalonia");

        Assert.False(File.Exists(Path.Combine(presentation, "DebugDemoFixture.cs")));
        Assert.False(File.Exists(Path.Combine(presentation, "MainWindow.DebugDemo.cs")));
        Assert.DoesNotContain("ApplyDebugDemoWhenNoLaunchOptions", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("#if DEBUG", startup, StringComparison.Ordinal);
    }

    /// <summary>Prevents repeated shell, panel, row, and text property bundles from drifting back into templates.</summary>
    [Fact]
    public void SharedControlStylesOwnTheCommonXamlRoles()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        foreach (string selector in new[]
        {
            "Border.shellBar",
            "Border.homePreviewSurface",
            "Border.roomyPanel",
            "Border.contentPanel",
            "Border.listRow",
            "Border.settingRow",
            "TextBlock.panelTitle",
            "TextBlock.compactTitle",
            "TextBlock.supportingText",
            "TextBlock.infoText",
            "TextBlock.technicalValue",
        })
        {
            Assert.Contains($"Selector=\"{selector}\"", styles, StringComparison.Ordinal);
        }

        string[] legacyPropertyBundles =
        [
            "FontSize=\"13\" FontWeight=\"SemiBold\" Foreground=\"#0F172A\"",
            "FontSize=\"12\" Foreground=\"#64748B\"",
            "FontSize=\"11\" Foreground=\"#64748B\"",
            "FontSize=\"10\" FontWeight=\"SemiBold\" Foreground=\"#64748B\"",
            "FontFamily=\"Cascadia Mono, Consolas\" FontSize=\"12\" Foreground=\"#0F172A\"",
        ];

        foreach (string xaml in ReadPresentationXamlFiles())
        {
            foreach (string bundle in legacyPropertyBundles)
            {
                Assert.DoesNotContain(bundle, xaml, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("CornerRadius=\"999\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Property=\"CornerRadius\" Value=\"999\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("CornerRadius=\"8\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Property=\"CornerRadius\" Value=\"8\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("CornerRadius=\"6\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Property=\"CornerRadius\" Value=\"6\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("compactStrongText", xaml, StringComparison.Ordinal);
        }
    }

    private static string ReadPresentationFile(string relativePath)
    {
        return File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Presentation.Avalonia", relativePath));
    }

    private static IEnumerable<string> ReadPresentationXamlFiles()
    {
        string presentationRoot = RepositoryPaths.FromRepositoryRoot("src", "NvtFwCombiner.Presentation.Avalonia");
        return Directory.EnumerateFiles(presentationRoot, "*.axaml", SearchOption.AllDirectories)
            .Select(File.ReadAllText);
    }

    private static Match[] ReadThemeTokenDefinitions()
    {
        return [.. ThemeTokenDefinitionPattern.Matches(ReadPresentationFile("Styles/ThemeTokens.axaml")).Cast<Match>()];
    }

    private static Match[] ReadThemeShadowTokenDefinitions()
    {
        return [.. ThemeShadowTokenDefinitionPattern.Matches(ReadPresentationFile("Styles/ThemeTokens.axaml")).Cast<Match>()];
    }

    private static Match[] ReadThemeCornerRadiusTokenDefinitions()
    {
        return [.. ThemeCornerRadiusTokenDefinitionPattern
            .Matches(ReadPresentationFile("Styles/ThemeTokens.axaml"))
            .Cast<Match>()];
    }

    private static Match[] ReadThemeFontFamilyTokenDefinitions()
    {
        return [.. ThemeFontFamilyTokenDefinitionPattern
            .Matches(ReadPresentationFile("Styles/ThemeTokens.axaml"))
            .Cast<Match>()];
    }

    private static Match[] ReadThemeSpacingTokenDefinitions()
    {
        return [.. ThemeSpacingTokenDefinitionPattern
            .Matches(ReadPresentationFile("Styles/ThemeTokens.axaml"))
            .Cast<Match>()];
    }

    private static Match[] ReadThemeFontSizeTokenDefinitions()
    {
        return [.. ThemeFontSizeTokenDefinitionPattern
            .Matches(ReadPresentationFile("Styles/ThemeTokens.axaml"))
            .Cast<Match>()];
    }

    [GeneratedRegex("<SolidColorBrush\\s+x:Key=\"(?<key>Nfc[A-Za-z]+)\"\\s+Color=\"(?<color>#[0-9A-Fa-f]{6,8})\"\\s*/>", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeTokenDefinitionRegex();

    [GeneratedRegex("<BoxShadows\\s+x:Key=\"(?<key>Nfc[A-Za-z]+)\">[^<]+</BoxShadows>", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeShadowTokenDefinitionRegex();

    [GeneratedRegex("<CornerRadius\\s+x:Key=\"(?<key>Nfc[A-Za-z]+)\">[^<]+</CornerRadius>", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeCornerRadiusTokenDefinitionRegex();

    [GeneratedRegex("<x:Double\\s+x:Key=\"(?<key>NfcSpace[0-9]+)\">(?<value>[^<]+)</x:Double>", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeSpacingTokenDefinitionRegex();

    [GeneratedRegex("<x:Double\\s+x:Key=\"(?<key>NfcFontSize[0-9]+)\">(?<value>[^<]+)</x:Double>", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeFontSizeTokenDefinitionRegex();

    [GeneratedRegex("<FontFamily\\s+x:Key=\"(?<key>Nfc[A-Za-z]+)\">(?<value>[^<]+)</FontFamily>", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeFontFamilyTokenDefinitionRegex();

    [GeneratedRegex("\\{DynamicResource\\s+(?<key>Nfc[A-Za-z0-9]+)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex DynamicThemeReferenceRegex();

    [GeneratedRegex("#[0-9A-Fa-f]{3,8}", RegexOptions.CultureInvariant)]
    private static partial Regex ColorLiteralRegex();

    [GeneratedRegex("\\b(?:Row|Column)?Spacing=\"(?:2|4|8|12|16)\"", RegexOptions.CultureInvariant)]
    private static partial Regex RawCommonSpacingRegex();

    [GeneratedRegex("(?:\\bFontSize=\"(?:10|11|12|13|14)\"|Property=\"FontSize\"\\s+Value=\"(?:10|11|12|13|14)\")", RegexOptions.CultureInvariant)]
    private static partial Regex RawCommonFontSizeRegex();
}
