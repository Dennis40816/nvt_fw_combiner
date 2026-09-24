using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Late file-session results cannot replace the latest Hex presentation.</summary>
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

    private sealed class ControlledFiles : IRawBinaryEditorFileSessionFactory, IRawBinaryEditorFileSession
    {
        private readonly Dictionary<string, TaskCompletionSource<RawBinaryEditorFileResult>> _pending = [];
        private RawBinaryEditorSession _editor = null!;
        public string? SourcePath { get; private set; }
        public string SuggestedOutputFileName => "edited.bin";

        public IRawBinaryEditorFileSession Create(RawBinaryEditorSession editor)
        {
            _editor = editor;
            return this;
        }

        public Task<RawBinaryEditorFileResult> LoadAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
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
