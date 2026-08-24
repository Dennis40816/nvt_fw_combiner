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
    /// <summary>Compact fact values are constrained by their own four-column cell.</summary>
    [AvaloniaTheory]
    [InlineData(684)]
    [InlineData(760)]
    public void FirmwareSlotFactTextNeverPaintsIntoTheNextColumn(double width)
    {
        var slot = new FirmwareSlotViewModel(
            "tp",
            "TP BIN",
            "Select TP firmware",
            FirmwareSlotKind.Tp)
        {
            FilePath = @"C:\firmware\tp-input.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        slot.SetFirmwareFacts(
        [
            new("Common FW Version", "2.0.0"),
            new("TP Version", "T04-00"),
            new("PID", "0x135E"),
            new("Jira Index", "AUTO_PRJ-576"),
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            DataContext = slot,
            Width = width,
        };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);

        host.Measure(new Size(width, 1_000));
        card.Measure(new Size(width, 1_000));
        card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));

        ItemsControl primaryFacts = Assert.IsType<ItemsControl>(
            card.FindControl<Control>("PrimaryFirmwareFactsHost"));
        UniformGrid factGrid = Assert.Single(
            primaryFacts.GetVisualDescendants().OfType<UniformGrid>());
        Control[] factCells = [.. factGrid.Children.OfType<Control>()];
        Assert.Equal(4, factCells.Length);
        foreach (Control cell in factCells)
        {
            TextBlock value = Assert.Single(
                cell.GetVisualDescendants().OfType<TextBlock>(),
                candidate => candidate.Classes.Contains("firmwareSlotFactValue"));
            Point valueOrigin = Assert.IsType<Point>(value.TranslatePoint(default, cell));
            Assert.True(
                valueOrigin.X + value.Bounds.Width <= cell.Bounds.Width + 0.5,
                $"Fact value '{value.Text}' escaped its cell: value={value.Bounds}, " +
                $"origin={valueOrigin}, cell={cell.Bounds}.");
        }
    }

    /// <summary>Compact cards move facts below identity/actions and retain readable two-column facts.</summary>
    [AvaloniaTheory]
    [InlineData(480)]
    [InlineData(684)]
    public void FirmwareSlotCompactLayoutUsesTwoFactColumns(double width)
    {
        var slot = new FirmwareSlotViewModel(
            "tp",
            "TP BIN",
            "Select TP firmware",
            FirmwareSlotKind.Tp)
        {
            FilePath = @"C:\firmware\a-very-long-selected-firmware-file-name.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        slot.SetFirmwareFacts(
        [
            new("Common FW Version", "2.0.0"),
            new("TP Version", "T04-00"),
            new("PID", "0x135E"),
            new("Jira Index", "AUTO_PRJ-576"),
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            DataContext = slot,
            Width = width,
        };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);

        host.Measure(new Size(width, 1_000));
        card.Measure(new Size(width, 1_000));
        card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));
        Dispatcher.UIThread.RunJobs();
        card.Measure(new Size(width, 1_000));
        card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));

        Grid layout = Assert.IsType<Grid>(card.FindControl<Control>("SlotLayout"));
        StackPanel identity = Assert.IsType<StackPanel>(card.FindControl<Control>("SlotIdentity"));
        StackPanel facts = Assert.IsType<StackPanel>(card.FindControl<Control>("SlotFactsRegion"));
        StackPanel actions = Assert.IsType<StackPanel>(card.FindControl<Control>("SlotActions"));
        ItemsControl primaryFacts = Assert.IsType<ItemsControl>(
            card.FindControl<Control>("PrimaryFirmwareFactsHost"));
        UniformGrid factGrid = Assert.Single(
            primaryFacts.GetVisualDescendants().OfType<UniformGrid>());

        Assert.Equal(2, layout.RowDefinitions.Count);
        Assert.Equal(0, Grid.GetRow(identity));
        Assert.Equal(0, Grid.GetRow(actions));
        Assert.Equal(1, Grid.GetRow(facts));
        Assert.Equal(2, Grid.GetColumnSpan(facts));
        Assert.Equal(2, factGrid.Columns);
        Control[] factCells = [.. factGrid.Children.OfType<Control>()];
        Assert.Equal(4, factCells.Length);
        Assert.Equal(factCells[0].Bounds.Y, factCells[1].Bounds.Y);
        Assert.True(factCells[2].Bounds.Y > factCells[0].Bounds.Y);
        Assert.Equal(factCells[2].Bounds.Y, factCells[3].Bounds.Y);
        Assert.True(
            facts.Bounds.Width > identity.Bounds.Width,
            $"facts={facts.Bounds}; identity={identity.Bounds}; layout={layout.Bounds}");
    }

    /// <summary>Collapsed CtrlRAM slot groups retain the shared two-pixel surface in both themes.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirmwareSlotGroupSurfaceKeepsItsOutlineAcrossThemes(bool useDarkTheme)
    {
        var panel = new SpaciousPanel
        {
            Width = 640,
            Child = new TextBlock { Text = "Common" },
        };
        panel.Classes.Add("compact");
        panel.Classes.Add("firmwareSlotGroupSurface");
        var host = new Window
        {
            Width = 700,
            Height = 120,
            RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = panel,
        };

        host.Show();
        try
        {
            host.Measure(new Size(700, 120));
            host.Arrange(new Rect(0, 0, 700, 120));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.Equal(new Thickness(2), panel.BorderThickness);
            Assert.NotNull(panel.BorderBrush);
            Assert.NotNull(panel.Background);
            Assert.True(panel.CornerRadius.TopLeft > 0);
            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
        }
        finally
        {
            host.Close();
        }
    }
}
