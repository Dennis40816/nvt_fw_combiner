using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Empty inputs retain normal bottom spacing after selection and clearing.</summary>
    [AvaloniaTheory]
    [InlineData(684, false, "Dp")]
    [InlineData(1100, false, "Dp")]
    [InlineData(684, true, "Tp")]
    [InlineData(1100, true, "Tp")]
    [InlineData(684, false, "CtrlRam")]
    [InlineData(1100, false, "CtrlRam")]
    public void EmptyFirmwareSlotDoesNotReserveHiddenRows(double width, bool dark, string kind)
    {
        var slot = new FirmwareSlotViewModel("input", "Input BIN",
            "Select the firmware input for this slot.", Enum.Parse<FirmwareSlotKind>(kind));
        var card = new FirmwareSlotCard { DataContext = slot, BrowseLabel = "Browse", Width = width };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

        for (int cycle = 0; cycle < 2; cycle++)
        {
            Layout();
            Border border = Assert.Single(card.GetVisualDescendants().OfType<Border>(),
                item => item.Classes.Contains("firmwareSlot"));
            TextBlock guidance = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(),
                item => item.Classes.Contains("firmwareSlotGuidance"));
            Assert.True(guidance.IsVisible);
            double bottom = guidance.TranslatePoint(new Point(0, guidance.Bounds.Height), border)!.Value.Y;
            Assert.InRange(border.Bounds.Height - bottom, 16, 20);

            slot.FilePath = @"C:\firmware\selected-input.bin";
            Layout();
            Assert.False(guidance.IsVisible);
            StackPanel actions = card.FindControl<StackPanel>("SlotActions")!;
            double actionsCenter = actions.TranslatePoint(new Point(0, actions.Bounds.Height / 2), border)!.Value.Y;
            Assert.InRange(Math.Abs(actionsCenter - (border.Bounds.Height / 2)), 0, 1);
            slot.FilePath = null;
        }

        void Layout()
        {
            host.Measure(new Size(width, 1000));
            card.Measure(new Size(width, 1000));
            card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));
        }
    }
}
