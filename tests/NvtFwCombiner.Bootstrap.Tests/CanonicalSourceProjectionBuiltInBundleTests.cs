using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Loads every built-in bundle migrated to canonical source-view coverage.</summary>
public sealed class CanonicalSourceProjectionBuiltInBundleTests
{
    /// <summary>Each source bundle must satisfy its exact manifest, schema, and projection contract.</summary>
    [Theory]
    [InlineData("nt51923-dp-replace", "14d3a379d5fc29b37904897b044fd834d8f6e1399cee73f7b00147276ce7bc79")]
    [InlineData("nt51923-standard-merge", "9661f30be8b114cd679d08af8177d44bd372973943f2293228f85ff25ecf608c")]
    [InlineData("nt51927-dp-replace", "1b97f66f779ab9bc260e43b26abfcba0b1488dd18fe215a4d76ce2d8393e8ae6")]
    [InlineData("nt51927-standard-merge", "b1c9234e76ff6995ac362ee66a22eb3423024d116a858a93d2b733c0c380eafa")]
    [InlineData("nt51928-dp-replace", "d9845bce9c2b3d8a8aa101450d534ef00417f1c63862e69bc833ad57713ab9e5")]
    [InlineData("nt51928-standard-merge", "20ccd90376bee9a67832b3a808940017f3cab202ae5d9dfad7cb2dc4b9774c4e")]
    [InlineData("nt51929-dp-replace", "31c545eb367ff902eb2e95bc0b90643c337ab26b4e5831169bfc1a31f060f3cd")]
    [InlineData("nt51929-standard-merge", "d70ee9a8534d2c91a1f674e92b888678d13ea1660a6540365abd42346c480a72")]
    [InlineData("nt51919-nt51929-nt51932-ab-merge", "3b6dcc3d1c87ab31e43852d3638b9658a64e886eae95c725e67b2d07f1cb8a61")]
    [InlineData("nt51950-ab-merge", "775c42fba1fbbf1c4c8869656c83c86ce34d612dda3ceed92a93cb4e82f7cd67")]
    [InlineData("nt51950-nt51951-dp-replace", "efc155288c2c470c0cac15e51142ebd357eff6151259b9b8164560f2a105ec6d")]
    [InlineData("nt51950-nt51951-standard-merge", "d62b6b3f83a2350724de476d582d3a8de3483366134c39d94f144b77ae1402d7")]
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
