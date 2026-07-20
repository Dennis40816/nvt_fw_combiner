namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps Build actions compact and responsive without hover popups obscuring the first click.</summary>
    [Fact]
    public void BuildActionsUseCompactAnimatedToolbarStyleWithoutTooltips()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string replaceSelection = ReadPresentationFile("Views/ReplaceSelectionModal.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string toolbarAction = ExtractStyle(styles, "Button.toolbarAction");
        string toolbarPresenter = ExtractStyle(
            styles,
            "Button.toolbarAction /template/ ContentPresenter#PART_ContentPresenter");

        Assert.Contains("MinHeight\" Value=\"34\"", toolbarAction, StringComparison.Ordinal);
        Assert.Contains("MinWidth\" Value=\"80\"", toolbarAction, StringComparison.Ordinal);
        Assert.Contains("Padding\" Value=\"10,6\"", toolbarAction, StringComparison.Ordinal);
        Assert.Contains("BrushTransition Property=\"Background\" Duration=\"0:0:0.12\"", toolbarPresenter, StringComparison.Ordinal);
        Assert.Contains("BrushTransition Property=\"BorderBrush\" Duration=\"0:0:0.12\"", toolbarPresenter, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.toolbarAction:pointerover", styles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.toolbarAction:pressed", styles, StringComparison.Ordinal);
        Assert.Equal(2, shell.Split("Classes=\"toolbarAction\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("BuildActionTip", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildActionTip", replaceSelection, StringComparison.Ordinal);
    }
}
