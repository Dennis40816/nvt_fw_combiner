using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Initialization text never claims to describe final bytes after compiled writes.</summary>
public sealed class MemoryInitializationTextTests
{
    /// <summary>Known initialization remains explicitly pre-write, with or without a contributing operation.</summary>
    [Theory]
    [InlineData(false, 255, false)]
    [InlineData(false, 0, true)]
    [InlineData(true, 255, true)]
    [InlineData(true, 0, false)]
    public void FillValueIsExplicitlyBeforeWrites(bool chinese, int value, bool writes)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        CompositionOperation[] operations = writes
            ? [CompositionOperation.CopyRange("copy-source", 0, "input", new ByteRange(0, 16), "output", new ByteRange(0, 16), OverlapPolicy.Reject, "test copy")]
            : [];
        string detail = text.FormatMemoryLayoutTechnicalDetail("region", (byte)value, operations);
        string expected = chinese
            ? $"區域 ID: region\n輸出初始化: 0x{value:X2}（寫入前）\n編譯操作: " + (writes ? "copy-source（順序 0）" : "沒有編譯操作寫入此範圍。")
            : $"Region ID: region\nInitialization: 0x{value:X2} (before writes)\nOperation: " + (writes ? "copy-source (Sequence 0)" : "No compiled operation writes this range.");
        Assert.Equal(expected, detail);
        Assert.Equal(3, text.FormatMemoryLayoutTechnicalFacts("region", (byte)value, operations).Count);
    }

    /// <summary>Reference initialization must not invent a fill value when none is published.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingFillValueDoesNotInventBlankBytes(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        Assert.Equal(chinese ? "區域 ID: region\n編譯操作: 沒有編譯操作寫入此範圍。" : "Region ID: region\nOperation: No compiled operation writes this range.", text.FormatMemoryLayoutTechnicalDetail("region", null, []));
        Assert.Equal(2, text.FormatMemoryLayoutTechnicalFacts("region", null, []).Count);
    }
}
