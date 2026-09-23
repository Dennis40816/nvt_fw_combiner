using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ReplaceSurfaceCompatibilityTests
{
    /// <summary>CtrlRAM selection cancellation cannot publish its Checking session as accepted.</summary>
    [Fact]
    public async Task CtrlRamSelectionPropagatesCallerCancellation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-cancellation");
        string basePath = workspace.Write(
            "reference.bin",
            ReadCtrlRamReference());
        string replacementPath = workspace.Write(
            "replacement.bin",
            ReadCtrlRamNormalSource());
        var inspection = new DpCancellationProbeFirmwareInspection(
            TestHost.FirmwareInspectionExperience,
            blockImmediately: false);
        MainWindowViewModel viewModel = CreateDpCancellationViewModel(inspection);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        Assert.NotNull(viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        inspection.Arm();
        using var cancellation = new CancellationTokenSource();

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-ctrlram-normal",
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
            slot.SlotId == "replace-ctrlram-normal");
        Assert.True(inspection.ObservedCancellation);
        Assert.NotEmpty(inspection.BlockedInputs);
        Assert.NotNull(Assert.Single(inspection.BlockedInputs, static input =>
            input.InspectionId == CompositionSlotIds.ReplaceBase).CtrlRamRequest);
        Assert.Equal("replace-ctrlram-normal", Assert.Single(inspection.BlockedInputs, static input =>
            input.InspectionId == "replace-ctrlram-normal").CtrlRamReplaceAddressSpaceId);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, viewModel.Replace.Inspection.State);
        Assert.True(baseSlot.HasFile);
        Assert.True(replacement.HasFile);
        Assert.Null(baseSlot.CurrentInspectionProjection);
        Assert.Null(replacement.CurrentInspectionProjection);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    /// <summary>CtrlRAM clear cancellation cannot republish the retained peer's old acceptance.</summary>
    [Fact]
    public async Task CtrlRamClearPropagatesCallerCancellationToRetainedSlotRefresh()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-clear-cancellation");
        string basePath = workspace.Write(
            "reference.bin",
            ReadCtrlRamReference());
        string replacementPath = workspace.Write(
            "initial-code.bin",
            ReadCtrlRamNormalSource());
        var inspection = new DpCancellationProbeFirmwareInspection(
            TestHost.FirmwareInspectionExperience,
            blockImmediately: false);
        MainWindowViewModel viewModel = CreateDpCancellationViewModel(inspection);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-ctrlram-normal",
            replacementPath,
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Replace.CanBuildReplace);
        inspection.Arm();
        using var cancellation = new CancellationTokenSource();

        Task clear = viewModel.WorkflowSession.ClearSlotFileAsync(
            "replace-ctrlram-normal",
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
            slot.SlotId == "replace-ctrlram-normal");
        Assert.True(inspection.ObservedCancellation);
        FirmwareInspectionSnapshotInput retainedBase = Assert.Single(inspection.BlockedInputs);
        Assert.Equal(CompositionSlotIds.ReplaceBase, retainedBase.InspectionId);
        Assert.NotNull(retainedBase.CtrlRamRequest);
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
