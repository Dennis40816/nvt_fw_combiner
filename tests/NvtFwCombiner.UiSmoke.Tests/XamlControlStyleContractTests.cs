using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.Behaviors;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
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
        Assert.Contains("Classes=\"fileRevealAction\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Placement=\"BottomEdgeAlignedLeft\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("ToolTip.VerticalOffset=\"8\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("ToolTip.BetweenShowDelay=\"-1\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding DisplayDetail}\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("Command=\"{ReflectionBinding $parent[Window].DataContext.RevealFileCommand}\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"{Binding FilePath}\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding SelectBinTooltip, ElementName=Root}\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding RemoveMappingTooltip, ElementName=Root}\"", mappingRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#F8FAFC\"", mappingRow, StringComparison.Ordinal);
        Assert.Contains("Classes=\"surface\"", sharedTemplates, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compactSurface\"", reportHistoryTemplates, StringComparison.Ordinal);
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
        Assert.Contains("IsVisible=\"{Binding IsGuidanceVisible}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Placement=\"BottomEdgeAlignedLeft\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("ToolTip.VerticalOffset=\"8\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("ToolTip.BetweenShowDelay=\"-1\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding DisplayDetail}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes=\"fileRevealAction\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Command=\"{ReflectionBinding $parent[Window].DataContext.RevealFileCommand}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding DisplayName}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasFile}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"{Binding FilePath}\"", slotCard, StringComparison.Ordinal);
    }

    /// <summary>The full-path hover card stays offset from the filename and leaves its routed click intact.</summary>
    [Fact]
    public void FileRevealHoverCardShowsTheAbsolutePathWithoutCompetingForThePointer()
    {
        const string selectedPath = @"C:\firmware\selected source.bin";
        Assert.Contains(
            "ToolTip.Tip=\"{Binding DisplayDetail}\"",
            ReadPresentationFile("Views/FirmwareSlotCard.axaml"),
            StringComparison.Ordinal);
        Assert.Contains(
            "ToolTip.Tip=\"{Binding DisplayDetail}\"",
            ReadPresentationFile("Views/GeneralMappingRow.axaml"),
            StringComparison.Ordinal);
        (Control View, object Context)[] cases =
        [
            (
                new FirmwareSlotCard(),
                new FirmwareSlotViewModel("base", "Base firmware", "Complete firmware", FirmwareSlotKind.Base)
                {
                    FilePath = selectedPath,
                }),
            (
                new GeneralMappingRow(),
                new GeneralMergeMappingViewModel("source-1", 1)
                {
                    FilePath = selectedPath,
                }),
        ];
        foreach ((Control view, object context) in cases)
        {
            view.DataContext = context;
            view.Measure(new Size(1600, 900));
            view.Arrange(new Rect(view.DesiredSize));
            Button fileButton = Assert.Single(
                view.GetLogicalDescendants().OfType<Button>(),
                button => button.Classes.Contains("fileRevealAction"));
            string pathText = context switch
            {
                FirmwareSlotViewModel slot => slot.DisplayDetail,
                GeneralMappingRowViewModel mapping => mapping.DisplayDetail,
                _ => throw new InvalidOperationException("Unknown file reveal context."),
            };

            Assert.Equal(selectedPath, pathText);
            Assert.Equal(PlacementMode.BottomEdgeAlignedLeft, ToolTip.GetPlacement(fileButton));
            Assert.Equal(8, ToolTip.GetVerticalOffset(fileButton));
            Assert.Equal(-1, ToolTip.GetBetweenShowDelay(fileButton));
            ToolTip.SetTip(fileButton, pathText);

            bool clicked = false;
            fileButton.Click += (_, _) => clicked = true;
            ToolTip.SetIsOpen(fileButton, true);

            Assert.True(ToolTip.GetIsOpen(fileButton));
            fileButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(clicked);

            ToolTip.SetIsOpen(fileButton, false);
        }
    }

    /// <summary>The IC detail tooltip supports pointer and keyboard discovery without becoming interactive.</summary>
    [Fact]
    public void IcDetailTooltipUsesOneNonInteractiveFocusAwareCard()
    {
        string shellPanels = ReadPresentationFile("Resources/MainWindowShellPanels.axaml");
        var document = XDocument.Parse(shellPanels);
        XElement combo = Assert.Single(
            document.Descendants(),
            element =>
                element.Name.LocalName == "ComboBox" &&
                (string?)element.Attribute("ItemsSource") == "{Binding WorkflowSession.IcChoices}");
        XElement tipProperty = Assert.Single(
            combo.Elements(),
            element => element.Name.LocalName == "ToolTip.Tip");
        XElement toolTip = Assert.Single(
            tipProperty.Elements(),
            element => element.Name.LocalName == "ToolTip");

        Assert.Equal("True", combo.Attributes().Single(attribute => attribute.Name.LocalName == "FocusToolTipBehavior.IsEnabled").Value);
        Assert.Equal("{Binding WorkflowSession.SelectedIcDetailAutomationText}", combo.Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.HelpText").Value);
        Assert.Equal("False", (string?)toolTip.Attribute("IsHitTestVisible"));
        XElement detailCard = Assert.Single(
            toolTip.Descendants(),
            element => (string?)element.Attribute("Classes") == "icDetailCard");
        Assert.Equal(
            4,
            detailCard.Descendants().Count(element =>
                element.Name.LocalName == "Path" &&
                ((string?)element.Attribute("Classes"))?.StartsWith("icDetail", StringComparison.Ordinal) == true));
        Assert.DoesNotContain(
            detailCard.Descendants(),
            element => (string?)element.Attribute("Text") is "{Binding WorkflowSession.SelectedIcDetailReuse}" or "{Binding WorkflowSession.SelectedIcDetailSupport}");

        var control = new ComboBox
        {
            ItemsSource = new[] { "NT51929", "NT51932" },
            SelectedIndex = 0,
        };
        ToolTip.SetTip(control, new ToolTip { IsHitTestVisible = false });
        FocusToolTipBehavior.SetIsEnabled(control, true);
        Assert.True(FocusToolTipBehavior.GetIsEnabled(control));
        Assert.False(Assert.IsType<ToolTip>(ToolTip.GetTip(control)).IsHitTestVisible);

        ToolTip.SetIsOpen(control, true);
        control.SelectedIndex = 1;
        Assert.False(ToolTip.GetIsOpen(control));
        Assert.False(ToolTip.GetServiceEnabled(control));

        control.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent));
        Assert.False(ToolTip.GetIsOpen(control));

        control.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
        Assert.True(ToolTip.GetServiceEnabled(control));
        control.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent));
        Assert.True(ToolTip.GetIsOpen(control));
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
        Assert.Contains("IsVisible=\"{Binding Replace.IsCtrlRamFirmwareVersionModalOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("SelectCtrlRamFirmwareVersionPreserveCommand", modal, StringComparison.Ordinal);
        Assert.Contains("SelectCtrlRamFirmwareVersionEditCommand", modal, StringComparison.Ordinal);
        Assert.Equal(2, modal.Split("Classes=\"segment versionChoice\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.CtrlRamFirmwareVersionKeepLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.CtrlRamFirmwareVersionEditLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Text=\"{Binding Text.CtrlRamFirmwareVersionKeepLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Text=\"{Binding Text.CtrlRamFirmwareVersionEditLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("TryCreateCtrlRamFirmwareVersionEdit", ReadPresentationFile("Views/CtrlRamFirmwareVersionModal.axaml.cs"), StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.CtrlRamFirmwareVersionByteLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.CtrlRamFirmwareSubVersionByteLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBox.hexByteInput\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"ToggleButton.versionChoice\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.versionSummaryCard\"", styles, StringComparison.Ordinal);
        Assert.Contains("Classes=\"technicalCenteredInput hexByteInput\"", modal, StringComparison.Ordinal);
        Assert.Contains("Classes=\"versionSummaryCard\"", modal, StringComparison.Ordinal);
        Assert.Contains("Classes=\"technicalValue versionValue\"", modal, StringComparison.Ordinal);
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
        string styles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        string nodeStyle = ExtractStyle(styles, "Border.runProgressNode");
        string activeNodeStyle = ExtractStyle(styles, "Border.runProgressNode.active");
        string markerStyle = ExtractStyle(styles, "TextBlock.runProgressMarker");
        var contextDocument = XDocument.Parse(contextPanel);
        XElement progressBar = Assert.Single(
            contextDocument.Descendants(),
            static element => element.Name.LocalName == "ProgressBar");

        Assert.DoesNotContain("<ProgressBar", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding RunSession.IsRunInProgress}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding RunSession.CompositionProgress.Steps}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleLabel}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding StateMarker}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"runProgressMarker\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AccessibilityView=\"Raw\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding RunSession.ActiveRunIc}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding RunSession.ActiveRunNumber}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding RunSession.IsRunInProgress}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding RunSession.ActiveRunContextLabel}\"", contextPanel, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding WorkflowSession.IsDeviceContextSelectionVisible}\"", contextPanel, StringComparison.Ordinal);
        Assert.Equal("True", progressBar.Attribute("IsIndeterminate")?.Value);
        Assert.Equal("{Binding RunSession.ShouldAnimateRunProgress}", progressBar.Attribute("IsVisible")?.Value);
        Assert.Equal(
            "{Binding RunSession.RunProgressStatusLabel}",
            progressBar.Attribute("AutomationProperties.Name")?.Value);
        Assert.Contains("Property=\"Height\" Value=\"22\"", nodeStyle, StringComparison.Ordinal);
        Assert.Contains("Property=\"BorderThickness\" Value=\"1\"", nodeStyle, StringComparison.Ordinal);
        Assert.Contains("Property=\"BorderThickness\" Value=\"2\"", activeNodeStyle, StringComparison.Ordinal);
        Assert.Contains("NfcTextStrongBrush", markerStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("AutomationProperties.Name=\"{Binding DeviceContextStatus}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"{Binding RunProgress", contextPanel, StringComparison.Ordinal);
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
        string window = ReadPresentationFile("MainWindow.axaml.cs");
        string presentation = RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Presentation.Avalonia");

        Assert.False(File.Exists(Path.Combine(presentation, "DebugDemoFixture.cs")));
        Assert.False(File.Exists(Path.Combine(presentation, "MainWindow.DebugDemo.cs")));
        Assert.DoesNotContain("ApplyDebugDemoWhenNoLaunchOptions", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("#if DEBUG", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportHistoryFileStore.LoadInto(viewModel);", window, StringComparison.Ordinal);
        Assert.Contains("protected override async void OnOpened", window, StringComparison.Ordinal);
        Assert.Contains("await ApplyDeferredLaunchOptionsAsync(", window, StringComparison.Ordinal);
        Assert.Contains("ReportHistoryFileStore.LoadAsync", startup, StringComparison.Ordinal);
        Assert.Contains("return viewModel.Reports.LoadReportJsonAsync(", startup, StringComparison.Ordinal);
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
