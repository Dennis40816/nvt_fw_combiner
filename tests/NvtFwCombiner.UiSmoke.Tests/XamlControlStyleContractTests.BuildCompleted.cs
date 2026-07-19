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

        Assert.Contains("<views:BuildCompletedModal", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsBuildCompletedModalOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding BuildCompletedOutputPath}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Click=\"OpenOutputFolderButton_OnClick\"", modal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CloseBuildCompletedModalCommand}\"", modal, StringComparison.Ordinal);
        Assert.Contains("LaunchDirectoryInfoAsync", modalCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", modalCode, StringComparison.Ordinal);
    }

    /// <summary>Adjacent CtrlRAM slot and coverage groups retain visible separation.</summary>
    [Fact]
    public void CtrlRamGroupsUseExplicitVerticalSpacing()
    {
        string workflows = ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml");
        string shared = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");

        Assert.Contains("ItemsSource=\"{Binding ReplaceSlotGroups}\"", workflows, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Spacing=\"{DynamicResource NfcSpace12}\" />", workflows, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceCoverageGroups}\"", shared, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Spacing=\"{DynamicResource NfcSpace12}\" />", shared, StringComparison.Ordinal);
    }
}
