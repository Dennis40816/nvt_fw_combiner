using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Renders the owner-requested primary Information hierarchy at wide and compact widths.</summary>
    [AvaloniaTheory]
    [InlineData(1180)]
    [InlineData(684)]
    public void InformationCardPreviewKeepsFourPrimaryFactsAndCollapsedDetails(double width)
    {
        var slot = new FirmwareSlotViewModel(
            "tp-a",
            "TP A firmware",
            "TP firmware input",
            FirmwareSlotKind.Tp)
        {
            FilePath = @"C:\firmware\nt51950-tpa-production-firmware.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        slot.SetFirmwareFacts(
        [
            new("TP Version", "T9B-00"),
            new("PID", "0x570A"),
            new("Common FW Version", "1.4.0"),
            new("Event Buffer Version", "0x97 - Desay"),
            new("DP Version", "D08-00"),
            new("Jira Index", "AUTO_PRJ-528"),
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            ClearSelectionLabel = "Clear selected file",
            DataContext = slot,
            VerticalAlignment = VerticalAlignment.Top,
            Width = width,
        };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = width;
        host.Height = 320;

        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.Equal(
                ["TP Version", "PID", "Common FW Version", "Event Buffer Version"],
                slot.PrimaryFirmwareFacts.Select(static fact => fact.Label));
            Assert.False(slot.IsAdditionalFirmwareFactsExpanded);
            Assert.Equal(2, slot.AdditionalFirmwareFacts.Count);
            Border surface = Assert.Single(card.GetVisualDescendants().OfType<Border>(),
                control => control.Classes.Contains("firmwareSlot"));
            Button filename = Assert.Single(card.GetVisualDescendants().OfType<Button>(),
                control => control.Classes.Contains("fileRevealAction"));
            Point filenameOrigin = filename.TranslatePoint(default, surface)!.Value;
            Assert.True(filenameOrigin.Y + filename.Bounds.Height <= surface.Bounds.Height - 19,
                "The filename must remain inside the card with its bottom padding.");

            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
                using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(directory, $"information-card-{width}.png"));
            }
        }
        finally
        {
            host.Close();
        }
    }
}
