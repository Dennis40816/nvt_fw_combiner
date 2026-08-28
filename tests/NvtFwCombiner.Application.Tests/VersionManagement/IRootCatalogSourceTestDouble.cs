using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Explicit root-based compatibility behavior for tests not concerned with exact files.</summary>
internal interface IRootCatalogSourceTestDouble : IUpdateCatalogSource
{
    ValueTask<UpdateCatalogLoadResult> IUpdateCatalogSource.LoadCatalogAsync(
        string catalogPath,
        CancellationToken cancellationToken)
    {
        string sourceRoot = Path.GetDirectoryName(catalogPath) ??
            throw new ArgumentException("Test Catalog path has no parent.", nameof(catalogPath));
        return LoadAsync(sourceRoot, cancellationToken);
    }
}
