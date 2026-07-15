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
}
