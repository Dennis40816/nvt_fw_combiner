using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia;

internal enum ShellPreloadAttemptState { Running, Succeeded, Failed, Cancelled }

internal readonly record struct ShellPreloadAttemptIdentity(long SessionGeneration, string StageId, int AttemptNumber);

internal sealed record ShellPreloadAttemptSnapshot(ShellPreloadAttemptIdentity Identity, ShellPreloadAttemptState State, double? Progress, string Diagnostic = "");

internal sealed record ShellPreloadStageSnapshot(string Id, int Index, int Count, bool IsRequired, ShellPreloadAttemptSnapshot? CurrentAttempt, ShellPreloadAttemptSnapshot? PreviousAttempt);

internal sealed class ShellPreloadSession : IDisposable
{
    internal const string CatalogStageId = "canonical-catalog";
    internal const int OptionalWorkerBudget = 2;
    internal static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly ObservableCollection<ShellPreloadStageSnapshot> _stages = [new(CatalogStageId, 1, 1, true, null, null)];
    private readonly Action<ShellPreloadStageSnapshot> _report;
    private readonly CancellationTokenSource _cancellation = new();
    private Task _active = Task.CompletedTask;
    private static long s_generation;
    private int _attempt;

    internal ShellPreloadSession(Action<ShellPreloadStageSnapshot> report)
    {
        _report = report ?? throw new ArgumentNullException(nameof(report));
        Stages = new(_stages);
    }

    internal ReadOnlyObservableCollection<ShellPreloadStageSnapshot> Stages { get; }
    internal ShellPreloadStageSnapshot CatalogStage => _stages[0];
    internal long Generation { get; } = Interlocked.Increment(ref s_generation);
    internal bool CanRetryCatalog =>
        !_cancellation.IsCancellationRequested && _active.IsCompleted &&
        CatalogStage.CurrentAttempt?.State == ShellPreloadAttemptState.Failed;

    internal Task<CapabilityCatalogReloadResult> RunCatalogAsync(
        ICanonicalCapabilityCatalogLoader loader, Func<CancellationToken, ValueTask> apply,
        bool retry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(apply);
        if (retry ? !CanRetryCatalog : CatalogStage.CurrentAttempt is not null)
        {
            throw new InvalidOperationException("The catalog attempt cannot start in its current state.");
        }

        var identity = new ShellPreloadAttemptIdentity(Generation, CatalogStageId, checked(++_attempt));
        Publish(new(identity, ShellPreloadAttemptState.Running, 0), CatalogStage.CurrentAttempt);
        _active = RunAsync(loader, apply, identity, cancellationToken);
        return (Task<CapabilityCatalogReloadResult>)_active;
    }

    internal async Task CancelAndDrainAsync()
    {
        _cancellation.Cancel();
        _ = await Task.WhenAny(_active, Task.Delay(DrainTimeout));
    }

    public void Dispose()
    {
        _cancellation.Dispose();
    }

    private async Task<CapabilityCatalogReloadResult> RunAsync(
        ICanonicalCapabilityCatalogLoader loader, Func<CancellationToken, ValueTask> apply,
        ShellPreloadAttemptIdentity identity, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, cancellationToken);
        CancellationToken token = linked.Token;
        CapabilityCatalogReloadResult? terminal = null;
        double? progress = null;
        try
        {
            await foreach (CanonicalCapabilityCatalogLoadUpdate update in loader.LoadAsync(token).WithCancellation(token))
            {
                token.ThrowIfCancellationRequested();
                if (terminal is not null)
                {
                    throw new InvalidOperationException("Catalog update followed its terminal result.");
                }
                if (update.Result is { } terminalResult)
                {
                    if ((terminalResult.Succeeded && update.Progress != 1) ||
                        (!terminalResult.Succeeded && update.Progress is not null))
                    {
                        throw new InvalidOperationException("Catalog terminal progress does not match its result.");
                    }
                    terminal = terminalResult;
                    continue;
                }

                double next = update.Progress ?? throw new InvalidOperationException("Catalog loading reported an empty update.");
                if (!double.IsFinite(next) || next is < 0 or >= 1 || next < progress)
                {
                    throw new InvalidOperationException("Catalog loading reported invalid progress.");
                }
                progress = next;
                Set(identity, ShellPreloadAttemptState.Running, progress);
            }

            CapabilityCatalogReloadResult result = terminal ??
                throw new InvalidOperationException("Catalog loading completed without a terminal result.");
            token.ThrowIfCancellationRequested();
            if (!result.Succeeded)
            {
                string diagnostic = string.Join(Environment.NewLine,
                    result.Issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
                Set(identity, ShellPreloadAttemptState.Failed, progress, diagnostic);
                return result;
            }

            Set(identity, ShellPreloadAttemptState.Running, 1);
            await apply(token);
            token.ThrowIfCancellationRequested();
            Set(identity, ShellPreloadAttemptState.Succeeded, 1);
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Set(identity, ShellPreloadAttemptState.Cancelled, progress);
            throw;
        }
        catch (Exception exception)
        {
            Set(identity, ShellPreloadAttemptState.Failed, progress, exception.Message);
            throw;
        }
    }

    private void Set(
        ShellPreloadAttemptIdentity identity, ShellPreloadAttemptState state,
        double? progress, string diagnostic = "")
    {
        ShellPreloadAttemptSnapshot current = CatalogStage.CurrentAttempt ??
            throw new InvalidOperationException("The catalog stage has no attempt.");
        if (current.Identity != identity || current.State != ShellPreloadAttemptState.Running)
        {
            throw new InvalidOperationException("The catalog attempt is stale or terminal.");
        }
        Publish(current with { State = state, Progress = progress, Diagnostic = diagnostic },
            CatalogStage.PreviousAttempt);
    }

    private void Publish(ShellPreloadAttemptSnapshot current, ShellPreloadAttemptSnapshot? previous)
    {
        _stages[0] = CatalogStage with { CurrentAttempt = current, PreviousAttempt = previous };
        _report(_stages[0]);
    }
}
