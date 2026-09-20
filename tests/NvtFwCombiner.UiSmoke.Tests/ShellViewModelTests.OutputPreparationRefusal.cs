using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>A newer ready CtrlRAM context cannot authorize a proposal prepared from an older exact session.</summary>
    [Fact]
    public async Task ReplaceOutputPreparationRejectsOlderExactSessionAfterReadyContextChanges()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("ui-replace-exact-session-refusal");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        CompositionRunContext original = viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode, build: true);
        ActiveSessionSnapshot older = Assert.IsType<ActiveSessionSnapshot>(original.AcceptedSession);
        Assert.True(original.IsPublicationCurrent);
        FirmwareSlotViewModel slot = viewModel.Replace.ReplaceSlots.Single(candidate =>
            candidate.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        string newerPath = workspace.Write("newer-vn-ctrlram.bin", File.ReadAllBytes(slot.FilePath!));
        viewModel.SetSlotFile(slot.SlotId, newerPath);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        CompositionRunContext current = viewModel.Replace.CaptureRunContext(viewModel.Replace.SelectedReplaceMode, build: true);
        ActiveSessionSnapshot newer = Assert.IsType<ActiveSessionSnapshot>(current.AcceptedSession);
        Assert.NotSame(older, newer);
        Assert.False(original.IsPublicationCurrent);
        Assert.True(current.IsPublicationCurrent);
        var retained = new UiRunResultViewModel("Current result", "retained", "No output", false);
        viewModel.RunSession.PublishRunResult(current.Owner, retained);

        await viewModel.Replace.RequestBuildOutputDeliveryAsync(exactSession: older);

        Assert.False(viewModel.OutputDelivery.IsOpen);
        Assert.Same(retained, current.Owner.LastRunResult);
        Assert.False(viewModel.Reports.HasReportHistory);
        await viewModel.Replace.RequestBuildOutputDeliveryAsync(exactSession: newer);
        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.Same(retained, current.Owner.LastRunResult);
    }

    /// <summary>An external Config edit after input acceptance must refuse Build without escaping the UI event.</summary>
    [Fact]
    public async Task InvalidatedAbConfigurationBlocksOutputConfirmationWithoutThrowing()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ui-ab-preparation-refusal");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        MainWindowViewModel viewModel = await CreateLoadedFormatAbViewModelAsync(workspace, host);
        _ = workspace.Write("format.json", "invalid"u8.ToArray());

        await viewModel.Merge.RequestBuildOutputDeliveryAsync();

        Assert.False(viewModel.OutputDelivery.IsOpen);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.Equal("Build blocked", viewModel.RunSession.LastRunResult.Title);
        Assert.Equal("No output", viewModel.RunSession.LastRunResult.Output);
        Assert.Contains("Save a valid Event Buffer Format configuration", viewModel.RunSession.LastRunResult.Detail);
        Assert.False(viewModel.Merge.HasCurrentAbMergeActionReadiness(build: true));
        Assert.False(viewModel.RunSession.IsRunInProgress);
    }

    /// <summary>A stale refusal cannot replace the current result or disturb a newer confirmation.</summary>
    [Theory]
    [InlineData("cancel")]
    [InlineData("reopen")]
    [InlineData("session")]
    [InlineData("mode")]
    [InlineData("page")]
    public async Task DelayedOutputPreparationRefusalPreservesCurrentOwnership(string change)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ui-ab-stale-preparation");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        var naming = new HeldOutputPreparation(host.CompositionOutputNaming);
        MainWindowViewModel viewModel = await CreateLoadedFormatAbViewModelAsync(workspace, host, outputNaming: naming);
        Task preparing = viewModel.Merge.RequestBuildOutputDeliveryAsync();
        ActiveSessionSnapshot session = await naming.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(session, TestContext.Current.CancellationToken);
        var currentResult = new UiRunResultViewModel("Current", "Current result", "No output", succeeded: false);
        int executions = 0;
        if (change == "cancel") { viewModel.OutputDelivery.CancelCommand.Execute(null); }
        if (change == "reopen")
        {
            viewModel.OutputDelivery.Open(new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null,
                _ => { executions++; return Task.CompletedTask; }));
        }
        if (change == "session") { viewModel.WorkflowSession.SelectedIc = "NT51932"; }
        if (change == "mode") { viewModel.Merge.SelectedMergeMode = NvtFwCombiner.Domain.Composition.ExperienceIds.StandardMerge; }
        if (change == "page")
        {
            viewModel.ShowReplaceCommand.Execute(null);
            viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
            Assert.Equal(ShellPage.Replace, viewModel.SelectedPage);
        }
        WorkflowRunState owner = change == "page" ? viewModel.Replace.RunState : viewModel.Merge.RunState;
        viewModel.RunSession.PublishRunResult(owner, currentResult);
        _ = workspace.Write("format.json", "invalid"u8.ToArray());
        naming.Release.SetResult();
        await preparing;
        Assert.Same(currentResult, viewModel.RunSession.LastRunResult);
        Assert.Equal(change == "reopen", viewModel.OutputDelivery.IsOpen);
        Assert.Equal(0, executions);
        Assert.False(viewModel.RunSession.IsRunInProgress);
    }

    private sealed class HeldOutputPreparation(ICompositionOutputNaming inner) : ICompositionOutputNaming
    {
        public CompositionOutputBundleValidationIssue? ValidateName(string value)
        {
            return inner.ValidateName(value);
        }

        internal TaskCompletionSource<ActiveSessionSnapshot> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<CompositionOutputBundleProposal> PrepareBundleProposalAsync(ActiveSessionSnapshot session, CancellationToken cancellationToken, CtrlRamFirmwareVersionDraftState? edit = null)
        {
            Entered.SetResult(session);
            await Release.Task.WaitAsync(cancellationToken);
            return await inner.PrepareBundleProposalAsync(session, cancellationToken, edit);
        }
        public ValueTask<bool> IsProposalCurrentAsync(CompositionOutputBundleProposal proposal, CancellationToken cancellationToken)
        {
            return inner.IsProposalCurrentAsync(proposal, cancellationToken);
        }
        public CompositionOutputPreparation ResolveAcceptedOutput(ActiveSessionSnapshot session, CtrlRamFirmwareVersionDraftState? edit = null)
        {
            return inner.ResolveAcceptedOutput(session, edit);
        }
        public CompositionOutputBundleProposal ResolveAcceptedBundleProposal(ActiveSessionSnapshot session, CtrlRamFirmwareVersionDraftState? edit = null)
        {
            return inner.ResolveAcceptedBundleProposal(session, edit);
        }
        public CompositionOutputBundleDestinationValidation ValidateBundleDestination(CompositionOutputBundleIntent intent)
        {
            return inner.ValidateBundleDestination(intent);
        }
        public ValueTask<CompositionOutputPreparation> PrepareAutomaticOutputAsync(ActiveSessionSnapshot session, CancellationToken cancellationToken)
        {
            return inner.PrepareAutomaticOutputAsync(session, cancellationToken);
        }
    }
}
