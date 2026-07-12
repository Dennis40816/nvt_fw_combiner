using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests typed canonical document projection from trusted immutable bundle snapshots.</summary>
public sealed class TrustedProfileBundleDocumentProjectionTests
{
    /// <summary>Verifies the projection preserves every trusted identity and binds canonical document roots.</summary>
    [Fact]
    public void ProjectionPreservesTrustedIdentityAndDeserializesCanonicalDocuments()
    {
        using TempWorkspace workspace = PrepareBundle(out ProfileBundleTrustAnchor trustAnchor, out BundleFacts facts);
        TrustedProfileBundle bundle = Load(workspace, trustAnchor);

        TrustedProfileBundleDocumentProjection projection = bundle.CreateDocumentProjection();

        Assert.Equal(facts.ManifestSha256, projection.ManifestSha256);
        Assert.Equal("bundle", projection.BundleId);
        Assert.Equal("1.0.0", projection.BundleVersion);
        Assert.Equal(facts.BundleContentHash, projection.BundleContentHash);
        Assert.Equal("release-manifest", projection.TrustAnchorBindingId);
        TrustedFirmwareFamilyDocumentEntry family = Assert.Single(projection.Families);
        Assert.Equal("family-entry", family.Identity.EntryId);
        Assert.Equal("families/family.json", family.Identity.Path);
        Assert.Equal(facts.FamilyContentHash, family.Identity.ContentHash);
        Assert.Equal("family", family.Document.GetProperty("familyId").GetString());
        Assert.Equal(
            "NT00001",
            family.Document.GetProperty("members")[0].GetProperty("memberId").GetString());
        TrustedCompositionProfileDocumentEntry profile = Assert.Single(projection.Profiles);
        Assert.Equal("profile-entry", profile.Identity.EntryId);
        Assert.Equal("profiles/profile.json", profile.Identity.Path);
        Assert.Equal(facts.ProfileContentHash, profile.Identity.ContentHash);
        Assert.Equal("profile", profile.Document.GetProperty("profileId").GetString());
        Assert.Equal(
            "copy-range",
            profile.Document.GetProperty("operations")[0].GetProperty("kind").GetString());
    }

    /// <summary>Verifies projection reads the immutable captured bytes rather than the source file.</summary>
    [Fact]
    public void ProjectionUsesCapturedBytesAfterTheSourceFileChanges()
    {
        using TempWorkspace workspace = PrepareBundle(out ProfileBundleTrustAnchor trustAnchor, out _);
        TrustedProfileBundle bundle = Load(workspace, trustAnchor);
        _ = workspace.Write("families/family.json", Encoding.UTF8.GetBytes(FamilyJson("changed-family")));

        TrustedProfileBundleDocumentProjection projection = bundle.CreateDocumentProjection();

        Assert.Equal(
            "family",
            Assert.Single(projection.Families).Document.GetProperty("familyId").GetString());
    }

    /// <summary>Verifies a canonical kind cannot be paired with an unrelated schema identity.</summary>
    [Fact]
    public void ProjectionRejectsCanonicalKindWithTheWrongSchemaIdentity()
    {
        using TempWorkspace workspace = PrepareBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            out _,
            familySchemaId: "https://example.invalid/nfc/schemas/other-family.schema.json");
        TrustedProfileBundle bundle = Load(workspace, trustAnchor);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(bundle.CreateDocumentProjection);

        Assert.Contains("families/family.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains(TrustedProfileBundleDocumentProjection.FirmwareFamilySchemaId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a composition-profile entry cannot use a noncanonical schema identity.</summary>
    [Fact]
    public void ProjectionRejectsCompositionProfileWithTheWrongSchemaIdentity()
    {
        using TempWorkspace workspace = PrepareBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            out _,
            profileSchemaId: "https://example.invalid/nfc/schemas/other-profile.schema.json");
        TrustedProfileBundle bundle = Load(workspace, trustAnchor);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(bundle.CreateDocumentProjection);

        Assert.Contains("profiles/profile.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            TrustedProfileBundleDocumentProjection.CompositionProfileSchemaId,
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>Verifies callers cannot mutate the canonical document entry collections.</summary>
    [Fact]
    public void ProjectionExposesReadOnlyCanonicalEntryCollections()
    {
        using TempWorkspace workspace = PrepareBundle(out ProfileBundleTrustAnchor trustAnchor, out _);
        TrustedProfileBundleDocumentProjection projection = Load(workspace, trustAnchor).CreateDocumentProjection();
        var families = (IList<TrustedFirmwareFamilyDocumentEntry>)projection.Families;
        var profiles = (IList<TrustedCompositionProfileDocumentEntry>)projection.Profiles;

        Assert.True(families.IsReadOnly);
        Assert.True(profiles.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => families.RemoveAt(0));
        _ = Assert.Throws<NotSupportedException>(() => profiles.RemoveAt(0));
    }

    /// <summary>Verifies noncanonical bundle entries are validated by the loader but omitted from this projection.</summary>
    [Fact]
    public void ProjectionSkipsEvidenceManifestsAndSavedRules()
    {
        using TempWorkspace workspace = PrepareBundle(
            out ProfileBundleTrustAnchor trustAnchor,
            out _,
            includeNonCanonicalEntries: true);
        TrustedProfileBundle bundle = Load(workspace, trustAnchor);

        TrustedProfileBundleDocumentProjection projection = bundle.CreateDocumentProjection();

        Assert.Equal(8, bundle.Entries.Count);
        _ = Assert.Single(projection.Families);
        _ = Assert.Single(projection.Profiles);
    }

    private static TrustedProfileBundle Load(TempWorkspace workspace, ProfileBundleTrustAnchor trustAnchor)
    {
        return ProfileBundleLoader.Load(workspace.Root, "profile-bundle.json", trustAnchor, Limits());
    }

    private static ProfileBundleLoadLimits Limits()
    {
        return new ProfileBundleLoadLimits(
            16384,
            32,
            new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8));
    }

    private static TempWorkspace PrepareBundle(
        out ProfileBundleTrustAnchor trustAnchor,
        out BundleFacts facts,
        string? familySchemaId = null,
        string? profileSchemaId = null,
        bool includeNonCanonicalEntries = false)
    {
        familySchemaId ??= TrustedProfileBundleDocumentProjection.FirmwareFamilySchemaId;
        profileSchemaId ??= TrustedProfileBundleDocumentProjection.CompositionProfileSchemaId;
        byte[] familySchema = CanonicalSchema(
            "firmware-family-v1.schema.json",
            TrustedProfileBundleDocumentProjection.FirmwareFamilySchemaId,
            familySchemaId);
        byte[] profileSchema = CanonicalSchema(
            "composition-profile-v2.schema.json",
            TrustedProfileBundleDocumentProjection.CompositionProfileSchemaId,
            profileSchemaId);
        byte[] family = Encoding.UTF8.GetBytes(FamilyJson("family"));
        byte[] profile = Encoding.UTF8.GetBytes(ProfileJson(Hash(family)));
        var entries = new List<ProfileBundleEntryDocument>
        {
            new("family-schema", "schema", "schemas/family.schema.json", familySchemaId, Hash(familySchema)),
            new("profile-schema", "schema", "schemas/profile.schema.json", profileSchemaId, Hash(profileSchema)),
            new("family-entry", "firmware-family", "families/family.json", familySchemaId, Hash(family)),
            new("profile-entry", "composition-profile", "profiles/profile.json", profileSchemaId, Hash(profile)),
        };
        byte[]? evidenceSchema = null;
        byte[]? savedRuleSchema = null;
        byte[]? evidence = null;
        byte[]? savedRule = null;
        if (includeNonCanonicalEntries)
        {
            const string evidenceSchemaId = "https://example.invalid/nfc/schemas/evidence-manifest-v1.schema.json";
            const string savedRuleSchemaId = "https://example.invalid/nfc/schemas/saved-composition-rule-v1.schema.json";
            evidenceSchema = Encoding.UTF8.GetBytes(GenericSchema(evidenceSchemaId));
            savedRuleSchema = Encoding.UTF8.GetBytes(GenericSchema(savedRuleSchemaId));
            evidence = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
            savedRule = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":2}");
            entries.AddRange(
            [
                new("evidence-schema", "schema", "schemas/evidence.schema.json", evidenceSchemaId, Hash(evidenceSchema)),
                new("saved-rule-schema", "schema", "schemas/saved-rule.schema.json", savedRuleSchemaId, Hash(savedRuleSchema)),
                new("evidence-entry", "evidence-manifest", "evidence/evidence.json", evidenceSchemaId, Hash(evidence)),
                new("saved-rule-entry", "saved-composition-rule", "saved-rules/rule.json", savedRuleSchemaId, Hash(savedRule)),
            ]);
        }
        string bundleContentHash = ProfileBundleEntryArrayHasher.CalculateContentHash(entries);
        trustAnchor = new ProfileBundleTrustAnchor(bundleContentHash, "release-manifest");

        var workspace = TempWorkspace.Create("nfc-trusted-profile-projection");
        _ = workspace.Write("schemas/family.schema.json", familySchema);
        _ = workspace.Write("schemas/profile.schema.json", profileSchema);
        _ = workspace.Write("families/family.json", family);
        _ = workspace.Write("profiles/profile.json", profile);
        if (includeNonCanonicalEntries)
        {
            _ = workspace.Write(
                "schemas/evidence.schema.json",
                evidenceSchema ?? throw new InvalidOperationException("Evidence schema was not created."));
            _ = workspace.Write(
                "schemas/saved-rule.schema.json",
                savedRuleSchema ?? throw new InvalidOperationException("Saved-rule schema was not created."));
            _ = workspace.Write(
                "evidence/evidence.json",
                evidence ?? throw new InvalidOperationException("Evidence document was not created."));
            _ = workspace.Write(
                "saved-rules/rule.json",
                savedRule ?? throw new InvalidOperationException("Saved-rule document was not created."));
        }
        byte[] manifest = Encoding.UTF8.GetBytes(Manifest(entries, bundleContentHash));
        _ = workspace.Write("profile-bundle.json", manifest);
        facts = new BundleFacts(
            bundleContentHash,
            Hash(manifest),
            Hash(family),
            Hash(profile));
        return workspace;
    }

    private static string Manifest(IEnumerable<ProfileBundleEntryDocument> entries, string contentHash)
    {
        string entryJson = string.Join(',', entries.Select(static entry => $$"""
            {
              "entryId": "{{entry.EntryId}}",
              "kind": "{{entry.Kind}}",
              "path": "{{entry.Path}}",
              "schemaId": "{{entry.SchemaId}}",
              "contentHash": "{{entry.ContentHash}}"
            }
            """));
        return $$"""
            {
              "schemaVersion": "1.0",
              "bundleId": "bundle",
              "bundleVersion": "1.0.0",
              "hashAlgorithm": "sha256-rfc8785-entry-array-v1",
              "contentHash": "{{contentHash}}",
              "trustAnchorBindingId": "release-manifest",
              "entries": [{{entryJson}}]
            }
            """;
    }

    private static byte[] CanonicalSchema(string fileName, string expectedSchemaId, string actualSchemaId)
    {
        string schema = File.ReadAllText(RepositoryPaths.FromRepositoryRoot("docs", "contracts", fileName));
        return Encoding.UTF8.GetBytes(StringComparer.Ordinal.Equals(expectedSchemaId, actualSchemaId)
            ? schema
            : schema.Replace(expectedSchemaId, actualSchemaId, StringComparison.Ordinal));
    }

    private static string GenericSchema(string schemaId)
    {
        return $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{schemaId}}",
              "type": "object",
              "additionalProperties": false,
              "required": ["value"],
              "properties": { "value": { "type": "integer" } }
            }
            """;
    }

    private static string FamilyJson(string familyId)
    {
        return $$"""
            {
              "schemaVersion": "1.1",
              "familyId": "{{familyId}}",
              "familyVersion": "1.0.0",
              "members": [
                { "memberId": "NT00001", "displayName": "Synthetic IC" }
              ],
              "capabilities": [],
              "regionSets": [
                {
                  "regionSetId": "physical",
                  "addressSpaceId": "flash",
                  "regions": [
                    {
                      "regionId": "root",
                      "owner": "system",
                      "kind": "image",
                      "range": { "start": 0, "length": 16 },
                      "writeConstraint": "forbidden",
                      "alignment": 1
                    }
                  ],
                  "evidenceRefs": ["region-evidence"]
                }
              ],
              "metadataSets": [],
              "imageMaps": [
                {
                  "mapId": "map",
                  "addressSpaceId": "flash",
                  "applicability": {
                    "memberIds": ["NT00001"],
                    "modeIds": ["standard"],
                    "topologyRequirement": { "kind": "none" },
                    "capacityBytes": 16
                  },
                  "coveragePolicy": "complete-with-explicit-gaps",
                  "regionSetIds": ["physical"],
                  "metadataSetIds": [],
                  "evidenceRefs": ["map-evidence"]
                }
              ],
              "factAliases": [],
              "evidenceRefs": ["family-evidence"]
            }
            """;
    }

    private static string ProfileJson(string familyContentHash)
    {
        return $$"""
            {
              "schemaVersion": "2.0",
              "profileId": "profile",
              "profileVersion": "1.0.0",
              "promotion": {
                "stage": "known",
                "blockers": [
                  {
                    "blockerId": "golden-missing",
                    "kind": "golden",
                    "reason": "Synthetic profile has no owner-approved golden.",
                    "evidenceRefs": []
                  }
                ]
              },
              "compositionKind": "merge",
              "experience": {
                "experienceId": "display-merge",
                "audience": "system",
                "layoutPolicy": "fixed",
                "inputPolicy": "fixed",
                "topologyAuthoring": "hidden",
                "displayNameKey": "profile.synthetic.merge"
              },
              "mapBinding": {
                "familyId": "family",
                "familyVersion": "1.0.0",
                "familyContentHash": "{{familyContentHash}}",
                "mapIds": ["map"],
                "requiredRegionIds": ["root"],
                "requiredMetadataStructureIds": [],
                "requiredCapabilityIds": []
              },
              "inputSlots": [
                {
                  "slotId": "tp-input",
                  "role": "tp",
                  "artifactClass": "tp-firmware",
                  "required": true,
                  "cardinality": "exactly-one",
                  "acceptedExtensions": [".bin"],
                  "acceptance": {
                    "lengthRule": { "kind": "tp-maximum-256k", "maximumBytes": 262144 },
                    "normalization": { "kind": "none" }
                  }
                }
              ],
              "spaces": [
                {
                  "spaceId": "tp-source",
                  "kind": "input-artifact",
                  "slotId": "tp-input",
                  "instancePolicy": "singleton"
                },
                {
                  "spaceId": "output",
                  "kind": "output-image",
                  "capacity": { "kind": "resolved-map" },
                  "initializer": { "kind": "blank", "fillByte": 255 }
                }
              ],
              "views": [
                {
                  "viewId": "tp-code",
                  "spaceId": "tp-source",
                  "selector": { "kind": "map-region", "regionId": "root" }
                },
                {
                  "viewId": "output-code",
                  "spaceId": "output",
                  "selector": { "kind": "space-range", "range": { "start": 0, "length": 16 } }
                }
              ],
              "metadataBindings": [],
              "regionAccessRules": [
                {
                  "regionId": "root",
                  "access": "read-only",
                  "reason": "Synthetic source is immutable."
                }
              ],
              "operations": [
                {
                  "operationId": "copy-code",
                  "sequence": 0,
                  "overlapPolicy": "reject",
                  "reason": "Copy the declared source view.",
                  "kind": "copy-range",
                  "sourceViewId": "tp-code",
                  "targetViewId": "output-code"
                }
              ],
              "validations": [],
              "processorStages": [],
              "output": {
                "fileNameTemplate": "{original-name}_merged.bin",
                "allowOverride": false,
                "invalidCharacterPolicy": "replace-underscore",
                "requiredTokenIds": ["original-name"]
              },
              "evidenceRefs": ["synthetic-evidence"]
            }
            """;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record BundleFacts(
        string BundleContentHash,
        string ManifestSha256,
        string FamilyContentHash,
        string ProfileContentHash);
}
