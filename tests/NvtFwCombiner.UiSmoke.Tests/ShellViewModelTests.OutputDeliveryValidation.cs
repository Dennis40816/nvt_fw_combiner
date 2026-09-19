using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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
