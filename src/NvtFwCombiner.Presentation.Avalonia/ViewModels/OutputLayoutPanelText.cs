// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Component labels follow the shell language; supplied firmware descriptions stay opaque.</summary>
internal sealed class OutputLayoutPanelText(ShellTextResources shell)
{
    internal ShellTextResources Shell => shell;
    internal string Title => Pick("Output layout", "輸出配置");
    internal string Kept => Pick("Kept from base", "保留基底內容");
    internal string NotWritten => Pick("Not written", "不寫入");
    internal string Context => Pick("Context", "相關內容");
    internal string ScaleUnavailable => Pick("A shared scale is unavailable; all ranges remain in the list below.",
        "無法顯示共用刻度；所有範圍仍列於下方清單。");
    internal string PinCluster(int count) => Pick($"{count} nearby markers", $"{count} 個相鄰標記");
    internal string Warning => Pick("Warning", "警告");
    internal string Conflict => Pick("Conflict", "衝突");
    internal string Partial => Pick("Partial", "部分替換");
    internal string ShowWarning => Pick("Show warning in Issues", "在問題清單看這則警告");
    internal string SmallBlocks(int count)
    {
        return Pick($"{count} small blocks", $"{count} 個小區塊");
    }

    internal string Group(OutputLayoutGroupKind kind, MemoryWorkflowDisposition write)
    {
        return kind switch
        {
            OutputLayoutGroupKind.Write => shell.GetOutputLayoutStateLabel(write, MemoryObservedChange.NotObserved),
            OutputLayoutGroupKind.Kept => Kept,
            OutputLayoutGroupKind.NotWritten => NotWritten,
            OutputLayoutGroupKind.Context => Context,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    internal string Issues(int warnings, int errors)
    {
        var parts = new List<string>();
        if (errors > 0) { parts.Add(Pick($"{errors} {(errors == 1 ? "error" : "errors")}", $"{errors} 則錯誤")); }
        if (warnings > 0) { parts.Add(Pick($"{warnings} {(warnings == 1 ? "warning" : "warnings")}", $"{warnings} 則警告")); }
        return string.Join(" · ", parts);
    }
    internal string Badge(MemoryCoverageSegmentViewModel segment)
    {
        return segment.FillRole == MemoryCoverageFillRole.Conflict ? Conflict
            : segment.DiagnosticSeverity == MemoryDiagnosticSeverity.Warning ? Warning
            : segment.ObservedChange != MemoryObservedChange.NotObserved
                ? shell.GetOutputLayoutStateLabel(segment.Disposition, segment.ObservedChange)
                : MemoryCoverageBarProjection.IsPartialReplacement(segment.DisplayParts) ? Partial : string.Empty;
    }
    private string Pick(string english, string chinese)
    {
        return shell.Language == ShellLanguage.ChineseTraditional ? chinese : english;
    }
}
