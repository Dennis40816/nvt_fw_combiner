using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps every editable IC Number surface on profile-declared choices instead of free text.</summary>
    [Fact]
    public void EditableIcNumberSurfacesUseChoiceSelectors()
    {
        var contextPanel = XDocument.Parse(ReadPresentationFile("Resources/MainWindowShellPanels.axaml"));
        var setupModal = XDocument.Parse(ReadPresentationFile("Views/WorkflowContextSetupModal.axaml"));

        XElement contextSelector = Assert.Single(
            contextPanel.Descendants(),
            static element =>
                element.Name.LocalName == "ComboBox" &&
                element.Attribute("ItemsSource")?.Value == "{Binding NumberSelectionChoices}");
        Assert.Equal(
            "{Binding SelectedNumberChoice, Mode=TwoWay}",
            contextSelector.Attribute("SelectedItem")?.Value);
        Assert.Equal(
            "{Binding IsDeviceContextSelectionVisible}",
            contextSelector.Attribute("IsVisible")?.Value);

        XElement activeRunNumber = Assert.Single(
            contextPanel.Descendants(),
            static element =>
                element.Name.LocalName == "TextBox" &&
                element.Attribute("Text")?.Value == "{Binding RunSession.ActiveRunNumber}");
        Assert.Equal("True", activeRunNumber.Attribute("IsReadOnly")?.Value);
        Assert.Equal("{Binding RunSession.IsRunInProgress}", activeRunNumber.Attribute("IsVisible")?.Value);
        XElement numberPresenter = Assert.IsType<XElement>(activeRunNumber.Parent);
        Assert.Same(numberPresenter, contextSelector.Parent);
        Assert.Equal("Grid", numberPresenter.Name.LocalName);
        Assert.Equal(
            "{Binding IsNumberSelectorVisible}",
            numberPresenter.Attribute("IsVisible")?.Value);

        XElement setupSelector = Assert.Single(
            setupModal.Descendants(),
            static element =>
                element.Name.LocalName == "ComboBox" &&
                element.Attribute("ItemsSource")?.Value == "{Binding WorkflowContextSetup.NumberChoices}");
        Assert.Equal(
            "{Binding WorkflowContextSetup.SelectedNumberChoice, Mode=TwoWay}",
            setupSelector.Attribute("SelectedItem")?.Value);

        Assert.DoesNotContain(
            contextPanel.Descendants(),
            static element =>
                element.Name.LocalName == "TextBox" &&
                element.Attributes().Any(attribute =>
                    attribute.Value.Contains("SelectedNumber", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            setupModal.Descendants(),
            static element =>
                element.Name.LocalName == "TextBox" &&
                element.Attributes().Any(attribute =>
                    attribute.Value.Contains("SelectedNumber", StringComparison.Ordinal)));
    }
}
