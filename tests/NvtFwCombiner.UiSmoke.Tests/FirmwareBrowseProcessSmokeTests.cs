using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name switch
            {
                "get_CanOpen" => true,
                "get_CanSave" or "get_CanPickFolder" => false,
                nameof(IStorageProvider.OpenFilePickerAsync) => CaptureOpenOptions(args),
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }

        private Task<IReadOnlyList<IStorageFile>> CaptureOpenOptions(object?[]? args)
        {
            OpenOptions = Assert.IsType<FilePickerOpenOptions>(Assert.Single(args!));
            return Task.FromResult<IReadOnlyList<IStorageFile>>([]);
        }
    }

}
