namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Settings catalog presentation belongs to a focused child rather than the shell.</summary>
    [Fact]
    public void SettingsPresentationLivesBehindFocusedChild()
    {
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string shellSettings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Settings.cs");
        string settings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/SettingsViewModel.cs");
        string pageTemplates = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml");

        Assert.Contains("Settings = new SettingsViewModel(", construction, StringComparison.Ordinal);
        Assert.Contains("hostServices.SupportMatrix", construction, StringComparison.Ordinal);
        Assert.Contains("public SettingsViewModel Settings", shellSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionService", shellSettings, StringComparison.Ordinal);
        Assert.Contains("ICanonicalSupportMatrixQuery", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionService", settings, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Settings.OverviewRows}\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Settings.CapabilityRows}\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding Settings.OpenSupportMatrixCommand}\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Settings.SupportMatrix.IcRows}\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Settings.SupportMatrix.WorkflowColumns}\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding AccessibleDetail}\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("behaviors:FocusToolTipBehavior.IsEnabled=\"True\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("behaviors:FocusOnRevealBehavior.IsEnabled=\"True\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProvenanceDetail}\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("ToolTip.ShowDelay=\"220\"", pageTemplates, StringComparison.Ordinal);
    }
}
