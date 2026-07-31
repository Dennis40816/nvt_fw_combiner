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
            using var profile = JsonDocument.Parse(ReadText(profilePath));
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

    /// <summary>Locks ADR 0035 to the canonical 950/951 relocation and import ownership.</summary>
    [Fact]
    public void Nt51950AndNt51951AdrMatchesCanonicalExecutionOwnership()
    {
        string adr = ReadText("docs/adr/0035-ab-topology-operator-selection.md");
        string contract = ReadText(
            "docs/architecture/nt51950-nt51951-ab-code-contract.md");

        Assert.Contains(
            "canonical execution amended for `0.10.x` issue #190",
            adr,
            StringComparison.Ordinal);
        Assert.Contains(
            "relocates only the TPB DIFF stored BIN-start field",
            adr,
            StringComparison.Ordinal);
        Assert.Contains(
            "imports only those three fields into the output",
            adr,
            StringComparison.Ordinal);
        Assert.Contains(
            "host imports only those",
            contract,
            StringComparison.Ordinal);
        Assert.Contains(
            "three four-byte fields",
            contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TPB ILM/DLM/DIFF addend", adr, StringComparison.Ordinal);
        Assert.DoesNotContain("all three TPB relocations", adr, StringComparison.Ordinal);
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
            "nt51917-nt51927-general-merge-logical-candidate",
            "nt51919-nt51929-nt51932-general-merge-logical-candidate",
            "nt51923-nt51926-general-merge-logical-candidate",
            "nt51928-general-merge-logical-candidate",
            "nt51950-nt51951-general-merge-logical-candidate",
            "nt51923-ctrlram-replace-candidate",
            "nt51926-ctrlram-replace-candidate",
            "nt51917-ctrlram-replace-alias-candidate",
            "nt51927-ctrlram-replace-candidate",
            "nt51928-ctrlram-replace-candidate",
            "nt51929-ctrlram-replace-candidate",
            "nt51932-ctrlram-replace-candidate",
            "nt51950-ctrlram-replace-candidate",
            "nt51951-ctrlram-replace-candidate",
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
            "nt51950-nt51951-standard-merge",
        ];
        string[] logicalOutputCandidateBundleIds =
        [
            "nt51917-nt51927-general-merge-logical-candidate",
            "nt51919-nt51929-nt51932-general-merge-logical-candidate",
            "nt51923-nt51926-general-merge-logical-candidate",
            "nt51928-general-merge-logical-candidate",
            "nt51950-nt51951-general-merge-logical-candidate",
        ];
        string[] relationshipSchemaBundleIds =
        [
            "nt51950-nt51951-general-merge-logical-candidate",
            "nt51923-dp-replace",
            "nt51927-dp-replace",
            "nt51929-dp-replace",
            "nt51950-nt51951-standard-merge",
        ];
        string[] tpHeaderSubjectSchemaBundleIds =
        [
            "nt51917-nt51927-general-merge-logical-candidate",
            "nt51923-nt51926-general-merge-logical-candidate",
            "nt51928-general-merge-logical-candidate",
            "nt51923-standard-merge",
            "nt51927-standard-merge",
            "nt51928-dp-replace",
            "nt51928-standard-merge",
        ];
        string[] sourceProjectionSchemaBundleIds =
        [
            "nt51923-dp-replace",
            "nt51923-standard-merge",
            "nt51927-dp-replace",
            "nt51927-standard-merge",
            "nt51928-dp-replace",
            "nt51928-standard-merge",
            "nt51929-dp-replace",
            "nt51929-standard-merge",
            "nt51950-ab-merge",
            "nt51950-nt51951-standard-merge",
        ];

        Assert.Equal(
            expectedBundleIds,
            bundles.Select(bundle => bundle.Attribute("Include")?.Value));
        foreach (XElement bundle in bundles)
        {
            XAttribute include = Assert.Single(bundle.Attributes());

            Assert.Equal("Include", include.Name.LocalName);
            if (include.Value is "nt51919-nt51929-nt51932-ab-merge" or "nt51950-ab-merge")
            {
                Assert.Equal(
                    "composition-profile-v2.14.schema.json",
                    Assert.Single(bundle.Elements("CompositionProfileSchemaFile")).Value);
            }
            else if (sourceProjectionSchemaBundleIds.Contains(
                    bundle.Attribute("Include")?.Value,
                    StringComparer.Ordinal))
            {
                Assert.Equal(
                    "composition-profile-v2.13.schema.json",
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
                         "nt51923-ctrlram-replace-candidate" or
                         "nt51926-ctrlram-replace-candidate" or
                         "nt51917-ctrlram-replace-alias-candidate" or
                         "nt51927-ctrlram-replace-candidate" or
                         "nt51928-ctrlram-replace-candidate" or
                         "nt51929-ctrlram-replace-candidate" or
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
                Assert.Empty(bundle.Elements("CompositionProfileSchemaFile"));
            }

            if (include.Value is "nt51919-nt51929-nt51932-ab-merge" or "nt51950-ab-merge")
            {
                Assert.Equal(
                    "firmware-family-v1.2-bank-instances.schema.json",
                    Assert.Single(bundle.Elements("FirmwareFamilySchemaFile")).Value);
            }
            else if (tpHeaderSubjectSchemaBundleIds.Contains(include.Value, StringComparer.Ordinal))
            {
                Assert.Equal(
                    "firmware-family-v1.2-tp-header-subjects.schema.json",
                    Assert.Single(bundle.Elements("FirmwareFamilySchemaFile")).Value);
            }
            else if (include.Value is
                    "nt51919-nt51929-nt51932-general-merge-logical-candidate" or
                    "nt51929-standard-merge")
            {
                Assert.Equal(
                    "firmware-family-v1.1-tp-header.schema.json",
                    Assert.Single(bundle.Elements("FirmwareFamilySchemaFile")).Value);
            }
            else if (relationshipSchemaBundleIds.Contains(include.Value, StringComparer.Ordinal))
            {
                Assert.Equal(
                    "firmware-family-v1-relations.schema.json",
                    Assert.Single(bundle.Elements("FirmwareFamilySchemaFile")).Value);
            }
            else
            {
                Assert.Empty(bundle.Elements("FirmwareFamilySchemaFile"));
            }

            (string? canonicalSource, string? canonicalDestination) = include.Value switch
            {
                "nt51917-nt51927-general-merge-logical-candidate" or
                "nt51928-general-merge-logical-candidate" => (
                    "nt51927-standard-merge\\families\\nt51927-nt51928.json",
                    "families\\nt51927-nt51928.json"),
                "nt51923-nt51926-general-merge-logical-candidate" => (
                    "nt51923-standard-merge\\families\\nt51923-nt51926.json",
                    "families\\nt51923-nt51926.json"),
                "nt51928-dp-replace" => (
                    "nt51928-standard-merge\\families\\nt51927-nt51928-v1.5.json",
                    "families\\nt51927-nt51928-v1.5.json"),
                "nt51950-nt51951-general-merge-logical-candidate" => (
                    "nt51950-nt51951-standard-merge\\families\\nt51950-nt51951-dp-perspective.json",
                    "families\\nt51950-nt51951-dp-perspective.json"),
                "nt51917-ctrlram-replace-alias-candidate" => (
                    "nt51927-ctrlram-replace-candidate\\families\\nt51927-ctrlram-replace.json",
                    "families\\nt51927-ctrlram-replace.json"),
                _ => (null, null),
            };
            if (canonicalSource is null)
            {
                Assert.Empty(bundle.Elements("CanonicalFirmwareFamilySource"));
                Assert.Empty(bundle.Elements("CanonicalFirmwareFamilyDestination"));
            }
            else
            {
                Assert.Equal(
                    canonicalSource,
                    Assert.Single(bundle.Elements("CanonicalFirmwareFamilySource")).Value);
                Assert.Equal(
                    canonicalDestination,
                    Assert.Single(bundle.Elements("CanonicalFirmwareFamilyDestination")).Value);
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
        Assert.Equal(
            "firmware-family-v1.schema.json",
            Assert.Single(document.Descendants("ItemDefinitionGroup")
                .Descendants("BuiltInProfileBundle"))
                .Element("FirmwareFamilySchemaFile")?.Value);
        Assert.Contains(
            "$(ProfileContractRoot)\\%(BuiltInProfileBundle.CompositionProfileSchemaFile)",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "$(ProfileContractRoot)\\%(BuiltInProfileBundle.FirmwareFamilySchemaFile)",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileSchemaSourceRoot", project, StringComparison.Ordinal);
        string retiredSchemaSource = Path.Combine(Root.FullName, "profiles", "schema-source");
        Assert.False(Directory.Exists(retiredSchemaSource) &&
            Directory.EnumerateFiles(retiredSchemaSource, "*", SearchOption.AllDirectories).Any());
        Assert.DoesNotContain(bundles, static bundle => bundle.Element("SourceRoot") is not null);
        Assert.DoesNotContain("**\\profile-bundle.json", project, StringComparison.Ordinal);
    }

    /// <summary>Retired ICs have no production profile, route, processor, package, or catalog owner.</summary>
    [Fact]
    public void RetiredIcCapabilitiesStayOutsideProductionOwners()
    {
        string[] retiredIds = ["51920", "51925", "51930", "51931"];
        string[] productionOwners =
        [
            "src/NvtFwCombiner.Profiles/IcSupportCatalog.cs",
            "src/NvtFwCombiner.Bootstrap/BuiltInV2Bundle.cs",
            "src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs",
            "src/NvtFwCombiner.Bootstrap/CtrlRamV2RouteRegistry.cs",
            "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
            "src/NvtFwCombiner.Application/FlashMaps/TpHeaderCatalog.Layouts.cs",
            "src/NvtFwCombiner.Application/FlashMaps/GenFlashVersionCatalog.cs",
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.IntegrityRanges.cs",
            "src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog.cs",
            "src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog.Loader.cs",
            "profiles/built-in/ctrlram-postbuild-v2/catalog.json",
            "profiles/built-in/ctrlram-postbuild-v2/flash-map.json",
        ];

        foreach (string owner in productionOwners)
        {
            string source = ReadText(owner);
            Assert.All(retiredIds, retiredId =>
                Assert.DoesNotContain(retiredId, source, StringComparison.Ordinal));
        }

        string builtInRoot = Path.Combine(Root.FullName, "profiles", "built-in");
        Assert.DoesNotContain(
            Directory.EnumerateFiles(builtInRoot, "*", SearchOption.AllDirectories),
            path => retiredIds.Any(retiredId =>
                Path.GetRelativePath(builtInRoot, path)
                    .StartsWith($"nt{retiredId}-", StringComparison.Ordinal)));

        Assert.True(Directory.Exists(Path.Combine(builtInRoot, "nt51923-ctrlram-replace-candidate")));
        Assert.True(Directory.Exists(Path.Combine(builtInRoot, "nt51926-ctrlram-replace-candidate")));

        string packagePolicy = ReadText("scripts/package.ps1");
        Assert.Contains("$RetiredIcTokens", packagePolicy, StringComparison.Ordinal);
        Assert.Contains("cannot publish retired IC", packagePolicy, StringComparison.Ordinal);
        Assert.All(retiredIds, retiredId =>
            Assert.Contains($"'{retiredId}'", packagePolicy, StringComparison.Ordinal));
    }

    /// <summary>Verifies each approved canonical firmware-family reuse is explicit and closed-root.</summary>
    [Fact]
    public void CandidateBundlesMaterializeOnlyApprovedCanonicalFirmwareFamilies()
    {
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        var document = XDocument.Parse(project);
        Assert.Equal(6, document.Descendants("CanonicalFirmwareFamilySource").Count(static element =>
            !string.IsNullOrWhiteSpace(element.Value)));
        Assert.Equal(6, document.Descendants("CanonicalFirmwareFamilyDestination").Count(static element =>
            !string.IsNullOrWhiteSpace(element.Value)));

        foreach (string bundleId in new[]
                 {
                     "nt51917-nt51927-general-merge-logical-candidate",
                     "nt51928-general-merge-logical-candidate",
                 })
        {
            XElement sharedPartsConsumer = Assert.Single(
                document.Descendants("BuiltInProfileBundle"),
                bundle => StringComparer.Ordinal.Equals(
                    bundle.Attribute("Include")?.Value,
                    bundleId));
            Assert.Equal(
                "nt51927-standard-merge\\families\\nt51927-nt51928.json",
                sharedPartsConsumer.Element("CanonicalFirmwareFamilySource")?.Value);
            Assert.Equal(
                "families\\nt51927-nt51928.json",
                sharedPartsConsumer.Element("CanonicalFirmwareFamilyDestination")?.Value);
            Assert.False(File.Exists(Path.Combine(
                Root.FullName,
                "profiles",
                "built-in",
                bundleId,
                "families",
                "nt51927-nt51928.json")));
        }

        XElement normalHeaderConsumer = Assert.Single(
            document.Descendants("BuiltInProfileBundle"),
            static bundle => StringComparer.Ordinal.Equals(
                bundle.Attribute("Include")?.Value,
                "nt51923-nt51926-general-merge-logical-candidate"));
        Assert.Equal(
            "nt51923-standard-merge\\families\\nt51923-nt51926.json",
            normalHeaderConsumer.Element("CanonicalFirmwareFamilySource")?.Value);
        Assert.Equal(
            "families\\nt51923-nt51926.json",
            normalHeaderConsumer.Element("CanonicalFirmwareFamilyDestination")?.Value);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51923-nt51926-general-merge-logical-candidate",
            "families",
            "nt51923-nt51926.json")));

        XElement nt51928DpReplace = Assert.Single(
            document.Descendants("BuiltInProfileBundle"),
            static bundle => StringComparer.Ordinal.Equals(
                bundle.Attribute("Include")?.Value,
                "nt51928-dp-replace"));
        Assert.Equal(
            "nt51928-standard-merge\\families\\nt51927-nt51928-v1.5.json",
            nt51928DpReplace.Element("CanonicalFirmwareFamilySource")?.Value);
        Assert.Equal(
            "families\\nt51927-nt51928-v1.5.json",
            nt51928DpReplace.Element("CanonicalFirmwareFamilyDestination")?.Value);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51928-standard-merge",
            "families",
            "nt51927-nt51928-v1.5.json")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51928-dp-replace",
            "families",
            "nt51927-nt51928-v1.5.json")));

        XElement dpPerspectiveConsumer = Assert.Single(
            document.Descendants("BuiltInProfileBundle"),
            static bundle => StringComparer.Ordinal.Equals(
                bundle.Attribute("Include")?.Value,
                "nt51950-nt51951-general-merge-logical-candidate"));
        Assert.Equal(
            "nt51950-nt51951-standard-merge\\families\\nt51950-nt51951-dp-perspective.json",
            dpPerspectiveConsumer.Element("CanonicalFirmwareFamilySource")?.Value);
        Assert.Equal(
            "families\\nt51950-nt51951-dp-perspective.json",
            dpPerspectiveConsumer.Element("CanonicalFirmwareFamilyDestination")?.Value);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51950-nt51951-general-merge-logical-candidate",
            "families",
            "nt51950-nt51951-dp-perspective.json")));

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

    /// <summary>Partial family vocabulary cannot expand back into role-specific runtime forms.</summary>
    [Fact]
    public void FamilyRelationshipsRetainExactlyTwoRuntimeForms()
    {
        string contract = ReadText(
            "src/NvtFwCombiner.Contracts/Firmware/FirmwareFamilyDocument.cs");
        string domain = ReadText(
            "src/NvtFwCombiner.Domain/Firmware/FirmwareFamilyRelationship.cs");
        string normalizer = ReadText(
            "src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.Relationships.cs");
        string relationshipSchema = ReadText(
            "docs/contracts/firmware-family-v1-relations.schema.json");
        string tpHeaderSchema = ReadText(
            "docs/contracts/firmware-family-v1.1-tp-header.schema.json");
        string productionFamilyJson = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    Path.Combine(Root.FullName, "profiles", "built-in"),
                    "*.json",
                    SearchOption.AllDirectories)
                .Where(static path =>
                    path.Contains(
                        $"{Path.DirectorySeparatorChar}families{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.Contains(
            "FirmwarePerfectFamilyRelationshipDocument",
            contract,
            StringComparison.Ordinal);
        Assert.Contains(
            "FirmwareSharedFactRelationshipDocument",
            contract,
            StringComparison.Ordinal);
        Assert.Contains(
            "sealed class PerfectFamilyRelationship",
            domain,
            StringComparison.Ordinal);
        Assert.Contains(
            "sealed class SharedFactRelationship",
            domain,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeSharedFactRelationship",
            normalizer,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FirmwareFamilyRelationshipKind",
            domain + normalizer,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FirmwareInitialCodeSharedFamilyRelationshipDocument",
            contract + normalizer,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FirmwareTpSharedFamilyRelationshipDocument",
            contract + normalizer,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"relationshipKind\": \"initial-code-shared-family\"",
            relationshipSchema + tpHeaderSchema + productionFamilyJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"relationshipKind\": \"tp-shared-family\"",
            relationshipSchema + tpHeaderSchema + productionFamilyJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"sharedRegionIds\"",
            relationshipSchema + tpHeaderSchema + productionFamilyJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"metadataDefinitionIds\"",
            relationshipSchema + tpHeaderSchema + productionFamilyJson,
            StringComparison.Ordinal);
    }

}
