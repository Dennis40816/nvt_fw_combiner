using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Approved Build messages preserve Application blocker priority, details and availability.</summary>
public sealed class ApprovedBuildMessageTests
{
    /// <summary>Every applied blocker code projects the exact sentence while retaining its original technical detail.</summary>
    [Theory]
    [InlineData(false, "AB_FORMAT_CONFIGURATION_INVALID", CapabilityReadinessDimension.Configuration, CapabilityReadinessNextAction.ReviewConfiguration, "Event Buffer Format settings are invalid or unreadable; review and save them in Settings.")]
    [InlineData(true, "AB_FORMAT_CONFIGURATION_INVALID", CapabilityReadinessDimension.Configuration, CapabilityReadinessNextAction.ReviewConfiguration, "Event Buffer Format 設定無效或無法讀取，請到「設定」檢查並儲存。")]
    [InlineData(false, "capability.readiness.authoring-unavailable", CapabilityReadinessDimension.Authoring, CapabilityReadinessNextAction.SelectAvailableRoute, "The selected workflow is unavailable; choose another workflow.")]
    [InlineData(true, "capability.readiness.authoring-unavailable", CapabilityReadinessDimension.Authoring, CapabilityReadinessNextAction.SelectAvailableRoute, "所選工作流程目前無法使用，請選擇其他工作流程。")]
    [InlineData(false, "capability.readiness.execution-not-admitted", CapabilityReadinessDimension.Execution, CapabilityReadinessNextAction.ReviewCompilation, "The selected workflow cannot run; review the Build status or choose another workflow.")]
    [InlineData(true, "capability.readiness.execution-not-admitted", CapabilityReadinessDimension.Execution, CapabilityReadinessNextAction.ReviewCompilation, "所選工作流程無法執行，請查看 Build 狀態或選擇其他工作流程。")]
    [InlineData(false, "capability.readiness.input-blocked", CapabilityReadinessDimension.Input, CapabilityReadinessNextAction.CorrectInput, "An input is blocking Build; review the input cards and their required actions.")]
    [InlineData(true, "capability.readiness.input-blocked", CapabilityReadinessDimension.Input, CapabilityReadinessNextAction.CorrectInput, "有輸入阻擋 Build，請查看輸入卡及其處理指示。")]
    [InlineData(false, "capability.readiness.input-pending", CapabilityReadinessDimension.Input, CapabilityReadinessNextAction.LoadRequiredInput, "Build prerequisites are not ready; check the workflow selection and required inputs.")]
    [InlineData(true, "capability.readiness.input-pending", CapabilityReadinessDimension.Input, CapabilityReadinessNextAction.LoadRequiredInput, "Build 所需條件尚未就緒，請確認工作流程選擇及必要輸入。")]
    [InlineData(false, "capability.readiness.postbuild-stage-authority-missing", CapabilityReadinessDimension.Execution, CapabilityReadinessNextAction.ReviewCompilation, "General Replace cannot prepare the required postbuild step; review the selected IC, base BIN, and mappings.")]
    [InlineData(true, "capability.readiness.postbuild-stage-authority-missing", CapabilityReadinessDimension.Execution, CapabilityReadinessNextAction.ReviewCompilation, "General Replace 無法準備必要的後處理步驟，請檢查所選 IC、基底 BIN 及對應。")]
    [InlineData(false, "capability.readiness.runtime-dependency-blocked", CapabilityReadinessDimension.RuntimeDependency, CapabilityReadinessNextAction.RefreshRuntimeDependencies, "A required external tool is not ready; select Refresh under System activity in Message Center.")]
    [InlineData(true, "capability.readiness.runtime-dependency-blocked", CapabilityReadinessDimension.RuntimeDependency, CapabilityReadinessNextAction.RefreshRuntimeDependencies, "必要的外部工具尚未就緒，請在訊息中心的「系統活動」按「重新整理」。")]
    [InlineData(false, "capability.readiness.runtime-snapshot-stale", CapabilityReadinessDimension.RuntimeDependency, CapabilityReadinessNextAction.RefreshRuntimeDependencies, "External tool status is not current; select Refresh under System activity in Message Center.")]
    [InlineData(true, "capability.readiness.runtime-snapshot-stale", CapabilityReadinessDimension.RuntimeDependency, CapabilityReadinessNextAction.RefreshRuntimeDependencies, "外部工具狀態尚未更新，請在訊息中心的「系統活動」按「重新整理」。")]
    public void BlockerSentencePreservesAvailability(bool chinese, string code,
        CapabilityReadinessDimension dimension, CapabilityReadinessNextAction nextAction, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var blocker = new CapabilityActionBlocker(code, dimension, "first", "Original detail: 123 bytes, tool.exe", nextAction);
        CapabilityActionBlocker next = blocker with { SubjectId = "second", Message = "Second detail: 456 bytes" };
        var availability = new CapabilityActionAvailability([new RankedBlocker(1, next), new RankedBlocker(0, blocker)]);
        ShellTextResources text = ShellTextResources.For(language);

        IssueCardViewModel card = IssueCardViewModel.FromBuildAvailability(availability, [], text, false, string.Empty);

        Assert.Equal(expected, text.FormatCapabilityActionBlocker(blocker));
        Assert.Equal(expected, card.Summary);
        bool specific = code is "capability.readiness.input-blocked" or
            "capability.readiness.postbuild-stage-authority-missing" or
            "capability.readiness.runtime-dependency-blocked";
        string detail = language == ShellLanguage.ChineseTraditional ? blocker.SubjectId : blocker.Message;
        Assert.Equal(specific ? detail : string.Empty, card.Action);
        Assert.Equal(code, card.DiagnosticCode);
        Assert.True(card.IsError);
        Assert.True(card.IsBuildStatus);
        Assert.False(availability.IsAvailable);
        Assert.Same(blocker, availability.PrimaryBlocker);
        Assert.Collection(availability.Blockers, first => Assert.Same(blocker, first), second => Assert.Same(next, second));
        Assert.Equal(1, card.AdditionalBlockerCount);
        Assert.Contains(expected, card.AdditionalBlockerText, StringComparison.Ordinal);
        string nextDetail = language == ShellLanguage.ChineseTraditional ? next.SubjectId : next.Message;
        Assert.Equal(specific, card.AdditionalBlockerText.Contains(nextDetail, StringComparison.Ordinal));
        Assert.Equal(nextAction, blocker.NextAction);
        Assert.Equal(dimension, blocker.Dimension);
    }

    /// <summary>The catalog diagnostic keeps its blocking severity and renders the complete sentence once.</summary>
    [Theory]
    [InlineData(false, "The capability catalog is unavailable, blocking Build; select Refresh under System activity in Message Center.")]
    [InlineData(true, "功能目錄無法使用而阻擋 Build，請在訊息中心的「系統活動」按「重新整理」。")]
    public void CatalogSentencePreservesBlockingSeverity(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var diagnostic = new ActionableSystemDiagnostic(SystemDiagnosticCodes.CapabilityCatalogUnavailable,
            SystemDiagnosticCategory.CapabilityCatalog, SystemDiagnosticSeverity.Blocking,
            "The capability catalog is unavailable, so Build is disabled.", "Correct the catalog source and reload.");
        ShellTextResources text = ShellTextResources.For(language);
        string message = text.GetSystemDiagnosticMessage(diagnostic);

        Assert.Equal(expected, message);
        Assert.Empty(text.GetSystemDiagnosticAction(diagnostic));
        Assert.Equal(SystemDiagnosticSeverity.Blocking, diagnostic.Severity);
        IssueCardViewModel card = IssueCardViewModel.FromBuildAvailability(
            new CapabilityActionAvailability([]), [], text, true, message);
        Assert.Equal(expected, card.Summary);
        Assert.Empty(card.Action);
        Assert.True(card.IsError);
    }

    /// <summary>AB readiness sentences preserve selected, error and warning counts in the existing detail.</summary>
    [Theory]
    [InlineData(false, false, 0, 0, 0, "AB Code is unavailable for this IC; select another IC or workflow.")]
    [InlineData(true, false, 0, 0, 0, "此 IC 無法使用 AB Code，請選擇其他 IC 或工作流程。")]
    [InlineData(false, true, 3, 1, 0, "An input is blocking Build; review the input cards and select compatible BINs.")]
    [InlineData(true, true, 3, 1, 0, "有輸入阻擋 Build，請查看輸入卡並選擇相容的 BIN。")]
    [InlineData(false, true, 1, 0, 0, "Required inputs are missing; select the BINs for the empty input cards.")]
    [InlineData(true, true, 1, 0, 0, "缺少必要輸入，請為空白輸入卡選擇所需 BIN。")]
    [InlineData(false, true, 3, 0, 2, "Inputs have warnings; review the highlighted files before Build.")]
    [InlineData(true, true, 3, 0, 2, "輸入有警告，請在 Build 前確認標示的檔案。")]
    public void AbReadinessSentenceKeepsCounts(bool chinese, bool supported,
        int selected, int blocking, int warnings, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        string message = ShellTextResources.For(language).GetAbMergeReadinessStatus(
            "NT51929", supported, selected, 3, blocking, warnings);

        Assert.Equal(expected, message.Split(Environment.NewLine)[0]);
        string fact = !supported ? "NT51929" : warnings > 0 ? $"{warnings} " : $"{selected}/3";
        Assert.Contains(fact, message.Split(Environment.NewLine)[1], StringComparison.Ordinal);
    }

    /// <summary>Replace readiness uses the approved blocked sentence and preserves the ready branch.</summary>
    [Theory]
    [InlineData(false, "ctrlram-replace", "CtrlRAM Replace is not ready; check the base BIN, CtrlRAM inputs, and Build status.")]
    [InlineData(true, "ctrlram-replace", "CtrlRAM Replace 尚未就緒，請檢查基底 BIN、CtrlRAM 輸入及 Build 狀態。")]
    [InlineData(false, "general-replace", "General Replace is not ready; check the base BIN, mappings, and Build status.")]
    [InlineData(true, "general-replace", "General Replace 尚未就緒，請檢查基底 BIN、對應及 Build 狀態。")]
    public void ReplaceReadinessSentencePreservesReadyBranch(bool chinese, string mode, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        ShellTextResources text = ShellTextResources.For(language);

        Assert.Equal(expected, text.GetReplaceReadinessStatus(mode, false));
        Assert.NotEqual(expected, text.GetReplaceReadinessStatus(mode, true));
        Assert.StartsWith(language == ShellLanguage.ChineseTraditional ? "已就緒：" : "Ready:",
            text.GetReplaceReadinessStatus(mode, true), StringComparison.Ordinal);
    }

    /// <summary>Unavailable Replace retains the current IC identity below its approved sentence.</summary>
    [Theory]
    [InlineData(false, "Replace is unavailable for the selected IC and mode; choose another IC or mode.")]
    [InlineData(true, "所選 IC 與模式無法使用 Replace，請選擇其他 IC 或模式。")]
    public void ReplaceUnavailableSentenceKeepsIc(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        string message = ShellTextResources.For(language).GetReplaceNotSupportedStatus("NT51929");
        Assert.Equal(expected + Environment.NewLine + "NT51929", message);
    }

    /// <summary>Unrecognized codes use the original message and typed next action without guessing a cause.</summary>
    [Theory]
    [InlineData(false, "Load the required input.")]
    [InlineData(true, "請載入必要輸入。")]
    public void UnknownBlockerPreservesOriginal(bool chinese, string action)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var blocker = new CapabilityActionBlocker("unknown", CapabilityReadinessDimension.Input, "input",
            "Opaque detail 123 bytes", CapabilityReadinessNextAction.LoadRequiredInput);
        ShellTextResources text = ShellTextResources.For(language);
        Assert.Equal(blocker.Message + " " + action, text.FormatCapabilityActionBlocker(blocker));
        IssueCardViewModel card = IssueCardViewModel.FromBuildAvailability(
            new CapabilityActionAvailability([new RankedBlocker(0, blocker)]), [], text, false, string.Empty);
        Assert.Empty(card.Action);
    }
}
