using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>The caller token reaches the one bounded Replace inspection lifecycle.</summary>
    [Fact]
    public async Task CtrlRamSelectionPropagatesCallerCancellationToInspection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-cancellation");
        var inspection = new CancellationProbeFirmwareInspection();
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        var viewModel = new MainWindowViewModel(
            "ui-smoke",
            "ui-smoke",
            ShellLanguage.English,
            services,
            inspection);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        using var cancellation = new CancellationTokenSource();

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("base.bin", [0x00]),
            cancellation.Token);
        await inspection.Entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await selection.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.True(inspection.ObservedCancellation);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, viewModel.Replace.Inspection.State);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    private sealed class CancellationProbeFirmwareInspection : IFirmwareInspection
    {
        private int _observedCancellation;

        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool ObservedCancellation => Volatile.Read(ref _observedCancellation) != 0;

        public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
            string icId,
            IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
            CancellationToken cancellationToken,
            IProgress<AuthoringInspectionProgress>? progress = null)
        {
            using CancellationTokenRegistration registration = cancellationToken.Register(
                () => Volatile.Write(ref _observedCancellation, 1));
            _ = Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("An infinite inspection delay completed without cancellation.");
        }

        public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
            string icId,
            string numberToken,
            FirmwareConfigMetadataSnapshot? baseFirmware)
        {
            return new(numberToken, [], []);
        }
    }
}
