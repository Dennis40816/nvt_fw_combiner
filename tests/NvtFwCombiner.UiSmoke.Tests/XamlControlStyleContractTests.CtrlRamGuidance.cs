using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Input guidance uses readable responsive cells without a suggested filename or a detached byte unit.</summary>
    [AvaloniaTheory]
    [InlineData(false, 2960, 0x2D100, 480, false, false)]
    [InlineData(true, 2960, 0x2D100, 480, false, true)]
    [InlineData(false, 18944, 0x21B90, 900, false, false)]
    [InlineData(true, 18944, 0x21B90, 900, false, true)]
    [InlineData(false, 2960, 0x2D100, 480, true, false)]
    [InlineData(true, 2960, 0x2D100, 480, true, true)]
    [InlineData(false, 18944, 0x21B90, 900, true, true)]
    [InlineData(true, 18944, 0x21B90, 900, true, false)]
    public void CtrlRamGuidanceKeepsSizeAndOutputOffsetInCompleteResponsiveCells(
        bool chinese, long length, long offset, int width, bool selected, bool dark)
    {
        var text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var facts = new CtrlRamInputDescriptionFacts("unused-name.bin",
            [new("Repeated title", ReplaceRegionGroup.Common, length, offset, "CtrlRAM")],
            false, "CtrlRAM", false, 1, [new("ctrlram", ReplaceRegionGroup.Common, offset, length)]);
        (string title, string description) = text.GetReplaceInputText(null, ReplaceInputRole.CtrlRam, ReplaceRegionGroup.Common,
            "CtrlRAM", "Legacy description", facts);
        string size = length == 2960 ? "2,960\u00a0B" : "18,944\u00a0B";
        string address = offset == 0x2D100 ? "0x2D100" : "0x21B90";
        Assert.Equal(chinese ? $"大小上限: {size}\n目標位址: {address}"
            : $"Max Size: {size}\nTarget Addr: {address}", description);
        var slot = new FirmwareSlotViewModel("ctrlram", title, description, FirmwareSlotKind.CtrlRam, isOptional: true,
            ctrlRamDescriptionFacts: facts);
        slot.ApplyExperienceText(text);
        if (selected) { slot.FilePath = @"C:\firmware\selected-ctrlram.bin"; }
        var card = new FirmwareSlotCard { DataContext = slot, BrowseLabel = "Browse", Width = width };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = width;
        host.Height = 500;
        host.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            TextBlock sizeValue = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), b => b.Text == size);
            TextBlock addressValue = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), b => b.Text == address);
            Assert.True(sizeValue.IsEffectivelyVisible);
            Assert.True(addressValue.IsEffectivelyVisible);
            _ = Assert.Single(sizeValue.TextLayout.TextLines);
            _ = Assert.Single(addressValue.TextLayout.TextLines);
            Point sizePoint = sizeValue.TranslatePoint(default, card)!.Value;
            Point addressPoint = addressValue.TranslatePoint(default, card)!.Value;
            Assert.InRange(Math.Abs(sizePoint.Y - addressPoint.Y), 0, 0.5);
            Assert.True(addressPoint.X > sizePoint.X);
            Assert.All(sizeValue.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
            Assert.All(addressValue.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
            Assert.Equal(selected ? 1 : 0, card.GetVisualDescendants().OfType<TextBlock>().Count(
                block => block.IsEffectivelyVisible && block.Text == "selected-ctrlram.bin"));
            Assert.Equal(!selected, slot.IsGuidanceVisible);
            slot.FilePath = null;
            Dispatcher.UIThread.RunJobs();
            Assert.True(sizeValue.IsEffectivelyVisible);
            Assert.True(addressValue.IsEffectivelyVisible);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>Compact shared guidance keeps topology and prerequisite facts; technical detail retains every mapping.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CtrlRamGuidancePreservesSharedPrerequisitesAndTechnicalDetails(bool chinese, bool equalLengths)
    {
        var text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var facts = new CtrlRamInputDescriptionFacts("NF_Ctrlram.bin",
            [new("NF master", ReplaceRegionGroup.Master, 512, 0x1000, "NF"),
             new("NF slave", ReplaceRegionGroup.SlaveRight, equalLengths ? 512 : 256, 0x2000, "NF")],
            true, "NF", true, 2,
            [new("nf-master", ReplaceRegionGroup.Master, 0x1000, 512),
             new("nf-slave", ReplaceRegionGroup.SlaveRight, 0x2000, equalLengths ? 512 : 256)]);
        (string _, string description) = text.GetReplaceInputText(null, ReplaceInputRole.CtrlRam, ReplaceRegionGroup.Common,
            "NF", "Legacy description", facts);
        Assert.StartsWith(chinese ? "共用至 2 個區域" : "Shared across 2 regions", description, StringComparison.Ordinal);
        Assert.Contains("DiffNFMerge", description, StringComparison.Ordinal);
        Assert.DoesNotContain("NF_Ctrlram.bin", description, StringComparison.Ordinal);
        IReadOnlyList<FirmwareSlotFactViewModel> guidance = text.GetCtrlRamGuidanceFacts(facts);
        Assert.Collection(guidance,
            row => Assert.Equal(equalLengths ? "512\u00a0B"
                : chinese ? "主 IC: 512\u00a0B\n右從 IC: 256\u00a0B"
                : "Master: 512\u00a0B\nSlave R: 256\u00a0B", row.Value),
            row => Assert.Equal(chinese ? "主 IC: 0x1000\n右從 IC: 0x2000"
                : "Master: 0x1000\nSlave R: 0x2000", row.Value));
        string detail = text.FormatCtrlRamTechnicalDescription(facts);
        Assert.Contains("NF_Ctrlram.bin", detail, StringComparison.Ordinal);
        Assert.Contains("0x1000", detail, StringComparison.Ordinal);
        Assert.Contains("0x2000", detail, StringComparison.Ordinal);
        Assert.Equal("Legacy description", text.GetReplaceInputText(null, ReplaceInputRole.CtrlRam,
            ReplaceRegionGroup.Common, "NF", "Legacy description", null).Description);

        var slot = new FirmwareSlotViewModel("shared", "NF CtrlRAM (Shared)", description,
            FirmwareSlotKind.CtrlRam, isOptional: true, ctrlRamDescriptionFacts: facts);
        slot.ApplyExperienceText(text);
        var card = new FirmwareSlotCard { DataContext = slot, Width = 480, BrowseLabel = "Browse" };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = 480;
        host.Height = 560;
        host.RequestedThemeVariant = chinese ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        try
        {
            host.Show();
            foreach (string? path in new[] { null, @"C:\firmware\shared.bin", null })
            {
                slot.FilePath = path;
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                foreach (FirmwareSlotFactViewModel fact in guidance)
                {
                    TextBlock value = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == fact.Value);
                    Assert.True(value.IsEffectivelyVisible);
                    Assert.All(value.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
                    Assert.Equal(fact.Value.Split('\n').Length, value.TextLayout.TextLines.Count);
                    Point origin = value.TranslatePoint(default, card)!.Value;
                    Assert.InRange(origin.X + value.Bounds.Width, 0, card.Bounds.Width);
                    Assert.InRange(origin.Y + value.Bounds.Height, 0, card.Bounds.Height);
                }
            }
        }
        finally { host.Close(); }
    }
}
