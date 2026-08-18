using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Ordinary semantic actions are rounded rectangles while intentional chips keep their pill geometry.</summary>
    [Fact]
    public void SemanticButtonsUseCompactCornersAndKeepExplicitPillExceptions()
    {
        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        var document = XDocument.Parse(styles);
        XElement theme = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "ControlTheme" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" &&
                attribute.Value == "NfcSemanticButtonTheme"));
        XElement presenter = Assert.Single(theme.Descendants(), element =>
            element.Name.LocalName == "ContentPresenter" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" &&
                attribute.Value == "PART_ContentPresenter"));

        Assert.Equal(
            "{DynamicResource NfcCompactCornerRadius}",
            (string?)presenter.Attribute("CornerRadius"));
        Assert.Contains(
            "CornerRadius\" Value=\"{DynamicResource NfcPillCornerRadius}",
            ExtractStyle(styles, "Button.railAction /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);
        Assert.Contains(
            "CornerRadius\" Value=\"{DynamicResource NfcPillCornerRadius}",
            ExtractStyle(styles, "Button.summaryChip /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);
        string controlStyles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        Assert.Contains(
            "CornerRadius\" Value=\"{DynamicResource NfcPillCornerRadius}",
            ExtractStyle(
                controlStyles,
                "Button.hexChangedBlockNavigator /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);
        Assert.Contains(
            "CornerRadius\" Value=\"0",
            ExtractStyle(
                controlStyles,
                "Button.hexChangedBlockRow /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);
    }

    /// <summary>Every production button declares a project-owned visual role instead of silently using a raw default.</summary>
    [AvaloniaFact]
    public void EveryButtonDeclaresAnOwnedVisualRole()
    {
        var mainWindow = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        XElement windowStyles = Assert.Single(mainWindow.Descendants(), element =>
            element.Name.LocalName == "Window.Styles");
        XElement buttonStyleInclude = Assert.Single(windowStyles.Elements(), element =>
            element.Name.LocalName == "StyleInclude" &&
            (string?)element.Attribute("Source") ==
            "avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowButtonStyles.axaml");
        Assert.Equal(
            "avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowButtonStyles.axaml",
            (string?)buttonStyleInclude.Attribute("Source"));

        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        AssertGlobalButtonThemeAndFocusContract(styles);
        string[] ownedRoles =
        [
            "semanticAction",
            "command",
            "fileRevealAction",
            "railAction",
            "breadcrumb",
            "summaryChip",
            "hexChangedBlockNavigator",
            "hexChangedBlockRow",
        ];

        foreach (XElement button in ReadPresentationXamlFiles()
                     .Select(XDocument.Parse)
                     .SelectMany(document => document.Descendants())
                     .Where(element => element.Name.LocalName == "Button"))
        {
            Assert.Null(button.Attribute("Theme"));
            Assert.Null(button.Attribute("FocusAdorner"));
            string[] classes = ((string?)button.Attribute("Classes") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(
                classes.Any(ownedRoles.Contains),
                $"Every Button must declare an owned visual role: {button}");
        }

        XElement[] settingsNavigationItems =
        [
            .. ReadPresentationXamlFiles()
                .Select(XDocument.Parse)
                .SelectMany(document => document.Descendants())
                .Where(element =>
                    element.Name.LocalName == "RadioButton" &&
                    ((string?)element.Attribute("Classes"))?.Split(' ').Contains("settingsNavItem") == true),
        ];
        Assert.Equal(3, settingsNavigationItems.Length);
        Assert.All(settingsNavigationItems, static item =>
        {
            Assert.Null(item.Attribute("Theme"));
            Assert.Null(item.Attribute("FocusAdorner"));
        });

        string radioOwner = ExtractStyle(styles, "RadioButton.settingsNavItem");
        Assert.Contains("Theme\" Value=\"{StaticResource NfcSemanticButtonTheme}", radioOwner, StringComparison.Ordinal);
        Assert.Contains("FocusAdorner\" Value=\"{x:Null}", radioOwner, StringComparison.Ordinal);
        string selectedNavigationStyle = ExtractStyle(
            styles,
            "RadioButton.settingsNavItem.selected /template/ ContentPresenter#PART_ContentPresenter");
        Assert.Contains(
            "NfcAccentSurfaceSubtleBrush",
            selectedNavigationStyle,
            StringComparison.Ordinal);
        Assert.Contains(
            "NfcAccentBorderLightBrush",
            selectedNavigationStyle,
            StringComparison.Ordinal);
        Assert.Contains(
            "BorderThickness\" Value=\"2",
            ExtractStyle(
                styles,
                "RadioButton.settingsNavItem:focus-visible /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);

        var navigationItem = new RadioButton
        {
            Content = "General",
            IsChecked = true,
        };
        navigationItem.Classes.Add("semanticAction");
        navigationItem.Classes.Add("command");
        navigationItem.Classes.Add("settingsNavItem");
        navigationItem.Classes.Add("selected");
        var styleHost = new Window
        {
            RequestedThemeVariant = ThemeVariant.Light,
        };
        styleHost.Styles.Add(new StyleInclude(ProductionButtonStylesUri)
        {
            Source = ProductionButtonStylesUri,
        });
        styleHost.Content = navigationItem;
        styleHost.Measure(new Size(188, 100));
        styleHost.Arrange(new Rect(0, 0, 188, 100));

        Assert.NotNull(navigationItem.Theme);
        Assert.Null(navigationItem.FocusAdorner);
        Assert.Equal(42, navigationItem.MinHeight);
        Assert.Equal(new Thickness(12, 9), navigationItem.Padding);
        Assert.Equal(HorizontalAlignment.Stretch, navigationItem.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Stretch, navigationItem.HorizontalContentAlignment);
        ISelectionItemProvider selectionItem = Assert.IsType<ISelectionItemProvider>(
            ControlAutomationPeer.CreatePeerForElement(navigationItem),
            exactMatch: false);
        Assert.True(selectionItem.IsSelected);
        ContentPresenter presenter = Assert.Single(
            navigationItem.GetVisualDescendants().OfType<ContentPresenter>(),
            static candidate => candidate.Name == "PART_ContentPresenter");
        Assert.Equal(new CornerRadius(6), presenter.CornerRadius);
        Assert.True(Avalonia.Application.Current!.TryGetResource(
            "NfcAccentSurfaceSubtleBrush",
            ThemeVariant.Light,
            out object? selectedSurface));
        Assert.Equal(
            Assert.IsType<SolidColorBrush>(selectedSurface).Color,
            Assert.IsType<ISolidColorBrush>(presenter.Background, exactMatch: false).Color);
        Assert.True(Avalonia.Application.Current.TryGetResource(
            "NfcAccentBorderLightBrush",
            ThemeVariant.Light,
            out object? selectedBorder));
        Assert.Equal(
            Assert.IsType<SolidColorBrush>(selectedBorder).Color,
            Assert.IsType<ISolidColorBrush>(presenter.BorderBrush, exactMatch: false).Color);
    }

    /// <summary>Report-history rows use the shared command theme rather than the Fluent fallback template.</summary>
    [Fact]
    public void ReportHistoryEntryUsesOwnedSemanticCommandStyle()
    {
        var document = XDocument.Parse(
            ReadPresentationFile("Resources/MainWindowReportHistoryTemplates.axaml"));
        XElement entry = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Command") ==
            "{ReflectionBinding $parent[Window].DataContext.OpenReportHistoryEntryAsyncCommand}");
        string[] classes = ((string?)entry.Attribute("Classes") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("semanticAction", classes);
        Assert.Contains("command", classes);
    }

    /// <summary>Pointer activation has no persistent focus frame while keyboard focus remains explicitly visible.</summary>
    [Fact]
    public void SemanticButtonFocusUsesFocusVisibleWithoutAPlainFocusSelector()
    {
        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string globalButton = ExtractStyle(styles, "Button");
        string focusVisible = ExtractStyle(
            styles,
            "Button:focus-visible /template/ ContentPresenter#PART_ContentPresenter");
        string[] focusSelectors =
        [
            .. ReadPresentationXamlFiles()
                .Select(XDocument.Parse)
                .SelectMany(document => document.Descendants())
                .Where(element => element.Name.LocalName == "Style")
                .Select(element => (string?)element.Attribute("Selector"))
                .OfType<string>()
                .Where(selector =>
                    selector.Contains("Button", StringComparison.Ordinal) &&
                    selector.Contains(":focus", StringComparison.Ordinal)),
        ];

        Assert.Contains("FocusAdorner\" Value=\"{x:Null}", globalButton, StringComparison.Ordinal);
        Assert.True(focusSelectors.Length > 0);
        Assert.All(
            focusSelectors,
            selector => Assert.Contains(":focus-visible", selector, StringComparison.Ordinal));
        Assert.Contains("BorderThickness\" Value=\"2", focusVisible, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderStrongBrush", focusVisible, StringComparison.Ordinal);
    }

    private static void AssertGlobalButtonThemeAndFocusContract(string styles)
    {
        var document = XDocument.Parse(styles);
        XElement globalButtonStyle = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "Style" &&
            (string?)element.Attribute("Selector") == "Button");
        XElement themeSetter = Assert.Single(globalButtonStyle.Elements(), element =>
            element.Name.LocalName == "Setter" &&
            (string?)element.Attribute("Property") == "Theme");
        XElement focusAdornerSetter = Assert.Single(globalButtonStyle.Elements(), element =>
            element.Name.LocalName == "Setter" &&
            (string?)element.Attribute("Property") == "FocusAdorner");
        Assert.Equal(
            "Button",
            (string?)themeSetter.Parent?.Attribute("Selector"));
        Assert.Equal(
            "Button",
            (string?)focusAdornerSetter.Parent?.Attribute("Selector"));
        Assert.Equal(
            "{StaticResource NfcSemanticButtonTheme}",
            (string?)themeSetter.Attribute("Value"));
        Assert.Equal("{x:Null}", (string?)focusAdornerSetter.Attribute("Value"));
        string globalButton = ExtractStyle(styles, "Button");
        Assert.Contains(
            "Theme\" Value=\"{StaticResource NfcSemanticButtonTheme}",
            globalButton,
            StringComparison.Ordinal);
        Assert.Contains(
            "FocusAdorner\" Value=\"{x:Null}",
            globalButton,
            StringComparison.Ordinal);
    }
}
