using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises firmware Browse actions through the real Avalonia event boundary.</summary>
public sealed class FirmwareBrowseProcessSmokeTests
{
    private const string ExpectedDiagnostic =
        "input.address-space.length-mismatch [replace-base]: Base firmware BIN length " +
        "0x37001 is unsupported for NT51950 / single CtrlRAM Replace; accepted exact " +
        "reference lengths are 0x37000 / 0x40000.";

    /// <summary>Firmware Browse guides users to BIN without exposing All Files as a bypass.</summary>
    [Fact]
    public async Task FirmwareBrowseOpenPickerIsWiredToOnlyBinChoice()
    {
        IStorageProvider storageProvider = DispatchProxy.Create<IStorageProvider, StorageProviderProxy>();
        StorageProviderProxy proxy = Assert.IsType<StorageProviderProxy>(
            storageProvider,
            exactMatch: false);

        string? selectedPath = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            storageProvider,
            "Select firmware BIN");

        Assert.Null(selectedPath);
        FilePickerOpenOptions options = Assert.IsType<FilePickerOpenOptions>(
            proxy.OpenOptions);
        Assert.False(options.AllowMultiple);
        FilePickerFileType choice = Assert.Single(options.FileTypeFilter!);
        Assert.Equal("Firmware BIN", choice.Name);
        Assert.Equal(["*.bin"], choice.Patterns);
        Assert.Equal(["application/octet-stream"], choice.MimeTypes);
    }

    /// <summary>The native firmware picker admits exactly one local provider result.</summary>
    [Theory]
    [InlineData(PickerResultScenario.Zero, false)]
    [InlineData(PickerResultScenario.OneLocal, true)]
    [InlineData(PickerResultScenario.TwoLocal, false)]
    [InlineData(PickerResultScenario.OneNonLocal, false)]
    public async Task FirmwareBrowseOpenPickerAcceptsExactlyOneLocalFile(
        PickerResultScenario scenario,
        bool expectedAccepted)
    {
        string localPath = Path.Combine(Path.GetTempPath(), "native-picker-firmware.bin");
        IStorageProvider storageProvider = CreateStorageProvider(
            fileResults: CreateStorageResults<IStorageFile>(scenario, localPath, "firmware.bin"));

        string? selectedPath = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            storageProvider,
            "Select firmware BIN");

        Assert.Equal(expectedAccepted ? localPath : null, selectedPath);
    }

    /// <summary>The bundle-parent picker admits exactly one local provider result.</summary>
    [Theory]
    [InlineData(PickerResultScenario.Zero, false)]
    [InlineData(PickerResultScenario.OneLocal, true)]
    [InlineData(PickerResultScenario.TwoLocal, false)]
    [InlineData(PickerResultScenario.OneNonLocal, false)]
    public async Task BundleParentBrowsePickerAcceptsExactlyOneLocalFolder(
        PickerResultScenario scenario,
        bool expectedAccepted)
    {
        string localPath = Path.Combine(Path.GetTempPath(), "native-picker-bundle-parent");
        IStorageProvider storageProvider = CreateStorageProvider(
            folderResults: CreateStorageResults<IStorageFolder>(scenario, localPath, "bundle-parent"));

        string? selectedPath = await FirmwareFilePickerDialogs.PickBundleParentDirectoryAsync(
            storageProvider,
            "Select bundle parent directory");

        Assert.Equal(expectedAccepted ? localPath : null, selectedPath);
    }

    /// <summary>Rejected native picker results leave the accepted selection and session untouched.</summary>
    [AvaloniaTheory]
    [InlineData(PickerResultScenario.Zero)]
    [InlineData(PickerResultScenario.TwoLocal)]
    [InlineData(PickerResultScenario.OneNonLocal)]
    public async Task RejectedFirmwareBrowseResultIsAnExactNoOpForAcceptedSession(
        PickerResultScenario scenario)
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-picker-cancel");
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await Task.Run(() =>
        {
            MainWindowViewModel prepared = PresentationTestHost.CreateViewModel();
            prepared.WorkflowSession.SelectedIc = "NT51926";
            prepared.ShowMergeCommand.Execute(null);
            prepared.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
            golden.CopyInputFilesToMergeSlots(prepared, workspace, goldenCase);
            return prepared;
        }, TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = viewModel.Merge.MergeDpSlot;
        string acceptedPath = Assert.IsType<string>(slot.FilePath);
        FirmwareInspectionSnapshot acceptedProjection =
            Assert.IsType<FirmwareInspectionSnapshot>(slot.CurrentInspectionProjection);
        AuthoringRevision acceptedRevision = viewModel.Merge.StandardMergeAuthoringRevision;
        Task acceptedTask = viewModel.Merge.Inspection.ActiveTask;
        FirmwareSlotFactViewModel[] acceptedFacts = [.. slot.FirmwareFacts];
        var pickerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePicker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pickerCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string localPath = Path.Combine(workspace.Root, "native-picker-rejected.bin");
        IStorageProvider storageProvider = CreateStorageProvider(
            fileResults: CreateStorageResults<IStorageFile>(scenario, localPath, "rejected.bin"));
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            DataContext = slot,
            PickFirmwareFileAsync = async (provider, title) =>
            {
                _ = provider;
                _ = pickerInvoked.TrySetResult();
                await releasePicker.Task;
                try
                {
                    return await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
                        storageProvider,
                        title);
                }
                finally
                {
                    _ = pickerCompleted.TrySetResult();
                }
            },
        };
        var window = new Window
        {
            DataContext = viewModel,
            Content = card,
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Button browse = Assert.IsType<Button>(card.FindControl<Control>("BrowseButton"));
            browse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await pickerInvoked.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            _ = releasePicker.TrySetResult();
            await pickerCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.Equal(acceptedPath, slot.FilePath);
            Assert.Same(acceptedProjection, slot.CurrentInspectionProjection);
            Assert.Equal(acceptedRevision, viewModel.Merge.StandardMergeAuthoringRevision);
            Assert.Same(acceptedTask, viewModel.Merge.Inspection.ActiveTask);
            Assert.Equal(acceptedFacts, slot.FirmwareFacts);
            Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The async-void Browse handler returns and the Dispatcher remains operational.</summary>
    [AvaloniaFact]
    public async Task MalformedBaseBrowseActionLeavesDispatcherAliveWithTypedDiagnostic()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        byte[] validBase = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(baseArtifact));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-action-probe");
        string malformedBase = workspace.Write("nt51950-tp-with-trailing-byte.bin", [.. validBase, 0x00]);
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = "single";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        FirmwareSlotViewModel baseSlot = viewModel.Replace.ReplaceBaseSlot;
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            DataContext = baseSlot,
            PickFirmwareFileAsync = (_, _) => Task.FromResult<string?>(malformedBase),
        };
        var window = new Window
        {
            DataContext = viewModel,
            Content = card,
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Button browse = Assert.IsType<Button>(card.FindControl<Control>("BrowseButton"));
            Assert.Equal(CompositionSlotIds.ReplaceBase, browse.Tag);
            Task previousInspection = viewModel.Replace.Inspection.ActiveTask;
            browse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Task browseInspection = viewModel.Replace.Inspection.ActiveTask;
            Assert.NotSame(previousInspection, browseInspection);
            await browseInspection.WaitAsync(
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken);

            Assert.Equal(WorkflowInspectionAttemptState.Failed, viewModel.Replace.Inspection.State);
            Assert.False(viewModel.Replace.CanBuildReplace);
            Assert.Equal(FirmwareInputInspectionSeverity.Blocking, baseSlot.InputInspectionSeverity);
            Assert.Equal(ExpectedDiagnostic, baseSlot.InputInspectionStatus);
            Assert.True(baseSlot.BlocksBuild);
            Assert.Equal(FirmwareSlotSemanticState.Error, baseSlot.SemanticState);
            Assert.Equal($"Error: {ExpectedDiagnostic}", baseSlot.SemanticStateAutomationText);

            var dispatcherSentinel = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(
                dispatcherSentinel.SetResult,
                DispatcherPriority.Input);
            await dispatcherSentinel.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            window.Close();
        }
    }

    [SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass of this proxy base.")]
    private class StorageProviderProxy : DispatchProxy
    {
        public FilePickerOpenOptions? OpenOptions { get; private set; }

        public IReadOnlyList<IStorageFile> FileResults { get; set; } = [];

        public IReadOnlyList<IStorageFolder> FolderResults { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name switch
            {
                "get_CanOpen" => true,
                "get_CanPickFolder" => true,
                "get_CanSave" => false,
                nameof(IStorageProvider.OpenFilePickerAsync) => CaptureOpenOptions(args),
                nameof(IStorageProvider.OpenFolderPickerAsync) => CaptureFolderOptions(args),
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }

        private Task<IReadOnlyList<IStorageFile>> CaptureOpenOptions(object?[]? args)
        {
            OpenOptions = Assert.IsType<FilePickerOpenOptions>(Assert.Single(args!));
            return Task.FromResult(FileResults);
        }

        private Task<IReadOnlyList<IStorageFolder>> CaptureFolderOptions(object?[]? args)
        {
            _ = Assert.IsType<FolderPickerOpenOptions>(Assert.Single(args!));
            return Task.FromResult(FolderResults);
        }
    }

    [SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass of this proxy base.")]
    private class StorageItemProxy : DispatchProxy
    {
        public required Uri ItemPath { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name switch
            {
                "get_Path" => ItemPath,
                "get_Name" => ItemPath.Segments.LastOrDefault() ?? string.Empty,
                nameof(IDisposable.Dispose) => null,
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }
    }

    private static IStorageProvider CreateStorageProvider(
        IReadOnlyList<IStorageFile>? fileResults = null,
        IReadOnlyList<IStorageFolder>? folderResults = null)
    {
        IStorageProvider storageProvider = DispatchProxy.Create<IStorageProvider, StorageProviderProxy>();
        StorageProviderProxy proxy = Assert.IsType<StorageProviderProxy>(
            storageProvider,
            exactMatch: false);
        proxy.FileResults = fileResults ?? [];
        proxy.FolderResults = folderResults ?? [];
        return storageProvider;
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "DispatchProxy storage items have a no-op Dispose and are scoped to one test invocation.")]
    private static IReadOnlyList<TStorageItem> CreateStorageResults<TStorageItem>(
        PickerResultScenario scenario,
        string localPath,
        string nonLocalName)
        where TStorageItem : class, IStorageItem
    {
        return scenario switch
        {
            PickerResultScenario.Zero => [],
            PickerResultScenario.OneLocal => [CreateStorageItem<TStorageItem>(new Uri(localPath))],
            PickerResultScenario.TwoLocal =>
            [
                CreateStorageItem<TStorageItem>(new Uri(localPath)),
                CreateStorageItem<TStorageItem>(new Uri(localPath + ".second")),
            ],
            PickerResultScenario.OneNonLocal =>
                [CreateStorageItem<TStorageItem>(new Uri($"https://example.invalid/{nonLocalName}"))],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private static TStorageItem CreateStorageItem<TStorageItem>(Uri path)
        where TStorageItem : class, IStorageItem
    {
        TStorageItem item = DispatchProxy.Create<TStorageItem, StorageItemProxy>();
        StorageItemProxy proxy = Assert.IsType<StorageItemProxy>(item, exactMatch: false);
        proxy.ItemPath = path;
        return item;
    }

    /// <summary>Provider result shapes exercised at the native picker boundary.</summary>
    public enum PickerResultScenario
    {
        /// <summary>No selected storage item.</summary>
        Zero,

        /// <summary>One selected local storage item.</summary>
        OneLocal,

        /// <summary>Two selected local storage items despite single-selection options.</summary>
        TwoLocal,

        /// <summary>One selected storage item without a local filesystem path.</summary>
        OneNonLocal,
    }

}
