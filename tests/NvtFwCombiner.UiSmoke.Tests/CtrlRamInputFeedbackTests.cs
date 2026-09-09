using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>CtrlRAM size feedback renders the inspector's accepted and ignored ranges without another size policy.</summary>
public sealed class CtrlRamInputFeedbackTests
{
    /// <summary>Warning details use actual typed sizes across source sizes, languages and diagnostic names.</summary>
    [Theory]
    [InlineData(false, 655360, 23552, "640 KiB", "23 KiB", "617 KiB", "CTRLRAM_SIZE_WARNING")]
    [InlineData(true, 655360, 23552, "640 KiB", "23 KiB", "617 KiB", "CTRLRAM_SIZE_WARNING")]
    [InlineData(false, 4097, 4096, "4097 bytes", "4 KiB", "1 byte", "CUSTOM_TRAILING_WARNING")]
    [InlineData(true, 4097, 4096, "4097 bytes", "4 KiB", "1 byte", "CUSTOM_TRAILING_WARNING")]
    public void IgnoredTrailingWarningUsesTypedCounts(
        bool chinese, int actual, long accepted, string actualText, string acceptedText, string ignoredText, string code)
    {
        AuthoringInputSlotStatus status = StandardMergeFeedbackTests.Status(
            code, AuthoringSlotLifecycle.Warning, actual, accepted, ignoredTrailingBytes: true);
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(status, chinese);
        IssueCardViewModel card = Assert.IsType<IssueCardViewModel>(slot.IssueCard);
        Assert.Contains(actualText, card.Summary, StringComparison.Ordinal);
        Assert.Contains(acceptedText, card.Summary, StringComparison.Ordinal);
        Assert.Contains(ignoredText, card.Summary, StringComparison.Ordinal);
        Assert.Contains(chinese ? "尾端" : "Trailing", card.Summary, StringComparison.Ordinal);
        Assert.Contains(chinese ? "忽略" : "ignored", card.Summary, StringComparison.Ordinal);
        // Hover, keyboard and expanded status all use this current computed card,
        // not cached input-status text which outlives a file selection change.
        Assert.Contains(card.Summary, slot.SemanticStateAutomationText, StringComparison.Ordinal);
        Assert.DoesNotContain(code, card.Summary, StringComparison.Ordinal);
        Assert.Equal(code, card.DiagnosticCode);
        Assert.False(slot.BlocksBuild);
        Assert.Equal(actual, status.AcceptedBytes!.Value.Length);

        slot.ApplyExperienceText(ShellTextResources.For(chinese ? ShellLanguage.English : ShellLanguage.ChineseTraditional));
        Assert.Contains(chinese ? "Trailing" : "尾端", slot.IssueCard!.Summary, StringComparison.Ordinal);
        Assert.Contains(ignoredText, slot.IssueCard.Summary, StringComparison.Ordinal);
        slot.FilePath = @"C:\firmware\different.bin";
        Assert.DoesNotContain(ignoredText, slot.SemanticStateAutomationText, StringComparison.Ordinal);
    }

    /// <summary>A code or large file alone does not authorize an ignored-tail claim or change terminal severity.</summary>
    [Theory]
    [InlineData(false, AuthoringSlotLifecycle.Warning)]
    [InlineData(true, AuthoringSlotLifecycle.Warning)]
    [InlineData(false, AuthoringSlotLifecycle.Error)]
    public void SizeCodeWithoutTypedIgnoredRangeKeepsExistingMeaning(bool chinese, AuthoringSlotLifecycle lifecycle)
    {
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(StandardMergeFeedbackTests.Status(
            "CTRLRAM_SIZE_WARNING", lifecycle, 8192, 4096), chinese);
        Assert.DoesNotContain("ignored", slot.IssueCard!.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("忽略", slot.IssueCard.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("8 KiB", slot.IssueCard.Summary, StringComparison.Ordinal);
        Assert.Equal(lifecycle == AuthoringSlotLifecycle.Error, slot.BlocksBuild);
    }
}
