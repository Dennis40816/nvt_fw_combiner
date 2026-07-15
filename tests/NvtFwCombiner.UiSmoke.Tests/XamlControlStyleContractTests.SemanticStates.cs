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
        string selectedSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.hasFile");
        string optionalSlot = ExtractStyle(styles, "Border.fileDropZone.firmwareSlot.optional");
        string missingBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement");
        string selectedBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement.hasFile");
        string optionalBadge = ExtractStyle(styles, "Label.slotBadge.firmwareSlotRequirement.optional");

        Assert.Contains("x:Key=\"NfcRequiredMissingBorderBrush\" Color=\"#FCA5A5\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcRequiredMissingBadgeSurfaceBrush\" Color=\"#FEE2E2\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcRequiredSelectedBorderBrush\" Color=\"#86EFAC\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcRequiredSelectedBadgeSurfaceBrush\" Color=\"#DCFCE7\"", tokens, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"NfcSuccessEmphasisBrush\" Color=\"#15803D\"", tokens, StringComparison.Ordinal);

        Assert.Contains("NfcCriticalSurfaceBrush", missingSlot, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredMissingBorderBrush", missingSlot, StringComparison.Ordinal);
        Assert.Contains("Value=\"1.5\"", missingSlot, StringComparison.Ordinal);
        Assert.Contains("NfcSuccessSurfaceBrush", selectedSlot, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredSelectedBorderBrush", selectedSlot, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceSubtleBrush", optionalSlot, StringComparison.Ordinal);
        Assert.Contains("NfcBorderBrush", optionalSlot, StringComparison.Ordinal);
        Assert.True(
            styles.IndexOf(optionalSlot, StringComparison.Ordinal) > styles.IndexOf(selectedSlot, StringComparison.Ordinal),
            "The optional slot selector must follow the selected selector so an optional selected slot stays neutral.");

        Assert.Contains("NfcRequiredMissingBadgeSurfaceBrush", missingBadge, StringComparison.Ordinal);
        Assert.Contains("NfcDangerTextBrush", missingBadge, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredSelectedBadgeSurfaceBrush", selectedBadge, StringComparison.Ordinal);
        Assert.Contains("NfcSuccessEmphasisBrush", selectedBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceBrush", optionalBadge, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", optionalBadge, StringComparison.Ordinal);
        Assert.True(
            styles.IndexOf(optionalBadge, StringComparison.Ordinal) > styles.IndexOf(selectedBadge, StringComparison.Ordinal),
            "The optional badge selector must follow the selected selector so its meaning does not drift after selection.");

        Assert.Contains("Classes=\"fileDropZone firmwareSlot\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.hasFile=\"{Binding HasFile}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.optional=\"{Binding IsOptional}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes=\"slotBadge firmwareSlotRequirement\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding RequirementLabel}\"", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotBackgroundBrush", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("RequirementBadgeForegroundBrush", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotBackgroundBrush", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("RequirementBadgeForegroundBrush", viewModel, StringComparison.Ordinal);
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
}
