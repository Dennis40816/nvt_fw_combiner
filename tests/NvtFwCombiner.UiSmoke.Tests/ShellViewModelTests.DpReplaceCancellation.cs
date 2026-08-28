using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DpReplaceWorkflowTests
{
    /// <summary>Hidden DP selection cancellation cannot publish its Checking session as accepted.</summary>
    [Fact]
    public async Task DpReplaceSelectionPropagatesCallerCancellation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-cancellation");
        string basePath = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x71));
        string replacementPath = workspace.Write(
            "replacement.bin",
            CreatePattern(0x40000, 0x41));
        var inspection = new DpCancellationProbeFirmwareInspection(
            TestHost.FirmwareInspectionExperience,
            blockImmediately: false);
        MainWindowViewModel viewModel = CreateDpCancellationViewModel(inspection);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        OpenReplace(viewModel, ExperienceIds.DpReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        Assert.NotNull(viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        inspection.Arm();
        using var cancellation = new CancellationTokenSource();

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceDp,
            replacementPath,
            cancellation.Token);
        await inspection.Entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await selection.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel baseSlot = viewModel.Replace.ReplaceBaseSlot;
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.ReplaceDp);
        Assert.True(inspection.ObservedCancellation);
        Assert.NotEmpty(inspection.BlockedInputs);
        Assert.All(
            inspection.BlockedInputs,
            static input => Assert.NotNull(input.ExactCapability));
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, viewModel.Replace.Inspection.State);
        Assert.True(baseSlot.HasFile);
        Assert.True(replacement.HasFile);
        Assert.Null(baseSlot.CurrentInspectionProjection);
        Assert.Null(replacement.CurrentInspectionProjection);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    /// <summary>Hidden DP clear cancellation cannot republish the retained peer's old acceptance.</summary>
    [Fact]
    public async Task DpReplaceClearPropagatesCallerCancellationToRetainedSlotRefresh()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-clear-cancellation");
        string basePath = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x71));
        string replacementPath = workspace.Write(
            "initial-code.bin",
            CreatePattern(0x40000, 0x41));
        var inspection = new DpCancellationProbeFirmwareInspection(
            TestHost.FirmwareInspectionExperience,
            blockImmediately: false);
        MainWindowViewModel viewModel = CreateDpCancellationViewModel(inspection);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        OpenReplace(viewModel, ExperienceIds.DpReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceDp,
            replacementPath,
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Replace.CanBuildReplace);
        inspection.Arm();
        using var cancellation = new CancellationTokenSource();

        Task clear = viewModel.WorkflowSession.ClearSlotFileAsync(
            CompositionSlotIds.ReplaceDp,
            cancellation.Token);
        await inspection.Entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await clear.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel baseSlot = viewModel.Replace.ReplaceBaseSlot;
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.ReplaceDp);
        Assert.True(inspection.ObservedCancellation);
        FirmwareInspectionSnapshotInput retainedBase = Assert.Single(inspection.BlockedInputs);
        Assert.Equal(CompositionSlotIds.ReplaceBase, retainedBase.InspectionId);
        Assert.NotNull(retainedBase.ExactCapability);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, viewModel.Replace.Inspection.State);
        Assert.True(baseSlot.HasFile);
        Assert.False(replacement.HasFile);
        Assert.Null(baseSlot.CurrentInspectionProjection);
        Assert.Null(replacement.CurrentInspectionProjection);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    private static MainWindowViewModel CreateDpCancellationViewModel(
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
        return viewModel;
    }

    private sealed class DpCancellationProbeFirmwareInspection(
        IFirmwareInspection inner,
        bool blockImmediately) : IFirmwareInspection
    {
        private readonly IFirmwareInspection _inner = inner;
        private int _block = blockImmediately ? 1 : 0;
        private int _observedCancellation;
        private FirmwareInspectionSnapshotInput[]? _blockedInputs;

        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool ObservedCancellation => Volatile.Read(ref _observedCancellation) != 0;

        internal IReadOnlyList<FirmwareInspectionSnapshotInput> BlockedInputs =>
            Volatile.Read(ref _blockedInputs) ?? [];

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
            Volatile.Write(ref _blockedInputs, [.. inputs]);
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
