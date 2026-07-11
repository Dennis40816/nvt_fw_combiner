using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests post-schema bundle manifest semantics before trust verification.</summary>
public sealed class ProfileBundleManifestNormalizerTests
{
    private const string SchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    /// <summary>Verifies every closed entry kind maps into one immutable sorted manifest.</summary>
    [Fact]
    public void NormalizeMapsEveryEntryKindAndSnapshotsEntries()
    {
        var entries = new List<ProfileBundleEntryDocument>
        {
            Entry("schema", "schema", "schemas/composition-profile-v2.schema.json"),
            Entry("family", "firmware-family", "families/family.json"),
            Entry("profile", "composition-profile", "profiles/profile.json"),
            Entry("evidence", "evidence-manifest", "evidence/evidence.json"),
            Entry("saved-rule", "saved-composition-rule", "saved-rules/rule.json"),
        };
        ProfileBundleDocument document = Document(entries);

        ProfileBundleManifest manifest = ProfileBundleManifestNormalizer.Normalize(document);
        entries.Clear();

        Assert.Equal("bundle", manifest.BundleId);
        Assert.Equal("1.0.0", manifest.BundleVersion);
        Assert.Equal("release-manifest", manifest.TrustAnchorBindingId);
        Assert.Equal(5, manifest.Entries.Count);
        Assert.Equal(
            Enum.GetValues<ProfileBundleEntryKind>().Order(),
            manifest.Entries.Select(static entry => entry.Kind).Order());
        Assert.Equal(
            ["evidence", "family", "profile", "saved-rule", "schema"],
            manifest.Entries.Select(static entry => entry.EntryId));
    }

    /// <summary>Verifies closed manifest and entry tokens fail at exact source paths.</summary>
    [Fact]
    public void NormalizeRejectsUnknownTokensWithPaths()
    {
        ProfileBundleManifestNormalizationException schemaVersion = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document() with { SchemaVersion = "2.0" }));
        ProfileBundleManifestNormalizationException algorithm = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document() with { HashAlgorithm = "sha256" }));
        ProfileBundleEntryDocument entry = Assert.Single(Document().Entries);
        ProfileBundleManifestNormalizationException kind = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document([entry with { Kind = "future" }])));
        ProfileBundleManifestNormalizationException prefix = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document([entry with { Path = "profiles/schema.json" }])));

        Assert.Equal("schemaVersion", schemaVersion.Path);
        Assert.Equal("hashAlgorithm", algorithm.Path);
        Assert.Equal("entries[0].kind", kind.Path);
        Assert.Equal("entries[0].path", prefix.Path);
    }

    /// <summary>Verifies duplicate identity, path, case, and schema ids fail closed.</summary>
    [Fact]
    public void NormalizeRejectsAmbiguousEntriesWithPaths()
    {
        ProfileBundleEntryDocument schema = Entry(
            "schema",
            "schema",
            "schemas/composition-profile-v2.schema.json");
        ProfileBundleManifestNormalizationException id = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document(
                [schema, Entry("schema", "composition-profile", "profiles/profile.json")])));
        ProfileBundleManifestNormalizationException path = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document(
                [schema, Entry("other", "schema", schema.Path)])));
        ProfileBundleManifestNormalizationException caseCollision = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document(
                [schema, Entry("other", "schema", "schemas/Composition-Profile-V2.schema.json")])));
        ProfileBundleManifestNormalizationException schemaId = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document(
                [schema, Entry("other", "schema", "schemas/other.schema.json")])));

        Assert.Equal("entries[1].entryId", id.Path);
        Assert.Equal("entries[1].path", path.Path);
        Assert.Equal("entries[1].path", caseCollision.Path);
        Assert.Equal("entries[1].schemaId", schemaId.Path);
    }

    /// <summary>Verifies every content entry references a schema listed by this manifest.</summary>
    [Fact]
    public void NormalizeRejectsUnlistedSchemaReference()
    {
        ProfileBundleEntryDocument schema = Entry(
            "schema",
            "schema",
            "schemas/composition-profile-v2.schema.json");
        ProfileBundleEntryDocument profile = Entry(
            "profile",
            "composition-profile",
            "profiles/profile.json") with
        {
            SchemaId = "https://example.invalid/nfc/schemas/other.schema.json",
        };

        ProfileBundleManifestNormalizationException exception = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document([schema, profile])));

        Assert.Equal("entries[1].schemaId", exception.Path);
    }

    /// <summary>Verifies null and empty entry arrays fail before a manifest can be trusted.</summary>
    [Fact]
    public void NormalizeRejectsMissingAndEmptyEntries()
    {
        ProfileBundleManifestNormalizationException missing = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document() with { Entries = null! }));
        ProfileBundleManifestNormalizationException empty = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document([])));
        ProfileBundleDocument valid = Document();
        ProfileBundleManifestNormalizationException item = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(valid with { Entries = [null!] }));

        Assert.Equal("entries", missing.Path);
        Assert.Equal("$", empty.Path);
        Assert.Equal("entries[0]", item.Path);
    }

    /// <summary>Verifies the declared entry-array hash must match canonical manifest entries.</summary>
    [Fact]
    public void NormalizeRejectsMismatchedDeclaredHash()
    {
        ProfileBundleManifestNormalizationException exception = Assert.Throws<ProfileBundleManifestNormalizationException>(() =>
            ProfileBundleManifestNormalizer.Normalize(Document() with { ContentHash = new string('0', 64) }));

        Assert.Equal("contentHash", exception.Path);
    }

    private static ProfileBundleDocument Document(IReadOnlyList<ProfileBundleEntryDocument>? entries = null)
    {
        IReadOnlyList<ProfileBundleEntryDocument> values = entries ??
        [
            Entry("schema", "schema", "schemas/composition-profile-v2.schema.json"),
        ];
        return new ProfileBundleDocument(
            "1.0",
            "bundle",
            "1.0.0",
            "sha256-rfc8785-entry-array-v1",
            ProfileBundleEntryArrayHasher.CalculateContentHash(values),
            "release-manifest",
            values);
    }

    private static ProfileBundleEntryDocument Entry(string entryId, string kind, string path)
    {
        return new ProfileBundleEntryDocument(entryId, kind, path, SchemaId, new string('a', 64));
    }
}
