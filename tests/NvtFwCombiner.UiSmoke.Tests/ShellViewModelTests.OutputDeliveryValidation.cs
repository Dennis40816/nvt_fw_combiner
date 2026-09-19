using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Reopening an uncommitted valid draft does not promote it to the recovery baseline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputDeliveryNameRecoveryDoesNotAcceptUncommittedDraftOnReopen(bool cancel)
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel shell = await CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926"));
        await shell.Merge.RequestBuildOutputDeliveryAsync();
        OutputDeliveryConfirmationViewModel vm = shell.OutputDelivery;
        vm.SetParentDirectory(destination.Root);
        vm.SetBundleEnabled(true);
        vm.BeginBundleDestinationEdit();
        vm.SetBundleFolderName("accepted-folder");
        vm.CompleteBundleDestinationEdit();
        vm.BeginBundleDestinationEdit();
        vm.SetBundleFolderName("uncommitted-folder");
        if (cancel)
        {
            vm.CancelCommand.Execute(null);
            await shell.Merge.RequestBuildOutputDeliveryAsync();
        }
        else
        {
            vm.SetBundleEnabled(false);
            vm.SetBundleEnabled(true);
        }
        Assert.Equal("uncommitted-folder", vm.BundleFolderName);
        vm.BeginBundleDestinationEdit();
        vm.SetBundleFolderName("CON");
        vm.CompleteBundleDestinationEdit();
        Assert.Equal("accepted-folder", vm.BundleFolderName);
        Assert.True(vm.CanConfirm, vm.ValidationMessage);
    }

    /// <summary>One invalid draft cannot overwrite the independent field's accepted snapshot.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputDeliveryNameCompletionKeepsIndependentDrafts(bool invalidPrimary)
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel shell = await CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926"));
        await shell.Merge.RequestBuildOutputDeliveryAsync();
        OutputDeliveryConfirmationViewModel vm = shell.OutputDelivery;
        vm.SetParentDirectory(destination.Root);
        vm.SetBundleEnabled(true);
        vm.BeginBundleDestinationEdit();
        vm.SetBundleFolderName("accepted-folder");
        vm.CompleteBundleDestinationEdit();
        vm.BeginOutputFileNameEdit();
        vm.SetOutputFileName("accepted.bin");
        vm.CompleteOutputFileNameEdit();
        vm.BeginOutputFileNameEdit();
        vm.BeginBundleDestinationEdit();
        vm.SetOutputFileName(invalidPrimary ? "CON.bin" : "new.bin");
        vm.SetBundleFolderName(invalidPrimary ? "new-folder" : "CON");
        if (invalidPrimary) { vm.CompleteBundleDestinationEdit(); }
        else { vm.CompleteOutputFileNameEdit(); }
        Assert.False(vm.CanConfirm);
        Assert.Equal(invalidPrimary ? "new-folder" : "CON", vm.BundleFolderName);
        Assert.Equal(invalidPrimary ? "CON.bin" : "new.bin", vm.OutputFileName);
        if (invalidPrimary) { vm.CompleteOutputFileNameEdit(); }
        else { vm.CompleteBundleDestinationEdit(); }
        Assert.True(vm.CanConfirm, vm.ValidationMessage);
        Assert.Equal(invalidPrimary ? "new-folder" : "accepted-folder", vm.BundleFolderName);
        Assert.Equal(invalidPrimary ? "accepted.bin" : "new.bin", vm.OutputFileName);
        Assert.False(vm.OutputFileNameUsesAutomaticName);
        Assert.Equal(vm.Text.OutputDeliveryNameRestored, vm.ValidationMessage);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>Draft recovery survives reopening and never masks an unrelated parent-directory failure.</summary>
    [Fact]
    public async Task OutputDeliveryNameRecoveryPreservesSnapshotsAndDestinationErrors()
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel shell = await CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926"));
        await shell.Merge.RequestBuildOutputDeliveryAsync();
        OutputDeliveryConfirmationViewModel vm = shell.OutputDelivery;
        vm.SetParentDirectory(destination.Root);
        vm.SetBundleEnabled(true);
        vm.BeginOutputFileNameEdit();
        vm.SetOutputFileName("accepted.bin");
        vm.CompleteOutputFileNameEdit();
        vm.BeginOutputFileNameEdit();
        vm.SetOutputFileName("bad?.bin");
        vm.CancelCommand.Execute(null);
        await shell.Merge.RequestBuildOutputDeliveryAsync();
        vm.BeginOutputFileNameEdit();
        vm.CompleteOutputFileNameEdit();
        Assert.Equal("accepted.bin", vm.OutputFileName);
        vm.SetParentDirectory(destination.PathFor("missing"));
        vm.BeginBundleDestinationEdit();
        vm.SetBundleFolderName("valid-folder");
        vm.CompleteBundleDestinationEdit();
        Assert.Equal("valid-folder", vm.BundleFolderName);
        Assert.False(vm.CanConfirm);
        string parentError = vm.ValidationMessage;
        vm.BeginBundleDestinationEdit();
        vm.SetBundleFolderName("CON");
        vm.CompleteBundleDestinationEdit();
        Assert.Equal("valid-folder", vm.BundleFolderName);
        Assert.Equal(parentError, vm.ValidationMessage);
        vm.SetBundleEnabled(false);
        vm.SetBundleEnabled(true);
        Assert.False(vm.CanConfirm);
        vm.SetParentDirectory(destination.Root);
        Assert.True(vm.CanConfirm, vm.ValidationMessage);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>The shared completion button and Enter path restore the name without committing output.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task OutputDeliveryPrimaryNameRecoveryUsesButtonAndKeyboard(bool bundle, bool chineseDark)
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel shell = await Task.Run(() => CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926")));
        if (chineseDark) { shell.SelectedLanguage = "Traditional Chinese"; }
        await shell.Merge.RequestBuildOutputDeliveryAsync();
        OutputDeliveryConfirmationViewModel vm = shell.OutputDelivery;
        vm.SetParentDirectory(destination.Root);
        vm.SetBundleEnabled(bundle);
        var modal = new OutputDeliveryConfirmationModal { DataContext = vm, IsOpen = true };
        var window = new Window
        {
            Content = modal,
            Width = 980,
            Height = 620,
            RequestedThemeVariant = chineseDark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        foreach (string path in new[] { "Styles/MainWindowStyles.axaml", "Styles/MainWindowButtonStyles.axaml", "Styles/MainWindowVisualStyles.axaml" })
        {
            var uri = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/{path}");
            window.Styles.Add(new StyleInclude(uri) { Source = uri });
        }
        try
        {
            window.Show();
            Button edit = modal.FindControl<Button>("EditOutputFileNameButton")!;
            Button done = modal.FindControl<Button>("CompleteOutputFileNameEditButton")!;
            TextBox input = modal.FindControl<TextBox>("OutputFileNameInput")!;
            edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            input.Text = "accepted.bin";
            Dispatcher.UIThread.RunJobs();
            done.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(vm.IsOutputFileNameEditing);
            Assert.Equal("accepted.bin", vm.OutputFileName);
            Assert.Equal(vm.Text.OutputDeliveryDoneOutputNameLabel, AutomationProperties.GetName(done));
            edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            input.Text = new string('x', 252) + ".bin";
            Dispatcher.UIThread.RunJobs();
            Assert.False(vm.CanConfirm);
            Assert.True(done.IsEffectivelyVisible);
            Assert.InRange(Math.Abs(
                input.TranslatePoint(default, window)!.Value.Y + (input.Bounds.Height / 2) -
                (done.TranslatePoint(default, window)!.Value.Y + (done.Bounds.Height / 2))), 0, 1);
            _ = input.Focus();
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Dispatcher.UIThread.RunJobs();
            Assert.False(vm.IsOutputFileNameEditing);
            Assert.Equal("accepted.bin", vm.OutputFileName);
            Assert.Equal(vm.Text.OutputDeliveryNameRestored, vm.ValidationMessage);
            Assert.True(vm.CanConfirm, vm.ValidationMessage);
            Assert.True(edit.IsFocused);
            Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            string? renderDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(renderDirectory))
            {
                _ = Directory.CreateDirectory(renderDirectory);
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(renderDirectory, $"name-recovered-{bundle}-{chineseDark}.png"));
            }
        }
        finally { window.Close(); }
    }

    /// <summary>Typing remains lossless; explicit invalid folder completion restores only its accepted name.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("CON")]
    [InlineData("../escape")]
    [InlineData("long")]
    public async Task BundleInvalidNameCompletionRestoresLastAcceptedName(string invalid)
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel shell = await CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926"));
        await shell.Merge.RequestBuildOutputDeliveryAsync();
        OutputDeliveryConfirmationViewModel vm = shell.OutputDelivery;
        vm.SetParentDirectory(destination.Root);
        vm.SetBundleEnabled(true);
        vm.BeginBundleDestinationEdit();
        vm.SetBundleFolderName("accepted-folder");
        vm.CompleteBundleDestinationEdit();
        vm.BeginOutputFileNameEdit();
        vm.SetOutputFileName("independent.bin");
        vm.BeginBundleDestinationEdit();
        string draft = invalid == "long" ? new string('x', 240) : invalid;
        vm.SetBundleFolderName(draft);
        Assert.Equal(draft, vm.BundleFolderName);
        Assert.False(vm.CanConfirm);
        vm.CompleteBundleDestinationEdit();
        Assert.Equal("accepted-folder", vm.BundleFolderName);
        Assert.Equal("independent.bin", vm.OutputFileName);
        Assert.True(vm.CanConfirm, vm.ValidationMessage);
        Assert.True(vm.HasValidationMessage);
        Assert.False(vm.IsBundleDestinationEditing);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>Loose delivery must validate a name before opening its destination picker.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("CON.bin")]
    [InlineData("bad?.bin")]
    [InlineData("long")]
    public async Task LooseInvalidNameBlocksConfirmationWhileEditing(string invalid)
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel shell = await CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926"));
        await shell.Merge.RequestBuildOutputDeliveryAsync();
        OutputDeliveryConfirmationViewModel vm = shell.OutputDelivery;
        vm.BeginOutputFileNameEdit();
        string draft = invalid == "long" ? new string('x', 252) + ".bin" : invalid;
        vm.SetOutputFileName(draft);
        Assert.Equal(draft, vm.OutputFileName);
        Assert.False(vm.CanConfirm);
        Assert.True(vm.HasValidationMessage);
        int pickerCalls = 0;
        await OutputDeliveryConfirmationModal.ConfirmPreparedLooseWithPickersAsync(vm,
            () => { pickerCalls++; return Task.FromResult<string?>(destination.PathFor("output.bin")); },
            () => Task.FromResult<string?>(null));
        Assert.Equal(0, pickerCalls);
        await vm.ConfirmLooseAsync(destination.PathFor("output.bin"), null, false, false);
        Assert.True(vm.IsOpen);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>A rejected long destination remains visible beside the actions regardless of content scrolling.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, 980, 620)]
    [InlineData(false, true, 980, 620)]
    [InlineData(true, false, 980, 620)]
    [InlineData(true, true, 980, 620)]
    [InlineData(false, false, 1280, 900)]
    [InlineData(false, true, 1280, 900)]
    [InlineData(true, false, 1280, 900)]
    [InlineData(true, true, 1280, 900)]
    public async Task OutputDeliveryLongNameErrorStaysVisibleAndRecovers(
        bool chineseDark, bool expanded, int width, int height)
    {
        using StandardMergeGoldenManifest golden = StandardMergeGoldenManifest.Load();
        using TempWorkspace destination = TempWorkspace.Create();
        MainWindowViewModel shell = await Task.Run(() => CreateReadyStandardMergeAsync(golden, golden.CaseByIc("51926")));
        if (chineseDark)
        {
            shell.SelectedLanguage = "Traditional Chinese";
        }

        await shell.Merge.RequestBuildOutputDeliveryAsync();
        OutputDeliveryConfirmationViewModel vm = shell.OutputDelivery;
        vm.SetBundleEnabled(true);
        vm.SetParentDirectory(destination.Root);
        vm.SetSourcesExpanded(expanded);
        var modal = new OutputDeliveryConfirmationModal { DataContext = vm, IsOpen = true };
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = modal,
            RequestedThemeVariant = chineseDark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        foreach (string path in new[] { "Styles/MainWindowStyles.axaml", "Styles/MainWindowButtonStyles.axaml", "Styles/MainWindowVisualStyles.axaml" })
        {
            var uri = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/{path}");
            window.Styles.Add(new StyleInclude(uri) { Source = uri });
        }

        try
        {
            window.Show();
            vm.BeginBundleDestinationEdit();
            Dispatcher.UIThread.RunJobs();
            TextBox input = modal.FindControl<TextBox>("FolderNameInput")!;
            input.Text = new string('x', 300);
            Dispatcher.UIThread.RunJobs();
            Assert.False(vm.CanConfirm);
            Assert.NotEmpty(vm.ValidationMessage);
            Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
            TextBlock warning = Assert.Single(modal.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == vm.ValidationMessage);
            ScrollViewer viewport = modal.FindControl<ScrollViewer>("BuildSettingsViewport")!;
            Button confirm = modal.FindControl<Button>("ConfirmButton")!;
            foreach (double offset in new[] { 0d, viewport.Extent.Height })
            {
                viewport.Offset = new Vector(0, offset);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                // Drain the binding/layout render before capturing the compositor's next frame.
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                string? renderDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
                if (!string.IsNullOrWhiteSpace(renderDirectory))
                {
                    _ = Directory.CreateDirectory(renderDirectory);
                    using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                    Assert.NotNull(frame);
                    frame.Save(Path.Combine(renderDirectory, $"name-error-{chineseDark}-{expanded}-{width}-{(offset == 0 ? "top" : "bottom")}.png"));
                }

                Point origin = warning.TranslatePoint(default, window)!.Value;
                double viewportBottom = viewport.TranslatePoint(default, window)!.Value.Y + viewport.Bounds.Height;
                Assert.True(warning.IsEffectivelyVisible);
                Assert.True(warning.Bounds.Height > 0);
                Assert.InRange(origin.Y, viewportBottom, window.Bounds.Height - warning.Bounds.Height);
                Assert.InRange(origin.X, 0, window.Bounds.Width - warning.Bounds.Width);
                Assert.True(origin.Y + warning.Bounds.Height <= confirm.TranslatePoint(default, window)!.Value.Y);
                Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(warning));
                Assert.False(confirm.IsEnabled);
            }

            input.Text = "corrected-bundle";
            Dispatcher.UIThread.RunJobs();
            Assert.True(vm.CanConfirm, vm.ValidationMessage);
            Assert.False(warning.IsEffectivelyVisible);
            Assert.True(confirm.IsEnabled);
            Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
            await vm.ConfirmBundleAsync();
            Assert.True(shell.RunSession.LastRunResult.Succeeded, shell.RunSession.LastRunResult.Detail);
            Assert.True(File.Exists(Path.Combine(destination.Root, "corrected-bundle", vm.OutputFileName)));
        }
        finally
        {
            window.Close();
        }
    }
}
