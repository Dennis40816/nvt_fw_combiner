using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Typed input keys preserve the existing complete input-card text in both languages.</summary>
public sealed class InputCardLabelTests
{
    /// <summary>AB titles and descriptions retain exact punctuation before and after size discovery.</summary>
    [Theory]
    [InlineData(false, "dp-ab", "DP_AB BIN", "Complete two-bank DP container.", "Complete two-bank DP container. Required prefix: 220 KiB.")]
    [InlineData(false, "tp-a", "TPA BIN", "Touch payload for bank A.", "Touch payload for bank A. Required prefix: 220 KiB.")]
    [InlineData(false, "tp-b", "TPB BIN", "Touch payload for bank B.", "Touch payload for bank B; relocation uses the compiled plan. Required prefix: 220 KiB.")]
    [InlineData(true, "dp-ab", "DP_AB BIN", "完整雙 bank DP container。", "完整雙 bank DP container；必要 prefix：220 KiB。")]
    [InlineData(true, "tp-a", "TPA BIN", "Bank A 的 Touch payload。", "Bank A 的 Touch payload；必要 prefix：220 KiB。")]
    [InlineData(true, "tp-b", "TPB BIN", "Bank B 的 Touch payload。", "Bank B 的 Touch payload；relocation 由 compiled plan 定義。必要 prefix：220 KiB。")]
    public void AbCardTextIsUnchanged(bool chinese, string role, string title, string pendingPrefix, string compiled)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var input = new CompiledAuthoringInputBinding("slot", "space", role);
        string suffix = chinese ? " 偵測格式後顯示尺寸需求。" : " Size requirements are available after format detection.";

        Assert.Equal(title, ShellTextResources.GetAbSlotTitle(input.RoleKind, input.Role));
        Assert.Equal(pendingPrefix + suffix, text.GetAbSlotDescription(input));
        Assert.Equal(compiled, text.GetAbSlotDescription(input with { RequiredEndExclusive = 0x37000 }));
    }

    /// <summary>Required-list labels remain compact and unknown or unrelated identities retain the exact raw id.</summary>
    [Theory]
    [InlineData("dp-input", "DP")]
    [InlineData("tp-input", "TP")]
    [InlineData("ldc-input", "LDC")]
    [InlineData("custom-input", "custom-input")]
    [InlineData("DP-INPUT", "DP-INPUT")]
    [InlineData("reference-base", "reference-base")]
    [InlineData("dp-ab-input", "dp-ab-input")]
    public void RequiredInputLabelsPreserveCompactTextAndRawFallback(string id, string expected)
    {
        Assert.Equal(expected, ShellTextResources.GetRequiredInputLabel(MemoryLayoutProjector.GetArtifactKind(id), id));
    }
}
