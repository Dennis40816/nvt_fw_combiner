// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Component text using the existing shell language selection.</summary>
internal sealed class OutputMapTextResources(ShellLanguage language)
{
    internal string Title => Pick("Output map", "輸出地圖");
    internal string ZoomTo => Pick("Zoom to", "放大至");
    internal string Hint => Pick("Hover or click a block, a region or a step row to zoom in.",
        "移到或點選區塊、區域或步驟列以放大檢視。");
    internal string Preview => Pick("Preview · click to pin", "預覽 · 點選以固定");
    internal string Pinned => Pick("Pinned · Esc clears", "已固定 · Esc 清除");
    internal string Issue => Pick("Issue", "問題");
    internal string ReadOnly => Pick("read-only", "唯讀");
    internal string Writable => Pick("read/write", "可讀寫");
    internal string Output => Pick("Output", "輸出");
    internal string Read => Pick("read", "讀取");
    internal string ZoomStrip => Pick("Output map zoom strip", "輸出地圖放大區");
    internal string Storage => Pick("storage", "儲存位置");
    internal string Warning => Pick("Warning", "警告");
    internal string Error => Pick("Error", "錯誤");
    internal string Kind(OutputMapBlockKind kind)
    {
        return kind switch
        {
            OutputMapBlockKind.Copy => Pick("Copy", "複製"),
            OutputMapBlockKind.Fill => Pick("Fill", "填滿"),
            OutputMapBlockKind.Patch => Pick("Patch", "修補"),
            OutputMapBlockKind.Script => Pick("Script", "腳本"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
    private string Pick(string english, string chinese)
    {
        return language == ShellLanguage.ChineseTraditional ? chinese : english;
    }
}
