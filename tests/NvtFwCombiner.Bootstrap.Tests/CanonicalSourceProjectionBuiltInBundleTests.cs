using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Loads every built-in bundle migrated to canonical source-view coverage.</summary>
public sealed class CanonicalSourceProjectionBuiltInBundleTests
{
    /// <summary>Each source bundle must satisfy its exact manifest, schema, and projection contract.</summary>
    [Theory]
    [InlineData("nt51923-standard-merge", "26707141ccf58dc3da87e755f41b9851af44914c6a5b14e1dc81f7cd099dbbcd")]
    [InlineData("nt51927-standard-merge", "59b51e5357fd7567315c6cc85289e2d5d78783e6ed027c421409c4a7f8cc6584")]
    [InlineData("nt51928-standard-merge", "0a511dbcfac0cfb6dcfc1a6faf5fe5818b6d0f6b3e267d6531c518c2917e949e")]
    [InlineData("nt51929-standard-merge", "b57bb9346535041d93d659f4173d0fdad659fe506e2d2b93fedb46af8e304011")]
    [InlineData("nt51919-nt51929-nt51932-ab-merge", "f082c1b93f895aedd8b1614c860c7da4d1e93c5a2a91366b9b4348cc71b1ca39")]
    [InlineData("nt51950-ab-merge", "74c20ce3f1d53ca343995e1b757e0785cb842fe926e0799140d6d8251f137c49")]
    [InlineData("nt51950-nt51951-standard-merge", "ecffb254e15097f64f321e806bd9c6e8eeef478b5ae7f5d9ffd5e1eac8897e1f")]
    public void MigratedBundleLoadsFromItsManifestPinnedSources(
        string bundleDirectory,
        string bundleContentHash)
    {
        using var workspace = TempWorkspace.Create(
            $"nfc-canonical-source-projection-{bundleDirectory}");

        _ = BuiltInProfileMaterializationTestSupport.LoadSourceCandidateCatalog(
            workspace,
            bundleDirectory,
            bundleContentHash);
    }
}
