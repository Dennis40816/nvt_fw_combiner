namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps typed selection readiness visible and equivalent to assistive technology.</summary>
    [Fact]
    public void FirmwareSlotSelectionReadinessHasLocalizedVisibleAndAccessibleText()
    {
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");

        Assert.Contains(
            "IsVisible=\"{Binding HasSelectionReadinessStatus}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "Content=\"{Binding SelectionReadinessLabel}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "Text=\"{Binding SelectionReadinessDetail}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding SelectionReadinessAutomationText}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.LiveSetting=\"Polite\"",
            slotCard,
            StringComparison.Ordinal);
    }
}
