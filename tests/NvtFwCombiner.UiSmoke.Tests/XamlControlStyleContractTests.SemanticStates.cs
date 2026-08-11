using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

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
        string tokens = ReadPresentationFile("Styles/ThemeTokens.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml") +
            ReadPresentationFile("Styles/FirmwareSlotExperienceStyles.axaml");
        string factTemplate = ExtractDataTemplate(
            ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml"),
            "FirmwareSlotInformationFactTemplate");
        string viewModel = ReadPresentationFile("ViewModels/FirmwareSlotFactViewModel.cs");
        string normalStyle = ExtractStyle(styles, "Border.firmwareSlotFact");
        string pendingStyle = ExtractStyle(styles, "Border.firmwareSlotFact.pendingInput");
        string unknownStyle = ExtractStyle(styles, "Border.firmwareSlotFact.unknown");
        string warningStyle = ExtractStyle(styles, "Border.firmwareSlotFact.warning");
        string errorStyle = ExtractStyle(styles, "Border.firmwareSlotFact.error");
        string warningTextStyle = ExtractStyle(styles, "Border.firmwareSlotFact.warning TextBlock");

        Assert.Contains("x:Key=\"NfcSlotFactSurfaceBrush\" Color=\"#EEF6FF\"", tokens, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcSlotFactSurfaceBrush}\"", normalStyle, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcAccentBorderLightBrush}\"", normalStyle, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcWarningSurfaceBrush}\"", warningStyle, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcWarningBorderStrongBrush}\"", warningStyle, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceBrush", pendingStyle, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceSubtleBrush", unknownStyle, StringComparison.Ordinal);
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

    /// <summary>Keeps Replace coverage chrome in shared styles while preserving data-bound fill and plain Merge rendering.</summary>
    [Fact]
    public void MemoryCoverageUsesSharedChangedAndKeptVisualStates()
    {
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string templates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string tooltip = ExtractDataTemplate(templates, "MemoryCoverageTooltipTemplate");
        string replaceBar = ExtractDataTemplate(templates, "MemoryCoverageSegmentBarTemplate");
        string replaceList = ExtractDataTemplate(templates, "MemoryCoverageSegmentListTemplate");
        string mergeBar = ExtractDataTemplate(templates, "MemoryCoveragePlainSegmentBarTemplate");
        string mergeList = ExtractDataTemplate(templates, "MemoryCoveragePlainSegmentListTemplate");
        string replacePanel = ExtractDataTemplate(templates, "ReplaceOutputLayoutPanelTemplate");
        string mergePanel = ExtractDataTemplate(templates, "MergeOutputLayoutPanelTemplate");
        string viewModel = ReadPresentationFile("ViewModels/MemoryCoverageSegmentViewModel.cs");
        string keptBadge = ExtractStyle(styles, "Label.countBadge.coverageChangeBadge");
        string changedBadge = ExtractStyle(styles, "Label.countBadge.coverageChangeBadge.changed");
        string keptMarker = ExtractStyle(styles, "Border.memoryCoverageMarker");
        string changedMarker = ExtractStyle(styles, "Border.memoryCoverageMarker.changed");

        Assert.Contains("NfcSurfaceSubtleBrush", keptBadge, StringComparison.Ordinal);
        Assert.Contains("NfcBorderBrush", keptBadge, StringComparison.Ordinal);
        Assert.Contains("NfcTextSecondaryBrush", keptBadge, StringComparison.Ordinal);
        Assert.Contains("NfcInfoSurfaceStrongBrush", changedBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderLightBrush", changedBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", changedBadge, StringComparison.Ordinal);
        Assert.Contains("Value=\"0\"", keptMarker, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", changedMarker, StringComparison.Ordinal);
        Assert.Contains("Value=\"1\"", changedMarker, StringComparison.Ordinal);

        AssertCoverageClasses(tooltip);
        AssertCoverageClasses(replaceBar);
        AssertCoverageClasses(replaceList);
        Assert.Contains("Classes=\"compactBadge neutralBadge countBadge coverageChangeBadge\"", tooltip, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding ChangeLabel}\"", tooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("memoryCoverageMarker", mergeBar, StringComparison.Ordinal);
        Assert.DoesNotContain("memoryCoverageMarker", mergeList, StringComparison.Ordinal);
        Assert.Contains("Background=\"{Binding FillBrush}\"", replaceBar, StringComparison.Ordinal);
        Assert.Contains("Background=\"{Binding FillBrush}\"", mergeBar, StringComparison.Ordinal);
        Assert.Contains("Focusable=\"True\"", replaceBar, StringComparison.Ordinal);
        Assert.Contains("FocusToolTipBehavior.IsEnabled=\"True\"", replaceBar, StringComparison.Ordinal);
        Assert.Contains("Focusable=\"True\"", mergeBar, StringComparison.Ordinal);
        Assert.Contains("FocusToolTipBehavior.IsEnabled=\"True\"", mergeBar, StringComparison.Ordinal);
        Assert.Contains("<Viewbox Height=\"22\" HorizontalAlignment=\"Stretch\" Stretch=\"Fill\">", replacePanel, StringComparison.Ordinal);
        Assert.Contains("<ItemsControl Width=\"300\" Height=\"22\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("<Viewbox Height=\"22\" HorizontalAlignment=\"Stretch\" Stretch=\"Fill\">", mergePanel, StringComparison.Ordinal);
        Assert.Contains("<ItemsControl Width=\"300\" Height=\"22\"", mergePanel, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"KeptDiagonalStripeBrush\"", templates, StringComparison.Ordinal);
        Assert.Contains("DestinationRect=\"0,0,10,10\"", templates, StringComparison.Ordinal);
        Assert.Contains("Geometry=\"M -2,10 L 10,-2\"", templates, StringComparison.Ordinal);
        Assert.Contains("Brush=\"{DynamicResource NfcKeptStripeBrush}\"", templates, StringComparison.Ordinal);
        Assert.Contains("TileMode=\"Tile\"", templates, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding UsesKeptPattern}\"", tooltip, StringComparison.Ordinal);
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
        Assert.Contains("Text=\"{Binding Text.OutputLayoutChangedStateLabel}\"", replacePanel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text.OutputLayoutKeptStateLabel}\"", replacePanel, StringComparison.Ordinal);

        Assert.Contains("public IBrush FillBrush", viewModel, StringComparison.Ordinal);
        Assert.Contains("public bool IsChanged", viewModel, StringComparison.Ordinal);
        Assert.Contains("public bool UsesBaseFirmwarePattern", viewModel, StringComparison.Ordinal);
        Assert.Contains("public bool UsesKeptPattern", viewModel, StringComparison.Ordinal);
        Assert.Contains("public string ChangeLabel", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeBadgeBackgroundBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeBadgeBorderBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeBadgeForegroundBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("OutlineBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("OutlineThickness", viewModel, StringComparison.Ordinal);
    }

    /// <summary>Keeps changed/kept meaning and data-bound fill available after presentation brushes move to XAML.</summary>
    [Fact]
    public void MemoryCoverageRetainsStateAndFillData()
    {
        MemoryCoverageSegmentViewModel kept = new("0x0000-0x0010", "Base flash", "Kept bytes", "#334155", 20, usesBaseFirmwarePattern: true);
        MemoryCoverageSegmentViewModel changed = new("0x0010-0x0020", "TP input", "Written bytes", "#2563EB", 20, isChanged: true);

        Assert.False(kept.IsChanged);
        Assert.True(kept.UsesBaseFirmwarePattern);
        Assert.True(kept.UsesKeptPattern);
        Assert.Equal("Kept", kept.ChangeLabel);
        Assert.Equal("#334155", kept.Fill);
        Assert.NotNull(kept.FillBrush);
        Assert.True(changed.IsChanged);
        Assert.False(changed.UsesBaseFirmwarePattern);
        Assert.False(changed.UsesKeptPattern);
        Assert.Equal("Changed", changed.ChangeLabel);
        Assert.Equal("#2563EB", changed.Fill);
        Assert.NotNull(changed.FillBrush);
    }

    /// <summary>Masked and full DiffDLM routes expose localized equivalent disclosure semantics.</summary>
    [Fact]
    public void DiffDlmCoverageFormatsPreservationAndFullArtifactStates()
    {
        var detail = new Application.MemoryLayout.MemoryLayoutPreservationDetail(
            "diff-nf-0",
            blockIndex: 0,
            Application.MemoryLayout.MemoryEndpointIdentity.NotApplicable,
            "postbuild-diffdlm",
            new ByteRange(0xB90, 0x870),
            new ByteRange(0x2DC90, 0x870));
        var masked = new MemoryCoverageSegmentViewModel(
            "0x2D100-0x2E4FF",
            "DiffDLM",
            "Canonical DiffDLM",
            "#D97706",
            20,
            isDiffDlm: true,
            preservationDetails: [detail],
            text: ShellTextResources.For(ShellLanguage.English));
        var localized = new MemoryCoverageSegmentViewModel(
            "0x2D100-0x2E4FF",
            "DiffDLM",
            "Canonical DiffDLM",
            "#D97706",
            20,
            isDiffDlm: true,
            preservationDetails: [detail],
            text: ShellTextResources.For(ShellLanguage.ChineseTraditional));
        var full = new MemoryCoverageSegmentViewModel(
            "0x27800-0x29FFF",
            "DiffDLM",
            "Canonical DiffDLM",
            "#D97706",
            20,
            isDiffDlm: true);

        Assert.Equal("Kept 1 active Diff NF segments", masked.PreservationSummary);
        Assert.Contains("Block 0", masked.AccessibleDetail, StringComparison.Ordinal);
        Assert.True(masked.HasPreservationDetails);
        Assert.Contains("保留 1 個有效 Diff NF 區段", localized.PreservationSummary, StringComparison.Ordinal);
        Assert.Equal("Entire DiffDLM", full.PreservationSummary);
        Assert.False(full.HasPreservationDetails);
    }

    /// <summary>Canonical DP Replace dispositions reach kept and changed coverage patterns.</summary>
    [Fact]
    public void MemoryCoveragePatternUsesTypedWorkbenchRole()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-memory-pattern");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51951";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.DpReplace;
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x80000]));
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceDp,
            workspace.Write("replacement.bin", new byte[0x80000]));

        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.UsesBaseFirmwarePattern && segment.UsesKeptPattern);
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            !segment.UsesBaseFirmwarePattern && !segment.UsesKeptPattern && segment.IsChanged);
    }

    private static string ExtractDataTemplate(string xaml, string key)
    {
        return ExtractXamlBlock(xaml, $"<DataTemplate x:Key=\"{key}\"", "</DataTemplate>");
    }

    private static string ExtractStyle(string xaml, string selector)
    {
        return ExtractXamlBlock(xaml, $"<Style Selector=\"{selector}\">", "</Style>");
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
        Assert.Contains("Classes=\"memoryCoverageMarker\"", template, StringComparison.Ordinal);
        Assert.Contains("Classes.changed=\"{Binding IsChanged}\"", template, StringComparison.Ordinal);
        Assert.DoesNotContain("OutlineBrush", template, StringComparison.Ordinal);
        Assert.DoesNotContain("OutlineThickness", template, StringComparison.Ordinal);
    }
}
