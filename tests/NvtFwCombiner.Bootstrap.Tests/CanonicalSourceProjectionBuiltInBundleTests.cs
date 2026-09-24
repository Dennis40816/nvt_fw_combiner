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
    [InlineData("nt51919-nt51929-nt51932-ab-merge", "892af5d0f1ff0094bb96a0e30ffad3b6c2cf18451a6705623c2ca97206422c6b")]
    [InlineData("nt51950-ab-merge", "18b43352606ca744f499e328d5778c3b9e08307a97fd122ac38fd8762d37c8d1")]
    [InlineData("nt51950-nt51951-standard-merge", "658e188b0724a9a1f5d3389f7bc685a75b1dacfd36e030d79c9d0f83d8135652")]
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
