using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Localized feedback explains typed findings without changing their authority.</summary>
public sealed class StandardMergeFeedbackTests
{
    /// <summary>Uniform findings explain the declared range, possible intent, action and non-blocking impact.</summary>
    [Theory]
    [InlineData("DP", false)]
    [InlineData("TP", false)]
    [InlineData("LDC", false)]
    [InlineData("DP", true)]
    [InlineData("TP", true)]
    [InlineData("LDC", true)]
    public void UniformWarningExplainsMeaningBeforeDiagnosticCode(string input, bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        string code = $"{input}_UNIFORM_CONTENT_WARNING";
        AuthoringInputSlotStatus status = Status(code, AuthoringSlotLifecycle.Warning);
        string detail = text.GetInputSlotInspectionStatus(status);

        Assert.Contains(chinese ? $"profile 指定的 {input} 範圍" : $"profile-declared {input} range", detail, StringComparison.Ordinal);
        Assert.Contains(chinese ? "同一個 byte" : "same byte", detail, StringComparison.Ordinal);
        Assert.Contains(chinese ? "不限於 00/FF" : "not only 00/FF", detail, StringComparison.Ordinal);
        Assert.Contains(chinese ? "刻意內容" : "intentional", detail, StringComparison.Ordinal);
        Assert.Contains(chinese ? "請確認來源 BIN" : "Check the source BIN", detail, StringComparison.Ordinal);
        Assert.Contains(chinese ? "此警告不會阻擋 Build" : "This warning does not block Build", detail, StringComparison.Ordinal);
        Assert.EndsWith(code, detail, StringComparison.Ordinal);
        Assert.DoesNotContain(code, detail.Split('\n')[0], StringComparison.Ordinal);

        FirmwareSlotViewModel slot = Slot(status, chinese);
        Assert.True(slot.IsSemanticStateWarning);
        Assert.False(slot.IsSemanticStateVerified);
        Assert.False(slot.BlocksBuild);
        Assert.Equal(detail, slot.InputInspectionStatus);
        Assert.Equal(slot.IssueCard!.AutomationText, slot.SemanticStateAutomationText);
        Assert.DoesNotContain(code, slot.IssueCard.Summary, StringComparison.Ordinal);
        Assert.Equal(code, status.InspectionIssueCode);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, status.AcceptedBytes!.Value.ToArray());
    }

    /// <summary>Unrelated warnings retain the existing diagnostic fallback.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownWarningRetainsItsDiagnosticCode(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        string detail = text.GetInputSlotInspectionStatus(Status("OTHER_WARNING", AuthoringSlotLifecycle.Warning));
        Assert.Equal(chinese
            ? "警告：profile 內容檢查 OTHER_WARNING；Build 前請確認。"
            : "Warning: profile content check OTHER_WARNING; review before Build.", detail);
    }

    /// <summary>A code alone never changes terminal severity or invents non-blocking treatment.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ErrorAndVerifiedKeepTheirExistingMeaning(bool chinese)
    {
        FirmwareSlotViewModel error = Slot(Status("TP_UNIFORM_CONTENT_WARNING", AuthoringSlotLifecycle.Error), chinese);
        Assert.True(error.IsSemanticStateError);
        Assert.True(error.BlocksBuild);
        Assert.DoesNotContain(chinese ? "不會阻擋" : "does not block", error.InputInspectionStatus, StringComparison.Ordinal);
        FirmwareSlotViewModel verified = Slot(Status(InputArtifactInspectionIssueCodes.Ready, AuthoringSlotLifecycle.Verified), chinese);
        Assert.True(verified.IsSemanticStateVerified);
        Assert.False(verified.BlocksBuild);
        Assert.Equal(chinese ? "Ready：所選 BIN 符合 compiled input contract。" : "Ready: the selected BIN satisfies the compiled input contract.", verified.InputInspectionStatus);
    }

    internal static FirmwareSlotViewModel Slot(AuthoringInputSlotStatus status, bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var slot = new FirmwareSlotViewModel("source", "TP BIN", "Select source", FirmwareSlotKind.Tp)
        {
            FilePath = @"C:\firmware\synthetic-input.bin",
        };
        slot.ApplyExperienceText(text);
        FirmwareInspectionProjection.ApplyInputSlotInspection(slot, status, text);
        return slot;
    }

    // This is a typed projection fixture, not an inspector/firmware Golden fixture.
    internal static AuthoringInputSlotStatus Status(string code, AuthoringSlotLifecycle lifecycle,
        int actualLength = 2, long requiredLength = 2, InputDiagnosticEvidence? evidence = null)
    {
        byte[] bytes = new byte[actualLength];
        Array.Fill(bytes, evidence?.RepeatedByte ?? 0xA5);
        FileStamp stamp = FileStamp.FromBytes(bytes);
        CompiledInputArtifactInspectionSeverity severity = lifecycle == AuthoringSlotLifecycle.Error
            ? CompiledInputArtifactInspectionSeverity.Blocking
            : lifecycle == AuthoringSlotLifecycle.Warning
                ? CompiledInputArtifactInspectionSeverity.Warning
                : CompiledInputArtifactInspectionSeverity.Valid;
        var inspection = new CompiledInputArtifactInspectionResult(
            "source", "source", bytes.Length, stamp.Sha256, requiredLength, [],
            new ByteRange(0, bytes.Length), stamp.Sha256, null, severity, code,
            lifecycle == AuthoringSlotLifecycle.Error, CompiledInputArtifactInspectionNextAction.None)
        {
            DiagnosticEvidence = evidence,
        };
        return new AuthoringInputSlotStatus(
            new CapabilityRouteIdentity("NT51929", ExperienceIds.StandardMerge, "none", "default"),
            new ResolutionToken("feedback-test"), new AuthoringRevision(1), new string('a', 64), new string('b', 64),
            new InputSelectionMemberReadiness("source", true, ResolvedChildReadiness.Ready, true, null, null),
            "source", lifecycle, stamp, inspection, acceptedBytes: bytes);
    }
}
