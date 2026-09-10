using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Generic declared processing must not claim a specific CRC algorithm or effect.</summary>
public sealed class MemoryPostprocessingTextTests
{
    /// <summary>Both generic processing actions use consistent wording while ordinary actions remain distinct.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericProcessorLabelsDoNotClaimCrc(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        Assert.Equal(chinese ? "替換 + 後處理" : "Replace + postprocess", text.GetMemoryPlanActionLabel(MemoryPlanActionKind.ReplaceAndCrc));
        Assert.Equal(chinese ? "後處理" : "Postprocess", text.GetMemoryPlanActionLabel(MemoryPlanActionKind.Postbuild));
        Assert.Equal(chinese ? "替換" : "Replace", text.GetMemoryPlanActionLabel(MemoryPlanActionKind.Replace));
        Assert.Equal(chinese ? "複製" : "Copy", text.GetMemoryPlanActionLabel(MemoryPlanActionKind.Copy));
        Assert.Equal(chinese ? "還原" : "Restore", text.GetMemoryPlanActionLabel(MemoryPlanActionKind.Restore));
    }
}
