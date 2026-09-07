using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Recomputes changed AB bundle projections through the existing hash owner.</summary>
public sealed class AbBundleSourceHashTests
{
    /// <summary>Source bytes and the reviewed bundle manifest must have identical identities.</summary>
    [Theory]
    [InlineData("nt51919-nt51929-nt51932-ab-merge")]
    [InlineData("nt51950-ab-merge")]
    public void AbBundlePinsMatchSourceBytes(string directory)
    {
        string root = RepositoryPaths.FromRepositoryRoot("profiles", "built-in", directory);
        ProfileBundleDocument manifest = JsonSerializer.Deserialize<ProfileBundleDocument>(
            File.ReadAllText(Path.Combine(root, "profile-bundle.json")),
            JsonSerializerOptions.Web)!;
        ProfileBundleEntryDocument[] actual = [.. manifest.Entries.Select(entry => entry.Kind != "composition-profile" ? entry : entry with
        {
            ContentHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, entry.Path)))).ToLowerInvariant(),
        })];
        foreach (ProfileBundleEntryDocument entry in actual.Where(entry => manifest.Entries.Single(old => old.EntryId == entry.EntryId).ContentHash != entry.ContentHash))
        {
            TestContext.Current.TestOutputHelper!.WriteLine($"ENTRY {entry.Path} {entry.ContentHash}");
        }
        string hash = ProfileBundleEntryArrayHasher.CalculateContentHash(actual);
        TestContext.Current.TestOutputHelper!.WriteLine($"BUNDLE {directory} {hash}");
        Assert.Equal(manifest.Entries, actual);
        Assert.Equal(manifest.ContentHash, hash);
    }
}
