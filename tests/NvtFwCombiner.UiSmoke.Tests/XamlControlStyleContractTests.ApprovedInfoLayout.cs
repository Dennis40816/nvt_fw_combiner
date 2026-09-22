using Avalonia;
using Avalonia.Controls;
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
    /// <summary>Priority is explicit and diagnostics remain visible regardless of position or requested detail priority.</summary>
    [Fact]
    public void InfoPriorityDoesNotDependOnOrderAndNeverHidesBlockingFacts()
    {
        var slot = new FirmwareSlotViewModel("base", "Base", "Select", FirmwareSlotKind.Base);
        var count = new FirmwareSlotFactViewModel("IC Count", "1", priority: FirmwareSlotFactPriority.Details);
        var version = new FirmwareSlotFactViewModel("TP Version", "T05-00");
        var error = new FirmwareSlotFactViewModel("IC Count", "0", FirmwareSlotFactState.Error, "Error", "Read 0", FirmwareSlotFactPriority.Details);
        var pending = new FirmwareSlotFactViewModel("DP Version", "Waiting", FirmwareSlotFactState.PendingInput, "Waiting", "Select TP", FirmwareSlotFactPriority.Details);
        var warning = new FirmwareSlotFactViewModel("TP Version", "Invalid", FirmwareSlotFactState.Warning, "Warning", "Invalid version bar", FirmwareSlotFactPriority.Details);
        slot.SetFirmwareFacts([count, version, error, pending, warning]);
        Assert.Equal([version, error, pending, warning], slot.PrimaryFirmwareFacts);
        Assert.Equal([count], slot.AdditionalFirmwareFacts);
        slot.SetFirmwareFacts([warning, pending, error, version, count]);
        Assert.Equal([warning, pending, error, version], slot.PrimaryFirmwareFacts);
        Assert.Equal([count], slot.AdditionalFirmwareFacts);
    }

    /// <summary>File identity precedes a three-column grid and disclosure never moves the actions.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ApprovedInfoLayoutKeepsFilenameAboveFactsAndActionsStationary(bool dark, bool chinese)
    {
        var slot = new FirmwareSlotViewModel("base", "Base firmware", "Select firmware", FirmwareSlotKind.Base)
        {
            FilePath = @"C:\firmware\NT51929_FlashCode_AB.bin",
        };
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        slot.ApplyExperienceText(text);
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Inspected");
        slot.SetFirmwareFacts([
            new("TPA Version", "T05-00"), new("TPB Version", "T06-00"),
            new("PID", "0x4703"), new("Common FW", "2.0.0"),
            new("Event Buffer Format", "0xA3 · Auto STLA v1"),
            new("IC Count", "1", priority: FirmwareSlotFactPriority.Details),
            new("DP Version", "D06-00", priority: FirmwareSlotFactPriority.Details),
        ]);
        var card = new FirmwareSlotCard { DataContext = slot, BrowseLabel = text.BrowseLabel, Width = 900 };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        host.Width = 940;
        host.Height = 600;
        card.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top;
        host.Show();
        try
        {
            Layout();
            Button filename = Assert.Single(card.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("fileRevealAction"));
            ItemsControl primary = card.FindControl<ItemsControl>("PrimaryFirmwareFactsHost")!;
            StackPanel actions = card.FindControl<StackPanel>("SlotActions")!;
            Grid main = card.FindControl<Grid>("SlotLayout")!;
            Assert.True(Bottom(filename) <= Top(primary), "Filename must precede firmware facts.");
            Assert.Equal(3, card.FactColumnCount);
            double center = Center(actions);
            Assert.InRange(Math.Abs(center - Center(main)), 0, 1);
            slot.IsAdditionalFirmwareFactsExpanded = true;
            Layout();
            Assert.InRange(Math.Abs(center - Center(actions)), 0, 0.5);
            ItemsControl details = card.FindControl<ItemsControl>("AdditionalFirmwareFactsHost")!;
            Assert.True(Top(details) >= Bottom(primary), $"Details top {Top(details)} overlaps primary bottom {Bottom(primary)}.");
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using global::Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (directory is not null)
            {
                _ = Directory.CreateDirectory(directory);
                frame.Save(Path.Combine(directory, $"info-card-{(dark ? "dark-zh" : "light-en")}.png"));
            }
        }
        finally
        {
            host.Close();
        }

        void Layout()
        {
            host.Measure(new Size(940, 1000));
            card.Measure(new Size(900, 1000));
            card.Arrange(new Rect(0, 0, 900, card.DesiredSize.Height));
            Dispatcher.UIThread.RunJobs();
            card.Measure(new Size(900, 1000));
            card.Arrange(new Rect(0, 0, 900, card.DesiredSize.Height));
        }
        double Top(Control control)
        {
            return control.TranslatePoint(default, card)!.Value.Y;
        }
        double Bottom(Control control)
        {
            return Top(control) + control.Bounds.Height;
        }
        double Center(Control control)
        {
            return Top(control) + (control.Bounds.Height / 2);
        }
    }
}
