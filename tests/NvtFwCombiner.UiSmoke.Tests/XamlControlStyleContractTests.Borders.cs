using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Full-perimeter outlines use the owner-approved two-pixel treatment without thickening dividers.</summary>
    [Fact]
    public void FullPerimeterThinOutlinesUseTwoPixels()
    {
        XElement[] onePixelOutlines =
        [
            .. ReadPresentationXamlFiles()
                .Select(XDocument.Parse)
                .SelectMany(static document => document.Descendants())
                .Where(static element =>
                    (string?)element.Attribute("BorderThickness") == "1" ||
                    (element.Name.LocalName == "Setter" &&
                     (string?)element.Attribute("Property") == "BorderThickness" &&
                     (string?)element.Attribute("Value") == "1")),
        ];

        Assert.Empty(onePixelOutlines);

        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        XElement semanticActionStyle = Assert.Single(
            XDocument.Parse(buttonStyles).Descendants(),
            static element =>
                element.Name.LocalName == "Style" &&
                (string?)element.Attribute("Selector") == "Button.semanticAction, RadioButton.settingsNavItem");
        Assert.Contains(
            semanticActionStyle.Elements(),
            static element =>
                (string?)element.Attribute("Property") == "BorderThickness" &&
                (string?)element.Attribute("Value") == "2");
        Assert.Contains("BorderThickness=\"0,1,0,0\"", string.Concat(ReadPresentationXamlFiles()), StringComparison.Ordinal);
    }

    /// <summary>Every shared dropdown keeps a stable two-pixel outline before and during keyboard focus.</summary>
    [AvaloniaFact]
    public void ComboBoxRestingAndKeyboardFocusUseTwoPixelOutline()
    {
        var comboBox = new ComboBox
        {
            Width = 180,
            ItemsSource = new[] { "Standard", "AB Code" },
            SelectedIndex = 0,
        };
        var host = new Window
        {
            Width = 240,
            Height = 100,
            Content = comboBox,
        };
        host.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });

        host.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AssertComboBoxAndTemplateUseTwoPixelOutline(comboBox);

            _ = comboBox.Focus(NavigationMethod.Tab, KeyModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(comboBox.IsKeyboardFocusWithin);
            AssertComboBoxAndTemplateUseTwoPixelOutline(comboBox);
        }
        finally
        {
            host.Close();
        }
    }

    private static void AssertComboBoxAndTemplateUseTwoPixelOutline(ComboBox comboBox)
    {
        Assert.Equal(new Thickness(2), comboBox.BorderThickness);
        Assert.Contains(
            comboBox.GetVisualDescendants().OfType<Border>(),
            border => border.Bounds is { Width: > 0, Height: > 0 } &&
                border.BorderThickness == new Thickness(2));
    }
}
