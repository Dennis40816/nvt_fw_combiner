namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static List<ReportInputGroupViewModel> CreateInputGroups(
        IReadOnlyList<ReportLineViewModel> inputs,
        ShellLanguage language)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        List<ReportInputGroupViewModel> groups = [];
        AddInputGroup(
            groups,
            inputs,
            "base",
            T(language, "Base image", "基準映像"),
            T(language, "Reference firmware copied before replacement.", "Replace 前複製的 reference firmware。"));
        AddInputGroup(
            groups,
            inputs,
            "ctrlram",
            T(language, "CtrlRAM replacement BINs", "CtrlRAM 替換 BIN"),
            T(language, "Region-specific payloads used by the replace workflow.", "Replace 流程使用的區域 payload。"));
        AddInputGroup(
            groups,
            inputs,
            "other",
            T(language, "Other inputs", "其他輸入"),
            T(language, "Additional source files recorded by the report.", "Report 記錄的其他來源檔案。"));
        return groups;
    }

    private static void AddInputGroup(
        List<ReportInputGroupViewModel> groups,
        IReadOnlyList<ReportLineViewModel> inputs,
        string classification,
        string title,
        string detail)
    {
        ReportLineViewModel[] rows = [.. inputs.Where(input =>
            string.Equals(input.Classification, classification, StringComparison.Ordinal))];
        if (rows.Length == 0)
        {
            return;
        }

        groups.Add(new ReportInputGroupViewModel(title, detail, rows));
    }
}
