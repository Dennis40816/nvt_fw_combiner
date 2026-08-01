using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps firmware fact content in the ViewModel while shared styles own its exact normal and warning presentation.</summary>
    [Fact]
    public void FirmwareSlotFactsUseSharedNormalAndWarningVisualStates()
    {
        string tokens = ReadPresentationFile("Styles/ThemeTokens.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string factTemplate = ExtractDataTemplate(
            ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml"),
            "FirmwareSlotFactTemplate");
        string viewModel = ReadPresentationFile("ViewModels/FirmwareSlotFactViewModel.cs");
        string normalStyle = ExtractStyle(styles, "Border.firmwareSlotFact");
        string warningStyle = ExtractStyle(styles, "Border.firmwareSlotFact.warning");
        string warningTextStyle = ExtractStyle(styles, "Border.firmwareSlotFact.warning TextBlock");

        Assert.Contains("x:Key=\"NfcSlotFactSurfaceBrush\" Color=\"#EEF6FF\"", tokens, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcSlotFactSurfaceBrush}\"", normalStyle, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcAccentBorderLightBrush}\"", normalStyle, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcWarningSurfaceBrush}\"", warningStyle, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcWarningBorderStrongBrush}\"", warningStyle, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.firmwareSlotFactLabel\"", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.firmwareSlotFactValue\"", styles, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource NfcWarningTextBrush}\"", warningTextStyle, StringComparison.Ordinal);

        Assert.Contains("Classes=\"firmwareSlotFact\"", factTemplate, StringComparison.Ordinal);
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
        Assert.Contains("string Label, string Value, bool IsWarning", viewModel, StringComparison.Ordinal);
    }

    /// <summary>Keeps missing, selected, and optional slot completion states distinct while localized text remains the non-color cue.</summary>
    [Fact]
    public void FirmwareSlotCompletionUsesOrderedSharedVisualStates()
    {
        string tokens = ReadPresentationFile("Styles/ThemeTokens.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");
        string viewModel = ReadPresentationFile("ViewModels/FirmwareSlotViewModel.cs");
        string missingSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot");
        string optionalSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.optional");
        string selectedSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.hasFile");
        string pendingSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.hasFile.pending");
        string validSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.hasFile.valid");
        string warningSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.hasFile.warning");
        string blockingSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.hasFile.blocking");
        string missingBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement");
        string optionalBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement.optional");
        string selectedBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement.hasFile");

        Assert.Contains("x:Key=\"NfcRequiredMissingBorderBrush\" Color=\"#FCA5A5\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcRequiredMissingBadgeSurfaceBrush\" Color=\"#FEE2E2\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcRequiredSelectedBorderBrush\" Color=\"#86EFAC\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcRequiredSelectedBadgeSurfaceBrush\" Color=\"#DCFCE7\"", tokens, StringComparison.Ordinal);
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
            "A selected optional slot must override the empty optional state with the common success surface.");
        Assert.Contains("NfcSurfaceBrush", pendingSlot, StringComparison.Ordinal);
        Assert.Contains("NfcBorderBrush", pendingSlot, StringComparison.Ordinal);
        Assert.Contains("NfcSuccessSurfaceBrush", validSlot, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredSelectedBorderBrush", validSlot, StringComparison.Ordinal);
        Assert.Contains("NfcWarningSurfaceBrush", warningSlot, StringComparison.Ordinal);
        Assert.Contains("NfcWarningBorderStrongBrush", warningSlot, StringComparison.Ordinal);
        Assert.Contains("NfcCriticalSurfaceBrush", blockingSlot, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredMissingBorderBrush", blockingSlot, StringComparison.Ordinal);
        Assert.True(
            styles.IndexOf(pendingSlot, StringComparison.Ordinal) > styles.IndexOf(selectedSlot, StringComparison.Ordinal) &&
            styles.IndexOf(validSlot, StringComparison.Ordinal) > styles.IndexOf(pendingSlot, StringComparison.Ordinal) &&
            styles.IndexOf(warningSlot, StringComparison.Ordinal) > styles.IndexOf(validSlot, StringComparison.Ordinal) &&
            styles.IndexOf(blockingSlot, StringComparison.Ordinal) > styles.IndexOf(warningSlot, StringComparison.Ordinal),
            "Pending, valid, warning, and blocking health must override generic selected styling in severity order.");

        Assert.Contains("NfcRequiredMissingBadgeSurfaceBrush", missingBadge, StringComparison.Ordinal);
        Assert.Contains("NfcDangerTextBrush", missingBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceBrush", optionalBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", optionalBadge, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredSelectedBadgeSurfaceBrush", selectedBadge, StringComparison.Ordinal);
        Assert.Contains("NfcSuccessEmphasisBrush", selectedBadge, StringComparison.Ordinal);
        Assert.True(
            styles.IndexOf(selectedBadge, StringComparison.Ordinal) > styles.IndexOf(optionalBadge, StringComparison.Ordinal),
            "A selected optional badge must override the empty optional state with the common success state.");

        Assert.Contains("Classes=\"fileDropZone firmwareSlot\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.hasFile=\"{Binding HasFile}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.blocking=\"{Binding IsInputInspectionBlocking}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.pending=\"{Binding IsInputInspectionPending}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.valid=\"{Binding IsInputInspectionValid}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.warning=\"{Binding IsInputInspectionWarning}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Stretch=\"Uniform\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding InputInspectionStatus}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.optional=\"{Binding IsOptional}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compactBadge slotBadge firmwareSlotRequirement\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding RequirementLabel}\"", slotCard, StringComparison.Ordinal);
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
        Assert.Contains(nameof(FirmwareSlotViewModel.HasFile), notifications);

        notifications.Clear();
        slot.SetInputInspectionPending("Inspecting");
        Assert.True(slot.IsInputInspectionPending);
        Assert.False(slot.IsInputInspectionValid);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionPending), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionValid), notifications);

        notifications.Clear();
        slot.SetInputInspection(WorkbenchInputInspectionSeverity.Valid, "Ready");
        Assert.False(slot.IsInputInspectionPending);
        Assert.True(slot.IsInputInspectionValid);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionPending), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionValid), notifications);

        notifications.Clear();
        slot.SetInputInspection(WorkbenchInputInspectionSeverity.Warning, "Review warning");
        Assert.False(slot.IsInputInspectionValid);
        Assert.True(slot.IsInputInspectionWarning);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionValid), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionWarning), notifications);

        notifications.Clear();
        slot.SetInputInspection(WorkbenchInputInspectionSeverity.Blocking, "Fix input");
        Assert.False(slot.IsInputInspectionWarning);
        Assert.True(slot.IsInputInspectionBlocking);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionWarning), notifications);
        Assert.Contains(nameof(FirmwareSlotViewModel.IsInputInspectionBlocking), notifications);
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

    /// <summary>Bootstrap's typed coverage role reaches Replace hatching while Merge stays plain.</summary>
    [Fact]
    public void MemoryCoveragePatternUsesTypedWorkbenchRole()
    {
        (_, _, IReadOnlyList<MemoryCoverageSegmentViewModel> replaceCoverage) = UiCompositionRunner.GetReplaceMemoryDisplay(
            "NT51951",
            "single",
            WorkbenchReplaceModes.Dp,
            dpBaseLength: 0x80000);
        (_, _, IReadOnlyList<MemoryCoverageSegmentViewModel> standardMergeCoverage) =
            UiCompositionRunner.GetStandardMergeMemoryDisplay("NT51926");
        (_, _, IReadOnlyList<MemoryCoverageSegmentViewModel> customizedMergeCoverage) =
            UiCompositionRunner.GetGeneralMergeMemoryDisplay(
                "NT51950",
                "0x100",
                []);

        Assert.Contains(replaceCoverage, segment => segment.UsesBaseFirmwarePattern);
        Assert.Contains(replaceCoverage, segment => !segment.UsesBaseFirmwarePattern);
        Assert.All(standardMergeCoverage, segment => Assert.False(segment.UsesBaseFirmwarePattern));
        Assert.All(customizedMergeCoverage, segment => Assert.False(segment.UsesBaseFirmwarePattern));
        Assert.All(standardMergeCoverage, segment => Assert.False(segment.UsesKeptPattern));
        Assert.All(customizedMergeCoverage, segment => Assert.False(segment.UsesKeptPattern));
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
