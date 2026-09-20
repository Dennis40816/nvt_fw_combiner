using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Independent pre-migration fact hashes and explicit target expectations for full-image view data.</summary>
public sealed class FullImageMetadataSourceTests
{
    /// <summary>Remove only admitted additions/pins and recover the exact frozen source bytes from cae71c04.</summary>
    [Theory]
    [InlineData("profiles/built-in/nt51923-nt51926-shared-facts/families/nt51923-nt51926.json", "1.3.0", "1.3.1", "78eae90596d03721f887492dfd85c70481f68646a87fe7c08165ce9d281a1072")]
    [InlineData("profiles/built-in/nt51917-nt51927-shared-facts/families/nt51927.json", "1.4.0", "1.4.1", "f6b87ff1b5c4ecbe4df9299ea2d10fc6d568d6d02d528a1119b5369465db2134")]
    [InlineData("profiles/built-in/nt51919-nt51929-nt51932-shared-facts/families/nt51929-nt51932.json", "1.3.0", "1.3.1", "6cd257c38e4c9ecb4e44c14d12027e44a6d484b8176112dceccb7328d153b617")]
    [InlineData("profiles/built-in/nt51928-standard-merge/families/nt51927-nt51928-v1.5.json", "1.5.0", "1.5.1", "538392be2e910627afe0283f947cb63f5e285e97a1d44dd50ea1f6985c177b20")]
    [InlineData("profiles/built-in/nt51950-nt51951-standard-merge/families/nt51950-nt51951-dp-perspective.json", "1.4.0", "1.4.1", "02597d709affd69adfbd92fac4a9a75f245385fb7c0954a5de1c86035e7babf6")]
    public void CanonicalFactsRecoverFrozenPreMigrationBytes(string sourcePath, string oldVersion, string newVersion, string baselineHash)
    {
        string raw = File.ReadAllText(RepositoryPaths.FromRepositoryRoot(sourcePath));
        int views = raw.IndexOf(",\n  \"fullImageMetadataViews\":", StringComparison.Ordinal);
        Assert.True(views > 0);
        string prior = raw[..views] + "\n}\n";
        string versionField = "\"familyVersion\": \"" + newVersion + "\"";
        int versionOffset = prior.IndexOf(versionField, StringComparison.Ordinal);
        Assert.True(versionOffset > 0);
        prior = prior.Remove(versionOffset, versionField.Length).Insert(versionOffset, "\"familyVersion\": \"" + oldVersion + "\"");
        prior = Regex.Replace(prior, "(\"familyId\": \"nt51929-nt51932\",\\s*\"familyVersion\": )\"1\\.3\\.1\"",
            "$1\"1.3.0\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Replace("d2499758dd19908422f857e5b7a68c24c47ac57961418da82d10dec2f039f3e8", "6cd257c38e4c9ecb4e44c14d12027e44a6d484b8176112dceccb7328d153b617", StringComparison.Ordinal)
            .Replace("e3bf3f27d1640d51620c108bce28b22edde2551cb441cf47d1d8641bb6e455af", "11a286731539bc79eb4bc956cd62d5d4e4bb8662874a8f65f7f0cde6f974c761", StringComparison.Ordinal);
        Assert.Equal(baselineHash, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prior))).ToLowerInvariant());
    }

    /// <summary>All fifteen independently captured DP member/capacity target declarations survive canonical loading.</summary>
    [Theory]
    [InlineData("nt51923-nt51926-shared-facts", "NT51923", "nt51923-standard-merge-256k", 262144, "dpcmi-inspection/dpcmi=dp-major,dp-minor,jira-high,jira-low|firmware-config-general-parameters-inspection/firmware-config-general-parameters=tp-firmware-version,tp-firmware-version-complement")]
    [InlineData("nt51923-nt51926-shared-facts", "NT51926", "nt51926-standard-merge-256k", 262144, "dpcmi-inspection/dpcmi=dp-major,dp-minor,jira-high,jira-low|firmware-config-general-parameters-inspection/firmware-config-general-parameters=tp-firmware-version,tp-firmware-version-complement")]
    [InlineData("nt51917-nt51927-shared-facts", "NT51917", "nt51917-standard-merge-256k", 262144, "dpcmi-inspection/dpcmi=dp-major,dp-minor,jira-high,jira-low|firmware-config-general-parameters-inspection/firmware-config-general-parameters=tp-firmware-version,tp-firmware-version-complement")]
    [InlineData("nt51917-nt51927-shared-facts", "NT51927", "nt51927-standard-merge-256k", 262144, "dpcmi-inspection/dpcmi=dp-major,dp-minor,jira-high,jira-low|firmware-config-general-parameters-inspection/firmware-config-general-parameters=tp-firmware-version,tp-firmware-version-complement")]
    [InlineData("nt51919-nt51929-nt51932-shared-facts", "NT51919", "nt51919-nt51929-nt51932-perfect-map-256k", 262144, "dpcmi-inspection/dpcmi=dp-major,dp-minor,jira-high,jira-low")]
    [InlineData("nt51919-nt51929-nt51932-shared-facts", "NT51929", "nt51919-nt51929-nt51932-perfect-map-256k", 262144, "dpcmi-inspection/dpcmi=dp-major,dp-minor,jira-high,jira-low")]
    [InlineData("nt51919-nt51929-nt51932-shared-facts", "NT51932", "nt51919-nt51929-nt51932-perfect-map-256k", 262144, "dpcmi-inspection/dpcmi=dp-major,dp-minor,jira-high,jira-low")]
    [InlineData("nt51928-standard-merge", "NT51928", "nt51928-standard-merge-256k", 262144, "")]
    [InlineData("nt51928-standard-merge", "NT51928", "nt51928-standard-merge-512k", 524288, "")]
    [InlineData("nt51950-nt51951-standard-merge", "NT51950", "nt51950-standard-merge-256k", 262144, "firmware-config-dp-replace-inspection/firmware-config-dp-replace=observed-ic-count,tp-firmware-version,tp-firmware-version-complement|nt51950-dpcmi-dp-replace-inspection/nt51950-dpcmi-dp-replace=dp-major,dp-minor,jira-high,jira-low")]
    [InlineData("nt51950-nt51951-standard-merge", "NT51950", "nt51950-standard-merge-512k", 524288, "firmware-config-dp-replace-inspection/firmware-config-dp-replace=observed-ic-count,tp-firmware-version,tp-firmware-version-complement|nt51950-dpcmi-dp-replace-inspection/nt51950-dpcmi-dp-replace=dp-major,dp-minor,jira-high,jira-low")]
    [InlineData("nt51950-nt51951-standard-merge", "NT51950", "nt51950-standard-merge-1024k", 1048576, "firmware-config-dp-replace-inspection/firmware-config-dp-replace=observed-ic-count,tp-firmware-version,tp-firmware-version-complement|nt51950-dpcmi-dp-replace-inspection/nt51950-dpcmi-dp-replace=dp-major,dp-minor,jira-high,jira-low")]
    [InlineData("nt51950-nt51951-standard-merge", "NT51951", "nt51951-standard-merge-256k", 262144, "firmware-config-dp-replace-inspection/firmware-config-dp-replace=observed-ic-count,tp-firmware-version,tp-firmware-version-complement|nt51951-dpcmi-dp-replace-inspection/nt51951-dpcmi-dp-replace=dp-major,dp-minor,jira-high,jira-low")]
    [InlineData("nt51950-nt51951-standard-merge", "NT51951", "nt51951-standard-merge-512k", 524288, "firmware-config-dp-replace-inspection/firmware-config-dp-replace=observed-ic-count,tp-firmware-version,tp-firmware-version-complement|nt51951-dpcmi-dp-replace-inspection/nt51951-dpcmi-dp-replace=dp-major,dp-minor,jira-high,jira-low")]
    [InlineData("nt51950-nt51951-standard-merge", "NT51951", "nt51951-standard-merge-1024k", 1048576, "firmware-config-dp-replace-inspection/firmware-config-dp-replace=observed-ic-count,tp-firmware-version,tp-firmware-version-complement|nt51951-dpcmi-dp-replace-inspection/nt51951-dpcmi-dp-replace=dp-major,dp-minor,jira-high,jira-low")]
    public void FullImageViewsPreserveFrozenMemberCapacityTargets(string bundle, string member, string mapId, long capacity, string expectedTargets)
    {
        ProfileBundlePackageTrustEntry owner = BuiltInV2BundleRegistry.TrustIndex.Bundles.Single(entry => entry.BundleDirectory == bundle);
        TrustedProfileBundleCatalog catalog = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(bundle, owner.ContentHash);
        FirmwareFamilyResolutionDefinition family = Assert.Single(catalog.Families).Family;
        Assert.Contains(owner.MetadataProviderFamilies, identity => identity.FamilyId == family.FamilyId && identity.FamilyVersion == family.FamilyVersion);
        FirmwareFullImageMetadataView view = Assert.Single(family.FullImageMetadataViews!, candidate =>
            candidate.ImageMap.MapId == mapId && candidate.MemberIds.Contains(member, StringComparer.Ordinal));
        Assert.Equal(capacity, view.ImageMap.CapacityBytes);
        Assert.Same(family.ImageMaps.Single(map => map.MapId == mapId), view.ImageMap);
        string actual = string.Join("|", view.MetadataBindings.Select(binding =>
            binding.BindingId + "/" + binding.Structure.StructureId + "=" +
            string.Join(",", binding.TargetReferences.Select(target => target.TargetId).Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal));
        Assert.Equal(expectedTargets, actual);
        foreach (FirmwareFullImageMetadataBinding binding in view.MetadataBindings)
        {
            Assert.Same(family.GetStructuresForMap(mapId).Single(structure => structure.StructureId == binding.Structure.StructureId), binding.Structure);
            Assert.All(binding.TargetReferences, target => Assert.Equal(FirmwareMetadataReferenceTargetKind.Field, target.Kind));
        }
    }

    /// <summary>Neutral metadata authorities deploy exactly their pinned schema/family inventory without runtime profiles.</summary>
    [Theory]
    [InlineData("nt51923-nt51926-shared-facts")]
    [InlineData("nt51917-nt51927-shared-facts")]
    [InlineData("nt51919-nt51929-nt51932-shared-facts")]
    public void FullImageNeutralProvidersHaveClosedMetadataOnlyInventory(string bundle)
    {
        ProfileBundlePackageTrustEntry owner = BuiltInV2BundleRegistry.TrustIndex.Bundles.Single(entry => entry.BundleDirectory == bundle);
        Assert.Empty(owner.RuntimeRegistrations);
        using var workspace = TempWorkspace.Create("full-image-source-" + bundle);
        TrustedProfileBundleCatalog candidate = BuiltInProfileMaterializationTestSupport.LoadSourceCandidateCatalog(workspace, bundle, owner.ContentHash);
        Assert.Empty(candidate.Profiles);
        Assert.NotEmpty(Assert.Single(candidate.Families).Family.FullImageMetadataViews!);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(workspace.PathFor("profile-bundle.json")));
        string[] expected = ["profile-bundle.json", .. manifest.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("path").GetString()!)];
        Assert.Equal(3, expected.Length);
        foreach (string root in new[] { workspace.Root, Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", bundle) })
        {
            Assert.Equal(expected.Order(StringComparer.Ordinal), Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Order(StringComparer.Ordinal));
            foreach (string path in expected.Where(path => path.StartsWith("families/", StringComparison.Ordinal)))
            {
                Assert.Equal(File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot("profiles", "built-in", bundle, path)), File.ReadAllBytes(Path.Combine(root, path)));
            }
        }
    }

    /// <summary>Only fingerprints emitted by the existing inventory producers can update reviewed route pins.</summary>
    [Fact]
    public void FullImageMigrationRoutePinsMatchExistingInventoryProducers()
    {
        var actual = new List<(string RouteId, string Expected, string Actual)>();
        Func<CapabilityRouteIdentity, CanonicalDynamicRoute> resolveDynamic = CanonicalDynamicRouteInventory.CreateResolver();
        foreach (CanonicalCapabilityPolicyRoute policy in BuiltInCanonicalCapabilityPolicy.Load().Routes)
        {
            string fingerprint = CanonicalDynamicRouteInventory.IsDynamic(policy.Identity)
                ? resolveDynamic(policy.Identity).CapabilityFingerprint
                : CanonicalCompiledRouteInventory.Resolve(policy.Identity).CapabilityFingerprint;
            TestContext.Current.TestOutputHelper!.WriteLine($"FULL_IMAGE_PIN {policy.Identity.RouteId} {policy.CapabilityFingerprint} {fingerprint}");
            actual.Add((policy.Identity.RouteId, policy.CapabilityFingerprint, fingerprint));
        }
        Assert.All(actual, row => Assert.Equal(row.Expected, row.Actual));
    }
}
