using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>A delayed prior CtrlRAM selection cannot publish after a newer path is selected.</summary>
    [Fact]
    public async Task CtrlRamRapidReplacementSelectionPublishesLatestInspectedBatch()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51923-fw141-single-auto-prj-662-20260717");
        JsonElement baseArtifact = CanonicalGoldenTestData.Artifact(fixtureCase, "expected-output");
        JsonElement normalArtifact = CanonicalGoldenTestData.Artifact(
            fixtureCase,
            "postbuild-normal-ctrlram");
        string firstPath = CanonicalGoldenTestData.ArtifactPath(normalArtifact);
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-latest-batch");
        string secondPath = workspace.Write("normal-b.bin", File.ReadAllBytes(firstPath));
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        var authoring = new RecordingCtrlRamAuthoring(services.Composition.CtrlRamAuthoring);
        services = WithCtrlRamAuthoring(services, authoring);
        var inspection = new DelayedPathFirmwareInspection(
            services.Composition.FirmwareInspection,
            firstPath);
        var viewModel = new MainWindowViewModel(
            "ui-smoke",
            "ui-smoke",
            ShellLanguage.English,
            services,
            inspection);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        viewModel.WorkflowSession.SelectedIc = "NT51923";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            CanonicalGoldenTestData.ArtifactPath(baseArtifact),
            TestContext.Current.CancellationToken);

        Task first = viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-ctrlram-normal",
            firstPath,
            TestContext.Current.CancellationToken);
        await inspection.Entered.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        Task second = viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-ctrlram-normal",
            secondPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            secondPath,
            viewModel.Replace.ReplaceSlots.Single(slot =>
                slot.SlotId == "replace-ctrlram-normal").FilePath);
        inspection.Release();
        await Task.WhenAll(first, second).WaitAsync(
            TimeSpan.FromSeconds(20),
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-normal");
        AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
            replacement.CurrentInspectionProjection?.InputSlotStatus);
        Assert.Equal(secondPath, status.SelectedPathHint);
        Assert.Equal(secondPath, replacement.FilePath);
        Assert.NotEqual(viewModel.Text.FirmwareInspectionStaleFileStatus, replacement.InputInspectionStatus);
        Assert.False(replacement.BlocksBuild);
        Assert.Equal(1, inspection.CountFor(firstPath));
        Assert.Equal(1, inspection.CountFor(secondPath));
        Assert.Equal(0, authoring.PrepareSessionCalls);
        Assert.Equal(1, authoring.AdoptInspectedBatchCalls);
        (AuthoringInputSlotStatus[] inspected, ActiveSessionSnapshot accepted) =
            authoring.SingleSuccessfulAdoption;
        Assert.All(inspected, status => Assert.Same(
            status.AcceptedByteArray,
            accepted.InputSlotStatuses.Single(adopted =>
                adopted.SlotId == status.SlotId).AcceptedByteArray));
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, viewModel.Replace.Inspection.State);
    }

    private static PresentationHostServices WithCtrlRamAuthoring(
        PresentationHostServices services,
        ICtrlRamAuthoring ctrlRamAuthoring)
    {
        PresentationCompositionServices current = services.Composition;
        var composition = new PresentationCompositionServices(
            current.Capabilities,
            current.StandardMergeAuthoring,
            current.AbMergeAuthoring,
            current.DpReplaceAuthoring,
            current.GeneralAuthoring,
            ctrlRamAuthoring,
            current.FirmwareInspection,
            current.OutputNaming,
            current.Execution);
        return new PresentationHostServices(
            composition,
            services.FileReveal,
            services.SupportMatrix,
            services.SystemInformation,
            services.SystemDiagnosticsExporter,
            services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader,
            services.ExternalEnvironmentLoader,
            services.LocalFiles,
            services.VersionManagement,
            services.ManagedApplicationStartup,
            services.StableLauncherHandoff);
    }

    private sealed class RecordingCtrlRamAuthoring(ICtrlRamAuthoring inner) : ICtrlRamAuthoring
    {
        private readonly List<(AuthoringInputSlotStatus[] Statuses, ActiveSessionSnapshot Snapshot)>
            _successfulAdoptions = [];

        internal int PrepareSessionCalls { get; private set; }

        internal int AdoptInspectedBatchCalls { get; private set; }

        internal (AuthoringInputSlotStatus[] Statuses, ActiveSessionSnapshot Snapshot)
            SingleSuccessfulAdoption => Assert.Single(_successfulAdoptions);

        public CtrlRamInspectionDisplay GetDiscoveryDisplay(
            string icId,
            string number)
        {
            return inner.GetDiscoveryDisplay(icId, number);
        }

        public CtrlRamInspectionDisplay GetDiscoveryDisplayFromAcceptedBase(
            string icId,
            string number,
            ReadOnlyMemory<byte> acceptedBaseBytes)
        {
            return inner.GetDiscoveryDisplayFromAcceptedBase(icId, number, acceptedBaseBytes);
        }

        public CtrlRamAuthoringSessionPreparation PrepareSession(
            AuthoringSessionState session,
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            IReadOnlyDictionary<string, byte[]> inputBytes,
            CtrlRamFirmwareVersionDraftState? firmwareVersionEdit = null)
        {
            PrepareSessionCalls++;
            return inner.PrepareSession(
                session,
                icId,
                number,
                slotPaths,
                inputBytes,
                firmwareVersionEdit);
        }

        public AuthoringSessionTransitionResult AdoptInspectedBatch(
            AuthoringSessionState session,
            AuthoringCapabilityCatalogSnapshot catalog,
            IReadOnlyCollection<AuthoringInputSlotStatus> statuses)
        {
            AdoptInspectedBatchCalls++;
            AuthoringSessionTransitionResult result = inner.AdoptInspectedBatch(
                session,
                catalog,
                statuses);
            if (result.Succeeded)
            {
                _successfulAdoptions.Add(([.. statuses], result.Snapshot!));
            }
            return result;
        }

        public ValueTask<CapabilityActionReadinessSnapshot?> GetActionReadinessAsync(
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            ActiveSessionSnapshot acceptedSession,
            CancellationToken cancellationToken)
        {
            return inner.GetActionReadinessAsync(
                icId,
                number,
                slotPaths,
                acceptedSession,
                cancellationToken);
        }

        public CtrlRamAuthoringTransitionResult TransitionFirmwareVersionCompilation(
            AuthoringSessionState session,
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            CtrlRamFirmwareVersionDraftState? firmwareVersionEdit)
        {
            return inner.TransitionFirmwareVersionCompilation(
                session,
                icId,
                number,
                slotPaths,
                firmwareVersionEdit);
        }

        public CompiledInputVersionObservation? ProjectFirmwareVersionConfirmationLease(
            ActiveSessionSnapshot session)
        {
            return inner.ProjectFirmwareVersionConfirmationLease(session);
        }

        public bool IsFirmwareVersionConfirmationLeaseCurrent(
            ActiveSessionSnapshot current,
            ActiveSessionSnapshot lease)
        {
            return inner.IsFirmwareVersionConfirmationLeaseCurrent(current, lease);
        }
    }

    private sealed class DelayedPathFirmwareInspection(
        IFirmwareInspection inner,
        string delayedPath) : IFirmwareInspection
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
        private readonly Lock _lock = new();
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int CountFor(string path)
        {
            lock (_lock)
            {
                return _counts.GetValueOrDefault(path);
            }
        }

        internal void Release()
        {
            _ = _release.TrySetResult();
        }

        public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
            string icId,
            IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
            CancellationToken cancellationToken,
            IProgress<AuthoringInspectionProgress>? progress = null)
        {
            foreach (string path in inputs.Select(static input => input.Path))
            {
                lock (_lock)
                {
                    _counts[path] = _counts.GetValueOrDefault(path) + 1;
                }
            }

            FirmwareInspectionBatchResult result = await inner.InspectFirmwareBatchAsync(
                icId,
                inputs,
                cancellationToken,
                progress);
            if (inputs.Any(input => StringComparer.Ordinal.Equals(input.Path, delayedPath)))
            {
                _ = Entered.TrySetResult();
                await _release.Task.ConfigureAwait(false);
            }

            return result;
        }

        public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
            string icId,
            string numberToken,
            FirmwareConfigMetadataSnapshot? baseFirmware)
        {
            return inner.ProjectCtrlRamInspectionDisplay(icId, numberToken, baseFirmware);
        }
    }
}
