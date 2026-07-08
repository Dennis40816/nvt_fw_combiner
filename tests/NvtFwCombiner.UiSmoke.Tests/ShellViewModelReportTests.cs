using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Smoke coverage for shell report review and report history surfaces.</summary>
public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies reports stay behind an explicit action until opened.</summary>
    [Fact]
    public void ReportReviewUsesToastAndModalState()
    {
        string json = ReportJsonSamples.Succeeded(runId: "ui-smoke");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.False(viewModel.CanOpenReport);
        Assert.False(viewModel.ShowReportCommand.CanExecute(null));
        Assert.Equal("No report", viewModel.ReportActionLabel);
        Assert.Equal("Build creates one", viewModel.ReportActionStatus);

        viewModel.LoadReportJson(json, "preview-report.json");

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.Equal("Open report", viewModel.ReportActionLabel);
        Assert.Equal("Succeeded", viewModel.ReportActionStatus);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal(1, viewModel.ReportToastOpacity);
        Assert.Equal(json, viewModel.LoadedReportJson);
        Assert.True(viewModel.HasReportHistory);
        Assert.Equal(1, viewModel.ReportHistoryCount);
        Assert.Equal("1 report in history", viewModel.ReportHistorySummary);
        Assert.True(viewModel.CanOpenReportHistory);
        Assert.True(viewModel.ShowReportHistoryCommand.CanExecute(null));
        Assert.True(viewModel.ClearReportHistoryCommand.CanExecute(null));
        Assert.False(viewModel.IsReportHistoryViewOpen);
        Assert.True(viewModel.IsReportReviewViewOpen);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.ReportHistoryEntries);
        Assert.Equal("#1", historyEntry.SequenceLabel);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", historyEntry.Title);
        Assert.Equal("Merge / standard-merge / NT51927", historyEntry.Context);
        Assert.Equal("abcdef", historyEntry.OutputHash);
        Assert.Equal("No external command", historyEntry.CommandSummary);
        Assert.Equal("No issue", historyEntry.IssueSummary);
        Assert.False(viewModel.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(string.Empty, viewModel.LoadedReport.OutputArtifactPath);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927).json", viewModel.ReportSaveFileName);
        Assert.True(viewModel.ShowReportCommand.CanExecute(null));
        Assert.False(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("Succeeded", viewModel.LoadedReport.OutcomeTitle);
        Assert.Contains("No issues reported", viewModel.LoadedReport.OutcomeDetail, StringComparison.Ordinal);
        Assert.Equal("Ready for audit", viewModel.LoadedReport.NextStepTitle);
        Assert.Contains(viewModel.LoadedReport.TriageRows, row =>
            row.Title == "1. Result" &&
            row.Detail == "Succeeded" &&
            row.Meta == "No issue");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Issues" &&
            row.Detail == "0" &&
            row.Meta == "No blocking issue");
        Assert.Equal(4, viewModel.LoadedReport.SummaryRows.Count);
        Assert.Contains(viewModel.LoadedReport.SummaryRows, row =>
            row.Title == "Status" &&
            row.Detail == "Succeeded" &&
            row.Meta == "No blocking issue");
        Assert.Equal(0, viewModel.LoadedReport.OperationCount);
        Assert.False(viewModel.LoadedReport.HasCommandOperations);
        Assert.False(viewModel.LoadedReport.HasStepOperations);

        var reportWithSessionPath = ReportReviewViewModel.FromJson(
            json,
            "preview-report.json",
            "C:/nfc/output/preview.bin");
        Assert.True(reportWithSessionPath.HasOutputArtifactPath);
        Assert.Equal("C:/nfc/output/preview.bin", reportWithSessionPath.OutputArtifactPath);

        viewModel.ShowReportCommand.Execute(null);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.IsReportHistoryViewOpen);
        Assert.True(viewModel.IsReportReviewViewOpen);
        Assert.False(viewModel.HasReportToast);
        Assert.Equal(0, viewModel.ReportToastOpacity);

        viewModel.CloseReportCommand.Execute(null);

        Assert.False(viewModel.IsReportModalOpen);
    }

    /// <summary>Verifies Replace reports surface accepted final-output CRC/header differences.</summary>
    [Fact]
    public void ReportReviewShowsAcceptedOutputDifferences()
    {
        string json = ReportJsonSamples.ReplaceWithAcceptedOutputDifferences();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(json, "replace-report.json");

        Assert.True(viewModel.LoadedReport.HasOutputDifferences);
        ReportLineViewModel difference = Assert.Single(viewModel.LoadedReport.OutputDifferences);
        Assert.Equal("diff-001", difference.Title);
        Assert.Equal("TP flash header / CRC fields", difference.SectionLabel);
        Assert.Equal("0x1C-0x1F (len 0x4)", difference.Range);
        Assert.Equal("4 bytes changed", difference.ChangedSummary);
        Assert.Contains("TP flash header", difference.Reason, StringComparison.Ordinal);
        Assert.Contains(difference.Badges, badge => badge.Text == "accepted");
        Assert.Contains(difference.Badges, badge => badge.Text == "CRC/header");
        Assert.Equal("Before bytes", difference.BeforeLabel);
        Assert.Equal("AA BB CC DD", difference.BeforeValue);
        Assert.Equal("After bytes", difference.AfterLabel);
        Assert.Equal("11 22 33 44", difference.AfterValue);
        Assert.Contains(difference.Facts, fact => fact.Label == "Reason" && fact.Value.Contains("TP flash header", StringComparison.Ordinal));
        Assert.DoesNotContain(difference.Facts, fact => fact.Value.Contains("...", StringComparison.Ordinal));
        Assert.True(viewModel.LoadedReport.HasOperationFlow);
        Assert.Contains(viewModel.LoadedReport.OperationFlow, node =>
            node.Title == "Refresh header and CRC" &&
            node.Number == "100" &&
            node.Meta == "details in Postbuild tab");
        Assert.DoesNotContain(viewModel.LoadedReport.StepOperations, operation => operation.HasCodeBlock);
        Assert.True(viewModel.LoadedReport.HasCommandOperations);
        Assert.Equal("Accepted changes", viewModel.LoadedReport.ByteDifferenceTitle);
        Assert.Contains("CRC/header", viewModel.LoadedReport.ByteDifferenceDetail, StringComparison.Ordinal);
        Assert.Equal("1/1 accepted", viewModel.LoadedReport.ByteDifferenceMeta);
        Assert.Contains(viewModel.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "CRC/header refresh" &&
            row.Count == "1" &&
            row.Status == "present");
        Assert.Contains(viewModel.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "Unexpected differences" &&
            row.Count == "0");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Output diff" &&
            row.Detail == "1" &&
            row.Meta == "all accepted");

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal("差異", viewModel.Text.ReportTabChanges);
        Assert.Equal("可接受變更", viewModel.LoadedReport.ByteDifferenceTitle);
        Assert.Equal("1/1 可接受", viewModel.LoadedReport.ByteDifferenceMeta);
        ReportLineViewModel localizedDifference = Assert.Single(viewModel.LoadedReport.OutputDifferences);
        Assert.Contains(localizedDifference.Badges, badge => badge.Text == "可接受");
        Assert.Equal("TP flash header / CRC fields", localizedDifference.SectionLabel);
        Assert.Equal("變更前 bytes", localizedDifference.BeforeLabel);
        Assert.Equal("AA BB CC DD", localizedDifference.BeforeValue);
        Assert.Contains(viewModel.LoadedReport.OutputDifferenceSummaryRows, row =>
            row.Label == "意外差異" &&
            row.Count == "0" &&
            row.Status == "無");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Output diff" &&
            row.Detail == "1" &&
            row.Meta == "全部可接受");
    }

    /// <summary>Verifies report history can reopen earlier reports without adding a new run.</summary>
    [Fact]
    public void ReportHistoryTracksSessionReportsAndReopensEarlierEntry()
    {
        string previewJson = ReportJsonSamples.Succeeded(
            runId: "preview-run",
            outputSize: 16,
            outputSha256: "abcdef0123456789abcdef");
        string buildJson = ReportJsonSamples.CtrlRamCommandSucceeded();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(previewJson, "preview-report.json");
        viewModel.LoadReportJson(buildJson, "build-report.json");

        Assert.True(viewModel.HasReportHistory);
        Assert.Equal(2, viewModel.ReportHistoryCount);
        Assert.Equal("2 reports in history", viewModel.ReportHistorySummary);
        Assert.Equal("nt51927-ctrlram-replace (NT51927)", viewModel.ReportHistoryEntries[0].Title);
        Assert.Equal("1 command", viewModel.ReportHistoryEntries[0].CommandSummary);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", viewModel.ReportHistoryEntries[1].Title);
        Assert.Equal("abcdef0123456789...", viewModel.ReportHistoryEntries[1].OutputHash);

        viewModel.ShowReportHistoryCommand.Execute(null);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.True(viewModel.IsReportHistoryViewOpen);
        Assert.False(viewModel.IsReportReviewViewOpen);

        viewModel.OpenReportHistoryEntryCommand.Execute(viewModel.ReportHistoryEntries[1]);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.IsReportHistoryViewOpen);
        Assert.True(viewModel.IsReportReviewViewOpen);
        Assert.False(viewModel.HasReportToast);
        Assert.Equal("preview-report.json", viewModel.LoadedReport.SourceName);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", viewModel.LoadedReport.Title);
        Assert.Equal(previewJson, viewModel.LoadedReportJson);
        Assert.Equal(2, viewModel.ReportHistoryCount);

        viewModel.ShowReportHistoryCommand.Execute(null);
        viewModel.RemoveReportHistoryEntryCommand.Execute(viewModel.ReportHistoryEntries[0]);

        Assert.True(viewModel.HasReportHistory);
        Assert.Equal(1, viewModel.ReportHistoryCount);
        Assert.Equal("1 report in history", viewModel.ReportHistorySummary);
        Assert.Equal("nt51927-standard-merge-gen-flash (NT51927)", Assert.Single(viewModel.ReportHistoryEntries).Title);

        viewModel.ClearReportHistoryCommand.Execute(null);

        Assert.False(viewModel.HasReportHistory);
        Assert.True(viewModel.IsReportHistoryEmpty);
        Assert.Equal(0, viewModel.ReportHistoryCount);
        Assert.Equal("No reports in history", viewModel.ReportHistorySummary);
        Assert.False(viewModel.CanOpenReportHistory);
        Assert.False(viewModel.ShowReportHistoryCommand.CanExecute(null));
        Assert.False(viewModel.ClearReportHistoryCommand.CanExecute(null));
        Assert.True(viewModel.HasLoadedReport);
        Assert.Equal("preview-report.json", viewModel.LoadedReport.SourceName);
    }

    /// <summary>Verifies local report history reports oversized storage and can be cleared in one action.</summary>
    [Fact]
    public void ReportHistoryFlagsOversizedStorageForOneClickCleanup()
    {
        string json = ReportJsonSamples.Succeeded();
        string paddedJson = json.Insert(json.LastIndexOf('}'), $",\"Padding\":\"{new string('A', 1024 * 1024)}\"");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(paddedJson, "large-report.json");
        viewModel.ShowReportHistoryCommand.Execute(null);

        Assert.True(viewModel.IsReportHistoryViewOpen);
        Assert.True(viewModel.HasReportHistoryStorageWarning);
        Assert.Contains("MB", viewModel.ReportHistoryStorageSummary, StringComparison.Ordinal);
        Assert.Contains("Clear history", viewModel.ReportHistoryStorageWarning, StringComparison.Ordinal);

        viewModel.ClearReportHistoryCommand.Execute(null);

        Assert.False(viewModel.HasReportHistoryStorageWarning);
        Assert.Equal("0 B stored locally", viewModel.ReportHistoryStorageSummary);
        Assert.Empty(viewModel.ExportReportHistory());
    }

    /// <summary>Verifies persisted report history snapshots restore report metadata and artifact path context.</summary>
    [Fact]
    public void ReportHistorySnapshotsRestoreAcrossViewModels()
    {
        string json = ReportJsonSamples.Succeeded(
            profileId: "nt51927-ctrlram-replace",
            modeId: "ctrlram-replace",
            experienceId: "ctrlram-replace",
            compositionKind: "Replace",
            runId: "persisted-build-run",
            startedAtUtc: "2026-07-01T00:05:00Z",
            outputFileName: "build.bin",
            outputSize: 32,
            committed: true,
            outputSha256: "0123456789abcdef012345");
        ReportHistorySnapshot snapshot = new(
            "build-report.json",
            json,
            "C:/nfc/output/build.bin");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportHistory([snapshot]);

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.False(viewModel.HasReportToast);
        Assert.Equal("Open report", viewModel.ReportActionLabel);
        Assert.Equal("Succeeded", viewModel.ReportActionStatus);
        Assert.Equal("build-report.json", viewModel.LoadedReport.SourceName);
        Assert.True(viewModel.LoadedReport.HasOutputArtifactPath);
        Assert.Equal("C:/nfc/output/build.bin", viewModel.LoadedReport.OutputArtifactPath);
        Assert.Equal("C:/nfc/output/build.bin", Assert.Single(viewModel.ReportHistoryEntries).ArtifactPath);

        IReadOnlyList<ReportHistorySnapshot> exported = viewModel.ExportReportHistory();
        ReportHistorySnapshot exportedSnapshot = Assert.Single(exported);
        Assert.Equal("build-report.json", exportedSnapshot.SourceName);
        Assert.Equal(json, exportedSnapshot.ReportJson);
        Assert.Equal("C:/nfc/output/build.bin", exportedSnapshot.OutputArtifactPath);
        Assert.Equal("nt51927-ctrlram-replace (NT51927)", exportedSnapshot.Metadata.Title);
        Assert.Equal("Succeeded", exportedSnapshot.Metadata.Status);
        Assert.Equal("Replace / ctrlram-replace / NT51927", exportedSnapshot.Metadata.Context);
        Assert.Equal("0123456789abcdef...", exportedSnapshot.Metadata.OutputHash);
        Assert.Equal("persisted-build-run", exportedSnapshot.Metadata.RunId);
        Assert.Equal("0 inputs / 0 steps / 0 mutations", exportedSnapshot.Metadata.EvidenceSummary);

        MainWindowViewModel restoredViewModel = ShellViewModelFactory.Create();
        restoredViewModel.LoadReportHistory(exported);

        Assert.Equal("nt51927-ctrlram-replace (NT51927)", restoredViewModel.LoadedReport.Title);
        Assert.Equal("C:/nfc/output/build.bin", restoredViewModel.LoadedReport.OutputArtifactPath);
        Assert.Equal(1, restoredViewModel.ReportHistoryCount);
    }

    /// <summary>Verifies local report history persistence round-trips and fails closed for bad JSON.</summary>
    [Fact]
    public void ReportHistoryFileStoreRoundTripsSnapshots()
    {
        string json = ReportJsonSamples.Succeeded(
            runId: "persisted-preview-run",
            outputSize: 16,
            outputSha256: "abcdef0123456789abcdef");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-report-history");
        string historyPath = workspace.PathFor(Path.Combine("state", "report-history.v1.json"));
        var metadata = new ReportHistoryMetadataSnapshot(
            "nt51927-standard-merge-gen-flash (NT51927)",
            "Succeeded",
            "Merge / standard-merge / NT51927",
            "preview.bin / 16 bytes",
            "abcdef0123456789...",
            "No external command",
            "No issue",
            "0 inputs / 0 steps / 0 mutations",
            "persisted-preview-run",
            "2026-07-01T00:00:00Z",
            "NT51927",
            "standard-merge",
            "standard-merge",
            "Merge");
        ReportHistorySnapshot snapshot = new(
            "preview-report.json",
            json,
            "C:/nfc/output/preview.bin",
            metadata);

        ReportHistoryFileStore.Save(historyPath, [snapshot]);

        IReadOnlyList<ReportHistorySnapshot> loaded = ReportHistoryFileStore.Load(historyPath);
        ReportHistorySnapshot loadedSnapshot = Assert.Single(loaded);
        Assert.Equal("preview-report.json", loadedSnapshot.SourceName);
        Assert.Equal(json, loadedSnapshot.ReportJson);
        Assert.Equal("C:/nfc/output/preview.bin", loadedSnapshot.OutputArtifactPath);
        Assert.Equal(metadata, loadedSnapshot.Metadata);

        string legacyJson = $$"""
            {
              "SchemaVersion": 1,
              "Entries": [
                {
                  "SourceName": "legacy-report.json",
                  "ReportJson": {{JsonSerializer.Serialize(json)}},
                  "OutputArtifactPath": ""
                }
              ]
            }
            """;
        File.WriteAllText(historyPath, legacyJson);

        ReportHistorySnapshot legacySnapshot = Assert.Single(ReportHistoryFileStore.Load(historyPath));
        Assert.Equal("legacy-report.json", legacySnapshot.SourceName);
        Assert.Equal(ReportHistoryMetadataSnapshot.Empty, legacySnapshot.Metadata);

        File.WriteAllText(historyPath, "{not valid json");

        Assert.Empty(ReportHistoryFileStore.Load(historyPath));
    }

    /// <summary>Verifies local shell preferences round-trip and invalid values keep fail-closed defaults.</summary>
    [Fact]
    public void ShellPreferenceFileStoreRoundTripsAndInvalidValuesFallBack()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences");
        string preferencesPath = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        var preferences = new ShellPreferenceSnapshot("Dark", "Warn only", "Traditional Chinese");

        ShellPreferenceFileStore.Save(preferencesPath, preferences);

        ShellPreferenceSnapshot loaded = ShellPreferenceFileStore.Load(preferencesPath);
        Assert.Equal(preferences, loaded);

        MainWindowViewModel restoredViewModel = ShellViewModelFactory.Create();
        restoredViewModel.LoadShellPreferences(loaded);

        Assert.Equal("Dark", restoredViewModel.SelectedTheme);
        Assert.Equal("Warn only", restoredViewModel.SelectedStrictness);
        Assert.Equal("Traditional Chinese", restoredViewModel.SelectedLanguage);
        Assert.Equal("設定", restoredViewModel.SettingsPreview.Title);
        Assert.Equal("繁體中文介面已套用並會在啟動時還原。", restoredViewModel.LanguagePreferenceStatus);
        Assert.Equal(preferences, restoredViewModel.ExportShellPreferences());

        File.WriteAllText(preferencesPath, "{not valid json");

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));

        MainWindowViewModel defaultViewModel = ShellViewModelFactory.Create();
        defaultViewModel.LoadShellPreferences(new ShellPreferenceSnapshot("Blue", "Relaxed", "Klingon"));

        Assert.Equal("System", defaultViewModel.SelectedTheme);
        Assert.Equal("Strict", defaultViewModel.SelectedStrictness);
        Assert.Equal("English", defaultViewModel.SelectedLanguage);
    }

    /// <summary>Verifies report loading errors still produce a reopenable report modal.</summary>
    [Fact]
    public void ReportReviewErrorsUseModalState()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportError("Startup report", "missing preview-report.json");

        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.Equal(string.Empty, viewModel.LoadedReportJson);
        Assert.Equal("Load failed", viewModel.ReportActionStatus);
        Assert.True(viewModel.HasReportHistory);
        ReportHistoryEntryViewModel historyEntry = Assert.Single(viewModel.ReportHistoryEntries);
        Assert.Equal("Report could not be loaded", historyEntry.Title);
        Assert.Equal("No output hash", historyEntry.OutputHash);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal("Report issue: Startup report", viewModel.ReportToastText);
        Assert.True(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("Report load failed", viewModel.LoadedReport.OutcomeTitle);
        Assert.Equal("Start with this issue", viewModel.LoadedReport.NextStepTitle);
        Assert.Equal("Load error", viewModel.LoadedReport.PrimaryIssue.Title);
        Assert.Contains("missing preview-report.json", viewModel.LoadedReport.PrimaryIssue.Detail, StringComparison.Ordinal);
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Issues" &&
            row.Detail == "1" &&
            row.Meta == "Load error");

        viewModel.ShowReportCommand.Execute(null);

        Assert.True(viewModel.IsReportModalOpen);
        Assert.False(viewModel.HasReportToast);
    }

    /// <summary>Verifies successful runs with warning diagnostics do not render as blocking issues.</summary>
    [Fact]
    public void ReportReviewSeparatesWarningsFromBlockingIssues()
    {
        string json = ReportJsonSamples.CtrlRamWarning();
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.LoadReportJson(json, "warning-report.json");

        Assert.Equal("Succeeded with 1 warning(s)", viewModel.ReportActionStatus);
        Assert.True(viewModel.LoadedReport.IsSuccessful);
        Assert.False(viewModel.LoadedReport.IsClean);
        Assert.True(viewModel.LoadedReport.HasWarnings);
        Assert.True(viewModel.LoadedReport.HasWarningsWithoutBlockingIssues);
        Assert.False(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.Equal("warning", Assert.Single(viewModel.LoadedReport.Issues).Severity);
        Assert.Equal(0, viewModel.LoadedReport.BlockingIssueCount);
        Assert.Equal(1, viewModel.LoadedReport.WarningCount);
        Assert.Equal("Succeeded with 1 warning(s)", viewModel.LoadedReport.OutcomeTitle);
        Assert.Equal("Review warning", viewModel.LoadedReport.NextStepTitle);
        Assert.Contains("truncated", viewModel.LoadedReport.NextStepDetail, StringComparison.Ordinal);
        Assert.Contains(viewModel.LoadedReport.TriageRows, row =>
            row.Title == "2. Warning" &&
            row.Detail == "input.address-space.truncated" &&
            row.Meta == "replace-ctrlram");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Issues" &&
            row.Detail == "0" &&
            row.Meta == "No blocking issue");
        Assert.Contains(viewModel.LoadedReport.EvidenceRows, row =>
            row.Title == "Warnings" &&
            row.Detail == "1" &&
            row.Meta == "input.address-space.truncated");
        Assert.Equal("1 warning", Assert.Single(viewModel.ReportHistoryEntries).IssueSummary);
    }

    /// <summary>Verifies report review uses schema severity before legacy code-based warning fallback.</summary>
    [Fact]
    public void ReportReviewUsesIssueSeverityForWarnings()
    {
        string json = ReportJsonSamples.CtrlRamWarning(
            runId: "ui-smoke-severity-warning",
            issueCode: "processor.review-note",
            message: "Processor completed with a review note.",
            operationId: "run-postbuild");

        var report = ReportReviewViewModel.FromJson(json, "severity-warning.json");

        Assert.True(report.HasWarningsWithoutBlockingIssues);
        Assert.False(report.HasPrimaryIssue);
        Assert.Equal(0, report.BlockingIssueCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal("Succeeded with 1 warning(s)", report.Status);
        ReportLineViewModel issue = Assert.Single(report.Issues);
        Assert.Equal("processor.review-note", issue.Title);
        Assert.Equal("warning", issue.Severity);
    }

    /// <summary>Verifies older reports without issue severity keep the documented truncation warning behavior.</summary>
    [Fact]
    public void ReportReviewKeepsLegacyTruncationWarningFallback()
    {
        string json = ReportJsonSamples.CtrlRamWarning(
            runId: "ui-smoke-legacy-warning",
            severity: null,
            message: "Input ctrlram-input was truncated.");

        var report = ReportReviewViewModel.FromJson(json, "legacy-warning.json");

        Assert.True(report.HasWarningsWithoutBlockingIssues);
        Assert.False(report.HasPrimaryIssue);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal("warning", Assert.Single(report.Issues).Severity);
    }

    /// <summary>Verifies report triage points users to the first issue and command evidence.</summary>
    [Fact]
    public void ReportReviewTriagePrioritizesIssueAndCommandEvidence()
    {
        string json = ReportJsonSamples.CtrlRamCommandIssue();

        var report = ReportReviewViewModel.FromJson(json, "preview-report.json");

        Assert.Equal("Needs attention", report.OutcomeTitle);
        Assert.Equal("Start with this issue", report.NextStepTitle);
        Assert.Equal("processor.tool.missing", report.PrimaryIssue.Title);
        Assert.Contains(report.TriageRows, row =>
            row.Title == "1. First issue" &&
            row.Detail == "processor.tool.missing" &&
            row.Meta == "run-ctrlram-postbuild");
        Assert.Contains(report.TriageRows, row =>
            row.Title == "4. Evidence" &&
            row.Detail == "Refresh commands" &&
            row.Meta == "1 command(s)");
        Assert.Contains(report.EvidenceRows, row =>
            row.Title == "Commands" &&
            row.Detail == "1" &&
            row.Meta == "external processors");
        Assert.True(report.ShouldExpandIssues);
        Assert.True(report.ShouldExpandCommandOperations);
        Assert.False(report.ShouldExpandStepOperations);
        ReportLineViewModel command = Assert.Single(report.CommandOperations);
        Assert.Equal("run-external-processor", command.OperationKind);
        Assert.Equal("(none)", command.OperationSource);
        Assert.Equal("output-image 0x0-0x7FFFF (len 0x80000)", command.OperationTarget);
        Assert.Equal("legacy-combiner", command.OperationProcessor);
        Assert.Equal("planned", command.OperationStatus);
        Assert.Contains("Combiner.exe", command.CodeBlock, StringComparison.Ordinal);
        Assert.Contains(command.Badges, badge => badge.Text == "planned");
        Assert.Contains(command.Badges, badge => badge.Text == "overlap reject");
        Assert.Contains(command.Badges, badge => badge.Text == "built-in-profile");
        Assert.Contains(command.Facts, fact => fact.Label == "Operation source" && fact.Value == "built-in-profile");
        Assert.Contains(command.Facts, fact => fact.Label == "Processor" && fact.Value == "legacy-combiner");
        Assert.Contains(command.Facts, fact => fact.Label == "Tool" && fact.Value == "legacy-combiner-1.13.0");
        Assert.True(command.HasRangeRows);
        Assert.Contains(command.RangeRows, row =>
            row.Kind == "Target" &&
            row.AddressSpace == "output-image" &&
            row.Range == "0x0-0x7FFFF (len 0x80000)" &&
            row.Source == "work image");
        Assert.Contains(command.RangeRows, row =>
            row.Kind == "Processor read" &&
            row.AddressSpace == "output-image" &&
            row.Range == "0x0-0x7FFFF (len 0x80000)" &&
            row.Source == "postbuild read policy");
        Assert.Contains(command.RangeRows, row =>
            row.Kind == "Processor write" &&
            row.AddressSpace == "output-image" &&
            row.Range == "0x7100-0x7103 (len 0x4)" &&
            row.Source == "postbuild write policy");
        Assert.Contains(command.RangeRows, row =>
            row.Kind == "Processor write" &&
            row.AddressSpace == "output-image" &&
            row.Range == "0x7118-0x711B (len 0x4)" &&
            row.Source == "postbuild write policy");
    }

}
