using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Loads every built-in bundle migrated to canonical source-view coverage.</summary>
public sealed class CanonicalSourceProjectionBuiltInBundleTests
{
    /// <summary>Each source bundle must satisfy its exact manifest, schema, and projection contract.</summary>
    [Theory]
    [InlineData("nt51923-standard-merge", "803780d0835dab32b68bc92cf7c8e175aa338b6aaf0e0c7caaddd9712de4576f")]
    [InlineData("nt51927-standard-merge", "985a7d231a5a40f9c0cfe752dd43fea43dcfa05fb48128379fa020cef041fc04")]
    [InlineData("nt51928-standard-merge", "8145e2e6f9697607fc91748d21f802ef2a8613021899d52a80828325bc50bae5")]
    [InlineData("nt51929-standard-merge", "e043dad07ffd7670c96b07b7732a9889b33a4b00b143eba56ad02cac1bb59cb5")]
    [InlineData("nt51919-nt51929-nt51932-ab-merge", "ece8e9ee7a81b3f00ce04bd7c1aa053acde26835d75c3042bf1a86302d8de793")]
    [InlineData("nt51950-ab-merge", "283f2c2e8d1f5dbeb17d27d07094d37b3644286a0bb9ca07b6ff17dcb43bff21")]
    [InlineData("nt51950-nt51951-standard-merge", "17b45d347a18078e522b412d7c4c3000f01be4e81e091167cc5c78299988da49")]
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
