using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Approved input messages preserve typed terminal health and diagnostic details.</summary>
public sealed class ApprovedInputMessageTests
{
    /// <summary>Each applied inspection branch renders its exact bilingual sentence without changing health.</summary>
    [Theory]
    [InlineData(false, "firmware-config.chip-count-required", AuthoringSlotLifecycle.Error, false, "The TP firmware IC count is zero; select a BIN with a valid IC count.")]
    [InlineData(true, "firmware-config.chip-count-required", AuthoringSlotLifecycle.Error, false, "TP 韌體的 IC 數量為零，請選擇 IC 數量有效的 BIN。")]
    [InlineData(false, "firmware-config.chip-count-unreadable", AuthoringSlotLifecycle.Error, false, "The TP firmware IC count could not be read; select a BIN with valid firmware configuration.")]
    [InlineData(true, "firmware-config.chip-count-unreadable", AuthoringSlotLifecycle.Error, false, "無法讀取 TP 韌體的 IC 數量，請選擇韌體設定有效的 BIN。")]
    [InlineData(false, "input.inspection.extension-not-accepted", AuthoringSlotLifecycle.Error, true, "This file extension is not accepted; select a file with an accepted extension.")]
    [InlineData(true, "input.inspection.extension-not-accepted", AuthoringSlotLifecycle.Error, true, "不接受此副檔名，請選擇使用允許副檔名的檔案。")]
    [InlineData(false, "input.inspection.source-unreadable", AuthoringSlotLifecycle.Error, true, "The selected BIN could not be read; select a readable local BIN.")]
    [InlineData(true, "input.inspection.source-unreadable", AuthoringSlotLifecycle.Error, true, "無法讀取所選 BIN，請選擇可讀取的本機 BIN。")]
    [InlineData(false, "OTHER_ERROR", AuthoringSlotLifecycle.Error, false, "The selected BIN does not meet the input requirements; select a compatible BIN.")]
    [InlineData(true, "OTHER_ERROR", AuthoringSlotLifecycle.Error, false, "所選 BIN 不符合輸入要求，請選擇相容的 BIN。")]
    [InlineData(false, "OTHER_WARNING", AuthoringSlotLifecycle.Warning, false, "The selected BIN has a warning; review its input status before Build.")]
    [InlineData(true, "OTHER_WARNING", AuthoringSlotLifecycle.Warning, false, "所選 BIN 有警告，請在 Build 前查看其輸入狀態。")]
    public void InspectionSentencePreservesHealth(bool chinese, string code,
        AuthoringSlotLifecycle lifecycle, bool unreadable, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        AuthoringInputSlotStatus status = Status(code, lifecycle, unreadable);
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(status, language == ShellLanguage.ChineseTraditional);

        Assert.Equal(expected, slot.InputInspectionStatus);
        Assert.Equal(expected, slot.IssueCard!.Summary);
        Assert.Equal(code, slot.IssueCard.DiagnosticCode);
        Assert.Equal(lifecycle, slot.InputIssueStatus!.InspectionLifecycle);
        Assert.Equal(lifecycle == AuthoringSlotLifecycle.Error, slot.BlocksBuild);
        Assert.Equal(lifecycle == AuthoringSlotLifecycle.Error, slot.IssueCard.IsError);
    }

    /// <summary>Short-file summary and action share one approved sentence while lengths stay in the detail.</summary>
    [Theory]
    [InlineData(false, "The selected BIN is too short; select a complete, compatible BIN.")]
    [InlineData(true, "所選 BIN 太短，請選擇完整且相容的 BIN。")]
    public void ShortInputKeepsNumericDetailAndOneSentence(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(StandardMergeFeedbackTests.Status(
            "input.source-view.incomplete", AuthoringSlotLifecycle.Error, 262143, 262144),
            language == ShellLanguage.ChineseTraditional);
        IssueCardViewModel card = Assert.IsType<IssueCardViewModel>(slot.IssueCard);

        Assert.Equal(expected, card.Summary);
        Assert.Equal(1, card.AutomationText.Split(expected, StringSplitOptions.None).Length - 1);
        Assert.Contains("≥ 256 KiB", card.Action, StringComparison.Ordinal);
        Assert.True(slot.BlocksBuild);
        Assert.True(card.IsError);
    }

    /// <summary>Typed report evidence retains all lengths and ranges under the approved short-file title.</summary>
    [Theory]
    [InlineData(false, "The selected BIN is too short; select a complete, compatible BIN.")]
    [InlineData(true, "所選 BIN 太短，請選擇完整且相容的 BIN。")]
    public void ShortReportHelpKeepsTypedFacts(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        ShellTextResources text = ShellTextResources.For(language);
        var evidence = new InputDiagnosticEvidence("source", 1, 2, new ByteRange(0, 2), null);
        (string title, string detail) = text.GetInputIssueHelp("input.source-view.incomplete", "error", evidence)!.Value;

        Assert.EndsWith(" (≥ 2 bytes) · " + expected, title, StringComparison.Ordinal);
        Assert.DoesNotContain(expected, detail, StringComparison.Ordinal);
        Assert.Contains("[0x0, 0x2)", detail, StringComparison.Ordinal);
        Assert.Contains("1 bytes", detail, StringComparison.Ordinal);
        Assert.Contains("2 bytes", detail, StringComparison.Ordinal);
    }

    /// <summary>Generic input errors retain the approved action and the existing blocking impact.</summary>
    [Theory]
    [InlineData(false, "The selected BIN has an input error; review its status and select a compatible BIN.")]
    [InlineData(true, "所選 BIN 有輸入錯誤，請查看其狀態並選擇相容的 BIN。")]
    public void GenericInputErrorKeepsBlockingAction(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        FirmwareSlotViewModel slot = StandardMergeFeedbackTests.Slot(StandardMergeFeedbackTests.Status(
            "OTHER_ERROR", AuthoringSlotLifecycle.Error), language == ShellLanguage.ChineseTraditional);

        Assert.Equal(expected, slot.IssueCard!.Action);
        Assert.True(slot.IssueCard.IsError);
        Assert.True(slot.BlocksBuild);
    }

    /// <summary>The stale-file text and generic blocked action preserve the producer's blocking state.</summary>
    [Theory]
    [InlineData(false, "The file changed during inspection; select it again before Build.", "This input is blocked; review its status before Build.")]
    [InlineData(true, "檔案在檢查期間已變更，請重新選取後再 Build。", "此輸入目前受阻擋，請在 Build 前查看其狀態。")]
    public void StaleInputKeepsBlockingSentence(bool chinese, string expected, string action)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var slot = new FirmwareSlotViewModel("source", "TP", "Select BIN", FirmwareSlotKind.Tp);
        ShellTextResources text = ShellTextResources.For(language);
        slot.ApplyExperienceText(text);
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Blocking, text.FirmwareInspectionStaleFileStatus);

        Assert.Equal(expected, slot.SemanticStateDetail);
        Assert.Equal(expected, slot.IssueCard!.Summary);
        Assert.Equal(action, slot.IssueCard.Action);
        Assert.True(slot.BlocksBuild);
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, slot.InputInspectionSeverity);
    }

    /// <summary>The approved CtrlRAM explanation preserves EOF and ignored-tail guidance.</summary>
    [Theory]
    [InlineData(false, "Use a complete base BIN; short files stop at their end and extra bytes are ignored.")]
    [InlineData(true, "請使用完整的基底 BIN；較短的檔案讀到結尾為止，超出的 bytes 會忽略。")]
    public void CtrlRamExplanationUsesApprovedSentence(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        Assert.Equal(expected, ShellTextResources.For(language).CtrlRamInputFilesDetail);
    }

    /// <summary>DiffNFMerge guidance uses only its declared prerequisite and keeps mapping facts.</summary>
    [Theory]
    [InlineData(false, "This CtrlRAM input requires DiffNFMerge output; select a BIN already generated by DiffNFMerge.")]
    [InlineData(true, "此 CtrlRAM 輸入需要 DiffNFMerge 輸出，請選擇已由 DiffNFMerge 產生的 BIN。")]
    public void DiffNfMergeGuidancePreservesTypedFacts(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var facts = new CtrlRamInputDescriptionFacts("NF_Ctrlram.bin",
            [new("NF", ReplaceRegionGroup.Master, 512, 0x1000, "NF")], true, "NF", false, 1,
            [new("nf", ReplaceRegionGroup.Master, 0x1000, 512)]);
        ShellTextResources text = ShellTextResources.For(language);

        Assert.Equal(expected, text.GetCtrlRamGuidanceNote(facts));
        var ordinary = new CtrlRamInputDescriptionFacts(facts.SourceFileName, facts.Sections, false,
            facts.TitleStem, facts.IsShared, facts.TargetRegionCount, facts.InputGuidanceTargets);
        Assert.Empty(text.GetCtrlRamGuidanceNote(ordinary));
        Assert.Contains("NF_Ctrlram.bin", text.FormatCtrlRamTechnicalDescription(facts), StringComparison.Ordinal);
        Assert.Contains("0x1000", text.FormatCtrlRamTechnicalDescription(facts), StringComparison.Ordinal);
        Assert.Contains("512", text.GetCtrlRamGuidanceFacts(facts)[0].Value, StringComparison.Ordinal);
    }

    /// <summary>General mapping inspection messages preserve the typed issue and file-selection availability.</summary>
    [Theory]
    [InlineData(false, "authoring.general.selected-file-inspection-failed", "The selected BIN could not be inspected; select it again.")]
    [InlineData(true, "authoring.general.selected-file-inspection-failed", "無法檢查所選 BIN，請重新選取。")]
    [InlineData(false, "authoring.general.selected-file-snapshot-required", "The selected BIN needs inspection; select it again before Build.")]
    [InlineData(true, "authoring.general.selected-file-snapshot-required", "所選 BIN 需要檢查，請重新選取後再 Build。")]
    public void GeneralInspectionSentencePreservesIssue(bool chinese, string code, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var row = new GeneralReplaceMappingViewModel("mapping", 1, ShellTextResources.For(language));
        var issue = new GeneralSelectedFileInspectionIssue(code, "Original diagnostic", "mapping");
        row.ApplyFileInspection(null, issue);

        Assert.Equal(expected, row.InspectionIssueMessage);
        Assert.Same(issue, row.InspectionIssue);
        Assert.True(row.HasInspectionIssue);
        Assert.Null(row.AcceptedFileStamp);
        Assert.True(row.CanSelectFile);
    }

    /// <summary>Unknown General codes retain their original diagnostic verbatim.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownGeneralInspectionPreservesOriginal(bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var issue = new GeneralSelectedFileInspectionIssue("future-code", "Opaque diagnostic 0x1234", "mapping");
        Assert.Equal(issue.Message, ShellTextResources.For(language).GetGeneralSelectedFileInspectionIssue(issue));
    }

    /// <summary>Creates typed terminal input facts, including a pre-content read failure.</summary>
    private static AuthoringInputSlotStatus Status(string code, AuthoringSlotLifecycle lifecycle, bool unreadable)
    {
        return unreadable ? new AuthoringInputSlotStatus(
            new CapabilityRouteIdentity("NT51929", ExperienceIds.StandardMerge, "none", "default"),
            new ResolutionToken("approved-messages"), new AuthoringRevision(1), new string('a', 64), new string('b', 64),
            new InputSelectionMemberReadiness("source", true, ResolvedChildReadiness.Ready, true, null, null),
            "source", AuthoringSlotLifecycle.Error, null, null, selectedPathHint: "selected.bin",
            preContentIssue: (code, CompiledInputArtifactInspectionNextAction.SelectReadableInput))
            : StandardMergeFeedbackTests.Status(code, lifecycle);
    }
}
