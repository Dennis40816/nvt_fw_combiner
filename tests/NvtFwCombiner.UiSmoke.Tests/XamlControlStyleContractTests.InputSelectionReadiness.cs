namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Projects typed selection readiness through the one visible and accessible slot-state surface.</summary>
    [Fact]
    public void FirmwareSlotSelectionReadinessHasLocalizedVisibleAndAccessibleText()
    {
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");

        Assert.Contains(
            "IsVisible=\"{Binding HasSemanticState}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "Text=\"{Binding SemanticStateLabel}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "<views:IssueDetailsCard DataContext=\"{Binding IssueCard}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding SemanticStateAutomationText}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.LiveSetting=\"Polite\"",
            slotCard,
            StringComparison.Ordinal);
    }
}
