using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;
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

    /// <summary>A real file commit remains the source of truth across delayed UI publication.</summary>
    [AvaloniaTheory]
    [InlineData("cancel-return")]
    [InlineData("cancel-throw")]
    [InlineData("newer-failure")]
    [InlineData("newer-success")]
    public async Task CommittedLoadAndDisplayedSourceRemainCoherent(string interleave)
    {
        ArgumentNullException.ThrowIfNull(interleave);
        using var workspace = TempWorkspace.Create("hex-accepted-load");
        string original = workspace.PathFor("original.bin");
        string first = workspace.PathFor("first.bin");
        string newer = workspace.PathFor("newer.bin");
        string saved = workspace.PathFor("saved.bin");
        await File.WriteAllBytesAsync(original, [1], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(first, [2, 3], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(newer, [4, 5, 6], TestContext.Current.CancellationToken);
        var files = new CommitDelayedFiles();
        var view = new HexEditorWorkspaceViewModel(ShellTextResources.For(ShellLanguage.English), files);
        await view.LoadAsync(original, TestContext.Current.CancellationToken);
        files.DelayedPath = first;
        files.ThrowAfterCommit = interleave == "cancel-throw";
        using var cancellation = new CancellationTokenSource();
        Task pending = view.LoadAsync(first, cancellation.Token);
        try
        {
            await files.Committed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (interleave.StartsWith("cancel", StringComparison.Ordinal))
            {
                cancellation.Cancel();
            }
            else
            {
                await view.LoadAsync(interleave == "newer-success" ? newer : workspace.PathFor("missing.bin"),
                    TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            _ = files.Release.TrySetResult();
        }
        if (files.ThrowAfterCommit)
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        else
        {
            await pending;
        }
        string accepted = interleave == "newer-success" ? newer : first;
        Assert.Equal(accepted, view.SourcePath);
        Assert.Equal(files.SourcePath, view.SourcePath);
        byte[] expected = await File.ReadAllBytesAsync(accepted, TestContext.Current.CancellationToken);
        expected[0] = 0;
        view.SetByteToZeroCommand.Execute(0L);
        Assert.True(view.CanSave);
        await view.SaveAsAsync(saved, TestContext.Current.CancellationToken);
        Assert.Equal(expected, await File.ReadAllBytesAsync(saved, TestContext.Current.CancellationToken));
    }

    private sealed class CommitDelayedFiles : IRawBinaryEditorFileSessionFactory, IRawBinaryEditorFileSession
    {
        private IRawBinaryEditorFileSession _inner = null!;
        internal string? DelayedPath { get; set; }
        internal bool ThrowAfterCommit { get; set; }
        internal TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? SourcePath => _inner.SourcePath;
        public RawBinaryEditorFileResult? AcceptedLoad => _inner.AcceptedLoad;
        public string SuggestedOutputFileName => _inner.SuggestedOutputFileName;
        public IRawBinaryEditorFileSession Create(RawBinaryEditorSession editor)
        {
            _inner = new RawBinaryEditorFileSessionFactory().Create(editor);
            return this;
        }
        public async Task<RawBinaryEditorFileResult> LoadAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            RawBinaryEditorFileResult result = await _inner.LoadAsync(sourcePath, cancellationToken);
            if (sourcePath == DelayedPath && result.Succeeded)
            {
                _ = Committed.TrySetResult();
                await Release.Task;
                if (ThrowAfterCommit) { cancellationToken.ThrowIfCancellationRequested(); }
            }
            return result;
        }
        public Task<RawBinaryEditorSearchResult> FindAsciiAsync(string text, long startOffset, CancellationToken cancellationToken = default)
        {
            return _inner.FindAsciiAsync(text, startOffset, cancellationToken);
        }
        public Task<RawBinaryEditorFileResult> SaveAsAsync(string outputPath, CancellationToken cancellationToken = default)
        {
            return _inner.SaveAsAsync(outputPath, cancellationToken);
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
        public RawBinaryEditorFileResult? AcceptedLoad { get; private set; }
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
                AcceptedLoad = RawBinaryEditorFileResult.Success(sourcePath, _editor.State);
                return Task.FromResult(AcceptedLoad);
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
                AcceptedLoad = RawBinaryEditorFileResult.Success(path, _editor.State);
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
