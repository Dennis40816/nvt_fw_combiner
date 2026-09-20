using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Accepted Base heading hierarchy stays readable without changing shared card actions.</summary>
    [AvaloniaTheory]
    [InlineData(480, false, false)]
    [InlineData(480, false, true)]
    [InlineData(480, true, false)]
    [InlineData(480, true, true)]
    [InlineData(900, false, false)]
    [InlineData(900, false, true)]
    [InlineData(900, true, false)]
    [InlineData(900, true, true)]
    public void CtrlRamHeadingUsesCompletePrimaryAndSecondaryLines(double width, bool chinese, bool dark)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var slot = new FirmwareSlotViewModel(CompositionSlotIds.ReplaceBase, "Base", "Select base", FirmwareSlotKind.Base);
        slot.ApplyDisplayText(text.GetReplaceBaseTitle(ExperienceIds.CtrlRamReplace),
            text.GetReplaceBaseDescription(ExperienceIds.CtrlRamReplace, null),
            text.RequiredLabel, text.OptionalLabel, text.NoBinSelectedLabel,
            ShellTextResources.GetReplaceBaseSubtitle(ExperienceIds.CtrlRamReplace));
        slot.ApplyExperienceText(text);
        var card = new FirmwareSlotCard { DataContext = slot, BrowseLabel = text.BrowseLabel, Width = width };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = width;
        host.Height = 400;
        host.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            TextBlock title = Assert.IsType<TextBlock>(card.FindControl<Control>("SlotTitle"));
            TextBlock subtitle = Assert.IsType<TextBlock>(card.FindControl<Control>("SlotSubtitle"));
            Button browse = Assert.IsType<Button>(card.FindControl<Control>("BrowseButton"));
            Assert.Equal(chinese ? "基底韌體" : "Base firmware", title.Text);
            Assert.Equal("FlashCode / TP FW", subtitle.Text);
            Assert.True(subtitle.IsVisible);
            _ = Assert.Single(title.TextLayout.TextLines);
            _ = Assert.Single(subtitle.TextLayout.TextLines);
            Assert.All(subtitle.TextLayout.TextLines, static line => Assert.False(line.HasCollapsed));
            Grid header = Assert.IsType<Grid>(card.FindControl<Control>("SlotHeaderContent"));
            StackPanel identity = Assert.IsType<StackPanel>(card.FindControl<Control>("SlotIdentity"));
            Point headerOrigin = Assert.IsType<Point>(header.TranslatePoint(default, card));
            Point identityOrigin = Assert.IsType<Point>(identity.TranslatePoint(default, card));
            Point subtitleOrigin = Assert.IsType<Point>(subtitle.TranslatePoint(default, card));
            Point browseOrigin = Assert.IsType<Point>(browse.TranslatePoint(default, card));
            Assert.InRange(Math.Abs(identityOrigin.X - subtitleOrigin.X), 0, 0.5);
            Assert.InRange(subtitleOrigin.Y - headerOrigin.Y - header.Bounds.Height, 11.5, 12.5);
            Assert.True(subtitleOrigin.X + subtitle.Bounds.Width < browseOrigin.X);
            Assert.NotEqual(title.Foreground, subtitle.Foreground);
            Assert.Contains(title.Text!, AutomationProperties.GetName(browse), StringComparison.Ordinal);

            slot.FilePath = @"C:\firmware\long-selected-ctrlram-base-firmware.bin";
            slot.SetBaseDiscoveryInspected(text.CtrlRamBaseInspectedDetail);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.True(subtitle.IsVisible);
            _ = Assert.Single(subtitle.TextLayout.TextLines);
            Assert.True(browse.IsEnabled);

            // Every display projection clears the optional line unless explicitly supplied.
            slot.ApplyDisplayText("TP BIN", "Select TP", text.RequiredLabel, text.OptionalLabel, text.NoBinSelectedLabel);
            Dispatcher.UIThread.RunJobs();
            Assert.False(subtitle.IsVisible);
            Assert.Equal(string.Empty, subtitle.Text);
            Assert.Equal("TP BIN", title.Text);
        }
        finally
        {
            host.Close();
        }
    }
}
