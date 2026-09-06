using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Real wrapped text reserves its complete line box before badges and the selected filename.</summary>
    [AvaloniaTheory]
    [InlineData(480)]
    [InlineData(900)]
    public void SelectorWrappedTitleAndFilenameKeepReadableGaps(double width)
    {
        var slot = new FirmwareSlotViewModel("base", "Firmware input with a long title", "Select firmware", FirmwareSlotKind.Base)
        {
            FilePath = @"C:\firmware\a-long-selected-firmware-file-name-for-ctrlram.bin",
        };
        slot.ApplyExperienceText(ShellTextResources.For(ShellLanguage.English));
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        var card = new FirmwareSlotCard { DataContext = slot, BrowseLabel = "Browse", Width = width };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = width;
        host.Height = 500;
        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            TextBlock title = Assert.IsType<TextBlock>(card.FindControl<Control>("SlotTitle"));
            ToggleButton badge = Assert.Single(card.GetVisualDescendants().OfType<ToggleButton>(), b => b.Classes.Contains("slotStateAction"));
            Button file = Assert.Single(card.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("fileRevealAction"));
            TextBlock filename = Assert.IsType<TextBlock>(file.Content);
            Border border = Assert.Single(card.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("firmwareSlot"));
            Grid layout = Assert.IsType<Grid>(card.FindControl<Control>("SlotLayout"));
            Assert.Equal(2, title.TextLayout.TextLines.Count);
            Point titlePoint = Assert.IsType<Point>(title.TranslatePoint(default, card));
            Point badgePoint = Assert.IsType<Point>(badge.TranslatePoint(default, card));
            Point filePoint = Assert.IsType<Point>(file.TranslatePoint(default, card));
            Assert.InRange(badgePoint.Y - titlePoint.Y - title.Bounds.Height, 8, 12);
            Assert.InRange(filePoint.Y - badgePoint.Y - badge.Bounds.Height, 12, 16);
            Assert.Equal(2, filename.TextLayout.TextLines.Count);
            Assert.All(filename.TextLayout.TextLines, static line => Assert.False(line.HasCollapsed));
            Assert.InRange(layout.Margin.Top, 16, 20);
            Assert.InRange(layout.Margin.Bottom, 16, 20);
            Assert.True(filePoint.Y + file.Bounds.Height <= border.Bounds.Bottom - 16);
        }
        finally
        {
            host.Close();
        }
    }
}
