using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>The normal run-result projection opens the confirmation for its committed output.</summary>
    [Fact]
    public async Task CompletedBuildProjectionOpensOutputConfirmation()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        CompositionRunResult result = await CreateDpReplaceInspectionResultAsync();
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        await viewModel.RunSession.ProjectAndApplyRunResultAsync(
            WithCommittedOutputId(result, outputPath),
            build: true,
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.BuildResult.IsOpen);
        Assert.Equal(outputPath, viewModel.BuildResult.OutputPath);
        Assert.Equal(outputPath, viewModel.BuildResult.LatestCommittedOutputPath);
        Assert.True(viewModel.BuildResult.HasLatestCommittedOutput);
    }

    /// <summary>A blocked Build opens its structured report immediately so the no-output reason is visible.</summary>
    [Fact]
    public async Task BlockedBuildProjectionOpensFailureReport()
    {
        CompositionRunResult result = CreateRunResult(
            succeeded: false,
            outputPath: null,
            [new CompositionIssue(
                "processor.tool.missing",
                "Combiner executable is not available.",
                "run-ctrlram-postbuild")]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        await viewModel.RunSession.ProjectAndApplyRunResultAsync(
            result,
            build: true,
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.Reports.IsReportModalOpen);
        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.Contains("Combiner executable is not available", viewModel.RunSession.LastRunResult.Detail, StringComparison.Ordinal);
    }

    /// <summary>A blocked Preview retains the report toast without treating its expected lack of committed output as Build failure.</summary>
    [Fact]
    public async Task BlockedPreviewDoesNotAutoOpenFailureReport()
    {
        CompositionRunResult result = CreateRunResult(
            succeeded: false,
            outputPath: null,
            [new CompositionIssue(
                "processor.tool.missing",
                "Combiner executable is not available.",
                "run-ctrlram-postbuild")]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        await viewModel.RunSession.ProjectAndApplyRunResultAsync(
            result,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(viewModel.Reports.IsReportModalOpen);
        Assert.True(viewModel.Reports.HasReportToast);
    }

    /// <summary>A Build exception also opens the structured UI error report produced by the workflow callback.</summary>
    [Fact]
    public async Task BuildExceptionOpensFailureReport()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        await viewModel.RunSession.RunCompositionAsync(
            build: true,
            (_, _) => throw new InvalidOperationException("No exact CtrlRAM route."),
            (action, message) => viewModel.Reports.LoadRunErrorReport(
                action,
                "nt51926-ctrlram-replace-workbench",
                "NT51926",
                "single",
                message,
                new Dictionary<string, string>()));

        Assert.True(viewModel.Reports.IsReportModalOpen);
        Assert.Equal("No exact CtrlRAM route.", Assert.Single(viewModel.Reports.LoadedReport.Issues).Detail);
        Assert.Equal("No output", viewModel.RunSession.LastRunResult.Output);
    }

    /// <summary>A committed Build opens one confirmation while OK retains the latest-output shortcut.</summary>
    [Fact]
    public void CommittedBuildShowsOutputConfirmation()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Assert.True(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: true));
        Assert.True(viewModel.BuildResult.IsOpen);
        Assert.Equal(outputPath, viewModel.BuildResult.OutputPath);
        Assert.Equal(outputPath, viewModel.BuildResult.LatestCommittedOutputPath);

        viewModel.BuildResult.CloseCommand.Execute(null);

        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.Equal(string.Empty, viewModel.BuildResult.OutputPath);
        Assert.Equal(outputPath, viewModel.BuildResult.LatestCommittedOutputPath);
        Assert.True(viewModel.BuildResult.HasLatestCommittedOutput);
    }

    /// <summary>The A FlashCode choice resolves before output selection and cannot leave the prompt open after either answer.</summary>
    [Fact]
    public async Task AbAFlashCodeDeliveryPromptResolvesBeforeBuild()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Task<bool> yes = viewModel.Merge.PromptForAbAFlashCodeDeliveryAsync();
        Assert.True(viewModel.Merge.IsAbAFlashCodeDeliveryPromptOpen);
        viewModel.Merge.AcceptAbAFlashCodeDeliveryPromptCommand.Execute(null);

        Assert.True(await yes);
        Assert.False(viewModel.Merge.IsAbAFlashCodeDeliveryPromptOpen);

        Task<bool> no = viewModel.Merge.PromptForAbAFlashCodeDeliveryAsync();
        viewModel.Merge.DeclineAbAFlashCodeDeliveryPromptCommand.Execute(null);

        Assert.False(await no);
        Assert.False(viewModel.Merge.IsAbAFlashCodeDeliveryPromptOpen);
    }

    /// <summary>Completion shows the already-delivered A FlashCode rather than offering a second post-Build export action.</summary>
    [Fact]
    public void CompletedBuildShowsAlreadyDeliveredAFlashCode()
    {
        string primaryPath = Path.Combine(Path.GetTempPath(), "output", "ab.bin");
        string aFlashCodePath = Path.Combine(Path.GetTempPath(), "output", "a.bin");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        CompositionRunResult result = WithDelivery(
            CreateRunResult(succeeded: true, primaryPath),
            [
                new CompositionDeliveryArtifact(
                    "ab-a-flashcode",
                    aFlashCodePath,
                    "a.bin",
                    0x40000,
                    new ByteRange(0, 0x40000),
                    "a-hash"),
            ]);

        Assert.True(viewModel.TryShowBuildCompleted(result, build: true));
        Assert.True(viewModel.BuildResult.HasAdditionalOutput);
        Assert.Equal(aFlashCodePath, viewModel.BuildResult.AdditionalOutputPath);
        Assert.Equal("a.bin", viewModel.BuildResult.AdditionalOutputDisplayName);
    }

    /// <summary>A failed second delivery keeps the primary artifact discoverable but never opens the all-success completion modal.</summary>
    [Fact]
    public void PartialDeliveryDoesNotClaimBuildCompletion()
    {
        string primaryPath = Path.Combine(Path.GetTempPath(), "output", "ab.bin");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        CompositionRunResult result = WithDelivery(
            CreateRunResult(succeeded: true, primaryPath),
            [],
            isDeliveryComplete: false,
            deliveryFailureMessage: "A FlashCode delivery failed.");

        Assert.False(viewModel.TryShowBuildCompleted(result, build: true));
        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.Equal(primaryPath, viewModel.BuildResult.LatestCommittedOutputPath);
    }

    /// <summary>Preview, blocked Build, and uncommitted success cannot claim that a BIN is ready.</summary>
    [Fact]
    public void OutputConfirmationRequiresSuccessfulCommittedBuild()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        Assert.False(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: false));
        Assert.False(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: false, outputPath), build: true));
        Assert.False(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath: null), build: true));
        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.False(viewModel.BuildResult.HasLatestCommittedOutput);
    }

    /// <summary>The latest-output shortcut is scoped to composition pages and survives later failed runs.</summary>
    [Fact]
    public void LatestOutputActionTracksCommittedOutputAndCompositionPage()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "output", "firmware.bin");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        OpenReplace(viewModel, ExperienceIds.DpReplace);

        Assert.False(viewModel.IsLatestOutputActionVisible);
        Assert.True(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: true));
        Assert.False(viewModel.IsCompositionActionRailVisible);
        Assert.False(viewModel.IsLatestOutputActionVisible);
        viewModel.BuildResult.Close();

        Assert.True(viewModel.IsCompositionActionRailVisible);
        Assert.True(viewModel.IsLatestOutputActionVisible);
        Assert.False(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: false, outputPath: "another.bin"), build: true));
        Assert.Equal(outputPath, viewModel.BuildResult.LatestCommittedOutputPath);

        viewModel.ShowHomeCommand.Execute(null);

        Assert.False(viewModel.IsLatestOutputActionVisible);
    }

    /// <summary>Every blocking shell surface suppresses and restores the action rail with bindable notifications.</summary>
    [Theory]
    [InlineData("replace-selection")]
    [InlineData("ctrlram-version")]
    [InlineData("workflow-context")]
    [InlineData("firmware-ic-mismatch")]
    [InlineData("firmware-number-mismatch")]
    [InlineData("navigation-clear")]
    [InlineData("report")]
    [InlineData("ab-a-flashcode-delivery")]
    [InlineData("build-completed")]
    [InlineData("hex-insert")]
    [InlineData("hex-save")]
    public async Task EveryBlockingSurfaceSuppressesAndRestoresTheCompositionActionRail(string surface)
    {
        using StandardMergeGoldenManifest? golden = surface == "ctrlram-version"
            ? StandardMergeGoldenManifest.Load()
            : null;
        using TempWorkspace? workspace = surface == "ctrlram-version"
            ? TempWorkspace.Create("nvt-fw-combiner-ui-rail-blocking-surface")
            : null;
        MainWindowViewModel viewModel = surface == "ctrlram-version"
            ? CreateCtrlRamVersionReadyViewModel(
                golden!.ReadExpectedOutput(golden.CaseByIc("51926")),
                workspace!)
            : PresentationTestHost.CreateViewModel();
        if (surface != "ctrlram-version")
        {
            OpenReplace(viewModel, ExperienceIds.DpReplace);
        }

        Assert.True(viewModel.IsCompositionActionRailVisible);
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        await SetBlockingSurfaceOpenAsync(viewModel, surface);

        Assert.True(IsBlockingSurfaceOpen(viewModel, surface));
        Assert.False(viewModel.IsCompositionActionRailVisible);
        Assert.Contains(nameof(MainWindowViewModel.IsCompositionActionRailVisible), notifications);
        Assert.Contains(nameof(MainWindowViewModel.IsLatestOutputActionVisible), notifications);

        notifications.Clear();
        SetBlockingSurfaceClosed(viewModel, surface);

        Assert.False(IsBlockingSurfaceOpen(viewModel, surface));
        Assert.True(viewModel.IsCompositionActionRailVisible);
        Assert.Contains(nameof(MainWindowViewModel.IsCompositionActionRailVisible), notifications);
        Assert.Contains(nameof(MainWindowViewModel.IsLatestOutputActionVisible), notifications);
    }

    /// <summary>The general reveal command forwards source and generated paths without rewriting them.</summary>
    [Theory]
    [InlineData(@"C:\firmware\selected source.bin")]
    [InlineData(@"C:\output\generated.bin")]
    public void RevealFileCommandForwardsExactPath(string filePath)
    {
        var reveal = new RecordingFileRevealService(result: true);
        MainWindowViewModel viewModel = CreateFileRevealViewModel(reveal);

        viewModel.RevealFileCommand.Execute(filePath);

        Assert.Equal([filePath], reveal.Paths);
    }

    private static async Task SetBlockingSurfaceOpenAsync(MainWindowViewModel viewModel, string surface)
    {
        switch (surface)
        {
            case "replace-selection":
                viewModel.Replace.ShowReplaceSelectionCommand.Execute(null);
                break;
            case "ctrlram-version":
                Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(TestContext.Current.CancellationToken));
                break;
            case "workflow-context":
                viewModel.WorkflowSession.IsWorkflowContextModalOpen = true;
                break;
            case "firmware-ic-mismatch":
                viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen = true;
                break;
            case "firmware-number-mismatch":
                viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen = true;
                break;
            case "navigation-clear":
                viewModel.IsNavigationClearConfirmationOpen = true;
                break;
            case "report":
                viewModel.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "rail-blocking-surface"), "report.json");
                viewModel.Reports.ShowReportCommand.Execute(null);
                break;
            case "build-completed":
                Assert.True(viewModel.TryShowBuildCompleted(
                    CreateRunResult(succeeded: true, outputPath: "output.bin"),
                    build: true));
                break;
            case "ab-a-flashcode-delivery":
                _ = viewModel.Merge.PromptForAbAFlashCodeDeliveryAsync();
                break;
            case "hex-insert":
                viewModel.HexEditorWorkspace.IsInsertBytesPromptOpen = true;
                break;
            case "hex-save":
                viewModel.HexEditorWorkspace.IsSaveConfirmationOpen = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown blocking surface.");
        }
    }

    private static void SetBlockingSurfaceClosed(MainWindowViewModel viewModel, string surface)
    {
        switch (surface)
        {
            case "replace-selection":
                viewModel.Replace.CloseReplaceSelectionCommand.Execute(null);
                break;
            case "ctrlram-version":
                viewModel.Replace.CloseCtrlRamFirmwareVersionModal();
                break;
            case "workflow-context":
                viewModel.WorkflowSession.CancelWorkflowContextCommand.Execute(null);
                break;
            case "firmware-ic-mismatch":
                viewModel.WorkflowSession.DismissFirmwareIcMismatchCommand.Execute(null);
                break;
            case "firmware-number-mismatch":
                viewModel.WorkflowSession.DismissFirmwareNumberMismatchCommand.Execute(null);
                break;
            case "navigation-clear":
                viewModel.CancelNavigationClearCommand.Execute(null);
                break;
            case "report":
                viewModel.Reports.CloseReportCommand.Execute(null);
                break;
            case "build-completed":
                viewModel.BuildResult.Close();
                break;
            case "ab-a-flashcode-delivery":
                viewModel.Merge.DeclineAbAFlashCodeDeliveryPromptCommand.Execute(null);
                break;
            case "hex-insert":
                viewModel.HexEditorWorkspace.CancelInsertBytesCommand.Execute(null);
                break;
            case "hex-save":
                viewModel.HexEditorWorkspace.CancelSaveCommand.Execute(null);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown blocking surface.");
        }
    }

    private static bool IsBlockingSurfaceOpen(MainWindowViewModel viewModel, string surface)
    {
        return surface switch
        {
            "replace-selection" => viewModel.Replace.IsReplaceSelectionModalOpen,
            "ctrlram-version" => viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen,
            "workflow-context" => viewModel.WorkflowSession.IsWorkflowContextModalOpen,
            "firmware-ic-mismatch" => viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen,
            "firmware-number-mismatch" => viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen,
            "navigation-clear" => viewModel.IsNavigationClearConfirmationOpen,
            "report" => viewModel.Reports.IsReportModalOpen,
            "ab-a-flashcode-delivery" => viewModel.Merge.IsAbAFlashCodeDeliveryPromptOpen,
            "build-completed" => viewModel.BuildResult.IsOpen,
            "hex-insert" => viewModel.HexEditorWorkspace.IsInsertBytesPromptOpen,
            "hex-save" => viewModel.HexEditorWorkspace.IsSaveConfirmationOpen,
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown blocking surface."),
        };
    }

    /// <summary>A failed source-file reveal reports the localized visible toast.</summary>
    [Fact]
    public void RevealFileFailureShowsVisibleToast()
    {
        var reveal = new RecordingFileRevealService(result: false);
        MainWindowViewModel viewModel = CreateFileRevealViewModel(reveal);

        viewModel.RevealFileCommand.Execute(@"C:\firmware\missing.bin");

        Assert.True(viewModel.Reports.HasReportToast);
        Assert.Equal("File unavailable", viewModel.Reports.ShellToastTitle);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Reports.ReportToastText));
    }

    /// <summary>A successful Build-output reveal closes the confirmation after forwarding the exact path.</summary>
    [Fact]
    public void BuildCompletedRevealSuccessClosesModal()
    {
        const string outputPath = @"C:\output\generated.bin";
        var reveal = new RecordingFileRevealService(result: true);
        MainWindowViewModel viewModel = CreateFileRevealViewModel(reveal);
        Assert.True(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: true));

        viewModel.BuildResult.RevealOutputCommand.Execute(null);

        Assert.Equal([outputPath], reveal.Paths);
        Assert.False(viewModel.BuildResult.IsOpen);
        Assert.Equal(string.Empty, viewModel.BuildResult.OutputPath);
    }

    /// <summary>A failed Build-output reveal keeps the confirmation open with its specific inline error.</summary>
    [Fact]
    public void BuildCompletedRevealFailureKeepsModalOpen()
    {
        const string outputPath = @"C:\output\generated.bin";
        var reveal = new RecordingFileRevealService(result: false);
        MainWindowViewModel viewModel = CreateFileRevealViewModel(reveal);
        Assert.True(viewModel.TryShowBuildCompleted(CreateRunResult(succeeded: true, outputPath), build: true));

        viewModel.BuildResult.RevealOutputCommand.Execute(null);

        Assert.Equal([outputPath], reveal.Paths);
        Assert.True(viewModel.BuildResult.IsOpen);
        Assert.True(viewModel.BuildResult.HasOpenFolderError);
        Assert.Equal(viewModel.Text.BuildCompletedOpenFolderError, viewModel.BuildResult.OpenFolderError);
    }

    private static MainWindowViewModel CreateFileRevealViewModel(IFileRevealService fileRevealService)
    {
        return new MainWindowViewModel(
            "test-shell",
            "test-app",
            ShellLanguage.English,
            PresentationTestHost.CreateServices("test-app"),
            TestHost.FirmwareInspectionExperience,
            fileRevealService);
    }

    private static CompositionRunResult CreateRunResult(
        bool succeeded,
        string? outputPath,
        IReadOnlyList<CompositionIssue>? issues = null)
    {
        DateTimeOffset timestamp = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        ReadOnlyMemory<byte> outputBytes = succeeded
            ? new byte[] { 0x01, 0x02, 0x03, 0x04 }
            : ReadOnlyMemory<byte>.Empty;
        var report = new CompositionRunReport(
            "ui-smoke-run",
            "test-profile",
            "1.0.0",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            CompositionKind.Replace,
            timestamp,
            timestamp,
            [],
            [],
            [],
            issues ?? [],
            new OutputArtifactSummary(
                succeeded ? "firmware.bin" : "No output",
                outputBytes.Length,
                succeeded ? "hash" : "empty-hash",
                committed: succeeded && outputPath is not null));
        return new CompositionRunResult(
            succeeded
                ? CompositionExecutionStatus.Succeeded
                : CompositionExecutionStatus.Failed,
            outputBytes,
            report,
            outputPath,
            previewToken: null,
            inspectionOutputSpaceId: null,
            inspectionReferenceSpaceId: null,
            inspectionReferenceBytes: null,
            inspectionOutputBytes: null,
            outcomeStatus: succeeded ? "Succeeded" : "Blocked");
    }

    private static CompositionRunResult WithCommittedOutputId(
        CompositionRunResult source,
        string committedOutputId)
    {
        return CloneRunResult(source, committedOutputId, source.Report);
    }

    private static CompositionRunResult WithDelivery(
        CompositionRunResult source,
        IReadOnlyList<CompositionDeliveryArtifact> deliveryArtifacts,
        bool isDeliveryComplete = true,
        string? deliveryFailureMessage = null)
    {
        CompositionRunResult clone = CloneRunResult(source, source.CommittedOutputId, source.Report);
        clone.DeliveryArtifacts = [.. deliveryArtifacts];
        clone.IsDeliveryComplete = isDeliveryComplete;
        clone.DeliveryFailureMessage = deliveryFailureMessage;
        return clone;
    }

    private static CompositionRunResult WithReport(
        CompositionRunResult source,
        CompositionRunReport report)
    {
        return CloneRunResult(source, source.CommittedOutputId, report);
    }

    private static CompositionRunResult CloneRunResult(
        CompositionRunResult source,
        string? committedOutputId,
        CompositionRunReport report)
    {
        CompositionRunInspectionSnapshot? inspection = source.InspectionSnapshot;
        var clone = new CompositionRunResult(
            source.Status,
            source.OutputBytes,
            report,
            committedOutputId,
            source.PreviewToken,
            inspection?.OutputSpaceId,
            inspection?.ReferenceSpaceId,
            inspection?.ReferenceBytes.ToArray(),
            inspection?.OutputBytes,
            source.OutcomeStatus,
            source.SuppressOutputInExternalReport,
            source.HasRunReport)
        {
            AcceptedGeneralMappingDraft = source.AcceptedGeneralMappingDraft,
            ResolvedCapability = source.ResolvedCapability,
            ActionReadiness = source.ActionReadiness,
            DeliveryArtifacts = [.. source.DeliveryArtifacts],
            IsDeliveryComplete = source.IsDeliveryComplete,
            DeliveryFailureMessage = source.DeliveryFailureMessage,
        };
        return clone;
    }

    private sealed class RecordingFileRevealService(bool result) : IFileRevealService
    {
        public List<string?> Paths { get; } = [];

        public bool TryRevealFile(string? filePath)
        {
            Paths.Add(filePath);
            return result;
        }
    }
}
