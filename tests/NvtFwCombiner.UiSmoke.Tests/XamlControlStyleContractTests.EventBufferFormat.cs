using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Preserves the approved four-column Config editor geometry in both shipped themes.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, 1672, 941)]
    [InlineData(true, false, 1672, 941)]
    [InlineData(false, true, 980, 640)]
    public async Task EventBufferFormatReferenceGeometryRendersInBothThemes(
        bool useDarkTheme, bool traditionalChinese, double width, double height)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        MainWindowViewModel viewModel = await Task.Run(async () =>
        {
            CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), null,
                configurationPath: workspace.PathFor("config.json"));
            IEventBufferFormatConfigurationSession session = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
            Assert.True((await session.SaveAsync([.. session.CreateDefaultsDraft().Select(entry => entry! with { AliasName = "Desay" })],
                TestContext.Current.CancellationToken)).Succeeded);
            return CreateEventBufferReferenceViewModel(session,
                traditionalChinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        }, TestContext.Current.CancellationToken);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        Assert.Equal([0x97, 0xA6], Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues);
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
        var modal = new SettingsModal { DataContext = viewModel, IsOpen = true };
        var window = new Window
        {
            Width = width,
            Height = height,
            RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
            DataContext = viewModel,
            Content = modal,
        };
        AddSettingsAcceptanceResources(window);

        try
        {
            window.Show();
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            // Retain the complete actual render even when a geometry assertion below is red.
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? visualOutput = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(visualOutput))
            {
                _ = Directory.CreateDirectory(visualOutput);
                frame.Save(Path.Combine(visualOutput, $"event-buffer-format-{width}x{height}-{(useDarkTheme ? "dark" : "light")}-{(traditionalChinese ? "zh-Hant" : "en")}.png"));
            }

            Assert.Equal(new Size(width, height), window.Bounds.Size);
            Grid editor = window.GetVisualDescendants()
                .OfType<Grid>()
                .Single(grid => grid.Name == "EventBufferFormatPageRoot");
            Border rail = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "SettingsNavigationRail");
            Assert.InRange(rail.Bounds.Width, 317.5, 318.5);
            Rect editorBounds = EventBufferBounds(editor, window);
            Assert.InRange(editorBounds.Right, width - 80, width);
            TextBlock pageTitle = Assert.Single(editor.GetVisualDescendants().OfType<TextBlock>(),
                control => control.Classes.Contains("pageTitle"));
            TextBlock pageSubtitle = Assert.Single(editor.GetVisualDescendants().OfType<TextBlock>(),
                control => control.Classes.Contains("pageSubtitle"));
            Assert.Equal(24, pageTitle.FontSize);
            Assert.Equal(13, pageSubtitle.FontSize);
            AssertEventBufferFits(EventBufferBounds(pageTitle, window), editorBounds);
            AssertEventBufferFits(EventBufferBounds(pageSubtitle, window), editorBounds);
            ComboBox identity = Assert.Single(editor.GetVisualDescendants().OfType<ComboBox>(),
                control => control.DataContext is EventBufferFormatDraftRowViewModel);
            TextBox alias = Assert.Single(editor.GetVisualDescendants().OfType<TextBox>(), control => control.Text == "Desay");
            Assert.Equal("Desay", Assert.IsType<EventBufferFormatIdentity>(identity.SelectedItem).DisplayName);
            Assert.Equal(13, identity.FontSize);
            Assert.Equal(13, alias.FontSize);
            Rect identityBounds = EventBufferBounds(identity, window);
            Rect aliasBounds = EventBufferBounds(alias, window);
            Assert.True(aliasBounds.Left > identityBounds.Right);
            Assert.InRange(Math.Abs(identityBounds.Center.Y - aliasBounds.Center.Y), 0, 8);
            Assert.InRange(aliasBounds.Height, 40, 48);
            Assert.InRange(identityBounds.Width, aliasBounds.Width * .75, aliasBounds.Width * 1.1);
            foreach (string byteLabel in new[] { "0x97", "0xA6" })
            {
                TextBlock chipText = Assert.Single(editor.GetVisualDescendants().OfType<TextBlock>(),
                    control => control.Text == byteLabel);
                Border chip = chipText.GetVisualAncestors().OfType<Border>().First();
                Assert.Equal(13, chipText.FontSize);
                AssertEventBufferFits(EventBufferBounds(chipText, window), EventBufferBounds(chip, window));
                if (width >= 1600)
                {
                    Assert.InRange(Math.Abs(EventBufferBounds(chipText, window).Center.Y - aliasBounds.Center.Y), 0, 4);
                }
            }
            Assert.All(editor.GetVisualDescendants().OfType<TextBox>().Where(control => control.IsEffectivelyVisible),
                control => AssertEventBufferFits(EventBufferBounds(control, window), editorBounds));
            TextBlock outputEffect = Assert.Single(editor.GetVisualDescendants().OfType<TextBlock>(),
                control => control.DataContext is EventBufferFormatDraftRowViewModel && control.Text == Assert.Single(viewModel.Settings.EventBufferFormatRows).OutputEffect);
            Assert.Equal(13, outputEffect.FontSize);
            Grid outputEffectRow = outputEffect.GetVisualAncestors().OfType<Grid>().First();
            AssertEventBufferFits(EventBufferBounds(outputEffect, window), EventBufferBounds(outputEffectRow, window));
            Button save = Assert.Single(editor.GetVisualDescendants().OfType<Button>(),
                control => ReferenceEquals(control.Command, viewModel.Settings.SaveEventBufferFormatCommand));
            Button discard = Assert.Single(editor.GetVisualDescendants().OfType<Button>(),
                control => ReferenceEquals(control.Command, viewModel.Settings.DiscardEventBufferFormatChangesCommand));
            Button restore = Assert.Single(editor.GetVisualDescendants().OfType<Button>(),
                control => ReferenceEquals(control.Command, viewModel.Settings.RestoreEventBufferFormatDefaultsCommand));
            Assert.Equal(14, Assert.Single(editor.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == viewModel.Text.EventBufferFormatRecognitionValuesHint).FontSize);
            Rect saveBounds = EventBufferBounds(save, window);
            Assert.InRange(saveBounds.Bottom, height - 125, height - 5);
            Assert.InRange(Math.Abs(EventBufferBounds(discard, window).Center.Y - saveBounds.Center.Y), 0, 2);
            Assert.InRange(Math.Abs(EventBufferBounds(restore, window).Center.Y - saveBounds.Center.Y), 0, 2);
            Assert.All<Button>([save, discard, restore], control =>
            {
                // Preserve the shared semantic-action style, including its existing 13-point text.
                Assert.Equal(13, control.FontSize);
                AssertEventBufferFits(EventBufferBounds(control, window), editorBounds);
                Assert.False(string.IsNullOrWhiteSpace(control.GetValue(AutomationProperties.NameProperty)));
            });
            Assert.False(save.IsEnabled);
            Assert.False(discard.IsEnabled);
            Assert.True(restore.IsEnabled);

            // Exercise production bindings and keyboard commands, not just the draft methods.
            EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
            Button Action(System.Windows.Input.ICommand command)
            {
                return Assert.Single(editor.GetVisualDescendants().OfType<Button>(), control => control.Command == command);
            }
            PressSettingsControl(window, Action(row.BeginAddRecognitionValueCommand));
            RenderSettingsVersion(window);
            Assert.True(row.IsAddingRecognitionValue);
            TextBox byteInput = Assert.Single(editor.GetVisualDescendants().OfType<TextBox>(),
                control => control.IsEffectivelyVisible && control.Text == "0x");
            byteInput.Text = "0x84";
            RenderSettingsVersion(window);
            Assert.Equal("0x84", row.RecognitionValueDraft);
            PressSettingsControl(window, Action(row.AddRecognitionValueCommand));
            RenderSettingsVersion(window);
            Assert.Equal([0x97, 0xA6, 0x84], row.RecognitionValues);
            Button remove = Assert.Single(editor.GetVisualDescendants().OfType<Button>(),
                control => control.Command == row.RemoveRecognitionValueCommand && control.CommandParameter is 0x97);
            PressSettingsControl(window, remove);
            RenderSettingsVersion(window);
            Assert.Equal([0xA6, 0x84], row.RecognitionValues);
            Assert.True(save.IsEnabled);
            Assert.Equal(viewModel.Text.EventBufferFormatUnsavedChangesLabel, viewModel.Settings.EventBufferFormatDraftStatus);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            RenderSettingsVersion(window);
            Assert.True(viewModel.IsSettingsModalOpen);
            Assert.True(viewModel.Settings.IsEventBufferFormatCloseConfirmationOpen);
            PressSettingsControl(window, Action(viewModel.Settings.CancelEventBufferFormatCloseCommand));
            Assert.False(viewModel.Settings.IsEventBufferFormatCloseConfirmationOpen);
            Assert.Equal([0xA6, 0x84], row.RecognitionValues);
            viewModel.CloseSettingsCommand.Execute(null);
            RenderSettingsVersion(window);
            PressSettingsControl(window, Action(viewModel.Settings.ConfirmEventBufferFormatCloseCommand));
            Assert.False(viewModel.IsSettingsModalOpen);
            Assert.Equal([0x97, 0xA6], Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues);
        }
        finally
        {
            window.Close();
        }
    }

    private static Rect EventBufferBounds(Control control, Window window)
    {
        return new(Assert.IsType<Point>(control.TranslatePoint(new Point(), window)), control.Bounds.Size);
    }

    private static void AssertEventBufferFits(Rect control, Rect container)
    {
        Assert.True(control.Width > 0 && control.Height > 0);
        Assert.InRange(control.Left, container.Left - 1, container.Right);
        Assert.InRange(control.Right, container.Left, container.Right + 1);
        Assert.InRange(control.Bottom, container.Top, container.Bottom + 1);
    }

    /// <summary>First asynchronous Config load enables the already-mounted draft actions.</summary>
    [AvaloniaFact]
    public async Task EventBufferFormatMountedBeforeFirstLoadEnablesSaveAndRestore()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), null,
            configurationPath: workspace.PathFor("config.json"));
        IEventBufferFormatConfigurationSession session = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        var acquisition = new TaskCompletionSource<IEventBufferFormatConfigurationSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        MainWindowViewModel viewModel = await Task.Run(
            () => CreateEventBufferReferenceViewModel(session, ShellLanguage.English, acquisition.Task), TestContext.Current.CancellationToken);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        var window = new Window
        {
            Width = 1672,
            Height = 941,
            Content = new SettingsModal { DataContext = viewModel, IsOpen = true },
        };
        AddSettingsAcceptanceResources(window);
        try
        {
            window.Show();
            RenderSettingsVersion(window);
            Button save = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                control => control.Command == viewModel.Settings.SaveEventBufferFormatCommand);
            Button restore = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                control => control.Command == viewModel.Settings.RestoreEventBufferFormatDefaultsCommand);
            Assert.True(viewModel.Settings.IsEventBufferFormatLoading);
            Assert.False(save.IsEffectivelyEnabled);
            Assert.False(restore.IsEffectivelyEnabled);
            acquisition.SetResult(session);
            await viewModel.Settings.EventBufferFormatLoadTask;
            RenderSettingsVersion(window);
            Assert.True(save.IsEffectivelyEnabled);
            Assert.True(restore.IsEffectivelyEnabled);
            PressSettingsControl(window, restore);
            PressSettingsControl(window, save);
            await Assert.IsType<Task>(viewModel.Settings.SaveEventBufferFormatCommand.ExecutionTask, exactMatch: false);
            Assert.Equal(EventBufferFormatConfigurationStatus.Ready, session.Current.Status);
        }
        finally
        {
            _ = acquisition.TrySetResult(session);
            window.Close();
        }
    }

    private static MainWindowViewModel CreateEventBufferReferenceViewModel(IEventBufferFormatConfigurationSession session, ShellLanguage language,
        Task<IEventBufferFormatConfigurationSession>? acquisition = null)
    {
        PresentationHostServices original = PresentationTestHost.CreateServices(ApplicationVersionProvider.InformationalVersion);
        var services = new PresentationHostServices(original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter, original.RawBinaryEditorFileSessions,
            original.CanonicalCatalogLoader, original.ExternalEnvironmentLoader, original.LocalFiles,
            versionManagement: null, managedApplicationStartup: null, stableLauncherHandoff: null,
            eventBufferFormatConfigurationSessionFactory: _ => acquisition ?? Task.FromResult(session));
        return PresentationTestHost.PublishCanonicalCatalog(services, ShellViewModelFactory.Create(services, language));
    }

}
