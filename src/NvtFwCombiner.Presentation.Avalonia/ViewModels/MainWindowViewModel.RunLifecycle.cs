using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private CancellationTokenSource? _activeRunCancellationSource;
    private bool? _activeRunIsBuild;

    /// <summary>True while one composition Preview or Build owns the external processing lifetime.</summary>
    public bool IsRunInProgress => _activeRunCancellationSource is not null;

    /// <summary>Gets the localized assistive label for the active Preview or Build operation.</summary>
    public string RunProgressAccessibleLabel => _activeRunIsBuild switch
    {
        true => Text.GetRunProgressAccessibleLabel(build: true),
        false => Text.GetRunProgressAccessibleLabel(build: false),
        null => string.Empty,
    };

    /// <summary>Cancels the active composition so external workers can terminate before the window closes.</summary>
    internal void CancelActiveRun()
    {
        _activeRunCancellationSource?.Cancel();
    }

    private CancellationTokenSource BeginRun(bool build)
    {
        if (_activeRunCancellationSource is not null)
        {
            throw new InvalidOperationException("Another Preview or Build operation is already running.");
        }

        var cancellationSource = new CancellationTokenSource();
        _activeRunCancellationSource = cancellationSource;
        _activeRunIsBuild = build;
        RefreshCommandState();
        return cancellationSource;
    }

    private void CompleteRun(CancellationTokenSource cancellationSource)
    {
        if (ReferenceEquals(_activeRunCancellationSource, cancellationSource))
        {
            _activeRunCancellationSource = null;
            _activeRunIsBuild = null;
            RefreshCommandState();
        }

        cancellationSource.Dispose();
    }

    private async Task RunCompositionAsync(
        bool build,
        Func<CancellationToken, ValueTask<WorkbenchRunResult>> run,
        Action<string, string> loadErrorReport)
    {
        CancellationTokenSource? cancellationSource = null;
        try
        {
            cancellationSource = BeginRun(build);
            WorkbenchRunResult result = await run(cancellationSource.Token);
            ApplyRunResult(result, build);
            RefreshCommandState();
        }
        catch (OperationCanceledException) when (cancellationSource is { IsCancellationRequested: true })
        {
            RefreshCommandState();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            RefreshCommandState();
            string action = build ? "Build" : "Preview";
            LastRunResult = new UiRunResultViewModel(
                $"{action} failed",
                exception.Message,
                "No output",
                succeeded: false);
            OnPropertyChanged(nameof(LastRunResult));
            loadErrorReport(action, exception.Message);
        }
        finally
        {
            if (cancellationSource is not null)
            {
                CompleteRun(cancellationSource);
            }
        }
    }
}
