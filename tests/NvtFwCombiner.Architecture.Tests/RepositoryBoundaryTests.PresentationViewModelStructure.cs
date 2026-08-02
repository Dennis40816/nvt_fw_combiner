namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Merge-only presentation lifetime belongs to a focused child rather than the shell.</summary>
    [Fact]
    public void MergePresentationLivesBehindFocusedChild()
    {
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string shellMerge = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.MergePresentation.cs");
        string mergePrompt = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.AbAFlashCodeDeliveryPrompt.cs");

        Assert.Contains("Merge = new MergePresentationViewModel", construction, StringComparison.Ordinal);
        Assert.Contains("public MergePresentationViewModel Merge", shellMerge, StringComparison.Ordinal);
        Assert.Contains("PromptForAbAFlashCodeDeliveryAsync", mergePrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public bool IsAbAFlashCodeDeliveryPromptOpen",
            ReadViewModelPartials(),
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels",
            "MainWindowViewModel.AbAFlashCodeDeliveryPrompt.cs")));
    }

    /// <summary>Workflow context and firmware mismatch prompts belong to the shared session child.</summary>
    [Fact]
    public void WorkflowSessionPresentationLivesBehindFocusedChild()
    {
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string shellSession = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.WorkflowSession.cs");
        string context = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.WorkflowContext.cs");
        string mismatch = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareIcMismatch.cs");
        string inspection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");
        string session = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.cs");
        string shellPartials = ReadViewModelPartials();

        Assert.Contains("WorkflowSession = new WorkflowSessionPresentationViewModel", construction, StringComparison.Ordinal);
        Assert.Contains("public WorkflowSessionPresentationViewModel WorkflowSession", shellSession, StringComparison.Ordinal);
        Assert.Contains("WorkflowContextSetupViewModel", context, StringComparison.Ordinal);
        Assert.Contains("ReconcileFirmwareIcMismatch", mismatch, StringComparison.Ordinal);
        Assert.Contains("FirmwareInspectionSession", session, StringComparison.Ordinal);
        Assert.Contains("InspectionSession.ReadBatch", inspection, StringComparison.Ordinal);
        Assert.Contains("SetSlotFileAsync", inspection, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareInspectionSession", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSlotFileAsync", shellPartials, StringComparison.Ordinal);
    }

    /// <summary>Report parsing, history, and commands belong to a focused child rather than the shell.</summary>
    [Fact]
    public void ReportPresentationLivesBehindFocusedChild()
    {
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string shellReport = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Report.cs");
        string report = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.cs");
        string history = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.History.cs");

        Assert.Contains("Reports = new ReportPresentationViewModel", construction, StringComparison.Ordinal);
        Assert.Contains("public ReportPresentationViewModel Reports", shellReport, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportReviewViewModel.FromJson", shellReport, StringComparison.Ordinal);
        Assert.Contains("ReportReviewViewModel.FromJson", report, StringComparison.Ordinal);
        Assert.Contains("ObservableCollection<ReportHistoryEntryViewModel>", history, StringComparison.Ordinal);
    }

    /// <summary>Build-result state and actions belong to a focused child rather than the shell.</summary>
    [Fact]
    public void BuildResultPresentationLivesBehindFocusedChild()
    {
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string shellBuildResult = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.BuildCompleted.cs");
        string buildResult = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/BuildResultViewModel.cs");
        string shell = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");
        string modal = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Views/BuildCompletedModal.axaml");

        Assert.Contains("BuildResult = new BuildResultViewModel", construction, StringComparison.Ordinal);
        Assert.Contains("public BuildResultViewModel BuildResult", shellBuildResult, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchDeliveryArtifact?", shellBuildResult, StringComparison.Ordinal);
        Assert.Contains("IFileRevealService", buildResult, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding BuildResult.IsOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding BuildResult.RevealOutputCommand}\"", modal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding BuildResult.CloseCommand}\"", modal, StringComparison.Ordinal);
    }

    /// <summary>Settings catalog presentation belongs to a focused child rather than the shell.</summary>
    [Fact]
    public void SettingsPresentationLivesBehindFocusedChild()
    {
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string shellSettings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Settings.cs");
        string settings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/SettingsViewModel.cs");
        string pageTemplates = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml");

        Assert.Contains("Settings = new SettingsViewModel(appVersion);", construction, StringComparison.Ordinal);
        Assert.Contains("public SettingsViewModel Settings", shellSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionService", shellSettings, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.GetSettingsSnapshot()", settings, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Settings.OverviewRows}\"", pageTemplates, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Settings.CapabilityRows}\"", pageTemplates, StringComparison.Ordinal);
    }

    /// <summary>Verifies the Presentation projection keeps only UI-owned contract adaptation.</summary>
    [Fact]
    public void UiCompositionRunnerConcernsStaySplit()
    {
        string catalog = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Catalog.cs");
        string common = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs");
        string merge = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Merge.cs");
        string replace = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");
        string bindings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Bindings.cs");
        string mergeViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Merge.cs");
        string replaceViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Replace.cs");

        Assert.Contains("GetNumberSelectionChoices", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDefaultIcId", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("private static MemoryMapRowViewModel ToMemoryMapRow", common, StringComparison.Ordinal);
        Assert.Contains("GetFirmwareSlotFacts", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", facts, StringComparison.Ordinal);
        Assert.Contains("WorkbenchReplaceModes", ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs"), StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryDisplay", merge, StringComparison.Ordinal);
        Assert.Contains("GetGeneralMergeMemoryDisplay", merge, StringComparison.Ordinal);
        Assert.DoesNotContain("RunGeneralMergeAsync", merge, StringComparison.Ordinal);
        Assert.Contains("GetReplaceMemoryDisplay", replace, StringComparison.Ordinal);
        Assert.DoesNotContain("RunReplaceAsync", replace, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.GetSupportedIcIds", bindings, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.RunStandardMergeWithProgressAsync", mergeViewModel, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.RunReplaceWithProgressAsync", replaceViewModel, StringComparison.Ordinal);
    }

    /// <summary>Verifies all composition commands share one UI-owned run lifecycle.</summary>
    [Fact]
    public void CompositionCommandsShareRunLifecycle()
    {
        string lifecycle = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.RunLifecycle.cs");
        string merge = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Merge.cs");
        string replace = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Replace.cs");

        Assert.Equal(3, CountOccurrences(merge, "return RunCompositionAsync("));
        Assert.Equal(1, CountOccurrences(replace, "return RunCompositionAsync("));
        Assert.Contains("WorkbenchCompositionService.RunStandardMergeWithProgressAsync", merge, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.RunGeneralMergeWithProgressAsync", merge, StringComparison.Ordinal);
        Assert.Contains("AbMergeWorkbenchCompositionService.RunAbMergeWithProgressAsync", merge, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.RunReplaceWithProgressAsync", replace, StringComparison.Ordinal);
        Assert.Contains("await Task.Yield();", lifecycle, StringComparison.Ordinal);
        Assert.Contains("await Task.Run(", lifecycle, StringComparison.Ordinal);
        Assert.Contains(
            "await ProjectAndApplyRunResultAsync(result, build, cancellationSource.Token);",
            lifecycle,
            StringComparison.Ordinal);
        int projectionMethodIndex = lifecycle.IndexOf(
            "internal async Task ProjectAndApplyRunResultAsync(",
            StringComparison.Ordinal);
        int generationIndex = lifecycle.IndexOf(
            "long reportProjectionGeneration = Reports.BeginReportProjection();",
            projectionMethodIndex,
            StringComparison.Ordinal);
        int reportProjectionIndex = lifecycle.IndexOf(
            "ReportReviewViewModel report = await Reports.ProjectReportAsync(",
            generationIndex,
            StringComparison.Ordinal);
        int cancellationIndex = lifecycle.IndexOf(
            "cancellationToken.ThrowIfCancellationRequested();",
            reportProjectionIndex,
            StringComparison.Ordinal);
        int publishIndex = lifecycle.IndexOf("publishReport: Reports.IsCurrentReportProjection(", StringComparison.Ordinal);
        Assert.True(
            projectionMethodIndex >= 0 &&
            generationIndex > projectionMethodIndex &&
            reportProjectionIndex > generationIndex &&
            cancellationIndex > reportProjectionIndex &&
            publishIndex > cancellationIndex,
            "Run cancellation must be rechecked after report projection and before publishing UI/history state.");
        Assert.Contains("long reportProjectionGeneration = Reports.BeginReportProjection();", lifecycle, StringComparison.Ordinal);
        Assert.Contains("result.CommittedOutputId", lifecycle, StringComparison.Ordinal);
        Assert.Contains("materializationErrorsAsReport: false", lifecycle, StringComparison.Ordinal);
        Assert.Contains("inspectionSnapshot: result.InspectionSnapshot", lifecycle, StringComparison.Ordinal);
        Assert.Contains("publishReport: Reports.IsCurrentReportProjection(reportProjectionGeneration)", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectRunReport(", ReadViewModelPartials(), StringComparison.Ordinal);
        Assert.Contains("ReportReviewViewModel.FromJsonCancellable(", ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.cs"), StringComparison.Ordinal);
        Assert.Contains("loadErrorReport(action, exception.Message);", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CompleteRun(cancellationSource);", lifecycle, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(
                ReadViewModelPartials(),
                "catch (OperationCanceledException) when (cancellationSource is { IsCancellationRequested: true })"));
        Assert.Equal(
            1,
            CountOccurrences(
                ReadViewModelPartials(),
                "catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)"));
    }

    /// <summary>Large report projection stays cancellable and UI bindings expose bounded pages.</summary>
    [Fact]
    public void ReportReviewProjectsOffDispatcherAndPagesLargeEvidence()
    {
        string report = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.cs");
        string reportHistory = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.History.cs");
        string localization = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.cs");
        string reportHistoryTemplate = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportHistoryTemplates.axaml");
        string factory = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.Factory.cs");
        string pager = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPagedListViewModel.cs");
        string bindings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.Bindings.cs");
        string parser = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.OutputDifferences.cs");
        string indexedRows = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportIndexedReadOnlyLists.cs");
        string differenceJson = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.OutputDifferenceJson.cs");
        string differenceGroups = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportDifferenceGroupViewModel.cs");
        string changes = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportChangeTemplates.axaml");

        Assert.Contains("await Task.Run(", report, StringComparison.Ordinal);
        Assert.Contains("FromJsonCancellable(", report, StringComparison.Ordinal);
        Assert.Contains("cancellationToken.ThrowIfCancellationRequested();", factory, StringComparison.Ordinal);
        Assert.Contains("new UTF8Encoding(", factory, StringComparison.Ordinal);
        Assert.Contains("throwOnInvalidBytes: true", factory, StringComparison.Ordinal);
        Assert.Contains("private readonly ObservableCollection<object> _items", pager, StringComparison.Ordinal);
        Assert.Contains("ReadOnlyObservableCollection<object> Items", pager, StringComparison.Ordinal);
        Assert.Contains("LoadMoreCommand", pager, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceGroupPage", bindings, StringComparison.Ordinal);
        Assert.Contains("MutationPage", bindings, StringComparison.Ordinal);
        Assert.Contains("IssuePage", bindings, StringComparison.Ordinal);
        Assert.Contains("MemoizedIndexedReadOnlyList<ReportLineViewModel>", parser, StringComparison.Ordinal);
        Assert.Contains("LazyThreadSafetyMode.ExecutionAndPublication", indexedRows, StringComparison.Ordinal);
        Assert.Contains("Utf8JsonReader", differenceJson, StringComparison.Ordinal);
        Assert.Contains("JsonValueSlice", differenceJson, StringComparison.Ordinal);
        Assert.Contains("Encoding.UTF8.GetCharCount", differenceJson, StringComparison.Ordinal);
        Assert.Contains("return AddCharBounds(reportUtf8, slices, cancellationToken);", differenceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("? AddCharBounds(", differenceJson, StringComparison.Ordinal);
        Assert.Contains("SkipJsonValue", differenceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("reader.Skip()", differenceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("differences.Clone()", parser, StringComparison.Ordinal);
        Assert.Contains("reportJson.AsMemory(slice.CharStart, slice.CharLength)", parser, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonDocument.Parse(reportUtf8.Slice", parser, StringComparison.Ordinal);
        Assert.Contains("loadInitialPage: false", differenceGroups, StringComparison.Ordinal);
        Assert.Contains("ReportHexDiffRangeRowTemplate", changes, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportOutputDifferenceGroupTemplate", changes, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderBy", parser, StringComparison.Ordinal);
        Assert.Contains("while (language != Text.Language);", report, StringComparison.Ordinal);
        Assert.Contains("long generation = BeginReportProjection();", report, StringComparison.Ordinal);
        Assert.Contains("if (IsCurrentReportProjection(generation))", report, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Increment(ref _reportProjectionGeneration)", report, StringComparison.Ordinal);
        Assert.Contains("private async Task OpenReportHistoryEntryAsync(", reportHistory, StringComparison.Ordinal);
        Assert.Contains("PreparedReportHistory prepared = await Task.Run(", reportHistory, StringComparison.Ordinal);
        Assert.Contains("if (!IsCurrentReportProjection(generation))", reportHistory, StringComparison.Ordinal);
        Assert.Contains("await ProjectReportAsync(", reportHistory, StringComparison.Ordinal);
        Assert.Contains("if (!IsCurrentReportProjection(generation))", reportHistory, StringComparison.Ordinal);
        Assert.Contains("BeginReportProjection(preserveHistoryReopen: true)", reportHistory, StringComparison.Ordinal);
        Assert.Contains("public IRelayCommand<ReportHistoryEntryViewModel> OpenReportHistoryEntryCommand", reportHistory, StringComparison.Ordinal);
        Assert.Contains("public IAsyncRelayCommand<ReportHistoryEntryViewModel> OpenReportHistoryEntryAsyncCommand", reportHistory, StringComparison.Ordinal);
        Assert.Contains("CancelReportHistoryReopen();", reportHistory, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)", reportHistory, StringComparison.Ordinal);
        Assert.Contains("OpenReportHistoryEntryCommand.NotifyCanExecuteChanged();", reportHistory, StringComparison.Ordinal);
        Assert.Contains("OpenReportHistoryEntryAsyncCommand", reportHistoryTemplate, StringComparison.Ordinal);
        Assert.Contains("RequestReportRelocalization();", localization, StringComparison.Ordinal);
        Assert.Contains("if (!_relocalizeLoadedReportCommand.IsRunning)", localization, StringComparison.Ordinal);
        Assert.Contains("_reportRelocalizationIterationCancellation)?.Cancel();", localization, StringComparison.Ordinal);
        Assert.Contains("private async Task RelocalizeLoadedReportAsync(", reportHistory, StringComparison.Ordinal);
        Assert.Contains("await ProjectReportAsync(", reportHistory, StringComparison.Ordinal);
        Assert.Contains("while (true)", reportHistory, StringComparison.Ordinal);
        Assert.Contains("CreateLinkedTokenSource(cancellationToken)", reportHistory, StringComparison.Ordinal);
        Assert.Contains("requestVersion == Volatile.Read(ref _reportRelocalizationRequestVersion)", reportHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportReviewViewModel.FromJson(\n                LoadedReportJson", reportHistory, StringComparison.Ordinal);
        Assert.Contains("await Reports.ProjectReportAsync(", ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.RunLifecycle.cs"), StringComparison.Ordinal);
    }

    /// <summary>Locks the shared Hex viewport redesign out of v0.9.11 while preserving both existing hosts.</summary>
    [Fact]
    public void V0911KeepsRawEditorAndReportDiffRenderersSeparate()
    {
        string rawEditor = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Views/HexEditorPanel.axaml");
        string reportDiff = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportAuditTemplates.axaml");
        string reportViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHexDiffViewModel.cs");
        string rawEditorViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/HexEditorWorkspaceViewModel.cs");

        Assert.Contains("HexEditorViewportControl", rawEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("HexEditorViewportControl", reportDiff, StringComparison.Ordinal);
        Assert.DoesNotContain("HexEditorWorkspaceViewModel", reportViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportHexDiffViewModel", rawEditorViewModel, StringComparison.Ordinal);
    }

    /// <summary>Prevents retired, unbound shell inspector projections from returning.</summary>
    [Fact]
    public void ShellViewModelOmitsUnboundInspectorCompatibilityState()
    {
        string viewModel = ReadViewModelPartials();

        foreach (string retiredName in new[]
                 {
                     "ActiveMergeRows",
                     "ActiveReplaceRows",
                     "ShowNormalMergeCommand",
                     "ShowGeneralMergeCommand",
                     "RemoveGeneralMergeMappingCommand",
                     "RemoveGeneralReplaceMappingCommand",
                     "GetCtrlRamRegionSummary",
                     "FirmwareIcMismatchSlotTitle",
                     "FooterStatus",
                     "SettingsPreferenceRows",
                     "SelectedStrictness",
                     "CanPreviewStandardMerge",
                     "CanBuildStandardMerge",
                     "CanPreviewMerge",
                     "CanPreviewReplace",
                 })
        {
            Assert.DoesNotContain(retiredName, viewModel, StringComparison.Ordinal);
        }
    }

    /// <summary>Prevents retired, unbound report projections from returning.</summary>
    [Fact]
    public void ReportReviewOmitsUnboundCompatibilityProjections()
    {
        string report = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(Root.FullName, "src/NvtFwCombiner.Presentation.Avalonia/ViewModels"),
                    "Report*ViewModel*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        foreach (string retiredName in new[]
                 {
                     "HasOutputFileName",
                     "public bool? OutputCommitted",
                      "CommandOperationCount",
                      "CommandOperations",
                      "HasCommandOperations",
                      "HasNoCommandOperations",
                     "HasNoStepOperations",
                     "HasNoMutations",
                     "HasOutputDifferenceGroups",
                     "HasNoOutputDifferences",
                     "IsSuccessful",
                     "HasArtifactPath",
                     "InputLabel",
                     "RoleLabel",
                     "HasSectionLabel",
                     "RuntimeCommandsLabel",
                     "public IReadOnlyList<ReportLineViewModel> SummaryRows",
                     "TriageRows",
                     "EvidenceRows",
                     "CreateSummaryRows",
                     "CreateTriageRows",
                     "CreateEvidenceRows",
                 })
        {
            Assert.DoesNotContain(retiredName, report, StringComparison.Ordinal);
        }

        string templates = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportTemplates.axaml");
        Assert.DoesNotContain("ReportEvidenceChipTemplate", templates, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportTriageRowTemplate", templates, StringComparison.Ordinal);
    }

    /// <summary>Prevents unbound memory-coverage and hex-cell projections from returning.</summary>
    [Fact]
    public void PresentationOmitsUnboundCoverageAndHexCellState()
    {
        string coverage = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MemoryCoverageSegmentViewModel.cs");
        string hexCell = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/HexEditorViewportViewModels.cs");

        Assert.DoesNotContain("TooltipText", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("InlineValidationMessage", hexCell, StringComparison.Ordinal);
    }

    /// <summary>Locks stale internal Hex searches to the unfiltered cancellation boundary.</summary>
    [Fact]
    public void HexEditorSearchAcceptsCancellationFromAnObsoleteInternalGeneration()
    {
        string search = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/HexEditorWorkspaceViewModel.Search.cs");

        Assert.Equal(1, CountOccurrences(search, "catch (OperationCanceledException)"));
        Assert.DoesNotContain(
            "catch (OperationCanceledException) when",
            search,
            StringComparison.Ordinal);
    }

    /// <summary>Verifies non-critical local UI stores share one JSON and atomic-promotion mechanism.</summary>
    [Fact]
    public void LocalUiFileStoresShareBestEffortJsonPersistence()
    {
        string helper = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/BestEffortLocalJsonFileStore.cs");
        string mainWindow = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs");
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string settings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Settings.cs");
        string context = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Context.cs");
        string persistenceCoordinator = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/LatestSnapshotPersistenceCoordinator.cs");
        string stores = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ReportHistoryFileStore.cs") +
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ShellPreferenceFileStore.cs");

        Assert.Equal(8, CountOccurrences(stores, "BestEffortLocalJsonFileStore."));
        Assert.DoesNotContain("JsonSerializerOptions", stores, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Replace", stores, StringComparison.Ordinal);
        Assert.Contains("JsonSerializerOptions", helper, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.SerializeAsync", helper, StringComparison.Ordinal);
        Assert.Contains("FileShare.Read | FileShare.Delete", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("FileShare.Write", helper, StringComparison.Ordinal);
        Assert.Contains("File.Replace", helper, StringComparison.Ordinal);
        Assert.Contains("UnauthorizedAccessException", helper, StringComparison.Ordinal);
        Assert.Contains("internal const long MaximumPreferencesFileBytes = 64L * 1024;", stores, StringComparison.Ordinal);
        Assert.Contains("MaximumPreferencesFileBytes);", stores, StringComparison.Ordinal);
        Assert.Contains("_reportHistoryPersistence.Queue", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_shellPreferencePersistence.Queue", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellPreferenceFileStore.LoadInto(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ShellTextResources.LanguageFromPreference(preferences.Language)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private readonly bool _isInitializing = true;", construction, StringComparison.Ordinal);
        Assert.Contains("_isInitializing = false;", construction, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshContextState();", construction, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSettingsState();", construction, StringComparison.Ordinal);
        Assert.Contains("_deferredState.EnsurePage(page, RefreshSettingsState", context, StringComparison.Ordinal);
        Assert.Contains("if (!_isInitializing)", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportHistoryFileStore.Save(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellPreferenceFileStore.Save(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = false", mainWindow, StringComparison.Ordinal);
        Assert.Contains("viewModel.CancelActiveRun();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("finalViewModel.CancelActiveRun();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("completion.WaitAsync(LocalStateCloseFlushTimeout)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_reportHistoryPersistence.CompleteAsync()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_shellPreferencePersistence.CompleteAsync()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Task.Run", persistenceCoordinator, StringComparison.Ordinal);
        Assert.Contains("_latestCancellation?.Cancel()", persistenceCoordinator, StringComparison.Ordinal);
        Assert.Contains("RecordFailure(exception)", persistenceCoordinator, StringComparison.Ordinal);
    }

    /// <summary>Report history retains compact immutable snapshots instead of every fully parsed review graph.</summary>
    [Fact]
    public void ReportHistoryMaterializesOnlyTheOpenedReview()
    {
        string entry = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHistoryEntryViewModel.cs");
        string history = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.History.cs");

        Assert.Contains("private readonly ReportHistorySnapshot snapshot;", entry, StringComparison.Ordinal);
        Assert.Contains("public long StoredByteCount", entry, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportReviewViewModel", entry, StringComparison.Ordinal);
        Assert.Contains("bool materializeAsCurrent = entries.Count == 0;", history, StringComparison.Ordinal);
        Assert.Contains("LoadedReport = prepared.LoadedReport;", history, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadReportHistoryEntry(", history, StringComparison.Ordinal);
        Assert.Contains("private async Task OpenReportHistoryEntryAsync(", history, StringComparison.Ordinal);
        Assert.Contains("report = await ProjectReportAsync(", history, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadReportHistoryEntry(entry);", history, StringComparison.Ordinal);
        Assert.Contains("entry.StoredByteCount", history, StringComparison.Ordinal);
        Assert.DoesNotContain("Encoding.UTF8.GetByteCount(entry.ReportJson)", history, StringComparison.Ordinal);
    }

    /// <summary>Verifies firmware slot model, icons, and fact badges stay split by UI responsibility.</summary>
    [Fact]
    public void FirmwareSlotViewModelConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.cs");
        string icons = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.Icons.cs");
        string replaceRunner = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotFactViewModel.cs");
        string kind = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotKind.cs");

        Assert.Contains("public sealed partial class FirmwareSlotViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public partial string? FilePath", root, StringComparison.Ordinal);
        Assert.Contains("public void ApplyDisplayText", root, StringComparison.Ordinal);
        Assert.Contains("public void SetFirmwareFacts", root, StringComparison.Ordinal);
        Assert.Contains("FirmwareSlotKind kind", root, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareSlotKindResolver", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconPathData", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public IBrush SlotBackgroundBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotBorderBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("RequirementBadgeForegroundBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record FirmwareSlotFactViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum FirmwareSlotKind", root, StringComparison.Ordinal);
        Assert.Contains("SlotIconPathData", icons, StringComparison.Ordinal);
        Assert.Contains("SlotIconTooltip", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia.Media", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconBackgroundBrush", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconBorderBrush", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconForegroundBrush", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("InferSlotKind", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchSlotIds", icons, StringComparison.Ordinal);
        Assert.Contains("WorkbenchReplaceModes.Dp => FirmwareSlotKind.Dp", replaceRunner, StringComparison.Ordinal);
        Assert.Contains("WorkbenchReplaceModes.CtrlRam => FirmwareSlotKind.CtrlRam", replaceRunner, StringComparison.Ordinal);
        Assert.Contains("public sealed record FirmwareSlotFactViewModel", facts, StringComparison.Ordinal);
        Assert.Contains("public enum FirmwareSlotKind", kind, StringComparison.Ordinal);
    }

    /// <summary>Verifies report line rows, chips, groups, and flow nodes stay split by UI responsibility.</summary>
    [Fact]
    public void ReportLineViewModelConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineViewModel.cs");
        string badges = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineBadgeViewModel.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineFactViewModel.cs");
        string rangeRows = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportRangeTableRowViewModel.cs");
        string differenceRows = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportDifferenceSummaryRowViewModel.cs");
        string inputGroups = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportInputGroupViewModel.cs");
        string flowNodes = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportOperationFlowNodeViewModel.cs");

        Assert.Contains("public sealed class ReportLineViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public static ReportLineViewModel Empty", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportLineBadgeViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportLineFactViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record ReportRangeTableRowViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record ReportDifferenceSummaryRowViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportInputGroupViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportOperationFlowNodeViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportLineBadgeViewModel", badges, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportLineFactViewModel", facts, StringComparison.Ordinal);
        Assert.Contains("public sealed record ReportRangeTableRowViewModel", rangeRows, StringComparison.Ordinal);
        Assert.Contains("public sealed record ReportDifferenceSummaryRowViewModel", differenceRows, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportInputGroupViewModel", inputGroups, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportOperationFlowNodeViewModel", flowNodes, StringComparison.Ordinal);
    }
}
