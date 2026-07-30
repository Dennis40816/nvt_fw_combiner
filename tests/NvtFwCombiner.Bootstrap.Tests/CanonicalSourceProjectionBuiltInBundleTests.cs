using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Loads every built-in bundle migrated to canonical source-view coverage.</summary>
public sealed class CanonicalSourceProjectionBuiltInBundleTests
{
    /// <summary>Each source bundle must satisfy its exact manifest, schema, and projection contract.</summary>
    [Theory]
    [InlineData("nt51923-dp-replace", "fd5ee9dda6de6b0ba2142adf0ddae9736282407fb96e53895e4cbfd505746df6")]
    [InlineData("nt51923-standard-merge", "8f95387d0bd00a6b07b651151c17170e0e36e4a9f7f23c9085ed97cabc84b4a0")]
    [InlineData("nt51927-dp-replace", "e69c8a23a3b55920ae0b27dc3b03412b464dcc8636c47aefa8022387723e1102")]
    [InlineData("nt51927-standard-merge", "74cf544dd7e7c6a834b8c4e31359a1a19fa1b7892d1b5fd2c5d96476a4fd7b8e")]
    [InlineData("nt51928-dp-replace", "9b1e8e49c48561d24877ca08c21888b56b2c93947c71752019ddbede18ebc5f7")]
    [InlineData("nt51928-standard-merge", "ebd187cf19770529649ce5dbbb21d3799ea667dccfa5322b3ef83a8e912272d5")]
    [InlineData("nt51929-dp-replace", "31c545eb367ff902eb2e95bc0b90643c337ab26b4e5831169bfc1a31f060f3cd")]
    [InlineData("nt51929-standard-merge", "c67e8ee68cd06f4e1a169abab7c900dc457bbd03f29da770fb7feefb848be380")]
    [InlineData("nt51919-nt51929-nt51932-ab-merge", "2c54c025d2afd3c8c15de6587894fb166a2a8cb7879f90fa241cba8dddeb5544")]
    [InlineData("nt51950-ab-merge", "069719655976439153a0d2d2f06f1289f3bcc76437463f89aa81ee19827b312f")]
    [InlineData("nt51950-nt51951-standard-merge", "e9eb1d6889552940be5363fe3eb5593cef88c89b38e1ef23b16cebfc99d8613e")]
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
