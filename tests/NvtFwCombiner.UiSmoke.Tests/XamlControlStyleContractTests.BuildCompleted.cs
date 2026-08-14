namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>A committed Build exposes its path and both requested completion actions.</summary>
    [Fact]
    public void SuccessfulBuildUsesActionableOutputConfirmation()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string modal = ReadPresentationFile("Views/BuildCompletedModal.axaml");

        Assert.Contains("<views:BuildCompletedModal", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding BuildResult.IsOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding BuildResult.OutputDisplayName}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding BuildResult.RevealOutputCommand}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding BuildResult.CloseCommand}\"", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"RevealOutputFileButton_OnClick\"", modal, StringComparison.Ordinal);
    }

    /// <summary>An eligible AB Build asks for the optional A delivery before either output is selected or committed.</summary>
    [Fact]
    public void AbBuildPromptsForAFlashCodeBeforeOutputSelection()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string buildCodeBehind = ReadPresentationFile("MainWindow.Build.cs");
        string prompt = ReadPresentationFile("Views/AbAFlashCodeDeliveryPromptModal.axaml");
        string modal = ReadPresentationFile("Views/BuildCompletedModal.axaml");

        Assert.Contains("<views:AbAFlashCodeDeliveryPromptModal", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding Merge.IsAbAFlashCodeDeliveryPromptOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding AcceptAbAFlashCodeDeliveryPromptCommand}\"", prompt, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DeclineAbAFlashCodeDeliveryPromptCommand}\"", prompt, StringComparison.Ordinal);
        Assert.Contains("Classes=\"semanticAction danger\"", prompt, StringComparison.Ordinal);
        Assert.Contains("PromptForAbAFlashCodeDeliveryAsync", buildCodeBehind, StringComparison.Ordinal);
        Assert.Contains("PickMergedFirmwareOutputPathAsync", buildCodeBehind, StringComparison.Ordinal);
        Assert.Contains("PickAbAFlashCodeOutputPathAsync", buildCodeBehind, StringComparison.Ordinal);
        Assert.True(
            buildCodeBehind.IndexOf("PromptForAbAFlashCodeDeliveryAsync", StringComparison.Ordinal) <
            buildCodeBehind.IndexOf("PickMergedFirmwareOutputPathAsync", StringComparison.Ordinal));
        Assert.Contains("IsVisible=\"{Binding BuildResult.HasAdditionalOutput}\"", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportAbAFlashCode", modal, StringComparison.Ordinal);
    }

    /// <summary>Composition Build remains fixed at the bottom of the right-side action rail.</summary>
    [Fact]
    public void CompositionBuildUsesFixedBottomActionRail()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string shared = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string loading = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string shellStyles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        int latestOutputAction = shell.IndexOf("Command=\"{Binding RevealFileCommand}\"", StringComparison.Ordinal);
        int mergeBuildAction = shell.IndexOf("Click=\"BuildMergeButton_OnClick\"", StringComparison.Ordinal);
        int replaceBuildAction = shell.IndexOf("Click=\"BuildReplaceButton_OnClick\"", StringComparison.Ordinal);

        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto,*,Auto\"", shell, StringComparison.Ordinal);
        Assert.Contains("<ScrollViewer Grid.Row=\"3\">", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompositionBuildActionRail\"", shell, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"3\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ZIndex=\"1\"", shell, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Right\"", shell, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Bottom\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsCompositionActionRailVisible}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsHitTestVisible=\"{Binding IsCompositionActionRailVisible}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsLatestOutputActionVisible}\"", shell, StringComparison.Ordinal);
        Assert.True(latestOutputAction >= 0);
        Assert.True(mergeBuildAction > latestOutputAction);
        Assert.True(replaceBuildAction > mergeBuildAction);
        Assert.Contains("Orientation=\"Vertical\"", shell, StringComparison.Ordinal);
        Assert.Contains("ForegroundLoadingStatusTemplate", shell, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding Merge.Inspection.Loading}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding Replace.Inspection.Loading}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding Merge.Inspection.Loading.IsVisible}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding Replace.Inspection.Loading.IsVisible}\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ForegroundLoadingStatusTemplate\"", shared, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgressPercentLabel}\"", loading, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"{Binding ShouldAnimate}\"", loading, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelCommand}\"", loading, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RetryCommand}\"", loading, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", loading, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.railAction\"", buttonStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector=\"Button.floatingAction.build\"", buttonStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("Selector=\"Border.compositionActionRail\"", shellStyles, StringComparison.Ordinal);
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
