// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Standalone output-layout text using the existing shell language selection.</summary>
internal sealed class OutputLayoutPartsTextResources(ShellLanguage language)
{
    internal static OutputLayoutPartsTextResources From(object? labels)
    {
        return labels as OutputLayoutPartsTextResources ?? new(
            (labels as ShellTextResources)?.Language ?? ShellLanguage.English);
    }

    internal string Zoom => Pick("CtrlRAM detail", "CtrlRAM 放大");
    internal string BankView => Pick("Bank view", "Bank 檢視");
    internal string SharedInput => Pick("Shared inputs", "共用輸入");
    internal string Target => Pick("Target Addr", "目標位址");
    internal string Size => Pick("Size", "大小");
    internal string Source => Pick("Source", "來源");
    internal string PositionHint => Pick("Select a block to inspect its details.", "選取區塊以查看細節。");

    private string Pick(string english, string chinese)
    {
        return language == ShellLanguage.ChineseTraditional ? chinese : english;
    }
}
