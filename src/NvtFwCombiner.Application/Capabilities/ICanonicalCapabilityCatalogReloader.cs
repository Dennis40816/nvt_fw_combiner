namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Explicitly reloads the host-owned canonical capability publication.</summary>
public interface ICanonicalCapabilityCatalogReloader
{
    /// <summary>Attempts one atomic reload; the catalog itself owns last-known-good retention.</summary>
    void Reload(CancellationToken cancellationToken);
}

/// <summary>One Application-owned catalog loading update.</summary>
public sealed record CanonicalCapabilityCatalogLoadUpdate(
    double? Progress,
    CapabilityCatalogReloadResult? Result);

/// <summary>Loads one typed stream with finite nondecreasing progress and exactly one final result.</summary>
public interface ICanonicalCapabilityCatalogLoader
{
    /// <summary>Success ends at progress 1; failure ends without progress; cancellation and faults complete exceptionally.</summary>
    IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
        CancellationToken cancellationToken);
}
