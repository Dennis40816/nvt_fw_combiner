
using System.Security.Cryptography;
using System.Text.Json;

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
        string versionDecoder = ReadText(
            "src/NvtFwCombiner.Application/InputInspection/CompiledInputArtifactObservationService.cs");
        string inputProjection = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchAbMergeInputProjection.cs");
        string topologyValidation = ReadText("src/NvtFwCombiner.Application/Composition/CompositionRunService.AbMergeTopology.cs");
        string executionAdapter = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.AbMerge.cs");

        Assert.Contains("CompiledInputArtifactObservationService.DecodeDpRegion", outputNaming, StringComparison.Ordinal);
        Assert.Contains("Provenance.ResolvedMap.ImageMap.Regions", versionDecoder, StringComparison.Ordinal);
        Assert.DoesNotContain("GenFlashVersionCatalog", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadCmiDpCode", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("GenFlashVersionCatalog", inputProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadCmiDpCode", inputProjection, StringComparison.Ordinal);
        string observedMetadataConsumers = outputNaming + inputProjection + topologyValidation + versionDecoder;
        Assert.DoesNotContain("ProductId", observedMetadataConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain(".Pid", observedMetadataConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("CommonFw", observedMetadataConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition.IcId", topologyValidation, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950", executionAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51951", executionAdapter, StringComparison.Ordinal);
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

    /// <summary>Verifies package admission is one explicit, hash-closed data index.</summary>
    [Fact]
    public void BuiltInBundleMaterializationUsesPackageTrustIndex()
    {
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        string builtInRoot = Path.Combine(Root.FullName, "profiles", "built-in");
        using var index = JsonDocument.Parse(
            ReadText("profiles/built-in/package-trust-index.json"));
        JsonElement[] bundles = [.. index.RootElement.GetProperty("bundles").EnumerateArray()];
        string[] indexedDirectories =
        [
            .. bundles
                .Select(static bundle => bundle.GetProperty("bundleDirectory").GetString()!)
                .Order(StringComparer.Ordinal),
        ];
        string[] sourceDirectories =
        [
            .. Directory.EnumerateDirectories(builtInRoot)
                .Where(static directory => File.Exists(Path.Combine(directory, "profile-bundle.json")))
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)!,
        ];

        Assert.Equal("1.0", index.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(sourceDirectories, indexedDirectories);
        Assert.All(bundles, bundle =>
        {
            string directory = bundle.GetProperty("bundleDirectory").GetString()!;
            string contentHash = bundle.GetProperty("contentHash").GetString()!;
            JsonElement materialization = bundle.GetProperty("materialization");
            using var manifest = JsonDocument.Parse(
                ReadText($"profiles/built-in/{directory}/profile-bundle.json"));

            Assert.DoesNotContain('/', directory);
            Assert.DoesNotContain('\\', directory);
            Assert.DoesNotContain("..", directory, StringComparison.Ordinal);
            Assert.Equal(64, contentHash.Length);
            Assert.All(contentHash, static character =>
                Assert.True(character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
            Assert.Equal(
                bundle.GetProperty("bundleSchemaVersion").GetString(),
                manifest.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal(
                bundle.GetProperty("bundleVersion").GetString(),
                manifest.RootElement.GetProperty("bundleVersion").GetString());
            Assert.True(File.Exists(Path.Combine(
                Root.FullName,
                "docs",
                "contracts",
                materialization.GetProperty("compositionProfileSchemaFile").GetString()!)));
            Assert.True(File.Exists(Path.Combine(
                Root.FullName,
                "docs",
                "contracts",
                materialization.GetProperty("firmwareFamilySchemaFile").GetString()!)));
        });

        Assert.Contains("LoadProfileBundleTrustIndex", project, StringComparison.Ordinal);
        Assert.Contains("package-trust-index.json", project, StringComparison.Ordinal);
        Assert.Contains(
            "$(BuiltInProfileSourceRoot)\\%(BuiltInProfileBundle.Identity)\\**\\*",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "Exclude=\"$(BuiltInProfileSourceRoot)\\%(BuiltInProfileBundle.Identity)\\schemas\\**\\*\"",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<BuiltInProfileBundle Include=", project, StringComparison.Ordinal);
        Assert.DoesNotContain("**\\profile-bundle.json", project, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileSchemaSourceRoot", project, StringComparison.Ordinal);
    }

    /// <summary>Retired ICs have no production profile, route, processor, package, or catalog owner.</summary>
    [Fact]
    public void RetiredIcCapabilitiesStayOutsideProductionOwners()
    {
        string[] retiredIds = ["51920", "51925", "51930", "51931"];
        string[] productionOwners =
        [
            "src/NvtFwCombiner.Profiles/IcWorkflowIds.cs",
            "src/NvtFwCombiner.Bootstrap/BuiltInV2Bundle.cs",
            "src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs",
            "src/NvtFwCombiner.Bootstrap/CtrlRamV2RouteRegistry.cs",
            "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
            "src/NvtFwCombiner.Application/ExternalTools/PostbuildWriteSections.cs",
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.ReportMetadata.cs",
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

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "FlashMaps",
            "TpHeaderCatalog.Layouts.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "FlashMaps",
            "GenFlashVersionCatalog.cs")));

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

    /// <summary>Verifies each approved canonical family reuse is explicit and hash-closed.</summary>
    [Fact]
    public void CandidateBundlesMaterializeOnlyApprovedCanonicalFirmwareFamilies()
    {
        using var index = JsonDocument.Parse(
            ReadText("profiles/built-in/package-trust-index.json"));
        JsonElement[] bundles = [.. index.RootElement.GetProperty("bundles").EnumerateArray()];
        (string Bundle, string Source, string Destination)[] expected =
        [
            ("nt51917-nt51927-general-merge-logical-candidate", "nt51927-standard-merge/families/nt51927-nt51928.json", "families/nt51927-nt51928.json"),
            ("nt51928-general-merge-logical-candidate", "nt51927-standard-merge/families/nt51927-nt51928.json", "families/nt51927-nt51928.json"),
            ("nt51923-nt51926-general-merge-logical-candidate", "nt51923-standard-merge/families/nt51923-nt51926.json", "families/nt51923-nt51926.json"),
            ("nt51928-dp-replace", "nt51928-standard-merge/families/nt51927-nt51928-v1.5.json", "families/nt51927-nt51928-v1.5.json"),
            ("nt51950-nt51951-general-merge-logical-candidate", "nt51950-nt51951-standard-merge/families/nt51950-nt51951-dp-perspective.json", "families/nt51950-nt51951-dp-perspective.json"),
            ("nt51917-ctrlram-replace-alias-candidate", "nt51927-ctrlram-replace-candidate/families/nt51927-ctrlram-replace.json", "families/nt51927-ctrlram-replace.json"),
        ];
        JsonElement[] canonicalEntries =
        [
            .. bundles.Where(static bundle =>
                bundle.GetProperty("materialization")
                    .TryGetProperty("canonicalFirmwareFamily", out _)),
        ];

        Assert.Equal(expected.Length, canonicalEntries.Length);
        foreach ((string bundleId, string source, string destination) in expected)
        {
            JsonElement bundle = Assert.Single(
                bundles,
                candidate => StringComparer.Ordinal.Equals(
                    candidate.GetProperty("bundleDirectory").GetString(),
                    bundleId));
            JsonElement canonical = bundle
                .GetProperty("materialization")
                .GetProperty("canonicalFirmwareFamily");
            Assert.Equal(source, canonical.GetProperty("source").GetString());
            Assert.Equal(destination, canonical.GetProperty("destination").GetString());

            string sourcePath = Path.Combine(
                Root.FullName,
                "profiles",
                "built-in",
                source.Replace('/', Path.DirectorySeparatorChar));
            string destinationPath = Path.Combine(
                Root.FullName,
                "profiles",
                "built-in",
                bundleId,
                destination.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(sourcePath));
            Assert.False(File.Exists(destinationPath));

            using var manifest = JsonDocument.Parse(
                ReadText($"profiles/built-in/{bundleId}/profile-bundle.json"));
            JsonElement familyEntry = Assert.Single(
                manifest.RootElement.GetProperty("entries").EnumerateArray(),
                entry => StringComparer.Ordinal.Equals(
                    entry.GetProperty("path").GetString(),
                    destination));
            Assert.Equal(
                familyEntry.GetProperty("contentHash").GetString(),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant());
        }

        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        Assert.Contains("Built-in profile canonical firmware-family metadata must declare both source and destination", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family source escapes the approved source root", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family destination escapes the bundle families root", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family source is missing", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family destination collides", project, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CanonicalFirmwareFamily",
            ReadText("src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs"),
            StringComparison.Ordinal);
    }

    /// <summary>Verifies retired DP Perspective C# facts cannot become a second oracle beside trusted V2 plans.</summary>
    [Fact]
    public void DpPerspectiveFactsStayOwnedByTrustedV2Profiles()
    {
        string registration = ReadText("src/NvtFwCombiner.Bootstrap/CanonicalCapabilityProjection.DpReplace.cs");
        string display = ReadText("src/NvtFwCombiner.Bootstrap/CompositionMemoryProjection.Replace.Dp.cs");
        string planning = ReadText("src/NvtFwCombiner.Bootstrap/CompositionPlanningAdapter.Replace.Planning.cs");

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
