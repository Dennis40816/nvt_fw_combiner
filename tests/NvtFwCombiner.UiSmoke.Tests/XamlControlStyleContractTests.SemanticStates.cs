using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>General rows expose prerequisite blocking and typed inspection failures without local firmware rules.</summary>
    [Fact]
    public void GeneralMappingRowBindsCanonicalSelectionAndInspectionState()
    {
        string row = ReadPresentationFile("Views/GeneralMappingRow.axaml");
        string codeBehind = ReadPresentationFile("Views/GeneralMappingRow.axaml.cs");

        Assert.Contains("DragDrop.AllowDrop=\"{Binding CanSelectFile}\"", row, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanSelectFile}\"", row, StringComparison.Ordinal);
        Assert.Contains("Classes=\"firmwareSlotFact pendingInput\"", row, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding FileSelectionAvailabilityMessage}\"", row, StringComparison.Ordinal);
        Assert.Contains("Classes=\"firmwareSlotFact error\"", row, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding IssueMessage}\"", row, StringComparison.Ordinal);
        Assert.Contains("CanSelectFile: true", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("replace-base", row, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("replace-base", codeBehind, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Keeps typed firmware fact state in the ViewModel while shared styles own each presentation.</summary>
    [Fact]
    public void FirmwareSlotFactsUseSharedNormalAndWarningVisualStates()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml") +
            ReadPresentationFile("Styles/FirmwareSlotExperienceStyles.axaml");
        string factTemplate = ExtractDataTemplate(
            ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml"),
            "FirmwareSlotInformationFactTemplate");
        string viewModel = ReadPresentationFile("ViewModels/FirmwareSlotFactViewModel.cs");
        string pendingStyle = ExtractStyle(styles, "Border.firmwareSlotFact.pendingInput");
        string warningStyle = ExtractStyle(styles, "Border.firmwareSlotFact.warning");
        string errorStyle = ExtractStyle(styles, "Border.firmwareSlotFact.error");
        string warningTextStyle = ExtractStyle(styles, "Border.firmwareSlotFact.warning TextBlock");

        Assert.Contains("Value=\"{DynamicResource NfcWarningSurfaceBrush}\"", warningStyle, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcWarningBorderStrongBrush}\"", warningStyle, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceBrush", pendingStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("<Style Selector=\"Border.firmwareSlotFact.unknown\">", styles, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<Style Selector=\"Border.firmwareSlotFact\">",
            ReadPresentationFile("Styles/MainWindowControlStyles.axaml"),
            StringComparison.Ordinal);
        Assert.Contains("NfcCriticalSurfaceBrush", errorStyle, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.firmwareSlotFactLabel\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.firmwareSlotFactValue\"", styles, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcWarningTextBrush}\"", warningTextStyle, StringComparison.Ordinal);

        Assert.Contains("Classes=\"firmwareSlotFact\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes.error=\"{Binding IsError}\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes.pendingInput=\"{Binding IsPendingInput}\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes.unknown=\"{Binding IsUnknown}\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes.warning=\"{Binding IsWarning}\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes=\"firmwareSlotFactLabel\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes=\"firmwareSlotFactValue\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{DynamicResource NfcTechnicalFontFamily}\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Label}\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Value}\"", factTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundBrush", factTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("LabelForegroundBrush", factTemplate, StringComparison.Ordinal);

        Assert.DoesNotContain("Avalonia.Media", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Brush.Parse", viewModel, StringComparison.Ordinal);
        Assert.Contains("FirmwareSlotFactState State", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("bool IsWarning = false", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Legacy", factTemplate, StringComparison.Ordinal);
    }

    /// <summary>Keeps empty requirements distinct while one semantic state surface owns selected-file meaning.</summary>
    [Fact]
    public void FirmwareSlotCompletionUsesOrderedSharedVisualStates()
    {
        string tokens = ReadPresentationFile("Styles/ThemeTokens.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml") +
            ReadPresentationFile("Styles/FirmwareSlotExperienceStyles.axaml");
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");
        string viewModel = ReadPresentationFile("ViewModels/FirmwareSlotViewModel.cs");
        string missingSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot");
        string optionalSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.optional");
        string selectedSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.hasFile");
        string compactSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.compactExperience");
        string missingBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement");
        string optionalBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement.optional");
        string selectedBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement.hasFile");

        Assert.Contains("x:Key=\"NfcRequiredMissingBorderBrush\" Color=\"#FCA5A5\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcRequiredMissingBadgeSurfaceBrush\" Color=\"#FEE2E2\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcRequiredSelectedBorderBrush\" Color=\"#86EFAC\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcSuccessEmphasisBrush\" Color=\"#15803D\"", tokens, StringComparison.Ordinal);

        Assert.Contains("NfcCriticalSurfaceBrush", missingSlot, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredMissingBorderBrush", missingSlot, StringComparison.Ordinal);
        Assert.Contains("Value=\"1.5\"", missingSlot, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceSubtleBrush", optionalSlot, StringComparison.Ordinal);
        Assert.Contains("NfcBorderBrush", optionalSlot, StringComparison.Ordinal);
        Assert.Contains("NfcSuccessSurfaceBrush", selectedSlot, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredSelectedBorderBrush", selectedSlot, StringComparison.Ordinal);
        Assert.True(
            styles.IndexOf(selectedSlot, StringComparison.Ordinal) > styles.IndexOf(optionalSlot, StringComparison.Ordinal),
            "The retired full-card state remains earlier than the canonical compact override.");
        Assert.Contains("NfcSurfaceBrush", compactSlot, StringComparison.Ordinal);
        Assert.Contains("NfcBorderBrush", compactSlot, StringComparison.Ordinal);
        Assert.True(
            styles.IndexOf(compactSlot, StringComparison.Ordinal) > styles.IndexOf(selectedSlot, StringComparison.Ordinal),
            "The canonical card keeps semantic color on its compact indicator rather than the whole card.");

        Assert.Contains("NfcRequiredMissingBadgeSurfaceBrush", missingBadge, StringComparison.Ordinal);
        Assert.Contains("NfcDangerTextBrush", missingBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceBrush", optionalBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", optionalBadge, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredSelectedBadgeSurfaceBrush", selectedBadge, StringComparison.Ordinal);
        Assert.Contains("Classes=\"fileDropZone firmwareSlot compactExperience\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.hasFile=\"{Binding HasFile}\"", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacySlotPresentation", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.checking=\"{Binding IsSemanticStateChecking}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.error=\"{Binding IsSemanticStateError}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.notApplicable=\"{Binding IsSemanticStateNotApplicable}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.verified=\"{Binding IsSemanticStateVerified}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.warning=\"{Binding IsSemanticStateWarning}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Stretch=\"Uniform\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding SemanticStateAutomationText}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.optional=\"{Binding IsOptional}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compactBadge slotBadge firmwareSlotRequirement availableInput\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding RequirementLabel}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsRequirementLabelVisible}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("firmwareSlotRequirement.hasFile", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotBackgroundBrush", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("RequirementBadgeForegroundBrush", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotBackgroundBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("RequirementBadgeForegroundBrush", viewModel, StringComparison.Ordinal);
    }

    /// <summary>Notifies every property bound by the slot's semantic classes as one selected input changes health.</summary>
    [Fact]
    public void FirmwareSlotInspectionStatesNotifyEveryBoundVisualClass()
    {
        var slot = new FirmwareSlotViewModel(
            "base",
            "Base firmware",
            "Complete firmware",
            FirmwareSlotKind.Base);
        var notifications = new HashSet<string>(StringComparer.Ordinal);
        slot.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                _ = notifications.Add(args.PropertyName);
            }
        };

        Assert.False(slot.HasFile);
        slot.FilePath = "C:\\firmware\\base.bin";
        Assert.True(slot.HasFile);
        Assert.True(slot.IsSemanticStateChecking);
        Assert.Contains(nameof(FirmwareSlotViewModel.HasFile), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsSemanticStateChecking), notifications);

        notifications.Clear();
        slot.SetInputInspectionPending("Inspecting");
        Assert.True(slot.IsInputInspectionPending);
        Assert.False(slot.IsInputInspectionValid);
        Assert.True(slot.IsSemanticStateChecking);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionPending), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionValid), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsSemanticStateChecking), notifications);

        notifications.Clear();
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Ready");
        Assert.False(slot.IsInputInspectionPending);
        Assert.True(slot.IsInputInspectionValid);
        Assert.True(slot.IsSemanticStateVerified);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionPending), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionValid), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsSemanticStateVerified), notifications);

        notifications.Clear();
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Warning, "Review warning");
        Assert.False(slot.IsInputInspectionValid);
        Assert.True(slot.IsInputInspectionWarning);
        Assert.True(slot.IsSemanticStateWarning);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionValid), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionWarning), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsSemanticStateWarning), notifications);

        notifications.Clear();
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Blocking, "Fix input");
        Assert.False(slot.IsInputInspectionWarning);
        Assert.True(slot.IsInputInspectionBlocking);
        Assert.True(slot.IsSemanticStateError);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionWarning), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionBlocking), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsSemanticStateError), notifications);
    }

    /// <summary>Keeps each slot icon category visually distinct while geometry and accessible tooltips stay in the ViewModel.</summary>
    [Fact]
    public void FirmwareSlotIconsUseSharedCategoryVisualStates()
    {
        string tokens = ReadPresentationFile("Styles/ThemeTokens.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowVisualStyles.axaml");
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");
        string viewModel = ReadPresentationFile("ViewModels/FirmwareSlotViewModel.Icons.cs");

        foreach ((string key, string color) in new[]
                 {
                     ("NfcReferenceInputSurfaceBrush", "#EEF2FF"),
                     ("NfcReferenceInputBorderBrush", "#C7D2FE"),
                     ("NfcReferenceInputTextBrush", "#4338CA"),
                     ("NfcControllerInputSurfaceBrush", "#F5F3FF"),
                     ("NfcControllerInputBorderBrush", "#DDD6FE"),
                     ("NfcControllerInputTextBrush", "#6D28D9"),
                     ("NfcCautionBorderSoftBrush", "#FDE68A"),
                 })
        {
            Assert.Contains($"x:Key=\"{key}\" Color=\"{color}\"", tokens, StringComparison.Ordinal);
        }

        AssertIconStyle(styles, "bin", "NfcCautionSurfaceBrush", "NfcCautionBorderSoftBrush", "NfcCautionTextBrush");
        AssertIconStyle(styles, "base", "NfcReferenceInputSurfaceBrush", "NfcReferenceInputBorderBrush", "NfcReferenceInputTextBrush");
        AssertIconStyle(styles, "dp", "NfcAccentSurfaceSubtleBrush", "NfcAccentBorderLightBrush", "NfcAccentStrongBrush");
        AssertIconStyle(styles, "tp", "NfcSuccessSurfaceBrush", "NfcSuccessBorderBrush", "NfcSuccessEmphasisBrush");
        AssertIconStyle(styles, "ctrlRam", "NfcControllerInputSurfaceBrush", "NfcControllerInputBorderBrush", "NfcControllerInputTextBrush");

        Assert.Contains("xmlns:converters=\"clr-namespace:Avalonia.Data.Converters;assembly=Avalonia.Base\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Converter={x:Static converters:ObjectConverters.Equal}", slotCard, StringComparison.Ordinal);
        foreach (string kind in new[] { "Unknown", "Base", "Dp", "Tp", "CtrlRam" })
        {
            Assert.Contains($"ConverterParameter={{x:Static vm:FirmwareSlotKind.{kind}}}", slotCard, StringComparison.Ordinal);
        }

        Assert.Contains("ToolTip.Tip=\"{Binding SlotIconTooltip}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Data=\"{Binding SlotIconPathData}\"", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconBackgroundBrush", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconBorderBrush", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconForegroundBrush", slotCard, StringComparison.Ordinal);
        Assert.Contains("SlotIconPathData", viewModel, StringComparison.Ordinal);
        Assert.Contains("SlotIconTooltip", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia.Media", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Brush.Parse", viewModel, StringComparison.Ordinal);
    }

    /// <summary>Keeps coverage colors and Replace state chrome in shared styles while preserving plain Merge rendering.</summary>
    [Fact]
    public void MemoryCoverageUsesSharedChangedAndKeptVisualStates()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml") +
            ReadPresentationFile("Styles/MemoryCoverageStyles.axaml");
        string tokens = ReadPresentationFile("Styles/ThemeTokens.axaml");
        var shell = System.Xml.Linq.XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        string templates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml") +
            ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml");
        string replaceBar = ExtractDataTemplate(templates, "MemoryCoverageSegmentBarTemplate");
        string replaceList = ExtractDataTemplate(templates, "MemoryCoverageSegmentListTemplate");
        string mergeBar = ExtractDataTemplate(templates, "MemoryCoveragePlainSegmentBarTemplate");
        string mergeList = ExtractDataTemplate(templates, "MemoryCoveragePlainSegmentListTemplate");
        string logicalItem = ExtractDataTemplate(templates, "MemoryCoverageLogicalItemTemplate");
        string logicalRange = ExtractDataTemplate(templates, "MemoryCoverageLogicalRangeTemplate");
        string replacePanel = ExtractDataTemplate(templates, "ReplaceOutputLayoutPanelTemplate");
        string mergePanel = ExtractDataTemplate(templates, "MergeOutputLayoutPanelTemplate");
        string viewModel = ReadPresentationFile("ViewModels/MemoryCoverageSegmentViewModel.cs");
        string keptBadge = ExtractStyle(styles, "Label.countBadge.coverageChangeBadge");
        string changedBadge = ExtractStyle(styles, "Label.countBadge.coverageChangeBadge.changed");
        string keptMarker = ExtractStyle(styles, "Border.memoryCoverageMarker");
        string changedMarker = ExtractStyle(styles, "Border.memoryCoverageMarker.changed");
        System.Xml.Linq.XElement[] memoryPanelHosts =
        [
            .. shell.Descendants().Where(element =>
                element.Name.LocalName == "ContentControl" &&
                ((string?)element.Attribute("ContentTemplate"))?.Contains(
                    "OutputLayoutPanelTemplate",
                    StringComparison.Ordinal) == true),
        ];

        Assert.Contains("NfcSurfaceSubtleBrush", keptBadge, StringComparison.Ordinal);
        Assert.Contains("NfcBorderBrush", keptBadge, StringComparison.Ordinal);
        Assert.Contains("NfcTextSecondaryBrush", keptBadge, StringComparison.Ordinal);
        Assert.Contains("NfcInfoSurfaceStrongBrush", changedBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderLightBrush", changedBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", changedBadge, StringComparison.Ordinal);
        Assert.Equal(2, memoryPanelHosts.Length);
        Assert.All(memoryPanelHosts, host =>
        {
            Assert.Equal("1", (string?)host.Attribute("Grid.Column"));
            Assert.Equal("Top", (string?)host.Attribute("VerticalAlignment"));
        });
        Assert.Contains("Value=\"0\"", keptMarker, StringComparison.Ordinal);
        Assert.DoesNotContain("NfcAccentStrongBrush", changedMarker, StringComparison.Ordinal);
        Assert.Contains("Value=\"0\"", changedMarker, StringComparison.Ordinal);

        AssertCoverageClasses(replaceBar);
        AssertCoverageClasses(replaceList);
        Assert.Contains("Text=\"{Binding ChangeLabel}\"", logicalRange, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasChangeState}\"", logicalRange, StringComparison.Ordinal);
        Assert.DoesNotContain("memoryCoverageMarker", mergeBar, StringComparison.Ordinal);
        Assert.DoesNotContain("memoryCoverageMarker", mergeList, StringComparison.Ordinal);
        Assert.Contains("Classes=\"memoryCoverageFill memoryCoverageMarker memoryCoverageBarSegment\"", replaceBar, StringComparison.Ordinal);
        Assert.Contains("Classes=\"memoryCoverageFill memoryCoverageBarSegment\"", mergeBar, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.memoryCoverageBarSegment\"", styles, StringComparison.Ordinal);
        Assert.Contains("Property=\"BorderThickness\" Value=\"0\"", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("NfcMemorySegmentDividerBrush", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("NfcMemorySegmentDividerBrush", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcMemoryTrackBrush\" Color=\"#E7EDF5\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcMemoryTrackBrush\" Color=\"#263449\"", tokens, StringComparison.Ordinal);
        foreach ((string token, string light, string dark) in new[]
        {
            ("NfcMemoryPanelSurfaceBrush", "#FFFFFF", "#101827"),
            ("NfcMemoryPanelBorderBrush", "#D8E0EC", "#2B3A50"),
            ("NfcMemoryTextSecondaryBrush", "#58708F", "#A8BDD8"),
            ("NfcMemoryAddressSurfaceBrush", "#F6F8FB", "#152035"),
            ("NfcMemoryAddressBorderBrush", "#CCD7E5", "#34465F"),
            ("NfcMemoryAddressTextBrush", "#243654", "#E1EBFA"),
            ("NfcMemoryDividerBrush", "#E4E9F1", "#27364A"),
        })
        {
            Assert.Contains($"x:Key=\"{token}\" Color=\"{light}\"", tokens, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"{token}\" Color=\"{dark}\"", tokens, StringComparison.Ordinal);
        }
        Assert.Contains("Focusable=\"False\"", replaceBar, StringComparison.Ordinal);
        Assert.DoesNotContain("FocusToolTipBehavior.IsEnabled", replaceBar, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding AccessibleDetail}\"", replaceBar, StringComparison.Ordinal);
        Assert.Contains("MemoryCoverageInteractionBehavior.IsEnabled=\"True\"", replaceBar, StringComparison.Ordinal);
        Assert.Contains("Classes.linked=\"{Binding Interaction.IsActive}\"", replaceBar, StringComparison.Ordinal);
        Assert.Contains("Focusable=\"False\"", mergeBar, StringComparison.Ordinal);
        Assert.DoesNotContain("FocusToolTipBehavior.IsEnabled", mergeBar, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding AccessibleDetail}\"", mergeBar, StringComparison.Ordinal);
        Assert.Contains("MemoryCoverageInteractionBehavior.IsEnabled=\"True\"", logicalItem, StringComparison.Ordinal);
        Assert.Contains("Classes.linked=\"{Binding Interaction.IsActive}\"", logicalItem, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"12,*\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"12,*\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Padding=\"0,10\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("Padding=\"0,10\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,4,0,0\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,4,0,0\"", replaceList, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"*,Auto\"", mergeList, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(replaceList, "ColumnDefinitions=\"*,Auto\""));
        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto,Auto\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AddressRangeLabel}\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LengthLabel}\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AddressRangeLabel}\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LengthLabel}\"", replaceList, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(templates, "Classes=\"technicalValue\" Text=\"·\""));
        Assert.Contains("Grid.Row=\"1\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"1\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Left\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Left\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"2\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"2\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"3\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Classes=\"captionText memoryInfoDescription\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("Classes=\"captionText memoryInfoDescription\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleDetail}\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding AccessibleDetail}\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleDetail}\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding AccessibleDetail}\"", replaceList, StringComparison.Ordinal);
        Assert.DoesNotContain("<StackPanel Grid.Column=\"1\"", mergeList, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource NfcMemoryTrackBrush}\"", replacePanel, StringComparison.Ordinal);
        Assert.DoesNotContain("<Viewbox", replacePanel, StringComparison.Ordinal);
        Assert.Contains("<views:ProportionalStackPanel />", replacePanel, StringComparison.Ordinal);
        Assert.Contains("ProportionalContentPresenterTheme", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource NfcMemoryTrackBrush}\"", mergePanel, StringComparison.Ordinal);
        Assert.DoesNotContain("<Viewbox", mergePanel, StringComparison.Ordinal);
        Assert.Contains("<views:ProportionalStackPanel />", mergePanel, StringComparison.Ordinal);
        Assert.Contains("ProportionalContentPresenterTheme", mergePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"panelSurface memoryInfoPanel\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"panelSurface memoryInfoPanel\"", mergePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"mutedText memoryInfoDescription\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"mutedText memoryInfoDescription\"", mergePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compactSubtleSurface memoryInfoAddress\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compactSubtleSurface memoryInfoAddress\"", mergePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"monoText detailText memoryInfoAddressText\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"monoText detailText memoryInfoAddressText\"", mergePanel, StringComparison.Ordinal);
        Assert.Contains("Classes=\"listRow memoryInfoRow memoryCoverageLinkedRow\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Classes=\"listRow memoryInfoRow memoryCoverageLinkedRow\"", mergeList, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Classes=\"spaciousList\" ItemTemplate=\"{StaticResource MemoryCoverageSegmentListTemplate}\"",
            replacePanel,
            StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceSelectedCoverageItems}\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceBaseCoverageItems}\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ReplaceSelectedCoverageSummary}\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ReplaceBaseCoverageSummary}\"", replacePanel, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding ReplaceCoverageGroups}\"", replacePanel, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryCoverageGroupTemplate", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding RangeSummaryLabel}\"", logicalItem, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasMultipleRanges}\"", logicalItem, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AddressRangeLabel}\"", logicalRange, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CompactDetail}\"", logicalRange, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,Auto\"", logicalRange, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding RegionGroupLabel}\"", logicalRange, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(logicalRange, "MemoryCoverageCompactMarkerTemplate"));
        Assert.DoesNotContain("MemoryCoverageCompactMarkerTemplate", logicalItem, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.memoryInfoPanel\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.memoryInfoDescription\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.memoryInfoAddress\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.memoryInfoAddressText\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.memoryInfoRow\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.memoryInfoRangeList\"", styles, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"KeptDiagonalStripeBrush\"", templates, StringComparison.Ordinal);
        Assert.Contains("DestinationRect=\"0,0,10,10\"", templates, StringComparison.Ordinal);
        Assert.Contains("Geometry=\"M -2,10 L 10,-2\"", templates, StringComparison.Ordinal);
        Assert.Contains("Brush=\"{DynamicResource NfcKeptStripeBrush}\"", templates, StringComparison.Ordinal);
        Assert.Contains("TileMode=\"Tile\"", templates, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding UsesKeptPattern}\"", replaceBar, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding UsesKeptPattern}\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding DetailsLabel}\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasPreservationDetails}\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("<Flyout Placement=\"RightEdgeAlignedTop\">", replaceList, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding PreservationDetails}\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ArtifactRangeLabel}\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding FlashRangeLabel}\"", replaceList, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DispositionLabel}\"", replaceList, StringComparison.Ordinal);
        Assert.DoesNotContain("UsesKeptPattern", mergeBar, StringComparison.Ordinal);
        Assert.DoesNotContain("UsesKeptPattern", mergeList, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReplaceCoverageStateLegend\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding ShowsGenericCoverageStateLegend}\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasObservedMemoryChanges}\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.OutputLayoutChangedStateLabel}\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.OutputLayoutKeptStateLabel}\"", replacePanel, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"320\"", replacePanel, StringComparison.Ordinal);
        Assert.DoesNotContain("VerticalScrollBarVisibility=\"Auto\"", replacePanel, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"320\"", mergePanel, StringComparison.Ordinal);

        Assert.Contains("public MemoryCoverageFillRole FillRole", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("public IBrush FillBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Brush.Parse", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"{Binding FillBrush}\"", templates, StringComparison.Ordinal);
        Assert.Contains("Classes.dp=\"{Binding FillRole", templates, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.memoryCoverageFill.dp\"", styles, StringComparison.Ordinal);
        foreach ((string role, string token, string light, string dark) in new[]
        {
            ("ctrlRamNf", "NfcMemoryCtrlRamNfFillBrush", "#4F46E5", "#818CF8"),
            ("ctrlRamNormal", "NfcMemoryCtrlRamNormalFillBrush", "#0891B2", "#22D3EE"),
            ("ctrlRamMp", "NfcMemoryCtrlRamMpFillBrush", "#7C3AED", "#A78BFA"),
            ("ctrlRamVn", "NfcMemoryCtrlRamVnFillBrush", "#DB2777", "#F472B6"),
            ("ctrlRamVector", "NfcMemoryCtrlRamVectorFillBrush", "#0F766E", "#2DD4BF"),
        })
        {
            Assert.Equal(4, CountOccurrences(templates, $"Classes.{role}=\"{{Binding FillRole"));
            string roleStyle = ExtractStyle(styles, $"Border.memoryCoverageFill.{role}");
            Assert.Contains(token, roleStyle, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"{token}\" Color=\"{light}\"", tokens, StringComparison.Ordinal);
            Assert.Contains($"x:Key=\"{token}\" Color=\"{dark}\"", tokens, StringComparison.Ordinal);
        }
        Assert.Contains("x:Key=\"NfcMemoryCtrlRamFillBrush\" Color=\"#64748B\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcMemoryCtrlRamFillBrush\" Color=\"#94A3B8\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcMemoryDiffDlmFillBrush\" Color=\"#4D7C0F\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcMemoryDiffDlmFillBrush\" Color=\"#A3E635\"", tokens, StringComparison.Ordinal);
        Assert.DoesNotContain("segment.RegionId.Split", ReadPresentationFile("UiCompositionRunner.Common.cs"), StringComparison.Ordinal);
        Assert.Contains("ResolveCtrlRamCoverageFillRole(", ReadPresentationFile("UiCompositionRunner.Common.cs"), StringComparison.Ordinal);
        Assert.Contains("CtrlRamRegionRole.Vector => MemoryCoverageFillRole.CtrlRamVector", ReadPresentationFile("UiCompositionRunner.Common.cs"), StringComparison.Ordinal);
        Assert.Contains("public bool IsChanged", viewModel, StringComparison.Ordinal);
        Assert.Contains("public bool UsesKeptPattern", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("UsesBaseFirmwarePattern", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("IsDiffDlm", viewModel, StringComparison.Ordinal);
        Assert.Contains("public string ChangeLabel", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeBadgeBackgroundBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeBadgeBorderBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeBadgeForegroundBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("OutlineBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("OutlineThickness", viewModel, StringComparison.Ordinal);
    }

    /// <summary>Keeps changed/kept meaning while XAML resolves theme-specific segment colors.</summary>
    [Fact]
    public void MemoryCoverageRetainsStateAndThemeNeutralFillRole()
    {
        MemoryCoverageSegmentViewModel kept = new(
            "0x0000-0x0010",
            "Base flash",
            "Kept bytes",
            MemoryCoverageFillRole.Kept,
            20,
            disposition: Application.MemoryLayout.MemoryWorkflowDisposition.Kept,
            usesBaseFirmwarePattern: true,
            regionGroup: ReplaceRegionGroup.Common);
        MemoryCoverageSegmentViewModel changed = new(
            "0x0010-0x0020",
            "TP input",
            "Written bytes",
            MemoryCoverageFillRole.Tp,
            20,
            disposition: Application.MemoryLayout.MemoryWorkflowDisposition.WillWrite,
            observedChange: Application.MemoryLayout.MemoryObservedChange.Changed);
        MemoryCoverageSegmentViewModel planned = new(
            "0x0010-0x0020",
            "TP input",
            "Planned bytes",
            MemoryCoverageFillRole.Tp,
            20,
            disposition: Application.MemoryLayout.MemoryWorkflowDisposition.WillReplace,
            text: ShellTextResources.For(ShellLanguage.ChineseTraditional),
            regionGroup: ReplaceRegionGroup.SlaveLeft);
        MemoryCoverageSegmentViewModel unchanged = new(
            "0x0010-0x0020",
            "TP input",
            "Compared bytes",
            MemoryCoverageFillRole.Tp,
            20,
            disposition: Application.MemoryLayout.MemoryWorkflowDisposition.WillReplace,
            observedChange: Application.MemoryLayout.MemoryObservedChange.Unchanged,
            text: ShellTextResources.For(ShellLanguage.ChineseTraditional));
        MemoryCoverageSegmentViewModel warning = new(
            "0x0030-0x0040",
            "CtrlRAM",
            "Warning",
            MemoryCoverageFillRole.CtrlRam,
            20,
            diagnosticSeverity: Application.MemoryLayout.MemoryDiagnosticSeverity.Warning,
            regionGroup: ReplaceRegionGroup.SlaveRight);
        MemoryCoverageSegmentViewModel reserved = new(
            "0x0020-0x0030",
            "Reserved",
            "Protected bytes",
            MemoryCoverageFillRole.Neutral,
            20,
            regionId: "customer-info");
        MemoryCoverageSegmentViewModel pendingConflict = new(
            "Pending",
            "Base BIN needs attention",
            "Fix the selected base BIN.",
            MemoryCoverageFillRole.Conflict,
            20);
        MemoryCoverageGroupViewModel group = new(
            "Common",
            [
                new MemoryCoverageLogicalItemViewModel(
                    "region:planned",
                    [planned],
                    ShellTextResources.For(ShellLanguage.English)),
                new MemoryCoverageLogicalItemViewModel(
                    "region:customer-info",
                    [reserved],
                    ShellTextResources.For(ShellLanguage.English)),
            ],
            isExpanded: false,
            regionGroup: ReplaceRegionGroup.Common,
            text: ShellTextResources.For(ShellLanguage.English));
        MemoryCoverageGroupViewModel[] projectedGroups =
        [
            .. ReplaceRegionGroupBuilder.CreateCoverageGroups(
                [kept, planned, warning],
                ShellTextResources.For(ShellLanguage.English)),
        ];

        Assert.False(kept.IsChanged);
        Assert.True(kept.UsesKeptPattern);
        Assert.Equal("Kept", kept.ChangeLabel);
        Assert.True(kept.HasChangeState);
        Assert.Equal(MemoryCoverageFillRole.Kept, kept.FillRole);
        Assert.True(changed.IsChanged);
        Assert.False(changed.UsesKeptPattern);
        Assert.Equal("Changed", changed.ChangeLabel);
        Assert.True(changed.HasChangeState);
        Assert.Contains("Changed", changed.AccessibleDetail, StringComparison.Ordinal);
        Assert.Equal(MemoryCoverageFillRole.Tp, changed.FillRole);
        Assert.False(planned.IsChanged);
        Assert.Equal("將替換", planned.ChangeLabel);
        Assert.True(planned.HasChangeState);
        Assert.False(unchanged.IsChanged);
        Assert.Equal("未變更", unchanged.ChangeLabel);
        Assert.True(unchanged.HasChangeState);
        Assert.False(reserved.UsesKeptPattern);
        Assert.False(reserved.HasChangeState);
        Assert.False(pendingConflict.HasChangeState);
        Assert.Equal(1, group.SelectedCount);
        Assert.Equal("1/2", group.CountLabel);
        Assert.Equal("1 selected / 2 areas.", group.SelectionSummary);
        Assert.False(projectedGroups.Single(candidate =>
            candidate.RegionGroup == ReplaceRegionGroup.Base).IsExpanded);
        Assert.True(projectedGroups.Single(candidate =>
            candidate.RegionGroup == ReplaceRegionGroup.SlaveLeft).IsExpanded);
        Assert.True(projectedGroups.Single(candidate =>
            candidate.RegionGroup == ReplaceRegionGroup.SlaveRight).IsExpanded);
    }

    private static string ExtractDataTemplate(string xaml, string key)
    {
        return ExtractXamlBlock(xaml, $"<DataTemplate x:Key=\"{key}\"", "</DataTemplate>");
    }

    private static string ExtractStyle(string xaml, string selector)
    {
        const string marker = "<Style Selector=\"";
        int searchStart = 0;
        while ((searchStart = xaml.IndexOf(marker, searchStart, StringComparison.Ordinal)) >= 0)
        {
            int valueStart = searchStart + marker.Length;
            int valueEnd = xaml.IndexOf('"', valueStart);
            string value = xaml[valueStart..valueEnd];
            if (value.Split(',', StringSplitOptions.TrimEntries).Contains(selector, StringComparer.Ordinal))
            {
                int end = xaml.IndexOf("</Style>", valueEnd, StringComparison.Ordinal);
                return xaml[searchStart..(end + "</Style>".Length)];
            }

            searchStart = valueEnd + 1;
        }

        throw new Xunit.Sdk.XunitException($"Missing XAML style selector: {selector}");
    }

    private static string ExtractXamlBlock(string xaml, string opening, string closing)
    {
        int start = xaml.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing XAML block: {opening}");
        int end = xaml.IndexOf(closing, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Unclosed XAML block: {opening}");
        return xaml[start..(end + closing.Length)];
    }

    private static void AssertIconStyle(
        string styles,
        string iconClass,
        string backgroundToken,
        string borderToken,
        string foregroundToken)
    {
        string borderStyle = ExtractStyle(styles, $"Border.slotTypeIcon.{iconClass}");
        string pathStyle = ExtractStyle(styles, $"Border.slotTypeIcon.{iconClass} Path");
        Assert.Contains(backgroundToken, borderStyle, StringComparison.Ordinal);
        Assert.Contains(borderToken, borderStyle, StringComparison.Ordinal);
        Assert.Contains(foregroundToken, pathStyle, StringComparison.Ordinal);
    }

    private static void AssertCoverageClasses(string template)
    {
        Assert.Contains("Classes=\"memoryCoverageFill memoryCoverageMarker", template, StringComparison.Ordinal);
        Assert.Contains("Classes.changed=\"{Binding IsChanged}\"", template, StringComparison.Ordinal);
        Assert.DoesNotContain("OutlineBrush", template, StringComparison.Ordinal);
        Assert.DoesNotContain("OutlineThickness", template, StringComparison.Ordinal);
    }
}
