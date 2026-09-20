using System.Globalization;
using System.Text;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests the package-owned exact bundle admission boundary.</summary>
public sealed class ProfileBundlePackageTrustIndexLoaderTests
{
    /// <summary>The file-size gate rejects an oversized index before allocating its snapshot.</summary>
    [Fact]
    public void LoadRejectsOversizedIndexAtFileBoundary()
    {
        using var workspace = TempWorkspace.Create("package-trust-index-oversized");
        string path = workspace.Write(
            "package-trust-index.json",
            new byte[ProfileBundlePackageTrustIndexLoader.MaximumBytes + 1]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundlePackageTrustIndexLoader.Load(path));

        Assert.Contains("exceeds", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ProfileBundlePackageTrustIndexLoader.MaximumBytes.ToString(CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>The checked-in index admits every reviewed bundle through immutable hash pins.</summary>
    [Fact]
    public void LoadReturnsVersionedHashClosedIndex()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "profiles",
            "built-in",
            "package-trust-index.json");

        ProfileBundlePackageTrustIndex index =
            ProfileBundlePackageTrustIndexLoader.Load(path);

        Assert.Equal("1.4", index.SchemaVersion);
        Assert.Equal("built-in-profile-bundles", index.TrustIndexId);
        Assert.Equal("1.1.10.1", index.TrustIndexVersion);
        Assert.Equal("built-in-profile-bundle-v2", index.TrustAnchorBindingId);
        Assert.Equal(27, index.Bundles.Count);
        Assert.Equal(
            64,
            index.Bundles.Sum(static bundle => bundle.RuntimeRegistrations.Count));
        ProfileBundleRuntimeRegistration generalReplace = index.Bundles
            .SelectMany(static bundle => bundle.RuntimeRegistrations)
            .Single(static registration => registration.WorkflowId == "general-replace");
        Assert.Equal("NT51926", generalReplace.IcId);
        Assert.Equal(
            "nt51926-general-replace-dp-single-candidate",
            generalReplace.ProfileId);
        Assert.Equal("0.1.0", generalReplace.ProfileVersion);
        Assert.Equal(
            ["nt51919-ab-merge-512k", "nt51929-ab-merge-512k", "nt51932-ab-merge-512k",
                "nt51928-dual-capacity-256k-512k", "nt51928-dual-capacity-256k-512k",
                "nt51950-ab-merge-maps", "nt51951-ab-merge-1024k",
                "nt51950-ab-desay-maps", "nt51951-ab-desay-maps", "nt51950-ab-common-2ic-maps"],
            index.Bundles.SelectMany(static bundle => bundle.RuntimeRegistrations)
                .Where(static registration => registration.MapVariantSetId is not null)
                .Select(static registration => registration.MapVariantSetId));
        Assert.Equal(
            4,
            index.Bundles.Sum(static bundle => bundle.MetadataProviderFamilies.Count));
        _ = Assert.Single(index.Bundles.SelectMany(static bundle => bundle.FamilyDisclosureFamilies));
        ProfileBundleRuntimeRegistration[] ctrlRam =
        [
            .. index.Bundles.SelectMany(static bundle => bundle.RuntimeRegistrations)
                .Where(static registration => registration.WorkflowId == "ctrlram-replace"),
        ];
        Assert.Equal(25, ctrlRam.Length);
        Assert.Equal(19, ctrlRam.Count(static registration =>
            registration.ReportMetadataMapId is not null));
        Assert.Equal(6, ctrlRam.Count(static registration =>
            registration.ReportMetadataMapId is null));
        Assert.Equal(
            index.Bundles.OrderBy(static bundle => bundle.BundleDirectory, StringComparer.Ordinal),
            index.Bundles);
        Assert.All(index.Bundles, static bundle =>
            Assert.Matches("^[0-9a-f]{64}$", bundle.ContentHash));
    }

    /// <summary>Duplicate bundle roots never acquire authority through array order.</summary>
    [Fact]
    public void LoadRejectsDuplicateBundleDirectory()
    {
        using TempWorkspace workspace = WriteIndex(
            Bundle(),
            Bundle());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundlePackageTrustIndexLoader.Load(
                Path.Combine(workspace.Root, "package-trust-index.json")));

        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Executable extension declarations are outside the closed trust-index schema.</summary>
    [Theory]
    [InlineData("script")]
    [InlineData("plugin")]
    [InlineData("dynamicAssembly")]
    [InlineData("watchPath")]
    public void LoadRejectsExecutableOrHotReloadExtensionFields(string propertyName)
    {
        string bundle = Bundle()[..^1] + $",\"{propertyName}\":\"forbidden\"}}";
        using TempWorkspace workspace = WriteIndex(bundle);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundlePackageTrustIndexLoader.Load(
                Path.Combine(workspace.Root, "package-trust-index.json")));

        Assert.Contains("does not satisfy schema", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Materialization sources cannot escape the reviewed built-in data root.</summary>
    [Fact]
    public void LoadRejectsParentTraversalMaterializationSource()
    {
        string bundle = Bundle(canonicalSource: "source/../private.json");
        using TempWorkspace workspace = WriteIndex(bundle);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundlePackageTrustIndexLoader.Load(
                Path.Combine(workspace.Root, "package-trust-index.json")));

        Assert.Contains("closed relative JSON path", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Existing workflow and selection-group vocabulary accepts a new IC/version as data.</summary>
    [Theory]
    [InlineData("standard-merge")]
    [InlineData("ab-merge")]
    [InlineData("dp-replace")]
    public void LoadProjectsSyntheticExistingVocabularyRegistration(string workflowId)
    {
        string bundle = Bundle().Replace(
            "\"runtimeRegistrations\": []",
            "\"runtimeRegistrations\":[{" +
            $"\"workflowId\":\"{workflowId}\"," +
            "\"icId\":\"NT12345\"," +
            "\"profileId\":\"synthetic-standard-merge\"," +
            "\"profileVersion\":\"9.8.7\"," +
            "\"mapVariantSetId\":\"synthetic-selection-map-set\"}]",
            StringComparison.Ordinal);
        using TempWorkspace workspace = WriteIndex(bundle);

        ProfileBundleRuntimeRegistration registration = Assert.Single(
            Assert.Single(ProfileBundlePackageTrustIndexLoader.Load(
                    Path.Combine(workspace.Root, "package-trust-index.json"))
                .Bundles)
            .RuntimeRegistrations);

        Assert.Equal("NT12345", registration.IcId);
        Assert.Equal("9.8.7", registration.ProfileVersion);
        Assert.Equal("synthetic-selection-map-set", registration.MapVariantSetId);
    }

    /// <summary>Different explicit map sets may share an IC, but duplicate exact keys remain rejected.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadDistinguishesExactFormatRegistrationKeys(bool duplicate)
    {
        const string first = "{\"workflowId\":\"ab-merge\",\"icId\":\"NT51950\",\"profileId\":\"format-a\"," +
            "\"profileVersion\":\"1.0.0\",\"mapVariantSetId\":\"format-a-maps\"}";
        string second = duplicate ? first : first.Replace("format-a", "format-b", StringComparison.Ordinal);
        string bundle = Bundle().Replace("\"runtimeRegistrations\": []",
            $"\"runtimeRegistrations\": [{first},{second}]", StringComparison.Ordinal);
        using TempWorkspace workspace = WriteIndex(bundle);
        string path = Path.Combine(workspace.Root, "package-trust-index.json");
        if (duplicate)
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(() => ProfileBundlePackageTrustIndexLoader.Load(path));
            Assert.Contains("unique", failure.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(2, Assert.Single(ProfileBundlePackageTrustIndexLoader.Load(path).Bundles).RuntimeRegistrations.Count);
        }
    }

    /// <summary>A map-set cannot extend a workflow outside the closed map-bound selection vocabulary.</summary>
    [Fact]
    public void LoadRejectsMapSetOnGeneralReplace()
    {
        string bundle = Bundle().Replace("\"runtimeRegistrations\": []",
            "\"runtimeRegistrations\":[{\"workflowId\":\"general-replace\",\"icId\":\"NT12345\"," +
            "\"profileId\":\"synthetic\",\"profileVersion\":\"1.0.0\",\"mapVariantSetId\":\"forbidden\"}]",
            StringComparison.Ordinal);
        using TempWorkspace workspace = WriteIndex(bundle);
        _ = Assert.Throws<InvalidDataException>(() => ProfileBundlePackageTrustIndexLoader.Load(
            Path.Combine(workspace.Root, "package-trust-index.json")));
    }

    /// <summary>A CtrlRAM registration retains its exact token-only report metadata counterpart.</summary>
    [Theory]
    [InlineData("reportMetadataMapId")]
    [InlineData("memoryLayoutContextMapId")]
    public void LoadProjectsSyntheticCtrlRamReportMetadataCounterpart(string field)
    {
        string bundle = Bundle().Replace(
            "\"runtimeRegistrations\": []",
            "\"runtimeRegistrations\":[{" +
            "\"workflowId\":\"ctrlram-replace\"," +
            "\"icId\":\"NT12345\"," +
            "\"profileId\":\"synthetic-ctrlram-replace\"," +
            "\"profileVersion\":\"9.8.7\"," +
            "\"postbuildProcessorId\":\"nfc.synthetic.ctrlram\"," +
            "\"postbuildBranch\":\"single-chip\"," +
            $"\"{field}\":\"synthetic-standard-map\"}}]",
            StringComparison.Ordinal);
        using TempWorkspace workspace = WriteIndex(bundle);

        ProfileBundleRuntimeRegistration registration = Assert.Single(
            Assert.Single(ProfileBundlePackageTrustIndexLoader.Load(
                    Path.Combine(workspace.Root, "package-trust-index.json"))
                .Bundles)
            .RuntimeRegistrations);

        Assert.Equal("synthetic-standard-map", field == "reportMetadataMapId" ?
            registration.ReportMetadataMapId : registration.MemoryLayoutContextMapId);
        Assert.Null(field == "reportMetadataMapId" ? registration.MemoryLayoutContextMapId : registration.ReportMetadataMapId);
    }

    /// <summary>The report counterpart is forbidden outside CtrlRAM and rejects non-token JSON values.</summary>
    [Theory]
    [InlineData("standard-merge", "\"synthetic-standard-map\"")]
    [InlineData("ctrlram-replace", "null")]
    [InlineData("ctrlram-replace", "123")]
    [InlineData("ctrlram-replace", "\"\"")]
    [InlineData("ctrlram-replace", "\"Synthetic-Map\"")]
    [InlineData("ctrlram-replace", "\"../synthetic-map\"")]
    public void LoadRejectsInvalidReportMetadataCounterpart(
        string workflowId,
        string reportMapValue)
    {
        string ctrlRamFields = workflowId == "ctrlram-replace"
            ? "\"postbuildProcessorId\":\"nfc.synthetic.ctrlram\"," +
              "\"postbuildBranch\":\"single-chip\","
            : string.Empty;
        foreach (string field in new[] { "reportMetadataMapId", "memoryLayoutContextMapId" })
        {
            string bundle = Bundle().Replace(
                "\"runtimeRegistrations\": []",
                "\"runtimeRegistrations\":[{" +
                $"\"workflowId\":\"{workflowId}\"," +
                "\"icId\":\"NT12345\"," +
                "\"profileId\":\"synthetic-profile\"," +
                "\"profileVersion\":\"9.8.7\"," +
                ctrlRamFields +
                $"\"{field}\":{reportMapValue}}}]",
                StringComparison.Ordinal);
            using TempWorkspace workspace = WriteIndex(bundle);
            _ = Assert.Throws<InvalidDataException>(() =>
                ProfileBundlePackageTrustIndexLoader.Load(Path.Combine(workspace.Root, "package-trust-index.json")));
        }
    }

    /// <summary>Both independent family authorities share exact pair shape and global uniqueness rules.</summary>
    [Theory]
    [InlineData("metadataProviderFamilies")]
    [InlineData("familyDisclosureFamilies")]
    public void FamilyAuthoritiesRejectMalformedAndDuplicateBindings(string field)
    {
        foreach (string value in new[] { "null", "{}", "[null]", "[{\"familyId\":\"test-family\",\"familyVersion\":123}]",
                     "[{\"familyId\":\"test-family\",\"familyVersion\":\"bad\"}]",
                     "[{\"familyId\":\"test-family\",\"familyVersion\":\"1.0.0\",\"runtime\":true}]" })
        {
            using TempWorkspace malformed = WriteIndex(Bundle()[..^1] + $",\"{field}\":{value}}}");
            _ = Assert.Throws<InvalidDataException>(() => ProfileBundlePackageTrustIndexLoader.Load(
                Path.Combine(malformed.Root, "package-trust-index.json")));
        }
        const string pair = "{\"familyId\":\"test-family\",\"familyVersion\":\"1.0.0\"}";
        string first = Bundle()[..^1] + $",\"{field}\":[{pair}]}}";
        string second = first.Replace("test-bundle", "other-bundle", StringComparison.Ordinal);
        using TempWorkspace duplicatedAcross = WriteIndex(first, second);
        _ = Assert.Throws<InvalidDataException>(() => ProfileBundlePackageTrustIndexLoader.Load(
            Path.Combine(duplicatedAcross.Root, "package-trust-index.json")));
        using TempWorkspace duplicatedWithin = WriteIndex(Bundle()[..^1] + $",\"{field}\":[{pair},{pair}]}}");
        _ = Assert.Throws<InvalidDataException>(() => ProfileBundlePackageTrustIndexLoader.Load(
            Path.Combine(duplicatedWithin.Root, "package-trust-index.json")));
    }

    /// <summary>Absence grants no family authority and one family may independently hold both authorities.</summary>
    [Fact]
    public void FamilyAuthoritiesAreOptionalAndIndependent()
    {
        using TempWorkspace omitted = WriteIndex(Bundle());
        ProfileBundlePackageTrustEntry empty = Assert.Single(ProfileBundlePackageTrustIndexLoader.Load(
            Path.Combine(omitted.Root, "package-trust-index.json")).Bundles);
        Assert.Empty(empty.MetadataProviderFamilies);
        Assert.Empty(empty.FamilyDisclosureFamilies);
        const string pair = "{\"familyId\":\"test-family\",\"familyVersion\":\"1.0.0\"}";
        using TempWorkspace both = WriteIndex(Bundle()[..^1] +
            $",\"metadataProviderFamilies\":[{pair}],\"familyDisclosureFamilies\":[{pair}]}}");
        ProfileBundlePackageTrustEntry accepted = Assert.Single(ProfileBundlePackageTrustIndexLoader.Load(
            Path.Combine(both.Root, "package-trust-index.json")).Bundles);
        Assert.Equal(Assert.Single(accepted.MetadataProviderFamilies), Assert.Single(accepted.FamilyDisclosureFamilies));
    }

    private static TempWorkspace WriteIndex(params string[] bundles)
    {
        var workspace = TempWorkspace.Create("package-trust-index");
        string json = $$"""
            {
              "schemaVersion": "1.4",
              "trustIndexId": "test-profile-bundles",
              "trustIndexVersion": "1.0.0",
              "trustAnchorBindingId": "test-profile-bundle-v2",
              "bundles": [{{string.Join(',', bundles)}}]
            }
            """;
        _ = workspace.Write("package-trust-index.json", Encoding.UTF8.GetBytes(json));
        return workspace;
    }

    private static string Bundle(string canonicalSource = "source/families/family.json")
    {
        return $$"""
            {
              "bundleDirectory": "test-bundle",
              "bundleSchemaVersion": "1.0",
              "bundleVersion": "1.0.0",
              "contentHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "materialization": {
                "compositionProfileSchemaFile": "composition-profile-v2.schema.json",
                "firmwareFamilySchemaFile": "firmware-family-v1.schema.json",
                "canonicalFirmwareFamily": {
                  "source": "{{canonicalSource}}",
                  "destination": "families/family.json"
                }
              },
              "runtimeRegistrations": []
            }
            """;
    }
}
