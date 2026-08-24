using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Serializes real Build routed-entry inspection regressions with other Avalonia controls.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class BuildEntryInspectionTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
    /// <summary>Opening Merge Build Settings never starts a second input inspection batch.</summary>
    [AvaloniaFact]
    public async Task MergeBuildRoutedClickDoesNotRepeatAcceptedFirmwareInspection()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        int inspectionBatchCount = 0;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PresentationHostServices services = await Task.Run(
            () => PresentationTestHost.CreateServices("ui-smoke"),
            cancellationToken);
        MainWindowViewModel viewModel = await Task.Run(async () =>
        {
            MainWindowViewModel candidate = CreateViewModel(
                services,
                () => Interlocked.Increment(ref inspectionBatchCount));
            _ = PresentationTestHost.PublishCanonicalCatalog(services, candidate);
            candidate.ShowMergeCommand.Execute(null);
            candidate.WorkflowSession.SelectedIc = "NT51926";
            await candidate.WorkflowSession.SetSlotFileAsync(
                CompositionSlotIds.MergeDp,
                golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input")),
                cancellationToken);
            await candidate.WorkflowSession.SetSlotFileAsync(
                CompositionSlotIds.MergeTp,
                golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input")),
                cancellationToken);
            return candidate;
        }, cancellationToken);
        Assert.True(viewModel.Merge.CanBuildMerge);
        int acceptedBatchCount = inspectionBatchCount;
        Assert.True(acceptedBatchCount > 0);

        await ClickBuildAsync(
            services,
            viewModel,
            "MergeBuildButton",
            viewModel.OutputDelivery,
            nameof(OutputDeliveryConfirmationViewModel.IsOpen),
            () => viewModel.OutputDelivery.IsOpen);

        Assert.Equal(acceptedBatchCount, inspectionBatchCount);
    }

    /// <summary>AB Build Settings retain the accepted source evidence without rereading changed source paths.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbMergeBuildRoutedClickUsesAcceptedSessionAfterSourcePathChanges(bool overwriteSources)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-build-accepted-session");
        var acceptedPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] =
                CopyCanonicalAbInput(workspace, CompositionAddressSpaceIds.DpAbInput),
            [CompositionAddressSpaceIds.TpAInput] =
                CopyCanonicalAbInput(workspace, CompositionAddressSpaceIds.TpAInput),
            [CompositionAddressSpaceIds.TpBInput] =
                CopyCanonicalAbInput(workspace, CompositionAddressSpaceIds.TpBInput),
        };
        Dictionary<string, AcceptedSourceEvidence> acceptedEvidence = acceptedPaths.ToDictionary(
            static entry => entry.Key,
            static entry => new AcceptedSourceEvidence(
                Path.GetFileName(entry.Value),
                new FileInfo(entry.Value).Length,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(entry.Value)))),
            StringComparer.Ordinal);
        int inspectionBatchCount = 0;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PresentationHostServices services = await Task.Run(
            () => PresentationTestHost.CreateServices("ui-smoke"),
            cancellationToken);
        MainWindowViewModel viewModel = await Task.Run(async () =>
        {
            MainWindowViewModel candidate = CreateViewModel(
                services,
                () => Interlocked.Increment(ref inspectionBatchCount));
            _ = PresentationTestHost.PublishCanonicalCatalog(services, candidate);
            candidate.ShowMergeCommand.Execute(null);
            candidate.WorkflowSession.SelectedIc = "NT51929";
            candidate.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
            await candidate.WorkflowSession.SetSlotFileAsync(
                CompositionAddressSpaceIds.DpAbInput,
                acceptedPaths[CompositionAddressSpaceIds.DpAbInput],
                cancellationToken);
            await candidate.WorkflowSession.SetSlotFileAsync(
                CompositionAddressSpaceIds.TpAInput,
                acceptedPaths[CompositionAddressSpaceIds.TpAInput],
                cancellationToken);
            await candidate.WorkflowSession.SetSlotFileAsync(
                CompositionAddressSpaceIds.TpBInput,
                acceptedPaths[CompositionAddressSpaceIds.TpBInput],
                cancellationToken);
            return candidate;
        }, cancellationToken);
        Assert.True(viewModel.Merge.CanBuildMerge);
        string acceptedCanonicalName = viewModel.Merge.MergeOutputFileName;
        Assert.StartsWith("NT51929_FlashCode_A_", acceptedCanonicalName, StringComparison.Ordinal);
        Assert.EndsWith(".bin", acceptedCanonicalName, StringComparison.Ordinal);
        int acceptedBatchCount = inspectionBatchCount;
        Assert.True(acceptedBatchCount > 0);

        foreach (string path in acceptedPaths.Values)
        {
            if (overwriteSources)
            {
                await File.WriteAllBytesAsync(
                    path,
                    new byte[new FileInfo(path).Length],
                    cancellationToken);
            }
            else
            {
                File.Delete(path);
            }
        }

        await ClickBuildAsync(
            services,
            viewModel,
            "MergeBuildButton",
            viewModel.OutputDelivery,
            nameof(OutputDeliveryConfirmationViewModel.IsOpen),
            () => viewModel.OutputDelivery.IsOpen);

        Assert.Equal(acceptedBatchCount, inspectionBatchCount);
        Assert.Equal(acceptedCanonicalName, viewModel.OutputDelivery.CanonicalOutputFileName);
        Assert.Equal(acceptedCanonicalName, viewModel.OutputDelivery.OutputFileName);
        Assert.Equal(3, viewModel.OutputDelivery.Sources.Count);
        Assert.All(viewModel.OutputDelivery.Sources, source =>
        {
            AcceptedSourceEvidence evidence = acceptedEvidence[source.SlotId];
            Assert.Equal(evidence.FileName, source.OriginalFileName);
            Assert.Equal(evidence.Size, source.Size);
            Assert.Equal(evidence.Sha256, source.Sha256);
        });
    }

    /// <summary>Opening CtrlRAM Build Settings never starts a second input inspection batch.</summary>
    [AvaloniaFact]
    public async Task CtrlRamBuildRoutedClickDoesNotRepeatAcceptedFirmwareInspection()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-build-inspection");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        int inspectionBatchCount = 0;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PresentationHostServices services = await Task.Run(
            () => PresentationTestHost.CreateServices("ui-smoke"),
            cancellationToken);
        MainWindowViewModel viewModel = await Task.Run(async () =>
        {
            MainWindowViewModel candidate = CreateViewModel(
                services,
                () => Interlocked.Increment(ref inspectionBatchCount));
            _ = PresentationTestHost.PublishCanonicalCatalog(services, candidate);
            candidate.WorkflowSession.SelectedIc = "NT51926";
            candidate.WorkflowSession.SelectedNumber = "cascade";
            OpenReplace(candidate, ExperienceIds.CtrlRamReplace);
            await candidate.WorkflowSession.SetSlotFileAsync(
                CompositionSlotIds.ReplaceBase,
                workspace.Write("base-from-golden.bin", baseBytes),
                cancellationToken);
            FirmwareSlotViewModel replacementSlot = candidate.Replace.ReplaceSlots.Single(slot =>
                slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
            CtrlRamRegionViewModel region = candidate.Replace.CtrlRamRegions.Single(regionCandidate =>
                regionCandidate.Name == replacementSlot.Title);
            (int start, int length) = ParseCtrlRamRegion(region);
            await candidate.WorkflowSession.SetSlotFileAsync(
                replacementSlot.SlotId,
                workspace.Write("self-vn-ctrlram.bin", baseBytes[start..(start + length)]),
                cancellationToken);
            return candidate;
        }, cancellationToken);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        int acceptedBatchCount = inspectionBatchCount;
        Assert.True(acceptedBatchCount > 0);

        await ClickBuildAsync(
            services,
            viewModel,
            "ReplaceBuildButton",
            viewModel.Replace,
            nameof(ReplacePresentationViewModel.IsCtrlRamFirmwareVersionModalOpen),
            () => viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);

        Assert.Equal(acceptedBatchCount, inspectionBatchCount);
    }

    private MainWindowViewModel CreateViewModel(PresentationHostServices services, Action onBatch)
    {
        return new MainWindowViewModel(
            "ui-smoke",
            "ui-smoke",
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                batchReader: (icId, inputs) =>
                {
                    onBatch();
                    return BuiltInFirmwareInspection.InspectFirmwareBatch(
                        (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                        icId,
                        inputs);
                }));
    }

    private static string CopyCanonicalAbInput(TempWorkspace workspace, string addressSpaceId)
    {
        string sourcePath = CanonicalGoldenTestData.ArtifactPath(
            "ab-merge",
            "51929",
            addressSpaceId,
            "t05-d06");
        string destinationPath = workspace.PathFor($"accepted-{addressSpaceId}.bin");
        File.Copy(sourcePath, destinationPath);
        return destinationPath;
    }

    private sealed record AcceptedSourceEvidence(string FileName, long Size, string Sha256);

    private static async Task ClickBuildAsync(
        PresentationHostServices services,
        MainWindowViewModel viewModel,
        string buttonName,
        System.ComponentModel.INotifyPropertyChanged source,
        string propertyName,
        Func<bool> predicate)
    {
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            services,
            ShellPreferenceSnapshot.Default)
        {
            DataContext = viewModel,
        };
        try
        {
            var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
            {
                if (args.PropertyName == propertyName && predicate())
                {
                    _ = observed.TrySetResult();
                }
            }

            source.PropertyChanged += OnChanged;
            try
            {
                Button build = Assert.IsType<Button>(window.FindControl<Control>(buttonName));
                build.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (predicate())
                {
                    _ = observed.TrySetResult();
                }
                await observed.Task.WaitAsync(
                    TimeSpan.FromSeconds(10),
                    TestContext.Current.CancellationToken);
            }
            finally
            {
                source.PropertyChanged -= OnChanged;
            }
        }
        finally
        {
            window.Close();
        }
    }
}
