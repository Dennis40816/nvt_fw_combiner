// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Approved confirmation wording, using the existing shell language selection.</summary>
internal sealed class RecalculationConfirmTextResources(ShellLanguage language)
{
    public string Title => Pick("Recalculate header and CRC before output?", "重算 header 與 CRC 再輸出？");
    public string Details => Pick("Details", "詳細說明");
    public string Why => Pick("Why recalculate", "為什麼要重算");
    public string WhyText => Pick(
        "After a segment is replaced, the header size, checksums and CRC no longer match the new content. Flashing or the boot check may fail.",
        "換掉段落後，header 的大小、檢查碼與 CRC 會和新內容對不上，\n燒錄或開機檢查可能失敗。");
    public string Range => Pick("What is recalculated", "重算範圍");
    public string Storage => Pick("CRC stored at", "CRC 存放位置");
    public string Coverage => Pick("CRC covers", "涵蓋範圍");
    public string UnknownCoverage => Pick("Decided by Combiner. The IC file does not declare it yet.",
        "涵蓋範圍由 Combiner 決定，IC 檔尚未宣告");
    public string NotDeclared => Pick("Not declared", "未宣告");
    public string Versions => Pick("Versions", "版本");
    public string CommandMode => Pick("Command mode", "指令模式");
    public string SavedChoice => Pick("Your choice is saved in this Saved Rule and preselected at the next build.",
        "你的選擇會存進這個 Saved Rule，下次建置時預選。");
    public string Cancel => Pick("Cancel", "取消");
    public string Skip => Pick("Write without recalculating", "不重算，照寫輸出");
    public string Recalculate => Pick("Recalculate and output", "重算並輸出");
    public string NoRuleTitle => Pick("This IC has no recalculation rule. The output is not recalculated.",
        "這個 IC 沒有重算規則，輸出不會重算");
    public string NoRuleText => Pick(
        "The header and CRC may not match the new content. Flashing or the boot check may fail.",
        "header 與 CRC 可能和新內容對不上，燒錄或開機檢查可能失敗。");
    public string Write => Pick("Write output as is", "照寫輸出");
    public string ListSeparator => Pick(", ", "、");
    public string SegmentPrefix => Pick("Your segment ", "你的段落 ");

    public string SegmentMiddle(int count)
    {
        string noun = count == 1 ? "word" : "words";
        return Pick(string.Format(CultureInfo.InvariantCulture, " covers {0} CRC {1}: ", count, noun), " 蓋到 ");
    }

    public string SegmentSuffix(int count)
    {
        return Pick(". Recalculation overwrites your bytes there.",
            string.Format(CultureInfo.InvariantCulture, " 這 {0} 個 CRC，重算會覆蓋你寫的值。", count));
    }

    private string Pick(string english, string chinese)
    {
        return language == ShellLanguage.ChineseTraditional ? chinese : english;
    }
}
