namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>A committed Build exposes its path and both requested completion actions.</summary>
    [Fact]
    public void SuccessfulBuildUsesActionableOutputConfirmation()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string modal = ReadPresentationFile("Views/BuildCompletedModal.axaml");
        string modalCode = ReadPresentationFile("Views/BuildCompletedModal.axaml.cs");
        string buildCode = ReadPresentationFile("MainWindow.Build.cs");

        Assert.Contains("<views:BuildCompletedModal", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsBuildCompletedModalOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding BuildCompletedOutputDisplayName}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Click=\"RevealOutputFileButton_OnClick\"", modal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseBuildCompletedModalCommand}\"", modal, StringComparison.Ordinal);
        Assert.Contains("FileRevealLauncher.TryReveal", modalCode, StringComparison.Ordinal);
        Assert.Contains("FileRevealLauncher.TryReveal", buildCode, StringComparison.Ordinal);
    }

    /// <summary>Composition Build remains fixed at the bottom of the right-side action rail.</summary>
    [Fact]
    public void CompositionBuildUsesFixedBottomActionRail()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string shellStyles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        string railStyle = ExtractStyle(shellStyles, "Border.compositionActionRail");
        int latestOutputAction = shell.IndexOf("Click=\"OpenLatestOutputFolderButton_OnClick\"", StringComparison.Ordinal);
        int mergeBuildAction = shell.IndexOf("Click=\"BuildMergeButton_OnClick\"", StringComparison.Ordinal);
        int replaceBuildAction = shell.IndexOf("Click=\"BuildReplaceButton_OnClick\"", StringComparison.Ordinal);

        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto,*,Auto\"", shell, StringComparison.Ordinal);
        Assert.Contains("<ScrollViewer Grid.Row=\"3\">", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompositionBuildActionRail\"", shell, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"3\"", shell, StringComparison.Ordinal);
        Assert.Contains("ZIndex=\"1\"", shell, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Right\"", shell, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Bottom\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compositionActionRail\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsCompositionActionRailVisible}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsLatestOutputActionVisible}\"", shell, StringComparison.Ordinal);
        Assert.True(latestOutputAction >= 0);
        Assert.True(mergeBuildAction > latestOutputAction);
        Assert.True(replaceBuildAction > mergeBuildAction);
        Assert.Contains("Orientation=\"Vertical\"", shell, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction\"", buttonStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector=\"Button.floatingAction.build\"", buttonStyles, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.compositionActionRail\"", shellStyles, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceCornerRadius", railStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("NfcPillCornerRadius", railStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"toolbarAction\"", shell, StringComparison.Ordinal);
    }

    /// <summary>Adjacent CtrlRAM slot and coverage groups retain visible separation.</summary>
    [Fact]
    public void CtrlRamGroupsUseExplicitVerticalSpacing()
    {
        string workflows = ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml");
        string shared = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string styles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string spaciousListStyle = ExtractStyle(styles, "ItemsControl.spaciousList");

        Assert.Contains("ItemsSource=\"{Binding ReplaceSlotGroups}\"", workflows, StringComparison.Ordinal);
        Assert.Contains("Classes=\"spaciousList\"", workflows, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceCoverageGroups}\"", shared, StringComparison.Ordinal);
        Assert.Contains("Classes=\"spaciousList\"", shared, StringComparison.Ordinal);
        Assert.Contains(
            "<StackPanel Spacing=\"{DynamicResource NfcSpace8}\" />",
            spaciousListStyle,
            StringComparison.Ordinal);
    }

    /// <summary>The CtrlRAM Base firmware uses the same self-padding section inset as topology groups.</summary>
    [Fact]
    public void CtrlRamBaseAndTopologyGroupsShareSpaciousPanelWidthBoundary()
    {
        var workflows = System.Xml.Linq.XDocument.Parse(
            ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml"));
        System.Xml.Linq.XElement baseSlot = Assert.Single(
            workflows.Descendants(),
            element =>
                element.Name.LocalName == "ContentControl" &&
                (string?)element.Attribute("Content") == "{Binding ReplaceBaseSlot}" &&
                element.Parent?.Name.LocalName == "SpaciousPanel");
        System.Xml.Linq.XElement section = Assert.IsType<System.Xml.Linq.XElement>(baseSlot.Parent);

        Assert.Equal("compact", (string?)section.Attribute("Classes"));
        Assert.Equal("Stretch", (string?)section.Attribute("HorizontalAlignment"));
        Assert.Equal("Stretch", (string?)baseSlot.Attribute("HorizontalAlignment"));
        Assert.Equal("Stretch", (string?)baseSlot.Attribute("HorizontalContentAlignment"));
    }
}
