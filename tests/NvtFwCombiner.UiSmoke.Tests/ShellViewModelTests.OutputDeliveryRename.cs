using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Invalid names block confirmation without writing, and a corrected draft can be retried.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("../escape.bin")]
    [InlineData("CON.bin")]
    [InlineData("bad?.bin")]
    [InlineData("trailing.bin.")]
    public async Task BundlePrimaryInvalidNameBlocksWithoutMutation(string invalidName)
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926"));
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.BeginOutputFileNameEdit();
        viewModel.OutputDelivery.SetOutputFileName(invalidName);
        Assert.False(viewModel.OutputDelivery.CanConfirm);
        Assert.NotEmpty(viewModel.OutputDelivery.ValidationMessage);
        await viewModel.OutputDelivery.ConfirmBundleAsync();
        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
        viewModel.OutputDelivery.SetOutputFileName("valid.bin");
        Assert.True(viewModel.OutputDelivery.CanConfirm, viewModel.OutputDelivery.ValidationMessage);
    }

    /// <summary>Cancel/reopen of the same accepted session retains independent primary and folder drafts.</summary>
    [Fact]
    public async Task BundlePrimaryCancelledDraftSurvivesReopen()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926"));
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), viewModel.OutputDelivery.ParentDirectory);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.BeginOutputFileNameEdit();
        viewModel.OutputDelivery.SetOutputFileName("draft.bin");
        viewModel.OutputDelivery.SetBundleFolderName("draft-folder");
        viewModel.OutputDelivery.CancelCommand.Execute(null);
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        Assert.Equal("draft.bin", viewModel.OutputDelivery.OutputFileName);
        Assert.Equal("draft-folder", viewModel.OutputDelivery.BundleFolderName);
        Assert.Equal(destination.Root, viewModel.OutputDelivery.ParentDirectory);
        Assert.True(viewModel.OutputDelivery.BundleEnabled);
    }

    /// <summary>The effective primary name reaches bytes and report without changing the canonical name or source copies.</summary>
    [Fact]
    public async Task BundlePrimaryOverrideMatchesAutomaticBytesAndReport()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel viewModel = await CreateReadyStandardMergeAsync(golden, goldenCase);
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        string automaticName = viewModel.OutputDelivery.OutputFileName;
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.SetBundleFolderName("automatic");
        await viewModel.OutputDelivery.ConfirmBundleAsync();
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        byte[] automaticBytes = File.ReadAllBytes(Path.Combine(destination.Root, "automatic", automaticName));
        using var automaticReport = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        viewModel.OutputDelivery.BeginOutputFileNameEdit();
        viewModel.OutputDelivery.SetOutputFileName("customer.bin");
        viewModel.OutputDelivery.SetBundleFolderName("custom");
        await viewModel.OutputDelivery.ConfirmBundleAsync();
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        string customPath = Path.Combine(destination.Root, "custom", "customer.bin");
        Assert.True(File.Exists(customPath));
        Assert.Equal(automaticBytes, File.ReadAllBytes(customPath));
        Assert.False(File.Exists(Path.Combine(destination.Root, "custom", automaticName)));
        foreach (CompositionOutputBundleSourceSummary source in viewModel.OutputDelivery.Sources)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(destination.Root, "custom", source.OriginalFileName));
            Assert.Equal(source.Sha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), ignoreCase: true);
        }
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement naming = report.RootElement.GetProperty("OutputNaming");
        Assert.Equal("customer.bin", naming.GetProperty("ActualFileName").GetString());
        Assert.Equal(automaticName, naming.GetProperty("AutomaticFileName").GetString());
        Assert.True(naming.GetProperty("IsExplicitOverride").GetBoolean());
        JsonElement automaticNaming = automaticReport.RootElement.GetProperty("OutputNaming");
        foreach (string property in new[] { "RendererKind", "Template", "Tokens", "Admission" })
        {
            Assert.Equal(automaticNaming.GetProperty(property).GetRawText(), naming.GetProperty(property).GetRawText());
        }
        Assert.Contains("customer.bin", report.RootElement.GetProperty("BundleDelivery").GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>Bundle mode retains an editable primary name independently of its destination folder.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BundlePrimaryNameRemainsEditableAndIndependent(bool chinese)
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await Task.Run(() => CreateReadyStandardMergeAsync(golden, goldenCase));
        if (chinese)
        {
            viewModel.SelectedLanguage = "Traditional Chinese";
        }
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        {
            DataContext = viewModel,
            Width = 1280,
            Height = 900,
            RequestedThemeVariant = chinese ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        try
        {
            window.Show();
            window.FindControl<ContentControl>("OutputDeliveryConfirmationModalHost")!.Content = viewModel.OutputDelivery;
            await viewModel.Merge.RequestBuildOutputDeliveryAsync();
            Dispatcher.UIThread.RunJobs();
            OutputDeliveryConfirmationModal modal = window.GetVisualDescendants().OfType<OutputDeliveryConfirmationModal>().Single();
            ToggleButton disclosure = modal.FindControl<ToggleButton>("SourcesDisclosureToggle")!;
            Assert.Contains("inlineDisclosure", disclosure.Classes);
            Assert.DoesNotContain("quietDisclosure", disclosure.Classes);
            Assert.Equal(chinese ? "檢視來源檔案" : "View source files", viewModel.Text.OutputDeliverySourcesLabel);
            _ = disclosure.Focus();
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Dispatcher.UIThread.RunJobs();
            Assert.True(viewModel.OutputDelivery.AreSourcesExpanded);
            ContentPresenter disclosureSurface = disclosure.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(static presenter => presenter.Name == "PART_ContentPresenter");
            Assert.Equal(0, Assert.IsType<ISolidColorBrush>(disclosureSurface.Background, exactMatch: false).Color.A);
            window.MouseMove(disclosure.TranslatePoint(new Point(17, 17), window)!.Value, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, Assert.IsType<ISolidColorBrush>(disclosureSurface.Background, exactMatch: false).Color.A);
            Assert.Equal(3, modal.FindControl<Control>("SourcesExpandedChevron")!.RenderTransform!.Value.M32);
            Assert.Equal(3, modal.FindControl<Control>("SourcesCollapsedChevron")!.RenderTransform!.Value.M32);
            window.MouseMove(new Point(5, 5), RawInputModifiers.None);
            Assert.True(modal.FindControl<Control>("SourcesExpandedChevron")!.IsVisible);
            Assert.False(modal.FindControl<Control>("SourcesCollapsedChevron")!.IsVisible);
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Dispatcher.UIThread.RunJobs();
            Assert.False(viewModel.OutputDelivery.AreSourcesExpanded);
            Button edit = modal.FindControl<Button>("EditOutputFileNameButton")!;
            TextBox input = modal.FindControl<TextBox>("OutputFileNameInput")!;
            edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            input.Text = "customer-release.bin";
            Dispatcher.UIThread.RunJobs();
            string folder = viewModel.OutputDelivery.BundleFolderName;
            viewModel.OutputDelivery.SetBundleEnabled(true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(edit.IsEffectivelyEnabled);
            Assert.Equal("customer-release.bin", viewModel.OutputDelivery.OutputFileName);
            Assert.Equal(folder, viewModel.OutputDelivery.BundleFolderName);
            input.Text = "customer-final.bin";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("customer-final.bin", viewModel.OutputDelivery.OutputFileName);
            viewModel.OutputDelivery.SetBundleFolderName("delivery-folder");
            Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), viewModel.OutputDelivery.ParentDirectory);
            viewModel.OutputDelivery.SetBundleEnabled(false);
            viewModel.OutputDelivery.SetBundleEnabled(true);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("customer-final.bin", viewModel.OutputDelivery.OutputFileName);
            Assert.Equal("delivery-folder", viewModel.OutputDelivery.BundleFolderName);
            double editLeft = edit.TranslatePoint(default, window)!.Value.X;
            double disclosureLeft = disclosure.TranslatePoint(default, window)!.Value.X;
            Assert.True(editLeft < disclosureLeft, "The pencil must precede the far-right disclosure.");
            Assert.Empty(disclosure.GetVisualDescendants().OfType<TextBlock>());
            Assert.InRange(Math.Abs(
                input.TranslatePoint(default, window)!.Value.Y + (input.Bounds.Height / 2) -
                (edit.TranslatePoint(default, window)!.Value.Y + (edit.Bounds.Height / 2))), 0, 1);
            foreach ((string valueName, string buttonName) in new[]
            {
                ("BundleFolderNameDisplay", "EditBundleDestinationButton"),
                ("ParentDirectoryDisplay", "ChooseParentButton"),
            })
            {
                Control valueControl = modal.FindControl<Control>(valueName)!;
                Control buttonControl = modal.FindControl<Control>(buttonName)!;
                Assert.InRange(Math.Abs(
                    valueControl.TranslatePoint(default, window)!.Value.Y + (valueControl.Bounds.Height / 2) -
                    (buttonControl.TranslatePoint(default, window)!.Value.Y + (buttonControl.Bounds.Height / 2))), 0, 1);
            }
            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(directory, chinese ? "bundle-primary-rename-zh.png" : "bundle-primary-rename.png"));
                _ = disclosure.Focus();
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using Avalonia.Media.Imaging.Bitmap? expandedFrame = window.GetLastRenderedFrame();
                Assert.NotNull(expandedFrame);
                expandedFrame.Save(Path.Combine(directory, chinese ? "bundle-expanded-zh.png" : "bundle-expanded.png"));
            }
            viewModel.OutputDelivery.CancelCommand.Execute(null);
            Assert.False(viewModel.OutputDelivery.IsOpen);
        }
        finally
        {
            viewModel.OutputDelivery.CancelCommand.Execute(null);
            window.Close();
        }
    }
}
