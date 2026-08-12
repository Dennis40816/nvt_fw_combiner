using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia;

internal enum CanonicalCatalogStartupPhase
{
    Dispatched,
    MaterializingRoutes,
    ApplyingState,
    Ready,
}

internal readonly record struct CanonicalCatalogStartupProgress(
    double Value,
    CanonicalCatalogStartupPhase Phase);

/// <summary>Coordinates one Presentation startup load without owning catalog semantics.</summary>
internal static class CanonicalCatalogStartupCoordinator
{
    private const double DispatchedProgress = 0.1;
    private const double RouteProgressRange = 0.7;
    private const double ApplyingProgress = 0.9;

    internal static async Task<CapabilityCatalogReloadResult> LoadAndApplyAsync(
        ICanonicalCapabilityCatalogLoader loader,
        Func<CanonicalCatalogStartupProgress, CancellationToken, ValueTask> reportProgress,
        Func<CancellationToken, ValueTask> applyValidatedState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(reportProgress);
        ArgumentNullException.ThrowIfNull(applyValidatedState);

        await reportProgress(
            new CanonicalCatalogStartupProgress(
                DispatchedProgress,
                CanonicalCatalogStartupPhase.Dispatched),
            cancellationToken);
        CapabilityCatalogReloadResult? terminal = null;
        double? lastSourceProgress = null;
        var lastVisible = new CanonicalCatalogStartupProgress(
            DispatchedProgress,
            CanonicalCatalogStartupPhase.Dispatched);

        await foreach (CanonicalCapabilityCatalogLoadUpdate update in
                       loader.LoadAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (terminal is not null)
            {
                throw new InvalidOperationException(
                    "Canonical catalog loading reported an update after its terminal result.");
            }

            if (update.Result is { } terminalUpdate)
            {
                if (terminalUpdate.Succeeded && update.Progress != 1)
                {
                    throw new InvalidOperationException(
                        "A successful canonical catalog terminal result requires completion progress.");
                }
                if (!terminalUpdate.Succeeded && update.Progress is not null)
                {
                    throw new InvalidOperationException(
                        "A failed canonical catalog terminal result cannot report completion progress.");
                }

                terminal = terminalUpdate;
                continue;
            }

            double sourceProgress = update.Progress ??
                throw new InvalidOperationException(
                    "Canonical catalog loading reported an empty update.");
            ValidateSourceProgress(sourceProgress, lastSourceProgress);
            if (sourceProgress == 1)
            {
                throw new InvalidOperationException(
                    "Canonical catalog completion progress requires a successful terminal result.");
            }

            lastSourceProgress = sourceProgress;
            var visible = new CanonicalCatalogStartupProgress(
                MapRouteProgress(sourceProgress),
                CanonicalCatalogStartupPhase.MaterializingRoutes);
            if (visible != lastVisible)
            {
                await reportProgress(visible, cancellationToken);
                lastVisible = visible;
            }
        }

        CapabilityCatalogReloadResult result = terminal ??
            throw new InvalidOperationException(
                "Canonical catalog loading completed without a terminal result.");
        if (!result.Succeeded)
        {
            return result;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await reportProgress(
            new CanonicalCatalogStartupProgress(
                0.8,
                CanonicalCatalogStartupPhase.MaterializingRoutes),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await reportProgress(
            new CanonicalCatalogStartupProgress(
                ApplyingProgress,
                CanonicalCatalogStartupPhase.ApplyingState),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await applyValidatedState(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await reportProgress(
            new CanonicalCatalogStartupProgress(1, CanonicalCatalogStartupPhase.Ready),
            cancellationToken);
        return result;
    }

    private static double MapRouteProgress(double sourceProgress)
    {
        return Math.Floor((DispatchedProgress + (RouteProgressRange * sourceProgress)) * 10) / 10;
    }

    private static void ValidateSourceProgress(
        double progress,
        double? previous)
    {
        if (!double.IsFinite(progress) ||
            progress is < 0 or > 1 ||
            (previous is { } prior && progress < prior))
        {
            throw new InvalidOperationException(
                "Canonical catalog loading reported invalid or decreasing progress.");
        }
    }
}
