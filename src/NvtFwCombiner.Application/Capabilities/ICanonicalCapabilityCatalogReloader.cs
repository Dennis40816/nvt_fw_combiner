namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Explicitly reloads the host-owned canonical capability publication.</summary>
public interface ICanonicalCapabilityCatalogReloader
{
    /// <summary>Attempts one atomic reload; the catalog itself owns last-known-good retention.</summary>
    void Reload(CancellationToken cancellationToken);
}
