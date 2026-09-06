using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Real AB facts fill the existing four/two cells without moving identity or actions.</summary>
    [AvaloniaTheory]
    [InlineData(1362, false, false)]
    [InlineData(1362, true, true)]
    [InlineData(1050, false, true)]
    [InlineData(1050, true, false)]
    [InlineData(620, false, false)]
    [InlineData(620, true, true)]
    public void AbDpFactsUseExistingResponsiveGeometry(double width, bool dark, bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        ShellTextResources text = ShellTextResources.For(language);
        FirmwareSlotViewModel slot = AbDpMetadataTests.CreateSlot(language,
            new(CompiledInputVersionKind.DpA, 6, 0, 4095),
            new(CompiledInputVersionKind.DpB, 9, 1, 607));
        var card = new FirmwareSlotCard
        {
            Width = width,
            DataContext = slot,
            BrowseLabel = text.BrowseLabel,
            ClearSelectionLabel = "Clear selected file",
        };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        try
        {
            Arrange(host, card);
            ItemsControl primary = card.FindControl<ItemsControl>("PrimaryFirmwareFactsHost")!;
            UniformGrid grid = Assert.Single(primary.GetVisualDescendants().OfType<UniformGrid>());
            Control[] cells = [.. grid.Children];
            Assert.Equal(width < 820 ? 2 : 4, grid.Columns);
            Assert.Equal(4, cells.Length);
            for (int index = 0; index < cells.Length; index++)
            {
                Control cell = cells[index];
                TextBlock value = Assert.Single(cell.GetVisualDescendants().OfType<TextBlock>(),
                    block => block.Classes.Contains("firmwareSlotFactValue"));
                Border fact = Assert.Single(cell.GetVisualDescendants().OfType<Border>(),
                    border => border.Classes.Contains("firmwareSlotFact"));
                Assert.Equal(slot.FirmwareFacts[index].Value, value.Text);
                Assert.Equal(value.Text, ToolTip.GetTip(value));
                Assert.All(value.TextLayout.TextLines, static line => Assert.False(line.HasCollapsed));
                Assert.Equal(slot.FirmwareFacts[index].StateAutomationText, AutomationProperties.GetName(fact));
                Assert.True(cell.Bounds.Width > 0);
            }
            Assert.Equal(cells[0].Bounds.Y, cells[1].Bounds.Y);
            Assert.Equal(cells[2].Bounds.Y, cells[3].Bounds.Y);
            Assert.Equal(width < 820, cells[2].Bounds.Y > cells[0].Bounds.Y);
            Assert.True(cells[3].Bounds.X > cells[2].Bounds.X);

            StackPanel identity = card.FindControl<StackPanel>("SlotIdentity")!;
            StackPanel actions = card.FindControl<StackPanel>("SlotActions")!;
            Button browse = card.FindControl<Button>("BrowseButton")!;
            Button clear = card.FindControl<Button>("ClearButton")!;
            Rect identityBounds = identity.Bounds;
            Rect actionBounds = actions.Bounds;
            double height = card.Bounds.Height;
            Assert.True(browse.IsEnabled && clear.IsEnabled && browse.Focusable && clear.Focusable);
            Assert.Equal(36, browse.Bounds.Height, precision: 3);
            Assert.Contains("DP_AB BIN", AutomationProperties.GetName(browse), StringComparison.Ordinal);

            // Measure the old content in a fresh host to avoid cached panel row sizing.
            FirmwareSlotViewModel oldSlot = AbDpMetadataTests.CreateSlot(language);
            oldSlot.SetFirmwareFacts([new("DP1", "D06-00 · AUTO_PRJ-4095"), new("DP2", "D09-01 · AUTO_PRJ-607")]);
            var oldCard = new FirmwareSlotCard
            {
                Width = width,
                DataContext = oldSlot,
                BrowseLabel = text.BrowseLabel,
                ClearSelectionLabel = "Clear selected file",
            };
            (Window oldHost, _, _) = HostWithProductionFirmwareSlotStyles(oldCard);
            oldHost.RequestedThemeVariant = host.RequestedThemeVariant;
            try
            {
                Arrange(oldHost, oldCard);
                Assert.Equal(identityBounds, oldCard.FindControl<StackPanel>("SlotIdentity")!.Bounds);
                Assert.Equal(actionBounds, oldCard.FindControl<StackPanel>("SlotActions")!.Bounds);
                Assert.Equal(width < 820 ? cells[0].Bounds.Height : 0, height - oldCard.Bounds.Height);
            }
            finally
            {
                oldHost.Close();
            }
        }
        finally
        {
            host.Close();
        }

        void Arrange(Window layoutHost, FirmwareSlotCard layoutCard)
        {
            layoutHost.Measure(new Size(width, 1000));
            layoutCard.Measure(new Size(width, 1000));
            layoutCard.Arrange(new Rect(0, 0, width, layoutCard.DesiredSize.Height));
            Dispatcher.UIThread.RunJobs();
            layoutCard.Measure(new Size(width, 1000));
            layoutCard.Arrange(new Rect(0, 0, width, layoutCard.DesiredSize.Height));
        }
    }
}
