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
        Assert.Contains("Text=\"{Binding BuildCompletedOutputPath}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Click=\"OpenOutputFolderButton_OnClick\"", modal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseBuildCompletedModalCommand}\"", modal, StringComparison.Ordinal);
        Assert.Contains("LaunchDirectoryInfoAsync", modalCode, StringComparison.Ordinal);
        Assert.Contains("OutputFolderLauncher.TryOpenAsync", buildCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", modalCode, StringComparison.Ordinal);
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
        Assert.Contains("Grid.Row=\"4\"", shell, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Right\"", shell, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Bottom\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"compositionActionRail\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsCompositionActionRailVisible}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsLatestOutputActionVisible}\"", shell, StringComparison.Ordinal);
        Assert.True(latestOutputAction >= 0);
        Assert.True(mergeBuildAction > latestOutputAction);
        Assert.True(replaceBuildAction > mergeBuildAction);
        Assert.Contains("Selector=\"Button.dockAction\"", buttonStyles, StringComparison.Ordinal);
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
}
