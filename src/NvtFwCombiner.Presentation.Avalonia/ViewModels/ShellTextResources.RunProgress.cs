using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    /// <summary>Returns the localized label for an Application-owned composition phase.</summary>
    public string GetCompositionRunPhaseLabel(CompositionRunPhase phase)
    {
        return (Language, phase) switch
        {
            (ShellLanguage.English, CompositionRunPhase.Preparing) => "Preparing run",
            (ShellLanguage.English, CompositionRunPhase.ReadingInputs) => "Reading input files",
            (ShellLanguage.English, CompositionRunPhase.ExecutingComposition) => "Executing composition",
            (ShellLanguage.English, CompositionRunPhase.RunningExternalProcessor) => "Running external processor",
            (ShellLanguage.English, CompositionRunPhase.ValidatingOutput) => "Validating output",
            (ShellLanguage.English, CompositionRunPhase.CommittingOutput) => "Committing output",
            (ShellLanguage.English, CompositionRunPhase.PreparingReport) => "Preparing report",
            (ShellLanguage.ChineseTraditional, CompositionRunPhase.Preparing) => "準備執行",
            (ShellLanguage.ChineseTraditional, CompositionRunPhase.ReadingInputs) => "讀取輸入檔案",
            (ShellLanguage.ChineseTraditional, CompositionRunPhase.ExecutingComposition) => "執行韌體合成",
            (ShellLanguage.ChineseTraditional, CompositionRunPhase.RunningExternalProcessor) => "執行外部處理器",
            (ShellLanguage.ChineseTraditional, CompositionRunPhase.ValidatingOutput) => "驗證輸出",
            (ShellLanguage.ChineseTraditional, CompositionRunPhase.CommittingOutput) => "寫入輸出檔案",
            (ShellLanguage.ChineseTraditional, CompositionRunPhase.PreparingReport) => "準備報告",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
        };
    }

    /// <summary>Returns the localized post-commit label while the report completes on a worker.</summary>
    public string GetCompositionArtifactCommittedLabel()
    {
        return Language == ShellLanguage.ChineseTraditional
            ? "輸出已就緒，正在背景整理報告"
            : "Output ready; preparing report in background";
    }

    /// <summary>Returns the localized label after the complete report becomes reviewable.</summary>
    public string GetCompositionReportReadyLabel()
    {
        return Language == ShellLanguage.ChineseTraditional
            ? "報告已就緒"
            : "Report ready";
    }

    /// <summary>Returns the localized terminal label when a committed output has no reviewable report.</summary>
    public string GetCompositionReportUnavailableLabel()
    {
        return SelectLanguage(
            "Output ready; report unavailable",
            "輸出已就緒，報告無法使用");
    }

    /// <summary>Gets the run-result title for a committed Build output whose report is unavailable.</summary>
    internal string CommittedOutputReportUnavailableTitle => SelectLanguage(
        "Build output committed; report unavailable",
        "Build 輸出已寫入，報告無法使用");

    /// <summary>Gets the report failure shown when cancellation arrives after the output commit.</summary>
    internal string CommittedOutputCancelledReportFailure => SelectLanguage(
        "Cancelled after output commit.",
        "輸出寫入後已取消。");

    /// <summary>Formats the committed output receipt that remains visible without its report.</summary>
    internal string FormatCommittedOutputReportUnavailableDetail(
        long outputSize,
        string outputSha256,
        string reportFailure)
    {
        return SelectLanguage(
            string.Create(
                CultureInfo.CurrentCulture,
                $"{outputSize} bytes / SHA-256 {outputSha256}. Report unavailable: {reportFailure}"),
            string.Create(
                CultureInfo.CurrentCulture,
                $"{outputSize} 位元組 / SHA-256 {outputSha256}。報告無法使用：{reportFailure}"));
    }

    /// <summary>Formats the lifecycle ordinal without presenting it as byte completion.</summary>
    public string FormatCompositionRunStepOrdinal(int currentStep, int stepCount)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? string.Create(CultureInfo.CurrentCulture, $"步驟 {currentStep}/{stepCount}")
            : string.Create(CultureInfo.CurrentCulture, $"Step {currentStep} of {stepCount}");
    }

    /// <summary>Formats the screen-reader live status for one Application phase transition.</summary>
    public string FormatCompositionRunProgressStatus(int currentStep, int stepCount, string phaseLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseLabel);
        return string.Create(
            CultureInfo.CurrentCulture,
            $"{FormatCompositionRunStepOrdinal(currentStep, stepCount)}: {phaseLabel}");
    }

    internal string FormatCompositionRunStepAccessibleLabel(
        string phaseLabel,
        CompositionRunProgressStepState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseLabel);
        string stateLabel = (Language, state) switch
        {
            (ShellLanguage.English, CompositionRunProgressStepState.Pending) => "pending",
            (ShellLanguage.English, CompositionRunProgressStepState.Active) => "in progress",
            (ShellLanguage.English, CompositionRunProgressStepState.Completed) => "completed",
            (ShellLanguage.ChineseTraditional, CompositionRunProgressStepState.Pending) => "尚未開始",
            (ShellLanguage.ChineseTraditional, CompositionRunProgressStepState.Active) => "執行中",
            (ShellLanguage.ChineseTraditional, CompositionRunProgressStepState.Completed) => "已完成",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
        return string.Create(CultureInfo.CurrentCulture, $"{phaseLabel}: {stateLabel}");
    }
}
