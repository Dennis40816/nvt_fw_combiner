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
    /// <summary>Input guidance uses two readable lines without a suggested filename or a detached byte unit.</summary>
    [AvaloniaTheory]
    [InlineData(false, 2960, 0x2D100, 480)]
    [InlineData(true, 2960, 0x2D100, 480)]
    [InlineData(false, 18944, 0x21B90, 900)]
    [InlineData(true, 18944, 0x21B90, 900)]
    public void CtrlRamGuidanceKeepsSizeAndOutputOffsetOnSeparateCompleteLines(bool chinese, long length, long offset, int width)
    {
        var text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var facts = new CtrlRamInputDescriptionFacts("unused-name.bin",
            [new("Repeated title", ReplaceRegionGroup.Common, length, offset, "CtrlRAM")],
            false, "CtrlRAM", false, 1);
        (string title, string description) = text.GetReplaceInputText(null, ReplaceInputRole.CtrlRam, ReplaceRegionGroup.Common,
            "CtrlRAM", "Legacy description", facts);
        string size = length == 2960 ? "2,960\u00a0B" : "18,944\u00a0B";
        string address = offset == 0x2D100 ? "0x2D100" : "0x21B90";
        Assert.Equal(chinese ? $"大小上限: {size}\n目標位址: {address}"
            : $"Max Size: {size}\nTarget Addr: {address}", description);
        var slot = new FirmwareSlotViewModel("ctrlram", title, description, FirmwareSlotKind.CtrlRam, isOptional: true,
            ctrlRamDescriptionFacts: facts);
        slot.ApplyExperienceText(text);
        var card = new FirmwareSlotCard { DataContext = slot, BrowseLabel = "Browse", Width = width };
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = width;
        host.Height = 500;
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
            Assert.Equal(sizeValue.TranslatePoint(default, card)!.Value.X, addressValue.TranslatePoint(default, card)!.Value.X);
            Assert.All(sizeValue.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
            Assert.All(addressValue.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>Compact shared guidance keeps topology and prerequisite facts; technical detail retains every mapping.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CtrlRamGuidancePreservesSharedPrerequisitesAndTechnicalDetails(bool chinese)
    {
        var text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var facts = new CtrlRamInputDescriptionFacts("NF_Ctrlram.bin",
            [new("NF master", ReplaceRegionGroup.Master, 512, 0x1000, "NF"),
             new("NF slave", ReplaceRegionGroup.SlaveRight, 256, 0x2000, "NF")],
            true, "NF", true, 2);
        (string _, string description) = text.GetReplaceInputText(null, ReplaceInputRole.CtrlRam, ReplaceRegionGroup.Common,
            "NF", "Legacy description", facts);
        Assert.StartsWith(chinese ? "共用至 2 個區域" : "Shared across 2 regions", description, StringComparison.Ordinal);
        Assert.Contains("DiffNFMerge", description, StringComparison.Ordinal);
        Assert.DoesNotContain("NF_Ctrlram.bin", description, StringComparison.Ordinal);
        string detail = text.FormatCtrlRamTechnicalDescription(facts);
        Assert.Contains("NF_Ctrlram.bin", detail, StringComparison.Ordinal);
        Assert.Contains("0x1000", detail, StringComparison.Ordinal);
        Assert.Contains("0x2000", detail, StringComparison.Ordinal);
        Assert.Equal("Legacy description", text.GetReplaceInputText(null, ReplaceInputRole.CtrlRam,
            ReplaceRegionGroup.Common, "NF", "Legacy description", null).Description);
    }
}
