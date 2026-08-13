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
        string mergeState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.State.cs");
        string mergeRequirements = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Requirements.cs");
        string mergeGeneral = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.General.cs");
        string mergeMemory = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Memory.cs");
        string mergeExecution = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Execution.cs");
        string mergePrompt = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.AbAFlashCodeDeliveryPrompt.cs");
        string shellPartials = ReadViewModelPartials();
        string shell = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");
        string shellCode = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs");

        Assert.Contains("Merge = new MergePresentationViewModel", construction, StringComparison.Ordinal);
        Assert.Contains("public MergePresentationViewModel Merge", shellMerge, StringComparison.Ordinal);
        Assert.Contains("public ObservableCollection<FirmwareSlotViewModel> MergeSlots", mergeState, StringComparison.Ordinal);
        Assert.Contains("public string MergeReadinessStatus", mergeState, StringComparison.Ordinal);
        Assert.Contains("RefreshMergeSlotRequirements", mergeRequirements, StringComparison.Ordinal);
        Assert.Contains("AddGeneralMergeMapping", mergeGeneral, StringComparison.Ordinal);
        Assert.Contains("RefreshMergeMemoryMapState", mergeMemory, StringComparison.Ordinal);
        Assert.Contains("public Task BuildMergeAsync", mergeExecution, StringComparison.Ordinal);
        Assert.Contains("PromptForAbAFlashCodeDeliveryAsync", mergePrompt, StringComparison.Ordinal);
        Assert.Contains("DataTemplate DataType=\"vm:MergePresentationViewModel\"", shell, StringComparison.Ordinal);
        Assert.Contains("LoadContent(MergePageHost, viewModel.IsMergeVisible, viewModel.Merge)", shellCode, StringComparison.Ordinal);
        Assert.DoesNotContain("public ObservableCollection<FirmwareSlotViewModel> MergeSlots", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public string MergeReadinessStatus", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public Task BuildMergeAsync", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool IsAbAFlashCodeDeliveryPromptOpen", shellPartials, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels",
            "MainWindowViewModel.AbAFlashCodeDeliveryPrompt.cs")));
    }

    /// <summary>Replace-only selection policy and modal lifetime belong to a focused child.</summary>
    [Fact]
    public void ReplacePresentationLivesBehindFocusedChild()
    {
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string shellReplace = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.ReplacePresentation.cs");
        string replaceState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.State.cs");
        string replaceMemory = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Memory.cs");
        string replaceGeneral = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.General.cs");
        string replaceExecution = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs");
        string ctrlRamVersion = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.CtrlRamFirmwareVersion.cs");
        string replaceSelection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Selection.cs");
        string shellPartials = ReadViewModelPartials();
        string shell = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");
        string shellCode = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs");

        Assert.Contains("Replace = new ReplacePresentationViewModel", construction, StringComparison.Ordinal);
        Assert.Contains("public ReplacePresentationViewModel Replace", shellReplace, StringComparison.Ordinal);
        Assert.Contains("public ObservableCollection<FirmwareSlotViewModel> ReplaceSlots", replaceState, StringComparison.Ordinal);
        Assert.Contains("public string SelectedReplaceMode", replaceState, StringComparison.Ordinal);
        Assert.Contains("RefreshReplaceMemoryMapState", replaceMemory, StringComparison.Ordinal);
        Assert.Contains("AddGeneralReplaceMapping", replaceGeneral, StringComparison.Ordinal);
        Assert.Contains("public Task BuildReplaceAsync", replaceExecution, StringComparison.Ordinal);
        Assert.Contains("TryOpenCtrlRamFirmwareVersionModalAsync", ctrlRamVersion, StringComparison.Ordinal);
        Assert.Contains("CreateReplaceSelectionMissingRows", replaceSelection, StringComparison.Ordinal);
        Assert.Contains("DataTemplate DataType=\"vm:ReplacePresentationViewModel\"", shell, StringComparison.Ordinal);
        Assert.Contains("LoadContent(ReplacePageHost, viewModel.IsReplaceVisible, viewModel.Replace)", shellCode, StringComparison.Ordinal);
        Assert.DoesNotContain("public ObservableCollection<FirmwareSlotViewModel> ReplaceSlots", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public string SelectedReplaceMode", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public Task BuildReplaceAsync", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool IsCtrlRamFirmwareVersionModalOpen", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool IsReplaceSelectionModalOpen", shellPartials, StringComparison.Ordinal);
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
        string deviceContext = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.DeviceContext.cs");
        string slots = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.Slots.cs");
        string mergeState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.State.cs");
        string replaceState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.State.cs");
        string shellPartials = ReadViewModelPartials();

        Assert.Contains("WorkflowSession = new WorkflowSessionPresentationViewModel", construction, StringComparison.Ordinal);
        Assert.Contains("public WorkflowSessionPresentationViewModel WorkflowSession", shellSession, StringComparison.Ordinal);
        Assert.Contains("WorkflowContextSetupViewModel", context, StringComparison.Ordinal);
        Assert.Contains("ReconcileFirmwareIcMismatch", mismatch, StringComparison.Ordinal);
        Assert.Contains("FirmwareInspectionSession", session, StringComparison.Ordinal);
        Assert.Contains("InspectionSession.ReadBatch", inspection, StringComparison.Ordinal);
        Assert.Contains("SetSlotFileAsync", inspection, StringComparison.Ordinal);
        Assert.Contains("public string SelectedIc", deviceContext, StringComparison.Ordinal);
        Assert.Contains("if (SetProperty(ref _selectedIc, value))", deviceContext, StringComparison.Ordinal);
        Assert.Contains("OnSelectedIcChanged(value);", deviceContext, StringComparison.Ordinal);
        Assert.Contains("internal void PublishCanonicalCatalogState()", deviceContext, StringComparison.Ordinal);
        Assert.Contains(
            "_selectedIc = defaultIcId;",
            deviceContext,
            StringComparison.Ordinal);
        Assert.Contains("public partial string SelectedNumber", deviceContext, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Capabilities.GetIcFamilySummary", deviceContext, StringComparison.Ordinal);
        Assert.Contains("private FirmwareSlotViewModel? SelectSlotFile", slots, StringComparison.Ordinal);
        Assert.Contains("public void RemoveGeneralMappingRow", slots, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.OutputNaming.ResolveAcceptedOutput", mergeState, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.OutputNaming.ResolveAcceptedOutput", replaceState, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels",
            "WorkflowSessionPresentationViewModel.OutputNaming.cs")));
        Assert.DoesNotContain("FirmwareInspectionSession", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSlotFileAsync", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public string SelectedIc", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public partial string SelectedNumber", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("private FirmwareSlotViewModel? SelectSlotFile", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("public void RemoveGeneralMappingRow", shellPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareOutputNamingProjection", shellPartials, StringComparison.Ordinal);
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

    /// <summary>Verifies the Presentation projection keeps only UI-owned contract adaptation.</summary>
    [Fact]
    public void UiCompositionRunnerConcernsStaySplit()
    {
        string catalog = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Catalog.cs");
        string common = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs");
        string replace = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");
        string deviceContext = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.DeviceContext.cs");
        string mergeViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Execution.cs");
        string replaceViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs");

        Assert.Contains("GetNumberSelectionChoices", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDefaultIcId", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("private static MemoryMapRowViewModel ToMemoryMapRow", common, StringComparison.Ordinal);
        Assert.Contains("GetFirmwareSlotFacts", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", facts, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.DpReplace", ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs"), StringComparison.Ordinal);
        Assert.Contains("GetMemoryDisplay", common, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "UiCompositionRunner.Merge.cs")));
        Assert.DoesNotContain("GetReplaceMemoryDisplay", replace, StringComparison.Ordinal);
        Assert.Contains("GetSelectedReplaceMemoryDisplay", ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Memory.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("RunReplaceAsync", replace, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Capabilities.GetIcIds", deviceContext, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Execution.ExecuteAsync", mergeViewModel, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Execution.ExecuteAsync", replaceViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalCapabilityProjection", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionExecutionAdapter", mergeViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionExecutionAdapter", replaceViewModel, StringComparison.Ordinal);
    }

    /// <summary>Verifies all composition commands share one UI-owned run lifecycle.</summary>
    [Fact]
    public void CompositionCommandsShareRunLifecycle()
    {
        string lifecycle = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunPresentationViewModel.cs");
        string merge = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Execution.cs");
        string replace = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs");
        string selection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");
        Assert.Equal(3, CountOccurrences(merge, "return RunCompositionAsync("));
        Assert.Equal(1, CountOccurrences(replace, "await RunCompositionAsync("));
        Assert.Equal(3, CountOccurrences(merge, "_compositionServices.Execution.ExecuteAsync"));
        Assert.Equal(1, CountOccurrences(replace, "_compositionServices.Execution.ExecuteAsync"));
        Assert.Contains("AcceptedCompositionExecutionRequest", merge, StringComparison.Ordinal);
        Assert.DoesNotContain("InspectGeneralSelectedFilesAsync", merge, StringComparison.Ordinal);
        Assert.DoesNotContain("RunGeneralMergeEphemeralDraftWithProgressAsync", merge, StringComparison.Ordinal);
        Assert.Contains("AcceptedCompositionExecutionRequest", replace, StringComparison.Ordinal);
        Assert.DoesNotContain("InspectGeneralSelectedFilesAsync", replace, StringComparison.Ordinal);
        Assert.DoesNotContain("RunGeneralReplaceEphemeralDraftWithProgressAsync", replace, StringComparison.Ordinal);
        Assert.DoesNotContain("InspectGeneralSelectedFileAsync", selection, StringComparison.Ordinal);
        Assert.Contains("GeneralMergeReadinessRefreshTask", selection, StringComparison.Ordinal);
        Assert.Contains("GeneralReplaceReadinessRefreshTask", selection, StringComparison.Ordinal);
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
            "long reportProjectionGeneration = reports.BeginReportProjection();",
            projectionMethodIndex,
            StringComparison.Ordinal);
        int reportProjectionIndex = lifecycle.IndexOf(
            "Task<ReportReviewViewModel> projectionTask = reports.ProjectReportAsync(",
            generationIndex,
            StringComparison.Ordinal);
        int cancellationIndex = lifecycle.IndexOf(
            "cancellationToken.ThrowIfCancellationRequested();",
            reportProjectionIndex,
            StringComparison.Ordinal);
        int publishIndex = lifecycle.IndexOf("publishReport: reports.IsCurrentReportProjection(", StringComparison.Ordinal);
        Assert.True(
            projectionMethodIndex >= 0 &&
            generationIndex > projectionMethodIndex &&
            reportProjectionIndex > generationIndex &&
            cancellationIndex > reportProjectionIndex &&
            publishIndex > cancellationIndex,
            "Run cancellation must be rechecked after report projection and before publishing UI/history state.");
        Assert.Contains("long reportProjectionGeneration = reports.BeginReportProjection();", lifecycle, StringComparison.Ordinal);
        Assert.Contains("result.CommittedOutputId", lifecycle, StringComparison.Ordinal);
        Assert.Contains("materializationErrorsAsReport: false", lifecycle, StringComparison.Ordinal);
        Assert.Contains("inspectionSnapshot: result.InspectionSnapshot", lifecycle, StringComparison.Ordinal);
        Assert.Contains("result.Report,", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "reports.ProjectReportAsync(\n            reportJson,",
            lifecycle,
            StringComparison.Ordinal);
        Assert.Contains("publishReport: reports.IsCurrentReportProjection(reportProjectionGeneration)", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectRunReport(", ReadViewModelPartials(), StringComparison.Ordinal);
        string reportPresentation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.cs");
        Assert.Contains("ReportReviewViewModel.FromJsonCancellable(", reportPresentation, StringComparison.Ordinal);
        Assert.Contains("ReportReviewViewModel.FromReportCancellable(", reportPresentation, StringComparison.Ordinal);
        Assert.Contains("loadErrorReport(action, exception.Message);", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CompleteRun(cancellationSource);", lifecycle, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(
                lifecycle,
                "catch (OperationCanceledException) when (cancellationSource is { IsCancellationRequested: true })"));
        Assert.Equal(
            1,
            CountOccurrences(
                lifecycle,
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
        Assert.DoesNotContain("internal OutputDifferenceProjection(", parser, StringComparison.Ordinal);
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
        string runPresentation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunPresentationViewModel.cs");
        Assert.Contains(
            "Task<ReportReviewViewModel> projectionTask = reports.ProjectReportAsync(",
            runPresentation,
            StringComparison.Ordinal);
        Assert.Contains("await Task.WhenAll(reportJsonTask, projectionTask);", runPresentation, StringComparison.Ordinal);
    }

    /// <summary>Locks #191/#192 to one read-only viewport with separate source adapters.</summary>
    [Fact]
    public void HexViewportIsReadOnlyAndSourceAdaptersStaySeparate()
    {
        string rawEditor = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Views/HexEditorPanel.axaml");
        string reportDiff = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportAuditTemplates.axaml");
        string reportViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHexDiffViewModel.cs");
        string reportAdapter = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHexDiffViewportAdapter.cs");
        string binAdapter = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/BinInspectorViewportAdapter.cs");
        string binHost = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Views/BinInspectorPanel.axaml");
        string binViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/BinInspectorViewModel.cs");
        string binFactory = ReadText(
            "src/NvtFwCombiner.Application/Metadata/FirmwareBinInspectionSnapshot.cs");
        string rawEditorViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/HexEditorWorkspaceViewModel.cs");
        string viewport = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(Root.FullName, "src", "NvtFwCombiner.Presentation.Avalonia", "Views"),
                    "HexViewportControl*.cs")
                .Select(File.ReadAllText));

        Assert.Contains("HexViewportControl", rawEditor, StringComparison.Ordinal);
        Assert.Contains("HexViewportControl", reportDiff, StringComparison.Ordinal);
        Assert.Contains("HexViewportControl", binHost, StringComparison.Ordinal);
        Assert.Contains("HexViewportCapabilityProfile.ReportDiff", reportAdapter, StringComparison.Ordinal);
        Assert.Contains("HexViewportCapabilityProfile.BinInspector", binAdapter, StringComparison.Ordinal);
        Assert.Contains("FirmwareBinInspectionSnapshot inspection", binViewModel, StringComparison.Ordinal);
        Assert.Contains("FirmwareMetadataInspectionFormatter.Format(inspection)", binFactory, StringComparison.Ordinal);
        Assert.Contains("Matches(identity, artifact)", binFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("FormattedMetadataStructure metadata,", binViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("HexEditorWorkspaceViewModel", reportViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportHexDiffViewModel", rawEditorViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("HexEditorWorkspaceViewModel", reportAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportHexDiffViewModel", binAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("Overwrite", reportAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("Overwrite", binAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("HexEditorWorkspaceViewModel", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("IRelayCommand", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("RawBinaryEditorSession", viewport, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels",
            "HexEditorViewportViewModels.cs")));
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
            "src/NvtFwCombiner.Presentation.Avalonia/HexViewport/HexViewportContracts.cs");

        Assert.DoesNotContain("TooltipText", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("InlineValidationMessage", hexCell, StringComparison.Ordinal);
        Assert.DoesNotContain("EditValue", hexCell, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEditing", hexCell, StringComparison.Ordinal);
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

    /// <summary>Verifies local UI state and report inputs share one bounded platform file adapter.</summary>
    [Fact]
    public void LocalUiFileStoresShareOneBoundedPlatformAdapter()
    {
        string adapter = ReadText("src/NvtFwCombiner.Infrastructure/Files/LocalFileStore.cs");
        string codec = ReadText("src/NvtFwCombiner.Presentation.Avalonia/LocalJsonDocument.cs");
        string mainWindow = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs");
        string reportInput = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.Report.cs");
        string history = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ReportHistoryFileStore.cs");
        string historyProjection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.History.cs");
        string bootstrap = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
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

        Assert.Contains("LocalFiles = new LocalFileStore();", bootstrap, StringComparison.Ordinal);
        Assert.Contains("public static ILocalFileStore CreateLocalFileStore()", bootstrap, StringComparison.Ordinal);
        Assert.Contains("FileShare.Read | FileShare.Delete", adapter, StringComparison.Ordinal);
        Assert.Contains("AtomicFileWriteScope.Open(fullPath)", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllText", reportInput, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadToEndAsync", reportInput, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", reportInput, StringComparison.Ordinal);
        Assert.Contains("MaximumStandaloneReportBytes = 10L * 1024 * 1024", reportInput, StringComparison.Ordinal);
        Assert.Contains("MaximumHistoryFileBytes = 64L * 1024 * 1024", history, StringComparison.Ordinal);
        Assert.Contains("ReportPresentationViewModel.MaximumReportHistoryStorageBytes", history, StringComparison.Ordinal);
        Assert.Contains("EntryTooLargeToPersist", history, StringComparison.Ordinal);
        Assert.Contains("OmitDerivableReportHistoryMetadata", history, StringComparison.Ordinal);
        Assert.Contains("normalized!.Metadata == snapshot.Metadata", historyProjection, StringComparison.Ordinal);
        Assert.Contains("RemoveAt(retained.Count - 1)", history, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", stores, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializerOptions", stores, StringComparison.Ordinal);
        Assert.Contains("JsonSerializerOptions", codec, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.DeserializeAsync", codec, StringComparison.Ordinal);
        Assert.Contains("internal const long MaximumPreferencesFileBytes = 64L * 1024;", stores, StringComparison.Ordinal);
        Assert.Contains("MaximumPreferencesFileBytes,", stores, StringComparison.Ordinal);
        Assert.Contains("_reportHistoryPersistence.Queue", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_shellPreferencePersistence.Queue", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellPreferenceFileStore.LoadInto(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ShellTextResources.LanguageFromPreference(startupPreferences.Language)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private readonly bool _isInitializing = true;", construction, StringComparison.Ordinal);
        Assert.Contains("_isInitializing = false;", construction, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshContextState();", construction, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSettingsState();", construction, StringComparison.Ordinal);
        Assert.Contains("_deferredState.EnsureSettings(RefreshSettingsState)", context, StringComparison.Ordinal);
        Assert.Contains("WorkflowSession.EnsureWorkflowLoaded()", context, StringComparison.Ordinal);
        Assert.Contains("if (!_isInitializing)", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportHistoryFileStore.Save(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellPreferenceFileStore.Save(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = false", mainWindow, StringComparison.Ordinal);
        Assert.Contains("viewModel.RunSession.CancelActiveRun();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("finalViewModel.RunSession.CancelActiveRun();", mainWindow, StringComparison.Ordinal);
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
