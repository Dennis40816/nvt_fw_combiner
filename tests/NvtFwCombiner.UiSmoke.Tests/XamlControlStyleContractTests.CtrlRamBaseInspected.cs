using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>The approved Base-inspected badge uses the blue non-terminal state without moving the card.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CtrlRamBaseInspectedStateKeepsApprovedSelectorGeometry(bool useDarkTheme)
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        var slot = new FirmwareSlotViewModel(
            CompositionSlotIds.ReplaceBase,
            "CtrlRAM Base",
            "Select base firmware",
            FirmwareSlotKind.Base)
        {
            FilePath = @"C:\firmware\nt51950-normal-ctrlram.bin",
        };
        slot.ApplyExperienceText(text);
        slot.SetBaseDiscoveryInspected(text.CtrlRamBaseInspectedDetail);
        slot.SetFirmwareFacts(
        [
            new("Common FW Version", "2.0.0"),
            new("Available regions", "3"),
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = text.BrowseLabel,
            DataContext = slot,
            Width = 900,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = 940;
        host.Height = 150;
        host.RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;

        try
        {
            host.Show();
            host.Measure(new Size(940, 150));
            host.Arrange(new Rect(0, 0, 940, 150));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Border selector = Assert.Single(
                card.GetVisualDescendants().OfType<Border>(),
                candidate => candidate.Classes.Contains("firmwareSlot"));
            Button browse = Assert.IsType<Button>(card.FindControl<Control>("BrowseButton"));
            ToggleButton state = Assert.Single(
                card.GetVisualDescendants().OfType<ToggleButton>(),
                candidate => candidate.Classes.Contains("slotStateAction"));
            string styles = ReadPresentationFile("Styles/FirmwareSlotExperienceStyles.axaml");
            string inspected = ExtractStyle(styles, "ToggleButton.slotStateAction.inspected");
            string inspectedPresenter = ExtractStyle(
                styles,
                "ToggleButton.slotStateAction.inspected /template/ ContentPresenter#PART_ContentPresenter");

            Assert.Equal(FirmwareSlotSemanticState.Inspected, slot.SemanticState);
            Assert.Equal("Base inspected", slot.SemanticStateLabel);
            Assert.Contains("inspected", state.Classes);
            Assert.InRange(selector.Bounds.Height, 90, 104);
            Assert.Equal(36, browse.Bounds.Height, precision: 3);
            Assert.Contains("NfcAccentSurfaceBrush", inspected, StringComparison.Ordinal);
            Assert.Contains("NfcAccentStrongBrush", inspected, StringComparison.Ordinal);
            Assert.Contains("NfcAccentSurfaceBrush", inspectedPresenter, StringComparison.Ordinal);
            Assert.Contains(
                "ToggleButton.slotStateAction.inspected Path.slotStateIcon",
                styles,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Padding", inspected, StringComparison.Ordinal);
            Assert.DoesNotContain("Width", inspected, StringComparison.Ordinal);
            Assert.DoesNotContain("Height", inspected, StringComparison.Ordinal);
            Assert.DoesNotContain("Margin", inspected, StringComparison.Ordinal);

            slot.ApplyExperienceText(ShellTextResources.For(ShellLanguage.ChineseTraditional));
            Assert.Equal("基礎檔已檢查", slot.SemanticStateLabel);
            Assert.Equal(
                "已檢查基礎韌體；請至少選擇一個 CtrlRAM 取代檔，才能編譯確切操作。",
                slot.SemanticStateDetail);
            slot.ApplyExperienceText(text);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                using FileStream output = File.Create(
                    Path.Combine(outputDirectory, $"ctrlram-base-inspected-{themeName}.png"));
                frame.Save(output);
            }
        }
        finally
        {
            host.Close();
        }
    }
}
