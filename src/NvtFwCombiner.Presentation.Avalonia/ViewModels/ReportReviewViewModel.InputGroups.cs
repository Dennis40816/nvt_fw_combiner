namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReportReviewViewModel
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
            ReportInputClassifications.Base,
            T(language, "Base image", "基準映像"),
            T(language, "Reference firmware copied before replacement.", "Replace 前複製的 reference firmware。"),
            language);
        AddInputGroup(
            groups,
            inputs,
            ReportInputClassifications.CtrlRam,
            T(language, "CtrlRAM replacement BINs", "CtrlRAM 替換 BIN"),
            T(language, "Region-specific payloads used by the replace workflow.", "Replace 流程使用的區域 payload。"),
            language);
        AddInputGroup(
            groups,
            inputs,
            ReportInputClassifications.Other,
            T(language, "Other inputs", "其他輸入"),
            T(language, "Additional source files recorded by the report.", "Report 記錄的其他來源檔案。"),
            language);
        return groups;
    }

    private static void AddInputGroup(
        List<ReportInputGroupViewModel> groups,
        IReadOnlyList<ReportLineViewModel> inputs,
        string classification,
        string title,
        string detail,
        ShellLanguage language)
    {
        ReportLineViewModel[] rows = [.. inputs.Where(input =>
            string.Equals(input.Classification, classification, StringComparison.Ordinal))];
        if (rows.Length == 0)
        {
            return;
        }

        groups.Add(new ReportInputGroupViewModel(
            title,
            detail,
            rows,
            T(language, "Size", "大小"),
            T(language, "Address space", "位址空間")));
    }
}
