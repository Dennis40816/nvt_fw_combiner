using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests deterministic RFC 8785 hashing of bundle entry arrays.</summary>
public sealed class ProfileBundleEntryArrayHasherTests
{
    private const string SchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    /// <summary>Verifies the C# projection matches an independently generated canonical vector.</summary>
    [Fact]
    public void CalculateContentHashMatchesCanonicalVector()
    {
        ProfileBundleEntryDocument[] entries =
        [
            Entry(
                "schema-a",
                "schema",
                "schemas/composition-profile-v2.schema.json",
                new string('b', 64)),
            Entry(
                "profile-a",
                "composition-profile",
                "profiles/profile-a.json",
                new string('c', 64)),
        ];

        string hash = ProfileBundleEntryArrayHasher.CalculateContentHash(entries);

        Assert.Equal("38f498d85b7db93885a55a813502a330a3eb14b880ac0142e653e254777bb4f2", hash);
    }

    /// <summary>Verifies caller entry order does not affect the canonical hash.</summary>
    [Fact]
    public void CalculateContentHashSortsEntriesByContractFields()
    {
        ProfileBundleEntryDocument profile = Entry(
            "profile-a",
            "composition-profile",
            "profiles/profile-a.json",
            new string('c', 64));
        ProfileBundleEntryDocument schema = Entry(
            "schema-a",
            "schema",
            "schemas/composition-profile-v2.schema.json",
            new string('b', 64));

        string forward = ProfileBundleEntryArrayHasher.CalculateContentHash([profile, schema]);
        string reverse = ProfileBundleEntryArrayHasher.CalculateContentHash([schema, profile]);

        Assert.Equal(forward, reverse);
    }

    /// <summary>Verifies every projected field contributes to the hash.</summary>
    [Fact]
    public void CalculateContentHashIncludesEveryEntryField()
    {
        ProfileBundleEntryDocument baseline = Entry(
            "profile-a",
            "composition-profile",
            "profiles/profile-a.json",
            new string('c', 64));
        string baselineHash = ProfileBundleEntryArrayHasher.CalculateContentHash([baseline]);
        ProfileBundleEntryDocument[] variants =
        [
            baseline with { EntryId = "profile-b" },
            baseline with { Kind = "saved-composition-rule" },
            baseline with { Path = "profiles/profile-b.json" },
            baseline with
            {
                SchemaId = "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json",
            },
            baseline with { ContentHash = new string('d', 64) },
        ];

        Assert.All(
            variants,
            variant => Assert.NotEqual(
                baselineHash,
                ProfileBundleEntryArrayHasher.CalculateContentHash([variant])));
    }

    /// <summary>Verifies null collections, entries, and fields fail before hashing.</summary>
    [Fact]
    public void CalculateContentHashRejectsNullValues()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            ProfileBundleEntryArrayHasher.CalculateContentHash(null!));
        _ = Assert.Throws<ArgumentException>(() =>
            ProfileBundleEntryArrayHasher.CalculateContentHash([null!]));
        _ = Assert.Throws<ArgumentException>(() =>
            ProfileBundleEntryArrayHasher.CalculateContentHash(
                [Entry("profile-a", "composition-profile", "profiles/profile-a.json", null!)]));
    }

    private static ProfileBundleEntryDocument Entry(
        string entryId,
        string kind,
        string path,
        string contentHash)
    {
        return new ProfileBundleEntryDocument(entryId, kind, path, SchemaId, contentHash);
    }
}
