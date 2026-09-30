namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Serializes tests that reload the catalog of the shared <see cref="GoldenTestHost"/>.</summary>
/// <remarks>A reload republishes the catalog, so a Golden run accepted in parallel is refused as stale.</remarks>
[CollectionDefinition(nameof(GoldenHostCatalogReloadSerialGroup), DisableParallelization = true)]
public sealed class GoldenHostCatalogReloadSerialGroup;
