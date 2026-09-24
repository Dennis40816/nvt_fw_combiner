using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>A folder selection started by an earlier edit session cannot replace the reopened draft.</summary>
    [Fact]
    public async Task StaleUpdateSourceBrowseIsRejectedAfterCancelAndReopen()
    {
        MainWindowViewModel vm = await Task.Run(
            () => PresentationTestHost.CreateViewModel(ShellLanguage.English),
            TestContext.Current.CancellationToken);
        vm.Settings.Refresh(vm.Text);
        vm.Settings.ApplyVersionSnapshot(CreateApprovedSettingsReferenceSnapshot());
        string originalSource = vm.Settings.UpdateSourcePath;
        string stalePath = Path.Combine(Path.GetTempPath(), "stale-update-source");
        int browseRequests = 0;
        vm.Settings.UpdateSourceBrowseRequested += (_, _) => browseRequests++;

        vm.Settings.BeginEditUpdateSourceCommand.Execute(null);
        vm.Settings.BrowseUpdateSourceCommand.Execute(null);
        Assert.Equal(1, browseRequests);
        long pendingBrowse = Assert.IsType<long>(vm.Settings.BeginUpdateSourceBrowse());

        vm.Settings.CancelEditUpdateSourceCommand.Execute(null);
        vm.Settings.BeginEditUpdateSourceCommand.Execute(null);
        vm.Settings.SetUpdateSourceDraft(stalePath, pendingBrowse);

        Assert.True(vm.Settings.IsUpdateSourceEditing);
        Assert.Equal(originalSource, vm.Settings.UpdateSourceDraft);
    }

    /// <summary>A closed and reopened Settings modal cannot accept its earlier folder picker result.</summary>
    [AvaloniaFact]
    public async Task StaleUpdateSourceBrowseIsRejectedAfterSettingsModalCloseAndReopen()
    {
        MainWindowViewModel vm = await Task.Run(
            () => PresentationTestHost.CreateViewModel(ShellLanguage.English),
            TestContext.Current.CancellationToken);
        vm.Settings.Refresh(vm.Text);
        vm.Settings.ApplyVersionSnapshot(CreateApprovedSettingsReferenceSnapshot());
        string originalSource = vm.Settings.UpdateSourcePath;
        string stalePath = Path.Combine(Path.GetTempPath(), "closed-update-source");
        var modal = new SettingsModal { DataContext = vm, IsOpen = true };
        var window = new Window { Width = 980, Height = 640, DataContext = vm, Content = modal };
        try
        {
            window.Show();
            vm.Settings.BeginEditUpdateSourceCommand.Execute(null);
            long pendingBrowse = Assert.IsType<long>(vm.Settings.BeginUpdateSourceBrowse());

            modal.IsOpen = false;
            modal.IsOpen = true;
            vm.Settings.SetUpdateSourceDraft(stalePath, pendingBrowse);

            Assert.Equal(originalSource, vm.Settings.UpdateSourceDraft);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Source editing and row disclosure remain independent, keyboard accessible and bounded.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SettingsVersionSourceAndNotesRespectTransactionsAndViewport(bool dark, bool compact)
    {
        MainWindowViewModel vm = await Task.Run(
            () => PresentationTestHost.CreateViewModel(compact ? ShellLanguage.ChineseTraditional : ShellLanguage.English),
            TestContext.Current.CancellationToken);
        vm.Settings.Refresh(vm.Text);
        VersionManagementSnapshot snapshot = CreateApprovedSettingsReferenceSnapshot();
        vm.Settings.ApplyVersionSnapshot(snapshot);
        vm.Settings.SelectSectionCommand.Execute(SettingsSection.Version);
        var modal = new SettingsModal { DataContext = vm, IsOpen = true };
        var window = new Window
        {
            Width = compact ? 980 : 1584,
            Height = compact ? 640 : 997,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            DataContext = vm,
            Content = modal,
        };
        AddSettingsAcceptanceResources(window);
        try
        {
            window.Show();
            RenderSettingsVersion(window);
            Button Action(System.Windows.Input.ICommand command)
            {
                return Assert.Single(modal.GetVisualDescendants().OfType<Button>(), b => b.Command == command);
            }
            Button browse = Action(vm.Settings.BrowseUpdateSourceCommand);
            Button check = Action(vm.Settings.CheckNowCommand);
            Button edit = Action(vm.Settings.BeginEditUpdateSourceCommand);
            TextBox path = Assert.Single(modal.GetVisualDescendants().OfType<TextBox>());
            Control viewport = Assert.Single(modal.GetVisualDescendants().OfType<Control>(), c => c.Name == "SettingsContentViewport");
            Border editor = Assert.Single(modal.GetVisualDescendants().OfType<Border>(), c => c.Classes.Contains("versionSourceEditor"));
            Control table = Assert.Single(modal.GetVisualDescendants().OfType<Control>(), c => c.Name == "VersionTableSurface");
            Border sourceStatus = Assert.Single(modal.GetVisualDescendants().OfType<Border>(), c => c.Classes.Contains("versionSourceStatusSegment"));
            Assert.False(browse.IsVisible);
            Assert.True(path.IsReadOnly);
            Assert.Equal(new Thickness(0), sourceStatus.BorderThickness);
            Assert.Null(sourceStatus.Background);
            Assert.InRange(sourceStatus.Bounds.Width, 39.5, 40.5);
            Assert.InRange(Math.Abs(editor.Bounds.Width - table.Bounds.Width), 0, 0.5);
            Assert.Contains("secondary", check.Classes);
            Assert.True(Assert.IsType<Point>(check.TranslatePoint(default, table)).Y < 0);
            Border banner = Assert.Single(modal.GetVisualDescendants().OfType<Border>(), c => c.Name == "VerifiedUpdateBanner");
            Avalonia.Controls.Shapes.Path infoIcon = Assert.Single(banner.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
            Assert.Equal(24, infoIcon.Bounds.Width);
            Assert.NotNull(infoIcon.Data);

            string originalSource = vm.Settings.UpdateSourcePath;
            int browseRequests = 0;
            vm.Settings.UpdateSourceBrowseRequested += (_, _) => browseRequests++;
            PressSettingsControl(window, edit);
            RenderSettingsVersion(window);
            Assert.True(browse.IsVisible);
            Assert.False(path.IsReadOnly);
            Assert.False(edit.IsVisible);
            PressSettingsControl(window, browse);
            Assert.Equal(1, browseRequests);
            string longPath = @"\\server\share\" + string.Join("\\", Enumerable.Repeat("long-update-source-folder", 14));
            vm.Settings.SetUpdateSourceDraft(longPath, Assert.IsType<long>(vm.Settings.BeginUpdateSourceBrowse()));
            RenderSettingsVersion(window);
            Assert.Equal(longPath, path.Text);
            Assert.Equal(longPath, ToolTip.GetTip(path));
            Assert.Equal(originalSource, vm.Settings.UpdateSourcePath);
            AssertSettingsHorizontalFit(editor, viewport);
            foreach (Button button in new[] { browse, Action(vm.Settings.CancelEditUpdateSourceCommand), Action(vm.Settings.ConfirmUpdateSourceCommand), check })
            {
                AssertSettingsHorizontalFit(button, viewport);
                Assert.True(button.Bounds.Width >= 32);
            }
            await SaveSettingsVersionFrame(window, $"editing-{(compact ? "980x640-zh" : "1584x997-en")}-{(dark ? "dark" : "light")}");
            PressSettingsControl(window, Action(vm.Settings.CancelEditUpdateSourceCommand));
            RenderSettingsVersion(window);
            Assert.False(vm.Settings.IsUpdateSourceEditing);
            Assert.Equal(originalSource, path.Text);
            Assert.False(browse.IsVisible);

            ToggleButton[] toggles = SettingsNotesToggles(modal);
            Assert.Equal(3, toggles.Length);
            Assert.All(toggles, toggle => Assert.False(toggle.IsChecked));
            Assert.All(toggles, toggle => Assert.Contains(
                Assert.IsType<SettingsVersionRowViewModel>(toggle.DataContext).VersionLabel,
                AutomationProperties.GetName(toggle), StringComparison.Ordinal));
            Assert.All(toggles, toggle => Assert.Contains(vm.Settings.ViewReleaseNotesLabel,
                AutomationProperties.GetName(toggle), StringComparison.Ordinal));
            PressSettingsControl(window, toggles[0]);
            RenderSettingsVersion(window);
            Assert.True(toggles[0].IsChecked);
            Assert.False(toggles[1].IsChecked);
            Assert.False(vm.Settings.IsVersionConfirmationOpen);
            TextBlock notes = Assert.Single(modal.GetVisualDescendants().OfType<TextBlock>(),
                c => c.Classes.Contains("versionReleaseNotes") && c.IsEffectivelyVisible);
            Assert.Equal(vm.Settings.VersionRows[0].ReleaseNotes, notes.Text);
            ContentPresenter presenter = Assert.Single(toggles[0].GetVisualDescendants().OfType<ContentPresenter>(), c => c.Name == "PART_ContentPresenter");
            Assert.Equal(Colors.Transparent, Assert.IsType<ISolidColorBrush>(presenter.Background, exactMatch: false).Color);
            await SaveSettingsVersionFrame(window, $"notes-{(compact ? "980x640-zh" : "1584x997-en")}-{(dark ? "dark" : "light")}");
            PressSettingsControl(window, toggles[0], enter: true);
            RenderSettingsVersion(window);
            Assert.False(toggles[0].IsChecked);
            Assert.False(notes.IsEffectivelyVisible);
            Assert.False(vm.Settings.IsVersionConfirmationOpen);

            PressSettingsControl(window, toggles[1]);
            vm.Settings.ApplyVersionSnapshot(snapshot);
            RenderSettingsVersion(window);
            Assert.All(SettingsNotesToggles(modal), toggle => Assert.False(toggle.IsChecked));
            string longNotes = string.Join("\n", Enumerable.Repeat("<script>Not executable</script> · CRC/header details remain plain catalog text.", 20));
            vm.Settings.VersionRows[0] = vm.Settings.VersionRows[0] with { ReleaseNotes = longNotes };
            vm.Settings.VersionRows[1] = vm.Settings.VersionRows[1] with { ReleaseNotes = " \r\n\t" };
            vm.Settings.VersionRows[2] = vm.Settings.VersionRows[2] with { ReleaseNotes = string.Empty };
            RenderSettingsVersion(window);
            toggles = SettingsNotesToggles(modal);
            Assert.True(toggles[0].IsVisible);
            Assert.False(toggles[1].IsVisible);
            Assert.False(toggles[2].IsVisible);
            PressSettingsControl(window, toggles[0]);
            RenderSettingsVersion(window);
            notes = Assert.Single(modal.GetVisualDescendants().OfType<TextBlock>(),
                c => c.Classes.Contains("versionReleaseNotes") && c.IsEffectivelyVisible);
            Assert.Equal(longNotes, notes.Text);
            Assert.Equal(TextWrapping.Wrap, notes.TextWrapping);
            AssertSettingsHorizontalFit(notes, viewport);
            Assert.False(vm.Settings.IsVersionConfirmationOpen);
            Button[] actions = [.. modal.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("versionTableAction"))];
            Point firstAction = Assert.IsType<Point>(actions[0].TranslatePoint(default, viewport));
            Assert.All(actions, action =>
            {
                AssertSettingsHorizontalFit(action, viewport);
                Assert.InRange(Math.Abs(Assert.IsType<Point>(action.TranslatePoint(default, viewport)).X - firstAction.X), 0, 0.5);
            });
            Assert.Equal(vm.Settings.RequestVersionPrimaryActionCommand, actions[0].Command);
            PressSettingsControl(window, actions[0]);
            RenderSettingsVersion(window);
            Assert.True(vm.Settings.IsVersionConfirmationOpen);
            vm.Settings.CancelVersionConfirmationCommand.Execute(null);
            Assert.False(vm.Settings.IsVersionConfirmationOpen);
            vm.Settings.ApplyVersionSnapshot(snapshot with { SourceStatus = VersionSourceStatus.PermissionDenied });
            RenderSettingsVersion(window);
            Assert.Equal(vm.Settings.SourceStatusText, AutomationProperties.GetName(sourceStatus));
            Assert.True(vm.Settings.IsSourceDisconnected);
            vm.Settings.SetSourceChecking(true);
            RenderSettingsVersion(window);
            _ = Assert.Single(sourceStatus.GetVisualDescendants().OfType<VersionCheckingIndicator>(), c => c.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static ToggleButton[] SettingsNotesToggles(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<ToggleButton>().Where(c => c.Name == "VersionNotesToggle")];
    }

    private static void RenderSettingsVersion(Window window)
    {
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void PressSettingsControl(Window window, Control control, bool enter = false)
    {
        Assert.True(control.Focus());
        window.KeyPress(enter ? Key.Enter : Key.Space, RawInputModifiers.None,
            enter ? PhysicalKey.Enter : PhysicalKey.Space, enter ? "\r" : " ");
        window.KeyRelease(enter ? Key.Enter : Key.Space, RawInputModifiers.None,
            enter ? PhysicalKey.Enter : PhysicalKey.Space, enter ? "\r" : " ");
    }

    private static void AssertSettingsHorizontalFit(Control control, Control viewport)
    {
        Point origin = Assert.IsType<Point>(control.TranslatePoint(default, viewport));
        Assert.InRange(origin.X, -0.5, viewport.Bounds.Width);
        Assert.InRange(origin.X + control.Bounds.Width, 0, viewport.Bounds.Width + 0.5);
    }

    private static async Task SaveSettingsVersionFrame(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            await using FileStream stream = File.Create(Path.Combine(directory, $"settings-version-{name}.png"));
            frame.Save(stream);
        }
    }
}
