using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies synthetic Replace definitions cannot re-enter production profile catalogs.</summary>
    [Fact]
    public void SyntheticReplaceProfilesStayTestOnly()
    {
        string synthetic = ReadText("tests/NvtFwCombiner.TestSupport/SyntheticReplaceProfiles.cs");
        string v2Registration = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInReplaceProfiles.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInReplaceProfiles.Synthetic.cs")));
        Assert.DoesNotContain("synthetic-dp-replace", ReadProfileSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-ctrlram-replace", ReadProfileSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-general-replace", ReadProfileSources(), StringComparison.Ordinal);
        Assert.Contains("public static class SyntheticReplaceProfiles", synthetic, StringComparison.Ordinal);
        Assert.Contains("CompositionProfileDefinition General", synthetic, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", synthetic, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInReplaceProfiles.DpPerspective.cs")));
        Assert.Contains("BuiltInV2Registration", v2Registration, StringComparison.Ordinal);
    }

    /// <summary>Verifies production Standard Merge facts live only in V2 bundles and registrations.</summary>
    [Fact]
    public void StandardMergeRetiresLegacyCSharpProfiles()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInStandardMergeProfiles.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInStandardMergeProfiles.GenFlash.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInStandardMergeProfiles.DpPerspective.cs")));

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "SyntheticCompositionProfiles.cs")));
        string registration = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "tests",
            "NvtFwCombiner.TestSupport",
            "SyntheticStandardMergeProfile.cs")));
        Assert.Contains("BuiltInV2Registration", registration, StringComparison.Ordinal);
    }

    /// <summary>Guards AB display and naming against reintroducing IC- or metadata-specific C# layout routing.</summary>
    [Fact]
    public void AbExecutionReadsDeclaredCmiRegionsWithoutInformationalMetadataRouting()
    {
        string outputNaming = ReadText("src/NvtFwCombiner.Application/Composition/AbCodeOutputNameResolver.cs");
        string inputProjection = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchAbMergeInputProjection.cs");
        string topologyValidation = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.AbMergeTopology.cs");
        string workbenchService = ReadText("src/NvtFwCombiner.Bootstrap/AbMergeWorkbenchCompositionService.cs");

        Assert.Contains("TryGetProfileCmiOffset", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("GenFlashVersionCatalog", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadCmiDpCode", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("GenFlashVersionCatalog", inputProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadCmiDpCode", inputProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductId", outputNaming + inputProjection + topologyValidation, StringComparison.Ordinal);
        Assert.DoesNotContain(".Pid", outputNaming + inputProjection + topologyValidation, StringComparison.Ordinal);
        Assert.DoesNotContain("CommonFw", outputNaming + inputProjection + topologyValidation, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition.IcId", topologyValidation, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950", workbenchService, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51951", workbenchService, StringComparison.Ordinal);
        Assert.Contains("not an IC, PID, or", outputNaming, StringComparison.Ordinal);
        Assert.Contains("ChipNumber", topologyValidation, StringComparison.Ordinal);

        foreach (string profilePath in new[]
                 {
                     "profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51919-ab-merge.json",
                     "profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51929-ab-merge.json",
                     "profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51932-ab-merge.json",
                     "profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge.json",
                     "profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge.json",
                 })
        {
            using JsonDocument profile = JsonDocument.Parse(ReadText(profilePath));
            string[] requiredRegions =
            [
                .. profile.RootElement.GetProperty("mapBinding").GetProperty("requiredRegionIds")
                    .EnumerateArray()
                    .Select(static item => item.GetString() ?? throw new InvalidOperationException(
                        "AB profile mapBinding.requiredRegionIds cannot contain null.")),
            ];

            Assert.Contains("a-cmi-dp-version", requiredRegions, StringComparer.Ordinal);
            Assert.Contains("b-cmi-dp-version", requiredRegions, StringComparer.Ordinal);
        }
    }

    /// <summary>Verifies built-in bundle materialization remains an explicit identity allowlist, never source discovery.</summary>
    [Fact]
    public void BuiltInBundleMaterializationUsesExplicitIdentityAllowlist()
    {
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        var document = XDocument.Parse(project);
        XElement[] bundles = [
            .. document.Descendants("BuiltInProfileBundle")
                .Where(static bundle => bundle.Attribute("Include") is not null),
        ];
        string[] expectedBundleIds =
        [
            "nt51920-standard-merge",
            "nt51920-general-merge-logical-candidate",
            "nt51917-nt51927-general-merge-logical-candidate",
            "nt51919-nt51929-nt51932-general-merge-logical-candidate",
            "nt51923-nt51926-general-merge-logical-candidate",
            "nt51928-general-merge-logical-candidate",
            "nt51930-general-merge-logical-candidate",
            "nt51931-general-merge-logical-candidate",
            "nt51950-nt51951-general-merge-logical-candidate",
            "nt51920-ctrlram-replace-candidate",
            "nt51923-ctrlram-replace-candidate",
            "nt51926-ctrlram-replace-candidate",
            "nt51917-ctrlram-replace-alias-candidate",
            "nt51927-ctrlram-replace-candidate",
            "nt51928-ctrlram-replace-candidate",
            "nt51929-ctrlram-replace-candidate",
            "nt51930-ctrlram-replace-candidate",
            "nt51931-ctrlram-replace-candidate",
            "nt51932-ctrlram-replace-candidate",
            "nt51950-ctrlram-replace-candidate",
            "nt51951-ctrlram-replace-candidate",
            "nt51920-dp-replace",
            "nt51923-dp-replace",
            "nt51923-standard-merge",
            "nt51927-dp-replace",
            "nt51927-standard-merge",
            "nt51928-dp-replace",
            "nt51928-standard-merge",
            "nt51929-dp-replace",
            "nt51929-standard-merge",
            "nt51919-nt51929-nt51932-ab-merge",
            "nt51950-ab-merge",
            "nt51930-standard-merge",
            "nt51931-dp-replace",
            "nt51931-standard-merge",
            "nt51950-nt51951-standard-merge",
        ];
        string[] logicalOutputCandidateBundleIds =
        [
            "nt51920-general-merge-logical-candidate",
            "nt51917-nt51927-general-merge-logical-candidate",
            "nt51919-nt51929-nt51932-general-merge-logical-candidate",
            "nt51923-nt51926-general-merge-logical-candidate",
            "nt51928-general-merge-logical-candidate",
            "nt51930-general-merge-logical-candidate",
            "nt51931-general-merge-logical-candidate",
            "nt51950-nt51951-general-merge-logical-candidate",
        ];

        Assert.Equal(
            expectedBundleIds,
            bundles.Select(bundle => bundle.Attribute("Include")?.Value));
        foreach (XElement bundle in bundles)
        {
            XAttribute include = Assert.Single(bundle.Attributes());

            Assert.Equal("Include", include.Name.LocalName);
            if (bundle.Attribute("Include")?.Value is
                    "nt51919-nt51929-nt51932-ab-merge" or
                    "nt51950-ab-merge")
            {
                Assert.Equal(
                    "composition-profile-v2.10.schema.json",
                    Assert.Single(bundle.Elements("CompositionProfileSchemaFile")).Value);
            }
            else if (logicalOutputCandidateBundleIds.Contains(
                    bundle.Attribute("Include")?.Value,
                    StringComparer.Ordinal))
            {
                Assert.Equal(
                    "composition-profile-v2.5.schema.json",
                    Assert.Single(bundle.Elements("CompositionProfileSchemaFile")).Value);
            }
            else if (bundle.Attribute("Include")?.Value is
                         "nt51920-ctrlram-replace-candidate" or
                         "nt51923-ctrlram-replace-candidate" or
                         "nt51926-ctrlram-replace-candidate" or
                         "nt51917-ctrlram-replace-alias-candidate" or
                         "nt51927-ctrlram-replace-candidate" or
                         "nt51928-ctrlram-replace-candidate" or
                         "nt51929-ctrlram-replace-candidate" or
                         "nt51930-ctrlram-replace-candidate" or
                         "nt51931-ctrlram-replace-candidate" or
                         "nt51932-ctrlram-replace-candidate" or
                         "nt51950-ctrlram-replace-candidate" or
                         "nt51951-ctrlram-replace-candidate")
            {
                Assert.Equal(
                    "composition-profile-v2.9.schema.json",
                    Assert.Single(bundle.Elements("CompositionProfileSchemaFile")).Value);
            }
            else
            {
                Assert.Empty(bundle.Elements());
            }
            Assert.DoesNotContain("*", include.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("$(", include.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("@(", include.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("%(", include.Value, StringComparison.Ordinal);
        }

        Assert.Contains("$(BuiltInProfileSourceRoot)\\%(BuiltInProfileBundle.Identity)\\**\\*", project, StringComparison.Ordinal);
        Assert.Contains(
            "Exclude=\"$(BuiltInProfileSourceRoot)\\%(BuiltInProfileBundle.Identity)\\schemas\\**\\*\"",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<DefaultCompositionProfileSchemaFile>composition-profile-v2.schema.json</DefaultCompositionProfileSchemaFile>",
            project,
            StringComparison.Ordinal);
        Assert.Equal(
            "$(DefaultCompositionProfileSchemaFile)",
            Assert.Single(document.Descendants("ItemDefinitionGroup")
                .Descendants("BuiltInProfileBundle"))
                .Element("CompositionProfileSchemaFile")?.Value);
        Assert.Contains(
            "$(ProfileContractRoot)\\%(BuiltInProfileBundle.CompositionProfileSchemaFile)",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileSchemaSourceRoot", project, StringComparison.Ordinal);
        string retiredSchemaSource = Path.Combine(Root.FullName, "profiles", "schema-source");
        Assert.False(Directory.Exists(retiredSchemaSource) &&
            Directory.EnumerateFiles(retiredSchemaSource, "*", SearchOption.AllDirectories).Any());
        Assert.DoesNotContain(bundles, static bundle => bundle.Element("SourceRoot") is not null);
        Assert.DoesNotContain("**\\profile-bundle.json", project, StringComparison.Ordinal);
    }

    /// <summary>Verifies each approved canonical firmware-family reuse is explicit and closed-root.</summary>
    [Fact]
    public void CandidateBundlesMaterializeOnlyApprovedCanonicalFirmwareFamilies()
    {
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        var document = XDocument.Parse(project);
        XElement candidate = Assert.Single(document.Descendants("BuiltInProfileBundle"), static bundle =>
            StringComparer.Ordinal.Equals(
                bundle.Attribute("Include")?.Value,
                "nt51930-general-merge-logical-candidate"));

        Assert.Equal(
            "nt51930-standard-merge\\families\\nt51930.json",
            candidate.Element("CanonicalFirmwareFamilySource")?.Value);
        Assert.Equal(
            "families\\nt51930.json",
            candidate.Element("CanonicalFirmwareFamilyDestination")?.Value);
        Assert.Equal(2, document.Descendants("CanonicalFirmwareFamilySource").Count(static element =>
            !string.IsNullOrWhiteSpace(element.Value)));
        Assert.Equal(2, document.Descendants("CanonicalFirmwareFamilyDestination").Count(static element =>
            !string.IsNullOrWhiteSpace(element.Value)));

        string canonicalFamilyPath = Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51930-standard-merge",
            "families",
            "nt51930.json");
        string candidateFamilyPath = Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51930-general-merge-logical-candidate",
            "families",
            "nt51930.json");
        Assert.True(File.Exists(canonicalFamilyPath));
        Assert.False(File.Exists(candidateFamilyPath));

        using var manifest = JsonDocument.Parse(ReadText(
            "profiles/built-in/nt51930-general-merge-logical-candidate/profile-bundle.json"));
        JsonElement familyEntry = Assert.Single(
            manifest.RootElement.GetProperty("entries").EnumerateArray(),
            static entry => StringComparer.Ordinal.Equals(
                entry.GetProperty("kind").GetString(),
                "firmware-family"));
        Assert.Equal("families/nt51930.json", familyEntry.GetProperty("path").GetString());
        Assert.Equal(
            familyEntry.GetProperty("contentHash").GetString(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(canonicalFamilyPath))).ToLowerInvariant());

        XElement nt51917Candidate = Assert.Single(document.Descendants("BuiltInProfileBundle"), static bundle =>
            StringComparer.Ordinal.Equals(
                bundle.Attribute("Include")?.Value,
                "nt51917-ctrlram-replace-alias-candidate"));
        Assert.Equal(
            "nt51927-ctrlram-replace-candidate\\families\\nt51927-ctrlram-replace.json",
            nt51917Candidate.Element("CanonicalFirmwareFamilySource")?.Value);
        Assert.Equal(
            "families\\nt51927-ctrlram-replace.json",
            nt51917Candidate.Element("CanonicalFirmwareFamilyDestination")?.Value);
        string canonicalCtrlRamFamilyPath = Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51927-ctrlram-replace-candidate",
            "families",
            "nt51927-ctrlram-replace.json");
        string aliasFamilyPath = Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51917-ctrlram-replace-alias-candidate",
            "families",
            "nt51927-ctrlram-replace.json");
        Assert.True(File.Exists(canonicalCtrlRamFamilyPath));
        Assert.False(File.Exists(aliasFamilyPath));
        using var aliasManifest = JsonDocument.Parse(ReadText(
            "profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profile-bundle.json"));
        JsonElement aliasFamilyEntry = Assert.Single(
            aliasManifest.RootElement.GetProperty("entries").EnumerateArray(),
            static entry => StringComparer.Ordinal.Equals(
                entry.GetProperty("kind").GetString(),
                "firmware-family"));
        Assert.Equal("families/nt51927-ctrlram-replace.json", aliasFamilyEntry.GetProperty("path").GetString());
        Assert.Equal(
            aliasFamilyEntry.GetProperty("contentHash").GetString(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(canonicalCtrlRamFamilyPath))).ToLowerInvariant());

        Assert.Contains("Built-in profile canonical firmware-family metadata must declare both source and destination", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family source escapes the approved source root", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family destination escapes the bundle families root", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family source is missing", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family destination collides", project, StringComparison.Ordinal);
        Assert.Contains("@(_BuiltInProfileCanonicalFamily->'%(SourceFile)')", project, StringComparison.Ordinal);
        Assert.Contains("@(_BuiltInProfileCanonicalFamily->'%(DestinationFile)')", project, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CanonicalFirmwareFamily",
            ReadText("src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs"),
            StringComparison.Ordinal);
    }

    /// <summary>Verifies retired DP Perspective C# facts cannot become a second oracle beside trusted V2 plans.</summary>
    [Fact]
    public void DpPerspectiveFactsStayOwnedByTrustedV2Profiles()
    {
        string registration = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.BuiltInV2.cs");
        string display = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.V2Display.cs");
        string planning = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Planning.cs");

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "DpPerspectiveCatalog.cs")));
        Assert.DoesNotContain("BuiltInReplaceProfiles", registration, StringComparison.Ordinal);
        Assert.Contains("TryResolveBuiltInV2DpReplaceDisplay", registration, StringComparison.Ordinal);
        Assert.Contains("composition.Plan.OrderedOperations", display, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInTpFlashMapCatalog", planning, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDpReplaceRegions", planning, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOperationKind", planning, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", ReadBootstrapSources(), StringComparison.Ordinal);
    }

}
