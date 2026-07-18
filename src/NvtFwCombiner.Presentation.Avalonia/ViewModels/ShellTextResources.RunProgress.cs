using System.Globalization;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
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
