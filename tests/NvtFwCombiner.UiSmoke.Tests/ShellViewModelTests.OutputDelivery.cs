using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>The production shell becomes inert and Build Settings retains and restores keyboard focus.</summary>
    [AvaloniaFact]
    public async Task OutputDeliveryModalOwnsLiveInertAndFocusLifecycle()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await Task.Run(
            () => CreateReadyStandardMergeAsync(golden, goldenCase));
        var build = new Button { Content = "Build" };
        var shell = new Border { Name = "ShellInteractionHost", Child = build };
        var modal = new OutputDeliveryConfirmationModal
        {
            DataContext = viewModel.OutputDelivery,
        };
        _ = modal.Bind(
            OutputDeliveryConfirmationModal.IsOpenProperty,
            new Binding(nameof(OutputDeliveryConfirmationViewModel.IsOpen)));
        var modalHost = new ContentControl
        {
            Content = modal,
            DataContext = viewModel.OutputDelivery,
        };
        _ = modalHost.Bind(
            Visual.IsVisibleProperty,
            new Binding(nameof(OutputDeliveryConfirmationViewModel.IsOpen)));
        var window = new Window
        {
            DataContext = viewModel,
            Content = new Grid
            {
                Children = { shell, modalHost },
            },
        };
        void ApplyShellState(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.OutputDelivery))
            {
                MainWindow.ApplyShellInteractionState(shell, isStartupShellEnabled: true, viewModel);
            }
        }

        viewModel.PropertyChanged += ApplyShellState;
        try
        {
            window.Show();
            MainWindow.ApplyShellInteractionState(shell, isStartupShellEnabled: true, viewModel);
            _ = build.Focus(NavigationMethod.Tab);
            modal.CaptureReturnFocus(build, () => viewModel.CanRestoreOutputDeliveryFocus);

            await viewModel.Merge.RequestBuildOutputDeliveryAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.OutputDelivery.IsOpen);
            Assert.False(shell.IsEnabled);
            Assert.False(shell.IsHitTestVisible);
            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(modal));
            AssertFocusedInside(window, modal);

            modal.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Tab,
            });
            Dispatcher.UIThread.RunJobs();
            AssertFocusedInside(window, modal);

            modal.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
            });
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.OutputDelivery.IsOpen);
            Assert.True(shell.IsEnabled);
            Assert.True(shell.IsHitTestVisible);
            Assert.Same(build, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            viewModel.PropertyChanged -= ApplyShellState;
            window.Close();
        }
    }

    /// <summary>The production host keeps Build Settings interactive while only its shell is inert.</summary>
    [AvaloniaFact]
    public async Task OutputDeliveryModalRemainsEnabledInProductionHost()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await Task.Run(
            () => CreateReadyStandardMergeAsync(golden, goldenCase));
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
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
            window.Show();
            ContentControl modalHost = Assert.IsType<ContentControl>(
                window.FindControl<ContentControl>("OutputDeliveryConfirmationModalHost"),
                exactMatch: false);
            modalHost.Content = viewModel.OutputDelivery;
            await viewModel.Merge.RequestBuildOutputDeliveryAsync();
            Dispatcher.UIThread.RunJobs();

            Control shell = Assert.IsType<Control>(
                window.FindControl<Control>("ShellInteractionHost"),
                exactMatch: false);
            OutputDeliveryConfirmationModal modal = Assert.Single(
                window.GetVisualDescendants()
                    .OfType<OutputDeliveryConfirmationModal>(),
                static candidate => candidate.IsVisible);
            CheckBox bundleToggle = Assert.IsType<CheckBox>(
                modal.FindControl<CheckBox>("BundleToggle"),
                exactMatch: false);
            TextBox outputName = Assert.IsType<TextBox>(
                modal.FindControl<TextBox>("OutputFileNameInput"),
                exactMatch: false);
            SelectableTextBlock outputNameDisplay = Assert.IsType<SelectableTextBlock>(
                modal.FindControl<SelectableTextBlock>("OutputFileNameDisplay"),
                exactMatch: false);
            Button editOutputName = Assert.IsType<Button>(
                modal.FindControl<Button>("EditOutputFileNameButton"),
                exactMatch: false);
            Button confirm = Assert.IsType<Button>(
                modal.FindControl<Button>("ConfirmButton"),
                exactMatch: false);
            MainWindow.ApplyShellInteractionState(
                shell,
                isStartupShellEnabled: true,
                viewModel);

            Assert.False(shell.IsEnabled);
            Assert.False(shell.IsHitTestVisible);
            Assert.DoesNotContain(shell, modal.GetVisualAncestors());
            Assert.True(modal.IsEffectivelyEnabled);
            Assert.True(bundleToggle.IsEffectivelyEnabled);
            Assert.True(outputNameDisplay.IsVisible);
            Assert.False(outputName.IsVisible);
            Assert.True(editOutputName.IsEffectivelyEnabled);

            editOutputName.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.OutputDelivery.IsOutputFileNameEditing);
            Assert.False(outputNameDisplay.IsVisible);
            Assert.True(outputName.IsVisible);
            Assert.False(outputName.IsReadOnly);
            Assert.Same(outputName, window.FocusManager?.GetFocusedElement());

            confirm.ApplyTemplate();
            ContentPresenter confirmSurface = Assert.Single(
                confirm.GetVisualDescendants().OfType<ContentPresenter>(),
                static candidate => candidate.Name == "PART_ContentPresenter");
            Color restingColor = Assert.IsType<ISolidColorBrush>(
                confirmSurface.Background,
                exactMatch: false).Color;
            Point pointer = Assert.IsType<Point>(confirm.TranslatePoint(
                new Point(confirm.Bounds.Width / 2, confirm.Bounds.Height / 2),
                window));
            window.MouseMove(pointer, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.True(confirm.IsPointerOver);
            Assert.NotEqual(
                restingColor,
                Assert.IsType<ISolidColorBrush>(
                    confirmSurface.Background,
                    exactMatch: false).Color);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The live confirm gate rejects a second picker or execution until the first completes.</summary>
    [AvaloniaFact]
    public void OutputDeliveryLiveConfirmIsNotReentrant()
    {
        var modal = new OutputDeliveryConfirmationModal();

        Assert.True(modal.TryBeginConfirmation());
        Assert.False(modal.TryBeginConfirmation());

        modal.EndConfirmation();
        Assert.True(modal.TryBeginConfirmation());
        modal.EndConfirmation();
    }

    /// <summary>Confirm completion retries its pending focus lease and defers to a successor modal.</summary>
    [AvaloniaFact]
    public void OutputDeliveryConfirmCompletionRestoresOrHandsOffFocus()
    {
        var build = new Button { Content = "Build" };
        var successor = new Button { Content = "Successor" };
        var modal = new OutputDeliveryConfirmationModal { IsOpen = true };
        var window = new Window
        {
            Content = new Grid
            {
                Children = { build, successor, modal },
            },
        };
        bool successorOpen = false;
        try
        {
            window.Show();
            _ = build.Focus(NavigationMethod.Tab);
            modal.CaptureReturnFocus(build, () => !successorOpen);

            Assert.True(modal.TryBeginConfirmation());
            _ = successor.Focus(NavigationMethod.Tab);
            modal.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(successor, window.FocusManager?.GetFocusedElement());

            modal.EndConfirmation();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(build, window.FocusManager?.GetFocusedElement());

            modal.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            modal.CaptureReturnFocus(build, () => !successorOpen);
            Assert.True(modal.TryBeginConfirmation());
            successorOpen = true;
            _ = successor.Focus(NavigationMethod.Tab);
            modal.IsOpen = false;
            modal.EndConfirmation();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(successor, window.FocusManager?.GetFocusedElement());

            successorOpen = false;
            modal.RetryPendingFocusRestore();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(build, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every Standard Merge Build command opens one canonical confirmation before writing.</summary>
    [Fact]
    public async Task StandardMergeBuildCommandRequiresSharedOutputConfirmation()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, goldenCase);

        await viewModel.Merge.BuildMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.False(viewModel.OutputDelivery.BundleEnabled);
        Assert.EndsWith(".bin", viewModel.OutputDelivery.OutputFileName, StringComparison.Ordinal);
        Assert.Equal(2, viewModel.OutputDelivery.Sources.Count);
        Assert.All(viewModel.OutputDelivery.Sources, static source =>
        {
            Assert.NotEmpty(source.OriginalFileName);
            Assert.True(source.Size > 0);
            Assert.Equal(64, source.Sha256.Length);
        });
    }

    /// <summary>The output name starts locked, becomes an explicit draft on edit, and resets for canonical bundles.</summary>
    [Fact]
    public async Task OutputConfirmationEditsLooseNameWithoutMisreportingAutomaticIdentity()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, goldenCase);
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        string canonicalName = viewModel.OutputDelivery.OutputFileName;

        Assert.False(viewModel.OutputDelivery.IsOutputFileNameEditing);
        Assert.True(viewModel.OutputDelivery.OutputFileNameUsesAutomaticName);

        viewModel.OutputDelivery.BeginOutputFileNameEdit();
        viewModel.OutputDelivery.SetOutputFileName("operator-name.bin");

        Assert.True(viewModel.OutputDelivery.IsOutputFileNameEditing);
        Assert.Equal("operator-name.bin", viewModel.OutputDelivery.OutputFileName);
        Assert.False(viewModel.OutputDelivery.OutputFileNameUsesAutomaticName);

        viewModel.OutputDelivery.SetBundleEnabled(true);

        Assert.False(viewModel.OutputDelivery.IsOutputFileNameEditing);
        Assert.Equal(canonicalName, viewModel.OutputDelivery.OutputFileName);
        Assert.True(viewModel.OutputDelivery.OutputFileNameUsesAutomaticName);
    }

    /// <summary>Cancel writes nothing and retains the operator's edited bundle state.</summary>
    [Fact]
    public async Task OutputConfirmationCancelRetainsStateWithoutFilesystemMutation()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, goldenCase);
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.SetBundleFolderName("operator-bundle");

        viewModel.OutputDelivery.CancelCommand.Execute(null);

        Assert.False(viewModel.OutputDelivery.IsOpen);
        Assert.True(viewModel.OutputDelivery.BundleEnabled);
        Assert.Equal(destination.Root, viewModel.OutputDelivery.ParentDirectory);
        Assert.Equal("operator-bundle", viewModel.OutputDelivery.BundleFolderName);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>Bundle ON passes the admitted intent and produces no loose primary BIN.</summary>
    [Fact]
    public async Task BundleConfirmationBuildsOnlyInsideAtomicFolder()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, goldenCase);
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.SetBundleFolderName("operator-bundle");
        Assert.True(
            viewModel.OutputDelivery.IsBundleDestinationValid,
            viewModel.OutputDelivery.ValidationMessage);

        await viewModel.OutputDelivery.ConfirmBundleAsync();

        string bundle = Path.Combine(destination.Root, "operator-bundle");
        Assert.True(
            Directory.Exists(bundle),
            $"Result={viewModel.RunSession.LastRunResult.Title}/" +
            $"{viewModel.RunSession.LastRunResult.Detail}; " +
            $"Entries={string.Join(',', Directory.EnumerateFileSystemEntries(destination.Root))}");
        Assert.Empty(Directory.EnumerateFiles(destination.Root, "*.bin"));
        Assert.Equal(3, Directory.EnumerateFiles(bundle, "*.bin").Count());
    }

    /// <summary>Bundle OFF retains the existing loose output execution path.</summary>
    [Fact]
    public async Task BundleDisabledBuildsOneLoosePrimaryOutput()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, goldenCase);
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        string outputPath = Path.Combine(destination.Root, viewModel.OutputDelivery.OutputFileName);

        await viewModel.OutputDelivery.ConfirmLooseAsync(
            outputPath,
            additionalOutputPath: null,
            outputPathUsesAutomaticName: false,
            additionalOutputPathUsesAutomaticName: false);

        Assert.True(File.Exists(outputPath));
        _ = Assert.Single(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>A proposal cannot execute after the accepted authoring session changes.</summary>
    [Fact]
    public async Task OutputConfirmationRejectsStaleAcceptedSessionWithoutWriting()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, goldenCase);
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.SetBundleFolderName("stale-bundle");
        viewModel.WorkflowSession.SelectedIc = "NT51927";

        await viewModel.OutputDelivery.ConfirmBundleAsync();

        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.Equal(viewModel.Text.OutputDeliveryStaleAcceptedSession, viewModel.OutputDelivery.ValidationMessage);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>The shared confirmation is exclusive and makes the composition shell inert.</summary>
    [Fact]
    public async Task OutputConfirmationExcludesSettingsAndCompositionActions()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, goldenCase);

        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        viewModel.OpenSettingsCommand.Execute(null);

        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.False(viewModel.IsSettingsModalOpen);
        Assert.False(viewModel.IsCompositionActionRailVisible);
    }

    /// <summary>The shared loose flow preserves suggested-vs-custom identity in the canonical run report.</summary>
    [Fact]
    public async Task OutputConfirmationPreservesAutomaticNameReportIdentity()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using TempWorkspace automaticDestination = TempWorkspace.Create();
        MainWindowViewModel automatic = await CreateReadyStandardMergeAsync(golden, goldenCase);
        await automatic.Merge.RequestBuildOutputDeliveryAsync();
        string automaticPath = automaticDestination.PathFor(automatic.OutputDelivery.OutputFileName);
        await automatic.OutputDelivery.ConfirmLooseAsync(
            automaticPath,
            additionalOutputPath: null,
            outputPathUsesAutomaticName: automatic.OutputDelivery.OutputFileNameUsesAutomaticName,
            additionalOutputPathUsesAutomaticName: false);
        Assert.True(automatic.RunSession.LastRunResult.Succeeded, automatic.RunSession.LastRunResult.Detail);
        using (var report = JsonDocument.Parse(automatic.Reports.LoadedReportJson))
        {
            Assert.False(report.RootElement.GetProperty("OutputNaming").GetProperty("IsExplicitOverride").GetBoolean());
        }

        using TempWorkspace customDestination = TempWorkspace.Create();
        MainWindowViewModel custom = await CreateReadyStandardMergeAsync(golden, goldenCase);
        await custom.Merge.RequestBuildOutputDeliveryAsync();
        custom.OutputDelivery.BeginOutputFileNameEdit();
        custom.OutputDelivery.SetOutputFileName("operator-name.bin");
        await custom.OutputDelivery.ConfirmLooseAsync(
            customDestination.PathFor(custom.OutputDelivery.OutputFileName),
            additionalOutputPath: null,
            outputPathUsesAutomaticName: custom.OutputDelivery.OutputFileNameUsesAutomaticName,
            additionalOutputPathUsesAutomaticName: false);
        Assert.True(custom.RunSession.LastRunResult.Succeeded, custom.RunSession.LastRunResult.Detail);
        using var customReport = JsonDocument.Parse(custom.Reports.LoadedReportJson);
        Assert.True(customReport.RootElement.GetProperty("OutputNaming").GetProperty("IsExplicitOverride").GetBoolean());
    }

    private static async Task<MainWindowViewModel> CreateReadyStandardMergeAsync(
        StandardMergeGoldenManifest golden,
        JsonElement goldenCase)
    {
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input")),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input")),
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Merge.CanBuildMerge);
        return viewModel;
    }

    private static void AssertFocusedInside(Window window, OutputDeliveryConfirmationModal modal)
    {
        Visual focused = Assert.IsType<Visual>(
            window.FocusManager?.GetFocusedElement(),
            exactMatch: false);
        Assert.Contains(focused, modal.GetVisualDescendants().Prepend(modal));
    }

}
