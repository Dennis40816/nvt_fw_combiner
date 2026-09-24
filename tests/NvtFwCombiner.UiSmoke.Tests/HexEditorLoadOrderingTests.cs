using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Late file-session results cannot replace the latest Hex presentation.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class HexEditorLoadOrderingTests
{
    /// <summary>Both obsolete success and obsolete failure leave the accepted view unchanged.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EarlierCompletionCannotChangeLatestView(bool earlierSucceeded)
    {
        var files = new ControlledFiles();
        var view = new HexEditorWorkspaceViewModel(ShellTextResources.For(ShellLanguage.English), files);
        Task first = view.LoadAsync("first.bin", TestContext.Current.CancellationToken);
        Task second = view.LoadAsync("second.bin", TestContext.Current.CancellationToken);
        files.Complete("second.bin", true);
        await second;
        string status = view.EditorStatus;
        string? source = view.SourcePath;
        files.Complete("first.bin", earlierSucceeded);
        await first;

        Assert.Equal(source, view.SourcePath);
        Assert.Equal("second.bin", view.SourceName);
        Assert.Equal(status, view.EditorStatus);
    }

    /// <summary>A late native picker result cannot replace a file loaded after Browse began.</summary>
    [AvaloniaFact]
    public async Task PendingBrowseDoesNotSupersedeNewerHexEditorLoad()
    {
        var files = new ControlledFiles(completeImmediately: true);
        var view = new HexEditorWorkspaceViewModel(ShellTextResources.For(ShellLanguage.English), files);
        var pickerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePicker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pickerReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var panel = new HexEditorPanel
        {
            DataContext = view,
            PickFirmwareFileAsync = async (_, _) =>
            {
                _ = pickerStarted.TrySetResult();
                try
                {
                    await releasePicker.Task;
                    return "stale-picker.bin";
                }
                finally
                {
                    _ = pickerReturned.TrySetResult();
                }
            },
        };
        var window = new Window { Content = panel };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Button browse = Assert.Single(
                panel.GetVisualDescendants().OfType<Button>(),
                button => button.Content?.ToString() == view.Text.BrowseLabel);
            browse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await pickerStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            await view.LoadAsync("newer-drop.bin", TestContext.Current.CancellationToken);
            Assert.Equal("newer-drop.bin", view.SourcePath);

            _ = releasePicker.TrySetResult();
            await pickerReturned.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.Equal("newer-drop.bin", view.SourcePath);
            Assert.Equal(["newer-drop.bin"], files.LoadedPaths);
        }
        finally
        {
            _ = releasePicker.TrySetResult();
            window.Close();
        }
    }

    /// <summary>A picker result is rejected if the panel context changes away and back.</summary>
    [AvaloniaFact]
    public async Task PendingBrowseIsRejectedAfterHexEditorContextChangesAwayAndBack()
    {
        var files = new ControlledFiles(completeImmediately: true);
        var view = new HexEditorWorkspaceViewModel(ShellTextResources.For(ShellLanguage.English), files);
        var otherView = new HexEditorWorkspaceViewModel(
            ShellTextResources.For(ShellLanguage.English),
            new ControlledFiles(completeImmediately: true));
        var pickerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePicker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pickerReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var panel = new HexEditorPanel
        {
            DataContext = view,
            PickFirmwareFileAsync = async (_, _) =>
            {
                _ = pickerStarted.TrySetResult();
                try
                {
                    await releasePicker.Task;
                    return "stale-picker.bin";
                }
                finally
                {
                    _ = pickerReturned.TrySetResult();
                }
            },
        };
        var window = new Window { Content = panel };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Button browse = Assert.Single(
                panel.GetVisualDescendants().OfType<Button>(),
                button => button.Content?.ToString() == view.Text.BrowseLabel);
            browse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await pickerStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            panel.DataContext = otherView;
            panel.DataContext = view;
            _ = releasePicker.TrySetResult();
            await pickerReturned.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.Null(view.SourcePath);
            Assert.Empty(files.LoadedPaths);
        }
        finally
        {
            _ = releasePicker.TrySetResult();
            window.Close();
        }
    }

    private sealed class ControlledFiles : IRawBinaryEditorFileSessionFactory, IRawBinaryEditorFileSession
    {
        private readonly Dictionary<string, TaskCompletionSource<RawBinaryEditorFileResult>> _pending = [];
        private RawBinaryEditorSession _editor = null!;
        private readonly bool _completeImmediately;

        internal ControlledFiles(bool completeImmediately = false)
        {
            _completeImmediately = completeImmediately;
        }

        internal List<string> LoadedPaths { get; } = [];

        public string? SourcePath { get; private set; }
        public string SuggestedOutputFileName => "edited.bin";

        public IRawBinaryEditorFileSession Create(RawBinaryEditorSession editor)
        {
            _editor = editor;
            return this;
        }

        public Task<RawBinaryEditorFileResult> LoadAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            LoadedPaths.Add(sourcePath);
            if (_completeImmediately)
            {
                _ = _editor.Load(sourcePath == "newer-drop.bin" ? [2, 3] : [1]);
                SourcePath = sourcePath;
                return Task.FromResult(RawBinaryEditorFileResult.Success(sourcePath, _editor.State));
            }

            var completion = new TaskCompletionSource<RawBinaryEditorFileResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(sourcePath, completion);
            return completion.Task;
        }

        internal void Complete(string path, bool success)
        {
            // The file owner already rejected stale mutation; only its delayed result is under test.
            if (path == "second.bin")
            {
                _ = _editor.Load([2, 3]);
                SourcePath = path;
            }

            _pending[path].SetResult(success
                ? RawBinaryEditorFileResult.Success(path, _editor.State)
                : RawBinaryEditorFileResult.Failure("Earlier read failed."));
        }

        public Task<RawBinaryEditorSearchResult> FindAsciiAsync(string text, long startOffset, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RawBinaryEditorFileResult> SaveAsAsync(string outputPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
