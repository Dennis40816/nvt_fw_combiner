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
        CompositionHostServices host = CompositionHostServices.Create();
        AcceptedCompositionExecutionRequest request =
            await PrepareAdmissionRequestAsync(host, workspace, build, withReadiness: true);
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        string previousJson = ReportJsonSamples.Succeeded(runId: "previous-run");
        viewModel.Reports.LoadReportJson(previousJson, "previous.json");
        ReportReviewViewModel previousReport = viewModel.Reports.LoadedReport;
        ReportHistoryEntryViewModel[] previousHistory = [.. viewModel.Reports.ReportHistoryEntries];
        bool previousToast = viewModel.Reports.HasReportToast;
        int reportLoads = 0;

        _ = await viewModel.RunSession.RunCompositionAsync(
            viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode),
            build,
            (progress, cancellationToken) => host.CompositionExecution.ExecuteAsync(
                request,
                progress,
                cancellationToken),
            (_, _) => reportLoads++);

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
        CompositionHostServices host = CompositionHostServices.Create();
        AcceptedCompositionExecutionRequest request =
            await PrepareAdmissionRequestAsync(host, workspace, build: true, withReadiness: false);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        _ = await viewModel.RunSession.RunCompositionAsync(
            viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode),
            build: true,
            (progress, cancellationToken) => host.CompositionExecution.ExecuteAsync(
                request,
                progress,
                cancellationToken),
            (action, message) => viewModel.Reports.LoadRunErrorReport(
                action,
                "nt51926-general-replace",
                "NT51926",
                "single",
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
        bool build,
        bool withReadiness)
    {
        byte[] baseBytes = FirmwareByteTestData.CreatePattern(0x40000, 0x51);
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        var draft = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "admission-map",
                ExplicitMappingOperationKind.ReplaceRange,
                GeneralMappingSource.File(replacementPath),
                new ByteRange(0, 2),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(AdmissionTargetStart, 2),
                OverlapPolicy.Reject,
                alignment: 1,
                "UI execution admission fixture."),
        ]);
        GeneralAuthoringSessionPreparation prepared = await host.GeneralAuthoring.PrepareReplaceSessionAsync(
            new AuthoringSessionState(ExperienceIds.GeneralReplace),
            "NT51926",
            "single",
            basePath,
            draft,
            TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        return new AcceptedCompositionExecutionRequest(
            prepared.AcceptedSession!,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionSlotIds.ReplaceBase] = basePath,
            },
            build,
            outputPath: build ? workspace.PathFor("output.bin") : null,
            actionReadiness: withReadiness ? prepared.Readiness : null);
    }
}
