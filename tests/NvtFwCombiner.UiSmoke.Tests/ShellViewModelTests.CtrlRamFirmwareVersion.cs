using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Serialized reference-fidelity coverage for the shared Build settings surface.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class OutputDeliveryReferenceTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
    /// <summary>The approved A3/T2 Build settings geometry is identical in Light and Dark themes.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CtrlRamBuildSettingsMatchesApprovedA3T2Reference(
        bool useDarkTheme,
        bool useTraditionalChinese)
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-build-settings-reference");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = await Task.Run(
            async () =>
            {
                MainWindowViewModel ready = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
                if (useTraditionalChinese)
                {
                    ready.SelectedLanguage = "Traditional Chinese";
                }

                Assert.True(await ready.Replace.RequestCtrlRamBuildSettingsAsync());
                ready.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
                ready.OutputDelivery.SetBundleEnabled(true);
                ready.OutputDelivery.SetParentDirectory(workspace.Root);
                ready.OutputDelivery.SetBundleFolderName("NT51926_D0T0_20260824_bundle");
                return ready;
            },
            TestContext.Current.CancellationToken);

        var modal = new OutputDeliveryConfirmationModal
        {
            DataContext = viewModel.OutputDelivery,
            IsOpen = true,
        };
        var window = new Window
        {
            Width = 980,
            Height = 720,
            RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = modal,
        };
        foreach (string stylePath in new[]
        {
            "Styles/MainWindowStyles.axaml",
            "Styles/MainWindowButtonStyles.axaml",
            "Styles/MainWindowVisualStyles.axaml",
        })
        {
            var uri = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/{stylePath}");
            window.Styles.Add(new StyleInclude(uri) { Source = uri });
        }

        try
        {
            window.Show();
            window.Measure(new Size(980, 720));
            window.Arrange(new Rect(0, 0, 980, 720));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Border surface = Assert.IsType<Border>(modal.FindControl<Control>("BuildSettingsSurface"));
            Border outputSection = Assert.IsType<Border>(modal.FindControl<Control>("OutputSettingSection"));
            Button close = Assert.IsType<Button>(modal.FindControl<Control>("CloseButton"));
            Grid mode = Assert.IsType<Grid>(modal.FindControl<Control>("CtrlRamVersionModeRow"));
            Grid fields = Assert.IsType<Grid>(modal.FindControl<Control>("CtrlRamVersionFieldRow"));
            StackPanel versionControls = Assert.IsType<StackPanel>(
                modal.FindControl<Control>("CtrlRamVersionControlGroup"));
            TextBlock baseVersion = Assert.IsType<TextBlock>(
                modal.FindControl<Control>("CtrlRamBaseVersionValue"));
            ToggleButton keepVersion = Assert.IsType<ToggleButton>(
                modal.FindControl<Control>("CtrlRamVersionKeepChoice"));
            ToggleButton editVersion = Assert.IsType<ToggleButton>(
                modal.FindControl<Control>("CtrlRamVersionEditChoice"));
            TextBox firmwareVersion = Assert.IsType<TextBox>(
                modal.FindControl<Control>("CtrlRamFirmwareVersionInput"));
            TextBox firmwareSubVersion = Assert.IsType<TextBox>(
                modal.FindControl<Control>("CtrlRamFirmwareSubVersionInput"));
            Grid review = Assert.IsType<Grid>(modal.FindControl<Control>("BundleReviewPanel"));
            Border sources = Assert.IsType<Border>(modal.FindControl<Control>("SourcesListPanel"));
            ToggleButton sourcesToggle = Assert.IsType<ToggleButton>(
                modal.FindControl<Control>("SourcesDisclosureToggle"));
            Button editBundle = Assert.IsType<Button>(modal.FindControl<Control>("EditBundleDestinationButton"));
            Button done = Assert.IsType<Button>(modal.FindControl<Control>("CompleteBundleDestinationEditButton"));
            Button chooseParent = Assert.IsType<Button>(modal.FindControl<Control>("ChooseParentButton"));
            TextBox folder = Assert.IsType<TextBox>(modal.FindControl<Control>("FolderNameInput"));
            SelectableTextBlock folderDisplay = Assert.IsType<SelectableTextBlock>(
                modal.FindControl<Control>("BundleFolderNameDisplay"));
            SelectableTextBlock parentDisplay = Assert.IsType<SelectableTextBlock>(
                modal.FindControl<Control>("ParentDirectoryDisplay"));

            Assert.InRange(surface.Bounds.Width, 759.5, 760.5);
            Assert.True(surface.Bounds.Height <= 720);
            Assert.Equal(40, close.Bounds.Width);
            Assert.Equal(close.Bounds.Width, close.Bounds.Height);
            Assert.True(mode.IsVisible);
            Assert.True(fields.IsVisible);
            Assert.InRange(versionControls.Bounds.Width, 299.5, 300.5);
            Assert.True(Avalonia.Application.Current!.TryGetResource(
                "NfcUiFontFamily",
                useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
                out object? fontResource));
            FontFamily uiFont = Assert.IsType<FontFamily>(fontResource);
            Assert.Equal(uiFont, baseVersion.FontFamily);
            Assert.Equal(uiFont, keepVersion.FontFamily);
            Assert.Equal(uiFont, editVersion.FontFamily);
            Assert.Equal(uiFont, firmwareVersion.FontFamily);
            Assert.Equal(uiFont, firmwareSubVersion.FontFamily);
            Assert.Equal(FontWeight.Medium, baseVersion.FontWeight);
            Assert.Equal(FontWeight.Medium, keepVersion.FontWeight);
            Assert.Equal(FontWeight.Medium, editVersion.FontWeight);
            Assert.Equal(FontWeight.Medium, firmwareVersion.FontWeight);
            Assert.Equal(FontWeight.Medium, firmwareSubVersion.FontWeight);
            Point modeOrigin = Assert.IsType<Point>(mode.TranslatePoint(new Point(), surface));
            Point fieldOrigin = Assert.IsType<Point>(fields.TranslatePoint(new Point(), surface));
            Assert.True(fieldOrigin.Y >= modeOrigin.Y + mode.Bounds.Height);
            Assert.True(review.IsVisible);
            Assert.True(folderDisplay.IsVisible);
            Assert.False(folder.IsVisible);
            Assert.True(parentDisplay.IsVisible);
            Assert.True(chooseParent.IsVisible);
            Assert.InRange(chooseParent.Bounds.Width, 33.5, 34.5);
            Assert.False(sources.IsVisible);
            Point sourceToggleOrigin = Assert.IsType<Point>(
                sourcesToggle.TranslatePoint(new Point(), outputSection));
            Assert.True(sourceToggleOrigin.X > outputSection.Bounds.Width / 2);

            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                string languageName = useTraditionalChinese ? "zh-tw" : "en";
                await using FileStream output = File.Create(Path.Combine(
                    outputDirectory,
                    $"build-settings-a3-t2-980x720-{themeName}-{languageName}.png"));
                frame.Save(output);
            }

            sourcesToggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(sources.IsVisible);
            Assert.InRange(
                Math.Abs(sources.Bounds.Width - outputSection.Bounds.Width),
                0,
                0.5);

            editBundle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(review.IsVisible);
            Assert.False(folderDisplay.IsVisible);
            Assert.True(folder.IsVisible);
            Assert.True(parentDisplay.IsVisible);
            Assert.True(chooseParent.IsVisible);
            Assert.Same(folder, window.FocusManager?.GetFocusedElement());

            done.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(review.IsVisible);
            Assert.True(folderDisplay.IsVisible);
            Assert.False(folder.IsVisible);
            Assert.Same(editBundle, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

}

public sealed partial class CtrlRamWorkflowTests
{

    /// <summary>CtrlRAM keeps the verified edit lease through proposal creation, then closes both authoring modals.</summary>
    [Fact]
    public async Task CtrlRamConfirmedEditOpensOutputDeliveryForExactAcceptedSession()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-output-confirmation");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.True(viewModel.OutputDelivery.HasCtrlRamOptions);
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        using var destination = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-build-settings-state");
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.SetBundleFolderName("operator-edited-bundle");
        (bool succeeded, CtrlRamFirmwareVersionDraftState? edit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(succeeded);

        Assert.True(await viewModel.OutputDelivery.PrepareModeSpecificAsync());

        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.True(viewModel.OutputDelivery.IsReplaceOutput);
        Assert.Equal("nt51926-ctrlram-replace.bin", viewModel.OutputDelivery.OutputFileName);
        Assert.True(viewModel.OutputDelivery.BundleEnabled);
        Assert.Equal(destination.Root, viewModel.OutputDelivery.ParentDirectory);
        Assert.Equal("operator-edited-bundle", viewModel.OutputDelivery.BundleFolderName);
    }

    /// <summary>Verifies CtrlRAM Build exposes a Backup-derived Preserve/Edit choice and validates staged bytes.</summary>
    [Fact]
    public async Task CtrlRamBuildFirmwareVersionChoiceUsesVerifiedBackupMetadata()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-choice");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);

        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionPreserveSelected);
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionEditSelected);
        Assert.True(viewModel.Replace.CanEditCtrlRamFirmwareVersion, viewModel.Replace.CtrlRamFirmwareVersionMetadataDetail);
        Assert.Matches("^[0-9A-F]{2} / [0-9A-F]{2}$", viewModel.Replace.CtrlRamFirmwareVersionCurrentValue);
        (bool preserveSucceeded, CtrlRamFirmwareVersionDraftState? preserveEdit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(preserveSucceeded);
        Assert.Null(preserveEdit);

        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        Assert.True(viewModel.Replace.IsCtrlRamFirmwareVersionEditSelected);
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionPreserveSelected);

        viewModel.Replace.CtrlRamFirmwareVersionText = "A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "04";
        (bool invalidSucceeded, _) = await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.False(invalidSucceeded);
        Assert.Equal(viewModel.Text.CtrlRamFirmwareVersionInvalidByteDetail, viewModel.Replace.CtrlRamFirmwareVersionValidationDetail);

        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        (bool editSucceeded, CtrlRamFirmwareVersionDraftState? edit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(editSucceeded);
        Assert.NotNull(edit);
        Assert.Equal((byte)0x2A, edit.FirmwareVersion);
        Assert.Equal((byte)0x0C, edit.FirmwareSubVersion);
        Assert.Equal("nt51926-ctrlram-replace.bin", viewModel.Replace.CreateCtrlRamReplaceOutputFileName(edit));

        viewModel.Replace.CloseCtrlRamFirmwareVersionModal();
        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
    }

    /// <summary>Verifies the confirmed version reaches the output Backup through the admitted V2 postbuild route.</summary>
    [Fact]
    public async Task CtrlRamBuildPropagatesConfirmedFirmwareVersionToOutputBackup()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-version-build");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        string outputPath = workspace.PathFor("ctrlram-version-output.bin");

        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        (bool editSucceeded, CtrlRamFirmwareVersionDraftState? edit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(editSucceeded);
        Assert.NotNull(edit);
        viewModel.Replace.CloseCtrlRamFirmwareVersionModal();

        await viewModel.Replace.BuildReplaceAsync(outputPath, edit);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath));
        FirmwareConfigMetadataSnapshot? outputMetadata =
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(TestProjection, "NT51926", outputPath);
        Assert.NotNull(outputMetadata);
        Assert.Equal(0x2A, outputMetadata.FirmwareVersion);
        Assert.Equal(0xD5, outputMetadata.FirmwareVersionBar);
        Assert.Equal(0x0C, outputMetadata.FirmwareSubVersion);
        Assert.True(viewModel.Reports.HasLoadedReport);
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        Assert.Empty(report.RootElement.GetProperty("Issues").EnumerateArray());
        JsonElement validation = Assert.Single(report.RootElement.GetProperty("Validations").EnumerateArray());
        Assert.Equal("verify-nvt-fwconfig-backup-version", validation.GetProperty("RuleId").GetString());
        Assert.Equal("Passed", validation.GetProperty("Status").GetString());
    }

    /// <summary>Build reuses accepted CtrlRAM bytes even when the selected path changes later.</summary>
    [Fact]
    public async Task CtrlRamBuildReusesAcceptedBytesAfterSelectedPathChanges()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-stale-build");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        string previewSha256 = viewModel.Reports.LoadedReport.OutputSha256;
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.Single(static slot =>
            slot.HasFile && slot.SlotId != CompositionSlotIds.ReplaceBase);
        string replacementPath = Assert.IsType<string>(replacement.FilePath);
        byte[] changed = File.ReadAllBytes(replacementPath);
        changed[0] ^= 0x01;
        File.WriteAllBytes(replacementPath, changed);
        string outputPath = workspace.PathFor("accepted-output.bin");

        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath));
        Assert.Equal(previewSha256, viewModel.Reports.LoadedReport.OutputSha256);
    }

    /// <summary>IC, number, and slot changes revoke both Preserve and Edit confirmation intent.</summary>
    [Theory]
    [InlineData("ic", false)]
    [InlineData("number", false)]
    [InlineData("base", false)]
    [InlineData("replacement", false)]
    [InlineData("number", true)]
    public async Task CtrlRamFirmwareVersionModalLeaseRejectsChangedBuildContext(
        string contextChange,
        bool selectEdit)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create(
            $"nvt-fw-combiner-ui-ctrlram-version-lease-{contextChange}-{selectEdit}");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);

        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        if (selectEdit)
        {
            viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        }

        Assert.True(viewModel.Replace.CanConfirmCtrlRamFirmwareVersion);
        Assert.True(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(cancellationToken));

        switch (contextChange)
        {
            case "ic":
                viewModel.WorkflowSession.SelectedIc = "NT51927";
                break;
            case "number":
                viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
                break;
            case "base":
                viewModel.SetSlotFile("replace-base", workspace.Write("changed-base.bin", baseBytes));
                break;
            case "replacement":
                FirmwareSlotViewModel replacementSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
                    !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot) && slot.HasFile);
                string replacementPath = Assert.IsType<string>(replacementSlot.FilePath);
                viewModel.SetSlotFile(
                    replacementSlot.SlotId,
                    workspace.Write("changed-replacement.bin", File.ReadAllBytes(replacementPath)));
                break;
            default:
                throw new InvalidOperationException($"Unsupported test context change '{contextChange}'.");
        }

        Assert.False(viewModel.Replace.IsCtrlRamFirmwareVersionModalOpen);
        Assert.False(viewModel.Replace.CanConfirmCtrlRamFirmwareVersion);
        Assert.False(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(cancellationToken));
        (bool succeeded, _) = await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.False(succeeded);
    }

    /// <summary>Build confirmation and execution retain the immutable bytes accepted by the verified session.</summary>
    [Fact]
    public async Task CtrlRamFirmwareVersionBuildConfirmationUsesAcceptedBytesAfterDiskMutation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-content-lease");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        string acceptedOutputSha256 = viewModel.Reports.LoadedReport.OutputSha256;
        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        string basePath = Assert.IsType<string>(viewModel.Replace.ReplaceBaseSlot.FilePath);
        byte[] changed = File.ReadAllBytes(basePath);
        changed[^1] ^= 0x01;
        File.WriteAllBytes(basePath, changed);

        Assert.True(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(
            cancellationToken));
        (bool succeeded, _) = await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(
            cancellationToken);
        Assert.True(succeeded);

        string outputPath = workspace.PathFor("accepted-session-output.bin");
        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(outputPath));
        Assert.Equal(acceptedOutputSha256, viewModel.Reports.LoadedReport.OutputSha256);
    }

    /// <summary>Open and Edit use canonical inspection facts even when the accepted Base path changes or disappears.</summary>
    [Theory]
    [InlineData("overwrite-before-open")]
    [InlineData("delete-before-open")]
    [InlineData("overwrite-after-open")]
    [InlineData("delete-after-open")]
    public async Task CtrlRamFirmwareVersionEditUsesAcceptedFactsWithoutPathReread(string mutation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutation);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-ui-ctrlram-version-{mutation}");
        byte[] baseBytes = golden.ReadExpectedOutput(golden.CaseByIc("51926"));
        MainWindowViewModel viewModel = CreateCtrlRamVersionReadyViewModel(baseBytes, workspace);
        string basePath = Assert.IsType<string>(viewModel.Replace.ReplaceBaseSlot.FilePath);
        CompiledInputVersionObservation acceptedMetadata = Assert.Single(
            viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection?
                .InputSlotStatus?.Observation.Versions ?? [],
            static version => version.Kind == CompiledInputVersionKind.TpReferenceFirmwareConfig);

        if (mutation.EndsWith("before-open", StringComparison.Ordinal))
        {
            MutateAcceptedPath(basePath, mutation);
        }

        Assert.True(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(cancellationToken));
        Assert.Equal(
            FormattableString.Invariant(
                $"{acceptedMetadata.Major:X2} / {acceptedMetadata.Minor:X2}"),
            viewModel.Replace.CtrlRamFirmwareVersionCurrentValue);
        if (mutation.EndsWith("after-open", StringComparison.Ordinal))
        {
            MutateAcceptedPath(basePath, mutation);
        }

        Assert.True(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(
            cancellationToken));
        viewModel.Replace.SelectCtrlRamFirmwareVersionEditCommand.Execute(null);
        viewModel.Replace.CtrlRamFirmwareVersionText = "2A";
        viewModel.Replace.CtrlRamFirmwareSubVersionText = "0C";
        (bool succeeded, CtrlRamFirmwareVersionDraftState? edit) =
            await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(cancellationToken);
        Assert.True(succeeded);
        Assert.NotNull(edit);

        string outputPath = workspace.PathFor($"{mutation}-output.bin");
        await viewModel.Replace.BuildReplaceAsync(outputPath, edit);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        FirmwareConfigMetadataSnapshot outputMetadata = Assert.IsType<FirmwareConfigMetadataSnapshot>(
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(
                TestProjection,
                "NT51926",
                outputPath));
        Assert.Equal(0x2A, outputMetadata.FirmwareVersion);
        Assert.Equal(0x0C, outputMetadata.FirmwareSubVersion);
    }

    private static void MutateAcceptedPath(string path, string mutation)
    {
        if (mutation.StartsWith("delete", StringComparison.Ordinal))
        {
            File.Delete(path);
            return;
        }

        byte[] changed = File.ReadAllBytes(path);
        changed[^1] ^= 0x01;
        File.WriteAllBytes(path, changed);
    }

}
