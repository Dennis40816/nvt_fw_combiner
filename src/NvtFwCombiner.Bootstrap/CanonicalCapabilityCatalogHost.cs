using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Serializes one injected catalog's reload and resolution publication.</summary>
internal sealed class CanonicalCapabilityCatalogHost
    : ICanonicalCapabilityCatalogReloader
{
    private readonly Lock _gate = new();
    private readonly CanonicalCapabilityCatalog _catalog;
    private CapabilityCatalogReloadResult? _latestReload;

    internal CanonicalCapabilityCatalogHost(
        ICanonicalCapabilityCatalogSource source)
    {
        _catalog = new CanonicalCapabilityCatalog(source);
    }

    internal CapabilityCatalogReloadResult? LatestReload
        => Volatile.Read(ref _latestReload);

    internal CapabilityCatalogReloadResult Reload(
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            CapabilityCatalogReloadResult result = _catalog.Reload(cancellationToken);
            Volatile.Write(ref _latestReload, result);
            return result;
        }
    }

    void ICanonicalCapabilityCatalogReloader.Reload(
        CancellationToken cancellationToken)
    {
        _ = Reload(cancellationToken);
    }

    internal void Warm(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _ = EnsureLoaded(cancellationToken);
        }
    }

    internal TResult Read<TResult>(
        Func<CanonicalCapabilityCatalog, TResult> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        lock (_gate)
        {
            _ = EnsureLoaded(CancellationToken.None);
            return reader(_catalog);
        }
    }

    private CapabilityCatalogReloadResult EnsureLoaded(
        CancellationToken cancellationToken)
    {
        CapabilityCatalogReloadResult? current = Volatile.Read(ref _latestReload);
        if (current is not null)
        {
            return current;
        }

        CapabilityCatalogReloadResult result = _catalog.Reload(cancellationToken);
        Volatile.Write(ref _latestReload, result);
        return result;
    }
}
