using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Standard selection cancellation reaches the inspector and publishes no accepted input.</summary>
    [Fact]
    public async Task StandardMergeSelectionPropagatesCallerCancellation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-standard-cancellation");
        var inspection = new MergeCancellationProbeFirmwareInspection(
            TestHost.FirmwareInspectionExperience,
            blockImmediately: true);
        MainWindowViewModel viewModel = CreateMergeCancellationViewModel(inspection);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        using var cancellation = new CancellationTokenSource();

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            workspace.Write("dp.bin", [0x00]),
            cancellation.Token);
        await inspection.Entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await selection.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        Assert.True(inspection.ObservedCancellation);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, viewModel.Merge.Inspection.State);
        Assert.Null(dp.CurrentInspectionProjection);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>Linked AB selection cancels its one shared inspection without accepting either TP binding.</summary>
    [Fact]
    public async Task AbSameTpSelectionPropagatesCallerCancellation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-cancellation");
        var inspection = new MergeCancellationProbeFirmwareInspection(
            TestHost.FirmwareInspectionExperience,
            blockImmediately: true);
        MainWindowViewModel viewModel = CreateMergeCancellationViewModel(inspection);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await viewModel.Merge.ToggleAbSameTpCommand.ExecuteAsync(null);
        using var cancellation = new CancellationTokenSource();

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("shared-tp.bin", [0x00]),
            cancellation.Token);
        await inspection.Entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await selection.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.True(inspection.ObservedCancellation);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, viewModel.Merge.Inspection.State);
        Assert.All(
            viewModel.Merge.MergeSlots.Where(static slot => slot.SlotId is
                CompositionAddressSpaceIds.TpAInput or CompositionAddressSpaceIds.TpBInput),
            static slot => Assert.Null(slot.CurrentInspectionProjection));
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>Merge clear cancellation stops retained-slot reinspection and cannot republish old acceptance.</summary>
    [Fact]
    public async Task StandardMergeClearPropagatesCallerCancellationToRetainedSlotRefresh()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input"));
        var inspection = new MergeCancellationProbeFirmwareInspection(
            TestHost.FirmwareInspectionExperience,
            blockImmediately: false);
        MainWindowViewModel viewModel = CreateMergeCancellationViewModel(inspection);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            tpPath,
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Merge.CanBuildMerge);
        inspection.Arm();
        using var cancellation = new CancellationTokenSource();

        Task clear = viewModel.WorkflowSession.ClearSlotFileAsync(
            CompositionSlotIds.MergeDp,
            cancellation.Token);
        await inspection.Entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await clear.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);
        Assert.True(inspection.ObservedCancellation);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, viewModel.Merge.Inspection.State);
        Assert.True(tp.HasFile);
        Assert.Null(tp.CurrentInspectionProjection);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    private static MainWindowViewModel CreateMergeCancellationViewModel(
        IFirmwareInspection inspection)
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        var viewModel = new MainWindowViewModel(
            "ui-smoke",
            "ui-smoke",
            ShellLanguage.English,
            services,
            inspection);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        viewModel.ShowMergeCommand.Execute(null);
        return viewModel;
    }

    private sealed class MergeCancellationProbeFirmwareInspection(
        IFirmwareInspection inner,
        bool blockImmediately) : IFirmwareInspection
    {
        private readonly IFirmwareInspection _inner = inner;
        private int _block = blockImmediately ? 1 : 0;
        private int _observedCancellation;

        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool ObservedCancellation => Volatile.Read(ref _observedCancellation) != 0;

        internal void Arm()
        {
            Volatile.Write(ref _block, 1);
        }

        public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
            string icId,
            IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
            CancellationToken cancellationToken,
            IProgress<AuthoringInspectionProgress>? progress = null)
        {
            if (Volatile.Read(ref _block) == 0)
            {
                return await _inner.InspectFirmwareBatchAsync(
                    icId,
                    inputs,
                    cancellationToken,
                    progress);
            }

            using CancellationTokenRegistration registration = cancellationToken.Register(
                () => Volatile.Write(ref _observedCancellation, 1));
            _ = Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException(
                "An infinite inspection delay completed without cancellation.");
        }

        public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
            string icId,
            string numberToken,
            FirmwareConfigMetadataSnapshot? baseFirmware)
        {
            return _inner.ProjectCtrlRamInspectionDisplay(icId, numberToken, baseFirmware);
        }
    }
}
