namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Settings is an application modal and has no workflow-page route or semantic owner.</summary>
    [Fact]
    public void SettingsModalDoesNotOwnWorkflowNavigationOrFirmwareState()
    {
        string presentationRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia");
        string source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        string shell = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");
        string modal = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/SettingsModal.axaml");
        string navigation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Navigation.cs");

        Assert.DoesNotContain("ShellPage.Settings", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowSettingsCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsSettingsVisible", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsUtilityButton\"", shell, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"2\"", shell, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenSettingsCommand}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding OpenSettingsCommand}\"\n              IsChecked", shell, StringComparison.Ordinal);
        Assert.Contains("IsSettingsModalOpen ||", navigation, StringComparison.Ordinal);
        Assert.Contains("KeyboardNavigation.TabNavigation=\"Cycle\"", modal, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"SettingsModal_OnKeyDown\"", modal, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.CloseLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", modal, StringComparison.Ordinal);
    }
}
