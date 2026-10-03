using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class BuildOutcomeTests
{
    private const int AdmissionTargetStart = 0x3E020;

    /// <summary>
    /// A catalog reload after acceptance is refused by the Application admission (owner decision 68):
    /// the action shows as blocked with the issue code and message, no report is loaded, and the loaded
    /// report and report history stay unchanged.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleCatalogRefusalIsBlockedAndKeepsReportHistory(bool build)
    {
        using var workspace = TempWorkspace.Create("nfc-ui-admission-refusal");
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        AcceptedCompositionExecutionRequest request =
            await PrepareAdmissionRequestAsync(host, workspace, build);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.GeneralMergeOutputLength = "0x40000";
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x0";
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            mapping.MappingId, workspace.PathFor("replacement.bin"), TestContext.Current.CancellationToken);
        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.BuildMergeCommand.CanExecute(null));
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        string previousJson = ReportJsonSamples.Succeeded(runId: "previous-run");
        viewModel.Reports.LoadReportJson(previousJson, "previous.json");
        ReportReviewViewModel previousReport = viewModel.Reports.LoadedReport;
        ReportHistoryEntryViewModel[] previousHistory = [.. viewModel.Reports.ReportHistoryEntries];
        bool previousToast = viewModel.Reports.HasReportToast;
        int reportLoads = 0;
        int executionCalls = 0;

        UiRunResultViewModel? result = await viewModel.RunSession.RunCompositionAsync(
            viewModel.Merge.CaptureRunContext(ExperienceIds.GeneralMerge, build),
            build,
            (progress, cancellationToken) =>
            {
                executionCalls++;
                return host.CompositionExecution.ExecuteAsync(request, progress, cancellationToken);
            },
            (_, _) => reportLoads++);

        Assert.Equal(1, executionCalls);
        Assert.NotNull(result);
        Assert.Same(result, viewModel.RunSession.LastRunResult);
        Assert.Equal(0, reportLoads);
        Assert.False(viewModel.Reports.IsReportModalOpen);
        Assert.Equal(previousToast, viewModel.Reports.HasReportToast);
        Assert.Same(previousReport, viewModel.Reports.LoadedReport);
        Assert.Equal(previousJson, viewModel.Reports.LoadedReportJson);
        Assert.Equal(previousHistory, viewModel.Reports.ReportHistoryEntries);
        Assert.Equal(build ? "Build blocked" : "Preview blocked", viewModel.RunSession.LastRunResult.Title);
        Assert.StartsWith(
            $"{CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale}: The capability catalog was reloaded",
            viewModel.RunSession.LastRunResult.Detail,
            StringComparison.Ordinal);
        Assert.Equal("No output", viewModel.RunSession.LastRunResult.Output);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
    }

    /// <summary>A genuine invariant failure from the same execution still takes the failed path and loads its error report.</summary>
    [Fact]
    public async Task ExecutionInvariantFailureStillOpensFailureReport()
    {
        using var workspace = TempWorkspace.Create("nfc-ui-admission-invariant");
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace", "nt51927-3chip-self-20260705");
        Dictionary<string, string> paths = fixture.GetProperty("artifacts").EnumerateArray()
            .ToDictionary(artifact => artifact.GetProperty("slotId").GetString()!, CanonicalGoldenTestData.ArtifactPath);
        Dictionary<string, byte[]> bytes = paths.ToDictionary(static pair => pair.Key, static pair => File.ReadAllBytes(pair.Value));
        CtrlRamAuthoringSessionPreparation prepared = host.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), "NT51927", "3", paths, bytes);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        var request = new AcceptedCompositionExecutionRequest(
            prepared.AcceptedSession!, paths, build: true, outputPath: workspace.PathFor("output.bin"), actionReadiness: null);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        _ = await viewModel.RunSession.RunCompositionAsync(
            viewModel.Replace.CaptureRunContext(ExperienceIds.CtrlRamReplace),
            build: true,
            (progress, cancellationToken) => host.CompositionExecution.ExecuteAsync(
                request,
                progress,
                cancellationToken),
            (action, message) => viewModel.Reports.LoadRunErrorReport(
                action,
                prepared.AcceptedSession!.ExactCapability!.CompiledComposition.V2Details.ProfileId,
                "NT51927",
                "3",
                message,
                new Dictionary<string, string>()));

        Assert.True(viewModel.Reports.IsReportModalOpen);
        Assert.Equal("Build failed", viewModel.RunSession.LastRunResult.Title);
        Assert.Contains(
            "requires its exact typed action readiness",
            Assert.Single(viewModel.Reports.LoadedReport.Issues).Detail,
            StringComparison.Ordinal);
        Assert.Equal("No output", viewModel.RunSession.LastRunResult.Output);
    }

    private static async Task<AcceptedCompositionExecutionRequest> PrepareAdmissionRequestAsync(
        CompositionHostServices host,
        TempWorkspace workspace,
        bool build)
    {
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        var draft = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "admission-map",
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(replacementPath),
                new ByteRange(0, 2),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(AdmissionTargetStart, 2),
                OverlapPolicy.Reject,
                alignment: 1,
                "UI execution admission fixture."),
        ]);
        GeneralAuthoringSessionPreparation prepared = await host.GeneralAuthoring.PrepareMergeSessionAsync(
            new AuthoringSessionState(ExperienceIds.GeneralMerge),
            "NT51926",
            new GeneralMergeDraftState(new GeneralMergeOutputInitializer(0x40000), draft),
            TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        return new AcceptedCompositionExecutionRequest(
            prepared.AcceptedSession!,
            new Dictionary<string, string>(StringComparer.Ordinal),
            build,
            outputPath: build ? workspace.PathFor("output.bin") : null,
            actionReadiness: prepared.Readiness);
    }
}
