using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class RunAndHexEditorTests
{
    /// <summary>Ignores cancellation from a stale search generation when the caller token remains active.</summary>
    [Fact]
    public async Task HexEditorIgnoresStaleSearchCancellationWithoutCallerTokenCancellation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-stale-search");
        string sourcePath = workspace.Write("source.bin", "AAAA"u8.ToArray());
        var searchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSearch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var editor = new HexEditorWorkspaceViewModel(
            ShellTextResources.For(ShellLanguage.English),
            PresentationTestHost.CreateRawBinaryEditorFileSessionFactory(),
            async (_, _, cancellationToken) =>
            {
                Assert.False(cancellationToken.IsCancellationRequested);
                _ = searchStarted.TrySetResult();
                await releaseSearch.Task.WaitAsync(cancellationToken);
                throw new OperationCanceledException("The internal search generation became stale.");
            });
        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.AsciiSearchText = "A";

        Task search = editor.FindAsciiCommand.ExecuteAsync(null);
        await searchStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        _ = releaseSearch.TrySetResult();

        await search;
        Assert.False(TestContext.Current.CancellationToken.IsCancellationRequested);
        Assert.False(editor.HasAsciiSearchResults);
        Assert.Equal(string.Empty, editor.AsciiSearchResultLabel);
    }
}
