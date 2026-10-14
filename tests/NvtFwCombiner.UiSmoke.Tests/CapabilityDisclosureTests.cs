// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Policy disclosure remains explicit in localized workflows and durable report review.</summary>
public sealed class CapabilityDisclosureTests
{
    /// <summary>Both languages disclose unverified evidence and explain the output without changing admission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerificationFieldAndOutputSentenceAreLocalized(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(
            chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        Assert.Equal(chinese ? "驗證狀態" : "Verification status", VerificationText.FieldLabel(text));
        Assert.Equal(chinese ? "未驗證（僅有定義）" : "Unverified (contract only)",
            text.SupportMatrixEvidenceValue(CapabilityEvidenceStatus.ContractOnly));
        Assert.Equal(chinese ? "未驗證（缺少證據）" : "Unverified (missing evidence)",
            text.SupportMatrixEvidenceValue(CapabilityEvidenceStatus.Missing));
        Assert.Equal(chinese ? "未驗證" : "Unverified", text.SupportMatrixContractOnlyLabel);
        Assert.Equal(chinese ? "部分未驗證" : "Some unverified", text.SupportMatrixReviewRequiredLabel);
        foreach (CapabilityEvidenceStatus evidence in new[]
                 { CapabilityEvidenceStatus.ContractOnly, CapabilityEvidenceStatus.Missing })
        {
            var disclosure = new CapabilityWorkflowReadiness(true, true, evidence, "policy", "review");
            Assert.Equal(chinese ? "未驗證" : "Unverified", text.GetWorkflowEvidenceLabel(disclosure));
            Assert.Equal(chinese ? "這個輸出沒有獨立的 Golden 結果。" : "This output has no independent Golden result.",
                VerificationText.OutputNotice(text, disclosure));
            Assert.Empty(VerificationText.OutputNotice(text, disclosure with { HasExactRoute = false }));
        }
        var verified = new CapabilityWorkflowReadiness(true, true,
            CapabilityEvidenceStatus.DirectGolden, "policy", "review");
        Assert.Empty(VerificationText.OutputNotice(text, verified));
        Assert.Empty(VerificationText.OutputNotice(text, null));
    }

    /// <summary>Save/load preserves all policy facts and renders the same verification as the typed report.</summary>
    [Theory]
    [InlineData(false, CapabilityEvidenceStatus.ContractOnly)]
    [InlineData(true, CapabilityEvidenceStatus.ContractOnly)]
    [InlineData(false, CapabilityEvidenceStatus.Missing)]
    [InlineData(true, CapabilityEvidenceStatus.Missing)]
    public void CapabilityReportSummaryRoundTripsWithoutReinterpretingPolicy(
        bool chinese, CapabilityEvidenceStatus evidence)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        ShellTextResources text = ShellTextResources.For(language);
        var identity = new CapabilityRouteIdentity("NT-SYNTHETIC", "standard-merge", "selector-free", "synthetic-map");
        var capability = new CapabilityRunSummary(identity.RouteId, new string('a', 64),
            CapabilityPublicationStatus.Candidate, evidence);
        CompositionRunReport report = ReportInputFeedbackTests.Report([], capability: capability);
        string json = ReportInputFeedbackTests.Json(report);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement fields = document.RootElement.GetProperty("Capability");
        Assert.Equal(5, fields.EnumerateObject().Count());
        Assert.Equal(identity.RouteId, fields.GetProperty("RouteId").GetString());
        Assert.Equal(new string('a', 64), fields.GetProperty("CapabilityFingerprint").GetString());
        Assert.Equal("Candidate", fields.GetProperty("Publication").GetString());
        Assert.Equal(evidence.ToString(), fields.GetProperty("Evidence").GetString());
        Assert.Equal("unverified", fields.GetProperty("VerificationStatus").GetString());
        CapabilityRunSummary hydrated = Assert.IsType<CapabilityRunSummary>(
            CompositionRunReportJson.ReadCapabilitySummary(document.RootElement));
        Assert.Equal(capability.RouteId, hydrated.RouteId);
        Assert.Equal(capability.CapabilityFingerprint, hydrated.CapabilityFingerprint);
        Assert.Equal(capability.Publication, hydrated.Publication);
        Assert.Equal(capability.Evidence, hydrated.Evidence);
        Assert.Equal(capability.VerificationStatus, hydrated.VerificationStatus);
        ReportReviewViewModel loaded = ReportReviewViewModel.FromJson(json, "synthetic-report.json", language: language);
        ReportReviewViewModel typed = ReportReviewViewModel.FromReportCancellable(
            report, false, "synthetic-report", null, null, language, TestContext.Current.CancellationToken);
        Assert.Equal(text.SupportMatrixEvidenceValue(evidence), loaded.VerificationStatusLabel);
        Assert.Equal(typed.VerificationSummary, loaded.VerificationSummary);
        Assert.Equal($"{VerificationText.FieldLabel(text)}: {text.SupportMatrixEvidenceValue(evidence)}",
            loaded.VerificationSummary);
        Assert.Equal(report.Output.FileName, loaded.OutputFileName);
        Assert.Equal(report.Output.Sha256, loaded.OutputSha256);
    }

    /// <summary>An older report stays readable and never gains verification from current policy.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyReportOmitsCapabilityAndDisplaysNotRecorded(bool chinese)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        string json = ReportInputFeedbackTests.Json(ReportInputFeedbackTests.Report([]));
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("Capability", out _));
        Assert.Null(CompositionRunReportJson.ReadCapabilitySummary(document.RootElement));
        Assert.Equal(CompositionRunReportJson.ReadCompleteness.Recognized,
            CompositionRunReportJson.AssessReadCompleteness(
                document.RootElement, TestContext.Current.CancellationToken));
        ReportReviewViewModel loaded = ReportReviewViewModel.FromJson(json, "legacy-report.json", language: language);
        Assert.False(loaded.IsOutcomeUnknown);
        Assert.Null(loaded.Capability);
        Assert.Equal(chinese ? "未記錄" : "Not recorded", loaded.VerificationStatusLabel);
        Assert.Equal(chinese ? "驗證狀態: 未記錄" : "Verification status: Not recorded", loaded.VerificationSummary);
        (string Route, string Fingerprint, string Evidence)[] invalidMetadata =
        [
            ("synthetic-route", "invalid", "ContractOnly"),
            (" ", new string('a', 64), "ContractOnly"),
            ("synthetic-route", new string('a', 64), "Unknown"),
        ];
        foreach ((string route, string fingerprint, string evidence) in invalidMetadata)
        {
            var incomplete = new JsonObject
            {
                ["Capability"] = new JsonObject
                {
                    ["RouteId"] = route,
                    ["CapabilityFingerprint"] = fingerprint,
                    ["Publication"] = "Candidate",
                    ["Evidence"] = evidence,
                },
            };
            ReportReviewViewModel unknown = ReportReviewViewModel.FromJson(
                incomplete.ToJsonString(), "incomplete-report.json", language: language);
            Assert.True(unknown.IsOutcomeUnknown);
            Assert.Null(unknown.Capability);
            Assert.Equal(chinese ? "未記錄" : "Not recorded", unknown.VerificationStatusLabel);
        }
    }

    /// <summary>The actual output-area bindings relocalize and disappear when a verified route is selected.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task WorkflowOutputNoticeUsesLiveLocalizedDisclosure(bool chinese, bool dark)
    {
        using var workspace = TempWorkspace.Create("capability-disclosure-controls");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            shell.ShowMergeCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51928";
            if (chinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            TextBlock merge = Assert.IsType<TextBlock>(window.FindControl<Control>("MergeOutputVerificationNotice"));
            Assert.True(merge.IsVisible);
            Assert.Equal(VerificationText.OutputSentence(shell.Text), merge.Text);
            shell.ShowReplaceCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51928";
            TextBlock replace = Assert.IsType<TextBlock>(window.FindControl<Control>("ReplaceOutputVerificationNotice"));
            Assert.True(replace.IsVisible);
            Assert.Equal(VerificationText.OutputSentence(shell.Text), replace.Text);
            shell.ShowMergeCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51927";
            Assert.False(merge.IsVisible);
            Assert.Empty(shell.Merge.OutputVerificationNotice);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }
}
